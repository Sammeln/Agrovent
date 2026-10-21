// File: Services/AGR_StorageWriter.cs
using System.IO;
using Agrovent.DAL.Services;
using Agrovent.Infrastructure.Helpers;
using AgroventInfrastructure;
using AgroventInfrastructure.Interfaces.Components;
using AgroventInfrastructure.Interfaces.Components.Base;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.swconst;
using Xarial.XCad.SolidWorks;

namespace Agrovent.Services
{
    /// <summary>
    /// Файловая часть сохранения (этап 4.4), вызывается внутри открытой транзакции БД:
    /// маркеры в новые версии → копирование в папки новых версий → ссылки на неотмеченные компоненты
    /// перенаправляются на их файлы в хранилище (папки существующих версий не перезаписываются).
    /// </summary>
    internal class AGR_StorageWriter
    {
        private readonly ISwApplication _swApp;
        private readonly IAGR_MarkerService _markers;
        private readonly ILogger _logger;
        private readonly Action<string> _log;

        /// <summary>Папки, созданные этим сохранением: удаляются при откате.</summary>
        public List<string> CreatedFolders { get; } = new();

        public AGR_StorageWriter(ISwApplication swApp, IAGR_MarkerService markers, ILogger logger, Action<string> log)
        {
            _swApp = swApp;
            _markers = markers;
            _logger = logger;
            _log = log;
        }

        private static string ModelPath(IAGR_BaseComponent comp)
        {
            var path = (comp as IAGR_HasFile)?.CurrentModelFilePath;
            return string.IsNullOrEmpty(path) ? comp.SwDocument?.Path ?? "" : path;
        }

        private static IEnumerable<AGR_VersionPlanItem> NewItems(AGR_SavePlan plan)
            => plan.Items.Values.Where(i => i.Action == AGR_VersionAction_e.NewVersion);

        /// <summary>Записывает маркер (Id, версия, отпечаток) в каждый файл новой версии. Документы должны быть открыты.
        /// Вызывать после CompleteAsync, когда у версий есть Id.</summary>
        public void WriteMarkers(AGR_SavePlan plan)
        {
            var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in NewItems(plan))
            {
                var path = ModelPath(item.Component);
                // Несколько конфигураций одного файла: маркер на файл один, пишем первую версию
                if (!done.Add(path)) continue;

                var cv = item.Saved ?? throw new InvalidOperationException($"{item.Component.Name}: версия не создана.");
                if (cv.Id == 0 || cv.ComponentId == 0)
                    throw new InvalidOperationException($"{item.Component.Name}: у версии нет Id (CompleteAsync не выполнен).");

                var doc = item.Component.SwDocument;
                var fingerprint = AGR_GeometryFingerprint.Compute(doc, out _);
                var marker = new AGR_Marker
                {
                    ComponentId = cv.ComponentId,
                    PartNumber = item.Component.PartNumber,
                    Version = cv.Version,
                    Fingerprint = fingerprint
                };

                // CHECK: Save() у документа детали, открытого только как компонент сборки (без своего окна)
                if (!_markers.SaveWithMarker(doc, marker, out var error))
                    throw new InvalidOperationException($"{item.Component.Name}: маркер не записан ({error}).");

                _log($"Маркер: {item.Component.Name} -> {marker.PartNumber} v{marker.Version}");
            }
        }

        /// <summary>Копирует файлы новых версий в хранилище. Закрывает все документы SolidWorks.</summary>
        public void CopyToStorage(AGR_SavePlan plan, IAGR_BaseComponent root)
        {
            var rootItem = plan.Get(root);
            if (rootItem.Action != AGR_VersionAction_e.NewVersion)
                throw new InvalidOperationException("Корневой компонент должен получать новую версию.");

            // 1. Куда копировать новые версии, куда ссылаться для существующих
            var targets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   // локальный -> новая папка
            var existing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);  // локальный -> файл версии в хранилище
            var errors = new List<string>();

