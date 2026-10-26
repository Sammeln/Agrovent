// File: Services/AGR_StorageWriter.cs
using System.IO;
using Agrovent.DAL.Services;
using Agrovent.Infrastructure.Helpers;
using AgroventInfrastructure;
using AgroventInfrastructure.Enums;
using AgroventInfrastructure.Interfaces.Components;
using AgroventInfrastructure.Interfaces.Components.Base;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.swconst;
using Xarial.XCad.SolidWorks;

namespace Agrovent.Services
{
    /// <summary>
    /// Файловая часть сохранения (этап 4.4, 6.3), вызывается внутри открытой транзакции БД:
    /// маркеры → копирование новых версий в их папки и файлов PropertiesChanged поверх их версии →
    /// ссылки на неотмеченные компоненты перенаправляются на их файлы в хранилище.
    /// Папки существующих версий не меняются, кроме файла модели при PropertiesChanged (с резервной копией для отката).
    /// </summary>
    internal class AGR_StorageWriter
    {
        // Модели и чертежи идут через CopyDocument (ссылки), остальные файлы с тем же именем - простым копированием
        private static readonly string[] SolidWorksExtensions = { ".sldprt", ".sldasm", ".slddrw" };

        private readonly ISwApplication _swApp;
        private readonly IAGR_MarkerService _markers;
        private readonly ILogger _logger;
        private readonly Action<string> _log;

        /// <summary>Папки, созданные этим сохранением: удаляются при откате.</summary>
        public List<string> CreatedFolders { get; } = new();

        /// <summary>Перезаписанные файлы существующих версий: файл в хранилище -> резервная копия.</summary>
        private readonly Dictionary<string, string> _backups = new(StringComparer.OrdinalIgnoreCase);
        private string? _backupFolder;

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

        /// <summary>Файлы, которые получают маркер: новые версии, неотмеченные с изменёнными свойствами,
        /// покупные без маркера (MarkerOnly, без копирования).</summary>
        private static IEnumerable<AGR_VersionPlanItem> MarkedItems(AGR_SavePlan plan)
            => plan.Items.Values.Where(i => i.Action == AGR_VersionAction_e.NewVersion || i.PropertiesChanged || i.MarkerOnly);

        /// <summary>Файл модели можно сохранить: нет атрибута «только чтение» и документ открыт не только для чтения.
        /// Библиотечные покупные часто только для чтения (6.5).</summary>
        internal static bool IsWritable(IAGR_BaseComponent comp)
        {
            try
            {
                var path = ModelPath(comp);
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
                if (File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly)) return false;
                return comp.SwDocument?.Model is { } model && !model.IsOpenedReadOnly();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Записывает маркер (Id, версия, GUID, отпечатки геометрии и свойств) в каждый файл новой версии
        /// и в файлы PropertiesChanged (та же версия, новый отпечаток свойств). Документы должны быть открыты.
        /// Вызывать после CompleteAsync, когда у версий есть Id. Отпечатки берутся из плана - те же, что в БД.</summary>
        public void WriteMarkers(AGR_SavePlan plan)
        {
            var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in MarkedItems(plan))
            {
                var path = ModelPath(item.Component);
                // Несколько конфигураций одного файла: маркер на файл один, пишем первую версию
                if (!done.Add(path)) continue;

                var cv = item.Saved ?? throw new InvalidOperationException($"{item.Component.Name}: версия не создана.");
                if (cv.Id == 0 || cv.ComponentId == 0)
                    throw new InvalidOperationException($"{item.Component.Name}: у версии нет Id (CompleteAsync не выполнен).");

                // Покупной в библиотеке только для чтения: без маркера, определится по артикулу или отпечатку (6.5)
                if (item.Component.IsPurchased && !IsWritable(item.Component))
                {
                    _log($"Маркер не записан (файл только для чтения): {item.Component.Name}");
                    continue;
                }

                var doc = item.Component.SwDocument;
                var marker = new AGR_Marker
                {
                    ComponentId = cv.ComponentId,
                    PartNumber = cv.Component?.PartNumber ?? item.Component.PartNumber,
                    Version = cv.Version,
                    StorageGuid = cv.Component!.StorageGuid,
                    GeometryFingerprint = item.GeometryFingerprint ?? AGR_GeometryFingerprint.Compute(doc, out _),
                    PropertiesFingerprint = item.PropertiesFingerprint ?? AGR_PropertiesFingerprint.Compute(doc, out _)
                };

                if (!_markers.SaveWithMarker(doc, marker, out var error))
                {
                    // Маркер покупного без копирования не обязателен: сохранение продолжается
                    if (item.MarkerOnly) { _log($"Маркер не записан: {item.Component.Name} ({error})"); continue; }
                    throw new InvalidOperationException($"{item.Component.Name}: маркер не записан ({error}).");
                }

                _log($"Маркер: {item.Component.Name} -> {marker.PartNumber} v{marker.Version}" +
                     (item.PropertiesChanged ? " (обновлены свойства)" : ""));
            }
        }

