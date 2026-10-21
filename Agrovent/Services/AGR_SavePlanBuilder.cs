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
    /// - есть маркер, замка нет → существующая версия из маркера;
    /// - маркера нет → новый компонент, сохраняется как есть (новая версия; если PartNumber уже в БД
    ///   и на нём мой замок, замок снимается, BasedOnVersion = BaseVersion);
    /// - покупной без маркера → последняя версия из БД, если есть;
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
                    item = new AGR_VersionPlanItem
                    {
                        Component = comp,
                        Action = AGR_VersionAction_e.NewVersion,
                        BasedOnVersion = lck!.BaseVersion,
                        LockedComponentId = componentId
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
                    item = new AGR_VersionPlanItem
                    {
                        Component = comp,
                        Action = AGR_VersionAction_e.UseExisting,
                        ComponentId = marker.ComponentId,
                        Version = marker.Version
                    };
                }
                else if (comp.IsPurchased && componentId > 0)
                {
                    // Покупные на редактирование не берутся: ссылаемся на последнюю версию
                    item = new AGR_VersionPlanItem
                    {
                        Component = comp,
                        Action = AGR_VersionAction_e.UseExisting,
                        ComponentId = componentId,
                        Version = editInfo[comp.PartNumber].Version
                    };
                }
                else
                {
                    item = new AGR_VersionPlanItem { Component = comp, Action = AGR_VersionAction_e.NewVersion };
                }

                plan.Set(comp, item);
                _logger.LogDebug($"План сохранения: {comp.Name} ({comp.PartNumber}) -> {item.Action}" +
                                 (item.Action == AGR_VersionAction_e.UseExisting ? $" v{item.Version}" : ""));
            }

            return (plan, errors);
        }

        /// <summary>null = совпадает (или сравнивать не с чем); иначе текст ошибки.</summary>
        private string? CheckFingerprint(IAGR_BaseComponent comp, AGR_Marker marker)
        {
            if (string.IsNullOrEmpty(marker.Fingerprint)) return null;
            try
            {
                var current = AGR_GeometryFingerprint.Compute(comp.SwDocument, out var canonical);
                if (current == marker.Fingerprint) return null;

                _logger.LogWarning($"Отпечаток {comp.Name} ({comp.PartNumber} v{marker.Version}) не совпал: " +
                                   $"маркер {marker.Fingerprint}, сейчас {current}. Текущее состояние:\n{canonical}");
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