            foreach (var item in plan.Items.Values)
            {
                var model = ModelPath(item.Component);
                if (string.IsNullOrEmpty(model)) continue;
                var cv = item.Saved ?? throw new InvalidOperationException($"{item.Component.Name}: версия не определена.");

                if (item.Action == AGR_VersionAction_e.NewVersion)
                {
                    if (targets.ContainsKey(model)) continue;
                    var folder = AGR_StoragePaths.GetVersionFolder(cv.HashSum, cv.Version);
                    targets[model] = Path.Combine(folder, Path.GetFileName(model));

                    var drawing = (item.Component as IAGR_HasFile)?.CurrentDrawFilePath;
                    if (string.IsNullOrEmpty(drawing)) drawing = Path.ChangeExtension(model, ".slddrw");
                    if (File.Exists(drawing)) targets[drawing] = Path.Combine(folder, Path.GetFileName(drawing));
                }
                else
                {
                    var storagePath = AGR_StoragePaths.ResolveExistingFilePath(cv.HashSum, cv.Version, model);
                    if (!File.Exists(storagePath))
                        errors.Add($"{item.Component.Name} v{cv.Version}: нет файла версии в хранилище ({storagePath}).");
                    else
                        existing[model] = storagePath;
                }
            }
            if (errors.Count > 0)
                throw new InvalidOperationException("Сохранение невозможно:\n" + string.Join("\n", errors));

            foreach (var key in existing.Keys.Where(targets.ContainsKey).ToList())
                existing.Remove(key); // файл и новый, и существующий (разные конфигурации) - копируем как новый

            AddUnknownDependencies(targets, existing);

            // 2. Папки новых версий
            foreach (var folder in targets.Values.Select(Path.GetDirectoryName).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder!);
                    CreatedFolders.Add(folder!);
                }
            }

            // 3. Копирование: только файлы новых версий
            var rootSource = ModelPath(root);
            var rootTarget = targets[rootSource];
            var sources = targets.Keys.ToArray();
            var dests = sources.Select(s => targets[s]).ToArray();

            _log("Закрытие всех документов перед копированием.");
            _swApp.Sw.CloseAllDocuments(true);

            var copyErrors = _swApp.Sw.CopyDocument(rootSource, rootTarget, sources, dests,
                (int)swMoveCopyOptions_e.swMoveCopyOptionsOverwriteExistingDocs);
            if (copyErrors != 0)
                throw new InvalidOperationException($"SolidWorks CopyDocument вернул ошибку {copyErrors} (swMoveCopyError_e).");
            _log($"Скопировано в хранилище: {sources.Length} файлов.");

            // 4. Ссылки новых сборок на неотмеченные компоненты -> файлы их версий в хранилище
            if (existing.Count == 0) return;
            foreach (var asm in dests.Where(d => d.EndsWith(".sldasm", StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var (local, storage) in existing)
                {
                    // CHECK: ReplaceReferencedDocument работает с закрытым документом; false = ссылки не было
                    if (_swApp.Sw.ReplaceReferencedDocument(asm, local, storage))
                        _logger.LogDebug($"Ссылка {Path.GetFileName(asm)}: {local} -> {storage}");
                }
            }
            _log("Ссылки на неизменённые компоненты направлены в хранилище.");
        }

        /// <summary>Прямые зависимости новых сборок, которых нет в плане (погашенные, конверты и т.п.):
        /// копируются в папку версии ссылающейся сборки.</summary>
        private void AddUnknownDependencies(Dictionary<string, string> targets, Dictionary<string, string> existing)
        {
            var assemblies = targets.Keys.Where(k => k.EndsWith(".sldasm", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var asm in assemblies)
            {
                object[]? deps;
                try
                {
                    // CHECK: формат GetDocumentDependencies2 (пары «имя, путь»), берём только существующие полные пути
                    deps = _swApp.Sw.GetDocumentDependencies2(asm, false, true, false) as object[];
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"GetDocumentDependencies2 для {asm}.");
                    continue;
                }
                if (deps == null) continue;

                var folder = Path.GetDirectoryName(targets[asm])!;
                foreach (var dep in deps.OfType<string>())
                {
                    if (!Path.IsPathRooted(dep) || !File.Exists(dep)) continue;
                    if (dep.Contains("Local\\Temp", StringComparison.OrdinalIgnoreCase)) continue; // встроенные/виртуальные
                    if (targets.ContainsKey(dep) || existing.ContainsKey(dep)) continue;

                    targets[dep] = Path.Combine(folder, Path.GetFileName(dep));
                    _log($"Зависимость вне спецификации копируется к сборке: {Path.GetFileName(dep)} -> {folder}");
                }
            }
        }

        /// <summary>Откат файловой части: удаляет папки, созданные этим сохранением.</summary>
        public void DeleteCreatedFolders()
        {
            foreach (var folder in CreatedFolders)
            {
                try
                {
                    if (Directory.Exists(folder)) Directory.Delete(folder, true);
                    _log($"Откат: удалена папка {folder}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Откат: не удалось удалить {folder}");
                    _log($"Откат: не удалось удалить {folder}: {ex.Message}");
                }
            }
        }
    }
}