        /// <summary>Копирует файлы в хранилище. Закрывает все документы SolidWorks.</summary>
        public void CopyToStorage(AGR_SavePlan plan, IAGR_BaseComponent root)
        {
            var rootItem = plan.Get(root);
            if (rootItem.Action != AGR_VersionAction_e.NewVersion)
                throw new InvalidOperationException("Корневой компонент должен получать новую версию.");

            // 1. Куда копировать, куда ссылаться для существующих
            var targets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   // локальный -> файл в хранилище (копируется)
            var existing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);  // локальный -> файл версии в хранилище (только ссылка)
            var overwritten = new HashSet<string>(StringComparer.OrdinalIgnoreCase);         // локальные файлы PropertiesChanged
            var extras = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);    // pdf, dxf... -> папка версии
            var errors = new List<string>();

            foreach (var item in plan.Items.Values)
            {
                var model = ModelPath(item.Component);
                if (string.IsNullOrEmpty(model)) continue;
                var cv = item.Saved ?? throw new InvalidOperationException($"{item.Component.Name}: версия не определена.");

                if (item.Action == AGR_VersionAction_e.NewVersion)
                {
                    if (targets.ContainsKey(model) && !overwritten.Contains(model)) continue;
                    overwritten.Remove(model); // файл и новый, и существующий (разные конфигурации) - копируем как новый
                    var folder = AGR_StoragePaths.GetVersionFolder(cv.Component.StorageGuid, cv.Version);
                    targets[model] = Path.Combine(folder, Path.GetFileName(model));

                    var drawing = (item.Component as IAGR_HasFile)?.CurrentDrawFilePath;
                    if (string.IsNullOrEmpty(drawing)) drawing = Path.ChangeExtension(model, ".slddrw");
                    if (File.Exists(drawing)) targets[drawing] = Path.Combine(folder, Path.GetFileName(drawing));

                    foreach (var extra in SameNameFiles(model))
                        extras[extra] = Path.Combine(folder, Path.GetFileName(extra));
                    continue;
                }

                var storagePath = AGR_StoragePaths.ResolveExistingFilePath(cv.Component.StorageGuid, cv.Version, model,
                    cv.Files.FirstOrDefault(f => f.FileType == AGR_FileType_e.StorageModel)?.FilePath);
                if (!File.Exists(storagePath))
                {
                    errors.Add($"{item.Component.Name} v{cv.Version}: нет файла версии в хранилище ({storagePath}).");
                    continue;
                }

                if (item.PropertiesChanged && !targets.ContainsKey(model))
                {
                    // Свойства изменены: локальный файл (с новым маркером) копируется поверх файла его версии
                    targets[model] = storagePath;
                    overwritten.Add(model);
                }
                else
                {
                    existing[model] = storagePath;
                }
            }
            if (errors.Count > 0)
                throw new InvalidOperationException("Сохранение невозможно:\n" + string.Join("\n", errors));

            foreach (var key in existing.Keys.Where(targets.ContainsKey).ToList())
                existing.Remove(key); // файл и копируется, и существующий (разные конфигурации) - копируем

            AddUnknownDependencies(targets, existing, overwritten);

