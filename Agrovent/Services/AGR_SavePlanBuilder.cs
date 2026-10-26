// File: Services/AGR_SavePlanBuilder.cs
using Agrovent.DAL.Services;
using Agrovent.Infrastructure.Helpers;
using AgroventInfrastructure.Enums;
using AgroventInfrastructure.Interfaces.Components.Base;
using Microsoft.Extensions.Logging;
using Xarial.XCad.Documents.Enums;
using Xarial.XCad.Documents; // TryFlatten (XComponentRepositoryExtension)
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.Services
{
    /// <summary>
    /// Решает до транзакции, какой компонент получает новую версию, а какой ссылается на существующую.
    /// Хеш в решении не участвует. Правила (EditingRework.md, этап 4):
    /// - есть маркер и мой замок → новая версия (BasedOnVersion = BaseVersion замка);
    /// - есть маркер, замка нет → существующая версия из маркера; изменена геометрия → ошибка (взять на редактирование);
    ///   изменены только свойства (этап 6.3) → свойства версии в БД и перезапись файла версии (PropertiesChanged);
    /// - маркера нет → новый компонент, сохраняется как есть (новая версия; если PartNumber уже в БД
    ///   и на нём мой замок, замок снимается, BasedOnVersion = BaseVersion);
    /// - покупной без маркера → последняя версия из БД, если есть (+ маркер в файл, если доступен на запись; поиск
    ///   существующего покупного без PartNumber - ComponentVersionService.ResolvePurchasedAsync);
    /// - замок другого пользователя, повреждённый маркер, незагруженный документ → ошибка, сохранение не начинается.
    /// </summary>
    internal class AGR_SavePlanBuilder
    {
        private readonly IAGR_MarkerService _markers;
        private readonly IAGR_EditLockService _locks;
        private readonly ILogger _logger;

        public AGR_SavePlanBuilder(IAGR_MarkerService markers, IAGR_EditLockService locks, ILogger logger)
        {
            _markers = markers;
            _locks = locks;
            _logger = logger;
        }

        /// <summary>Облегчённые (lightweight) компоненты во всей сборке: их маркер не прочитать.</summary>
        public static List<string> FindLightweight(ISwAssembly assembly)
        {
            return assembly.Configurations.Active.Components.TryFlatten()
                .Where(c => c.State.HasFlag(ComponentState_e.Lightweight)
                         && !c.State.HasFlag(ComponentState_e.Suppressed))
                .Select(c => c.Name)
                .Distinct()
                .ToList();
        }

        public async Task<(AGR_SavePlan Plan, List<string> Errors)> BuildAsync(IEnumerable<IAGR_BaseComponent> components)
        {
            var plan = new AGR_SavePlan();
            var errors = new List<string>();

            // 1. Маркеры (по одному на ключ файл+конфигурация)
            var entries = new List<(IAGR_BaseComponent Comp, AGR_Marker? Marker)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var comp in components)
            {
                if (!seen.Add(AGR_SavePlan.KeyOf(comp))) continue;

                var doc = comp.SwDocument;
                if (doc == null || !doc.IsAlive)
                {
                    errors.Add($"{comp.Name}: документ не загружен (облегчённый или закрыт).");
                    continue;
                }

                switch (_markers.TryRead(doc, out var marker, out var error))
                {
                    case AGR_MarkerReadResult_e.Ok:
                        if (!string.Equals(marker!.PartNumber, comp.PartNumber, StringComparison.Ordinal))
                        {
                            // CHECK этап 5: PartNumber файла разошёлся с маркером. Пока верим маркеру.
                            _logger.LogWarning($"{comp.Name}: PartNumber файла '{comp.PartNumber}' не совпадает с маркером '{marker.PartNumber}'.");
                        }
                        entries.Add((comp, marker));
                        break;
                    case AGR_MarkerReadResult_e.Missing:
                        entries.Add((comp, null));
                        break;
                    default:
                        errors.Add($"{comp.Name}: маркер повреждён ({error}).");
                        break;
                }
            }

            // 1б. Маркер относится к этой БД: компонент с этим Id есть и у него тот же StorageGuid.
            //     Иначе маркер чужой (БД очищена или пересоздана, файл из другой БД) - компонент сохраняется как без маркера.
            //     Id после очистки БД мог достаться другому компоненту, поэтому одного Id мало.
            var identities = await _locks.GetMarkerIdentitiesAsync(entries
                .Where(e => e.Marker != null)
                .Select(e => e.Marker!.ComponentId));
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var (comp, marker) = entries[i];
                if (marker == null) continue;

                if (!identities.TryGetValue(marker.ComponentId, out var identity) || identity.StorageGuid != marker.StorageGuid)
                {
                    _logger.LogWarning($"{comp.Name}: маркер не относится к этой БД (Id {marker.ComponentId}, {marker.PartNumber} v{marker.Version}), " +
                                       "компонент сохраняется как файл без маркера.");
                    entries[i] = (comp, null);
                }
                else if (!identity.Versions.Contains(marker.Version))
                {
                    errors.Add($"{comp.Name} ({marker.PartNumber}): версии v{marker.Version} из маркера нет в БД.");
                    entries.RemoveAt(i);
                }
            }

            // 2. Компоненты в БД по PartNumber (для файлов без маркера) и замки
            var editInfo = await _locks.GetEditInfoAsync(entries
                .Where(e => e.Marker == null)
                .Select(e => e.Comp.PartNumber));

            var ids = entries
                .Select(e => e.Marker?.ComponentId
                          ?? (editInfo.TryGetValue(e.Comp.PartNumber ?? "", out var i) ? i.ComponentId : 0))
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            var locks = await _locks.GetLocksAsync(ids);
            var userId = await _locks.GetCurrentUserIdAsync();

            // 3. Решение по каждому компоненту
            foreach (var (comp, marker) in entries)
            {
                int componentId = marker?.ComponentId
                    ?? (editInfo.TryGetValue(comp.PartNumber ?? "", out var info) ? info.ComponentId : 0);

                locks.TryGetValue(componentId, out var lck);
                if (lck != null && lck.UserId != userId)
                {
                    errors.Add($"{comp.Name} ({comp.PartNumber}): на редактировании у {lck.User?.LastName} {lck.User?.FirstName}.");
                    continue;
                }
                bool myLock = lck != null;

                AGR_VersionPlanItem item;
                if (myLock)
                {
                    if (!TryComputeFingerprints(comp, errors, out var geometry, out var properties)) continue;
                    item = new AGR_VersionPlanItem
                    {
                        Component = comp,
                        Action = AGR_VersionAction_e.NewVersion,
                        BasedOnVersion = lck!.BaseVersion,
                        LockedComponentId = componentId,
                        GeometryFingerprint = geometry,
                        PropertiesFingerprint = properties
                    };
                }
                else if (marker != null)
                {
                    // Вариант Б: неотмеченный компонент не должен быть изменён геометрически
                    var changed = CheckFingerprint(comp, marker);
                    if (changed != null)
                    {
                        errors.Add(changed);
                        continue;
                    }
                    item = CheckProperties(comp, marker) ?? new AGR_VersionPlanItem
                    {
                        Component = comp,
                        Action = AGR_VersionAction_e.UseExisting,
                        ComponentId = marker.ComponentId,
                        Version = marker.Version
                    };
                }
                else if (comp.IsPurchased && componentId > 0)
                {
                    // Покупные на редактирование не берутся: ссылаемся на последнюю версию.
                    // 6.5: маркер в файл (если доступен на запись), чтобы в следующий раз определить по нему, без поиска.
                    string? geometry = null, properties = null;
                    bool writeMarker = AGR_StorageWriter.IsWritable(comp)
                                       && TryComputeFingerprints(comp, new List<string>(), out geometry, out properties);
                    item = new AGR_VersionPlanItem
                    {
                        Component = comp,
                        Action = AGR_VersionAction_e.UseExisting,
                        ComponentId = componentId,
                        Version = editInfo[comp.PartNumber].Version,
                        MarkerOnly = writeMarker,
                        GeometryFingerprint = geometry,
                        PropertiesFingerprint = properties
                    };
                }
                else
                {
                    if (!TryComputeFingerprints(comp, errors, out var geometry, out var properties)) continue;
                    item = new AGR_VersionPlanItem
                    {
                        Component = comp,
                        Action = AGR_VersionAction_e.NewVersion,
                        GeometryFingerprint = geometry,
                        PropertiesFingerprint = properties
                    };
                }

                plan.Set(comp, item);
                _logger.LogDebug($"План сохранения: {comp.Name} ({comp.PartNumber}) -> {item.Action}" +
                                 (item.Action == AGR_VersionAction_e.UseExisting ? $" v{item.Version}" : "") +
                                 (item.PropertiesChanged ? " (изменены свойства)" : ""));
            }

            return (plan, errors);
        }

        /// <summary>Отпечатки новой версии: те же значения пишутся в БД и в маркер.</summary>
        private bool TryComputeFingerprints(IAGR_BaseComponent comp, List<string> errors, out string geometry, out string properties)
        {
            try
            {
                geometry = AGR_GeometryFingerprint.Compute(comp.SwDocument, out _);
                properties = AGR_PropertiesFingerprint.Compute(comp.SwDocument, out _);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Отпечатки {comp.Name}: ошибка расчёта.");
                errors.Add($"{comp.Name}: не удалось посчитать отпечатки ({ex.Message}).");
                geometry = properties = "";
                return false;
            }
        }

        /// <summary>
        /// Неотмеченный компонент (геометрия уже совпала): отпечаток свойств отличается от маркера → пункт плана
        /// с PropertiesChanged (свойства версии в БД + перезапись файла версии). null = свойства не менялись
        /// или сравнивать нечего. Покупные не обновляются: артикул покупного - его идентификатор.
        /// </summary>
        private AGR_VersionPlanItem? CheckProperties(IAGR_BaseComponent comp, AGR_Marker marker)
        {
            if (comp.IsPurchased || string.IsNullOrEmpty(marker.PropertiesFingerprint) || !marker.FingerprintsComparable)
                return null;
            try
            {
                var current = AGR_PropertiesFingerprint.Compute(comp.SwDocument, out var canonical);
                if (current == marker.PropertiesFingerprint) return null;

                _logger.LogInformation($"Свойства {comp.Name} ({comp.PartNumber} v{marker.Version}) изменены без взятия на редактирование:\n{canonical}");
                return new AGR_VersionPlanItem
                {
                    Component = comp,
                    Action = AGR_VersionAction_e.UseExisting,
                    ComponentId = marker.ComponentId,
                    Version = marker.Version,
                    PropertiesChanged = true,
                    GeometryFingerprint = marker.GeometryFingerprint, // совпал с текущим
                    PropertiesFingerprint = current,
                    FileProperties = AGR_PropertiesFingerprint.ReadEffective(comp.SwDocument)
                };
            }
            catch (Exception ex)
            {
                // Свойства не критичны: сохранение идёт, версия остаётся как есть
                _logger.LogError(ex, $"Отпечаток свойств {comp.Name}: ошибка расчёта, свойства не обновляются.");
                return null;
            }
        }

        /// <summary>null = совпадает (или сравнивать не с чем); иначе текст ошибки.</summary>
        private string? CheckFingerprint(IAGR_BaseComponent comp, AGR_Marker marker)
        {
            // Отпечаток другой версии алгоритма не сравниваем (ложная остановка хуже: после смены алгоритма стояли бы все)
            if (string.IsNullOrEmpty(marker.GeometryFingerprint) || !marker.FingerprintsComparable) return null;
            try
            {
                var current = AGR_GeometryFingerprint.Compute(comp.SwDocument, out var canonical);
                if (current == marker.GeometryFingerprint) return null;

                _logger.LogWarning($"Отпечаток {comp.Name} ({comp.PartNumber} v{marker.Version}) не совпал: " +
                                   $"маркер {marker.GeometryFingerprint}, сейчас {current}. Текущее состояние:\n{canonical}");
                return $"{comp.Name} ({comp.PartNumber} v{marker.Version}): изменена геометрия или состав, " +
                       "но компонент не взят на редактирование. Возьмите его на редактирование или отмените изменения.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Отпечаток {comp.Name}: ошибка расчёта.");
                return $"{comp.Name}: не удалось проверить изменения ({ex.Message}).";
            }
        }
    }
}