            // 2. Папки новых версий
            foreach (var folder in targets.Values.Concat(extras.Values).Select(Path.GetDirectoryName).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder!);
                    CreatedFolders.Add(folder!);
                }
            }

            // 3. Резервные копии файлов существующих версий, которые будут перезаписаны
            foreach (var local in overwritten)
                Backup(targets[local]);

            // 4. Копирование: файлы новых версий и PropertiesChanged
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
            _log($"Скопировано в хранилище: {sources.Length} файлов" +
                 (overwritten.Count > 0 ? $", из них поверх своей версии (изменены свойства): {overwritten.Count}." : "."));

            // 5. Остальные файлы модели с тем же именем (pdf, dxf...) в папки новых версий
            foreach (var (source, dest) in extras)
            {
                File.Copy(source, dest, true);
                File.SetAttributes(dest, File.GetAttributes(dest) & ~FileAttributes.ReadOnly);
            }
            if (extras.Count > 0) _log($"Скопировано файлов модели (pdf, dxf и т.п.): {extras.Count}.");

            // 6. Ссылки скопированных сборок на неотмеченные компоненты -> файлы их версий в хранилище
            if (existing.Count == 0) return;
            foreach (var asm in dests.Where(d => d.EndsWith(".sldasm", StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var (local, storage) in existing)
                {
                    
                    if (_swApp.Sw.ReplaceReferencedDocument(asm, local, storage))
                        _logger.LogDebug($"Ссылка {Path.GetFileName(asm)}: {local} -> {storage}");
                }
            }
            _log("Ссылки на неизменённые компоненты направлены в хранилище.");
        }

        /// <summary>Файлы рядом с моделью с тем же именем, кроме моделей и чертежей SolidWorks (pdf, dxf, step...).</summary>
        private static IEnumerable<string> SameNameFiles(string model)
        {
            var dir = Path.GetDirectoryName(model);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) yield break;
            var baseName = Path.GetFileNameWithoutExtension(model);

            foreach (var file in Directory.EnumerateFiles(dir, baseName + ".*"))
            {
                if (!string.Equals(Path.GetFileNameWithoutExtension(file), baseName, StringComparison.OrdinalIgnoreCase)) continue;
                var ext = Path.GetExtension(file);
                if (SolidWorksExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase))) continue;
                if (Path.GetFileName(file).StartsWith("~$")) continue;
                yield return file;
            }
        }

        /// <summary>Прямые зависимости копируемых сборок, которых нет в плане (погашенные, конверты и т.п.).
        /// Новая сборка: копируются в её папку версии. Перезаписываемая (PropertiesChanged): если файл уже лежит
        /// в папке её версии - ссылка на него, иначе копируется туда же.</summary>
        private void AddUnknownDependencies(Dictionary<string, string> targets, Dictionary<string, string> existing,
            HashSet<string> overwritten)
        {
            var assemblies = targets.Keys.Where(k => k.EndsWith(".sldasm", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var asm in assemblies)
            {
                object[]? deps;
                try
                {
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

                    var inFolder = Path.Combine(folder, Path.GetFileName(dep));
                    if (overwritten.Contains(asm) && File.Exists(inFolder))
                    {
                        existing[dep] = inFolder;
                        continue;
                    }
                    targets[dep] = inFolder;
                    _log($"Зависимость вне спецификации копируется к сборке: {Path.GetFileName(dep)} -> {folder}");
                }
            }
        }

        /// <summary>Резервная копия файла существующей версии перед перезаписью (для отката).</summary>
        private void Backup(string storageFile)
        {
            if (_backups.ContainsKey(storageFile) || !File.Exists(storageFile)) return;
            _backupFolder ??= Path.Combine(Path.GetTempPath(), "Agrovent", "Rollback", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_backupFolder);

            var backup = Path.Combine(_backupFolder, _backups.Count + "_" + Path.GetFileName(storageFile));
            File.Copy(storageFile, backup, true);
            File.SetAttributes(storageFile, File.GetAttributes(storageFile) & ~FileAttributes.ReadOnly); // CopyDocument должен перезаписать
            _backups[storageFile] = backup;
        }

        /// <summary>Откат файловой части: удаляет папки, созданные этим сохранением, и возвращает перезаписанные файлы.</summary>
        public void RollbackFiles()
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

            foreach (var (storageFile, backup) in _backups)
            {
                try
                {
                    File.Copy(backup, storageFile, true);
                    _log($"Откат: восстановлен {storageFile}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Откат: не удалось восстановить {storageFile} из {backup}");
                    _log($"Откат: НЕ восстановлен {storageFile}, резервная копия: {backup}");
                    return; // резервные копии не удаляем
                }
            }
            DiscardBackups();
        }

        /// <summary>После успешного коммита: резервные копии не нужны.</summary>
        public void DiscardBackups()
        {
            if (_backupFolder == null) return;
            try { Directory.Delete(_backupFolder, true); }
            catch (Exception ex) { _logger.LogWarning(ex, $"Не удалось удалить резервные копии {_backupFolder}"); }
            _backups.Clear();
            _backupFolder = null;
        }
    }
}
