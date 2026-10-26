// File: Services/Migration/AGR_MigrationReportService.cs
using System.Diagnostics;
using System.IO;
using System.Text;
using Agrovent.DAL;
using AgroventInfrastructure;
using AgroventInfrastructure.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SolidWorks.Interop.sldworks;
using Xarial.XCad.Documents.Enums;
using Xarial.XCad.Documents.Extensions;
using Xarial.XCad.SolidWorks;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.Services.Migration
{
    /// <summary>
    /// Отчёт перед миграцией хранилища (этап 1 миграции). Ничего не меняет ни в БД, ни в файлах.
    /// Уровень 1 (быстро): БД, папки хранилища, ссылки сборок и чертежей (GetDocumentDependencies2 по закрытому файлу).
    /// Уровень 2 (долго, openFiles): каждая модель открывается скрытой только для чтения: «Обозначение» по конфигурациям, маркер.
    /// </summary>
    public interface IAGR_MigrationReportService
    {
        /// <summary>Возвращает папку с отчётом. Вызывать с главного потока SolidWorks, все документы закрыты.</summary>
        Task<string> BuildReportAsync(bool openFiles);
    }

    public class AGR_MigrationReportService : IAGR_MigrationReportService
    {
        public const string StopFileName = "STOP";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ISwApplication _swApp;
        private readonly IAGR_MarkerService _markers;
        private readonly AGR_MissingReferenceResolver _missingRefs;
        private readonly ILogger<AGR_MigrationReportService> _logger;

        public AGR_MigrationReportService(
            IServiceScopeFactory scopeFactory,
            ISwApplication swApp,
            IAGR_MarkerService markers,
            AGR_MissingReferenceResolver missingRefs,
            ILogger<AGR_MigrationReportService> logger)
        {
            _scopeFactory = scopeFactory;
            _swApp = swApp;
            _markers = markers;
            _missingRefs = missingRefs;
            _logger = logger;
        }

        public static string ReportsRoot =>
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "Agrovent", "Migration");

        public async Task<string> BuildReportAsync(bool openFiles)
        {
            var folder = Path.Combine(ReportsRoot, "Report_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(folder);
            var report = new AGR_MigrationReport(folder);
            var timer = Stopwatch.StartNew();

            // Все обращения к БД до работы с SolidWorks: дальше цикл синхронный, на главном потоке
            var data = await LoadAsync();
            report.Progress($"Загружено из БД: версий {data.Versions.Count}, строк структуры {data.StructureRows}.");

            CheckDatabase(data, report);
            CheckStorageFiles(data, report);
            CheckReferences(data, report);
            if (openFiles && !report.Stopped) CheckModels(data, report);

            report.WriteAll(data, timer.Elapsed, openFiles);
            _logger.LogInformation($"Миграция: отчёт готов, {folder}");
            return folder;
        }

        #region Загрузка

        private async Task<AGR_MigrationData> LoadAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();

            var versions = await db.ComponentVersions.AsNoTracking()
                .Select(v => new
                {
                    v.Id,
                    v.ComponentId,
                    v.Component.PartNumber,
                    v.Version,
                    v.HashSum,
                    v.Name,
                    v.ComponentType,
                    Files = v.Files.Select(f => new { f.FileType, f.FilePath }).ToList()
                })
                .ToListAsync();

            var structure = await db.AssemblyStructures.AsNoTracking()
                .Select(s => new { s.ParentComponentVersionId, s.ChildComponentVersionId })
                .ToListAsync();

            var data = new AGR_MigrationData
            {
                StructureRows = structure.Count,
                EditLocks = await db.ComponentEditLocks.CountAsync(),
                ComponentsWithoutVersions = await db.Components.AsNoTracking()
                    .Where(c => !c.Versions.Any())
                    .Select(c => c.PartNumber)
                    .ToListAsync(),
            };

            foreach (var v in versions)
            {
                var models = v.Files.Where(f => f.FileType == AGR_FileType_e.StorageModel && !string.IsNullOrWhiteSpace(f.FilePath)).ToList();
                var drawings = v.Files.Where(f => f.FileType == AGR_FileType_e.StorageDrawing && !string.IsNullOrWhiteSpace(f.FilePath)).ToList();
                data.Versions.Add(new AGR_MigrationVersion
                {
                    VersionId = v.Id,
                    ComponentId = v.ComponentId,
                    PartNumber = v.PartNumber ?? "",
                    Version = v.Version,
                    HashSum = v.HashSum,
                    Name = v.Name ?? "",
                    ComponentType = v.ComponentType,
                    ModelPath = models.FirstOrDefault()?.FilePath,
                    ModelRecords = models.Count,
                    DrawingPath = drawings.FirstOrDefault()?.FilePath,
                });
            }

            var byId = data.Versions.ToDictionary(v => v.VersionId);
            foreach (var s in structure)
            {
                if (!byId.TryGetValue(s.ParentComponentVersionId, out var parent)
                    || !byId.TryGetValue(s.ChildComponentVersionId, out var child)) continue;
                if (!parent.Children.Contains(child)) parent.Children.Add(child);
            }
            return data;
        }

        #endregion

        #region Уровень 1: БД и папки

        private void CheckDatabase(AGR_MigrationData data, AGR_MigrationReport report)
        {
            foreach (var pn in data.ComponentsWithoutVersions)
                report.Add(AGR_Severity.Info, "Компонент без версий", null, pn, "");

            if (data.EditLocks > 0)
                report.Add(AGR_Severity.Warning, "Есть замки редактирования", null, "",
                    $"Записей в ComponentEditLocks: {data.EditLocks}. Перед миграцией снять.");

            foreach (var v in data.Versions)
            {
                if (v.ModelRecords == 0)
                    report.Add(AGR_Severity.Error, "Нет записи о модели в хранилище", v, "", "ComponentFiles без StorageModel");
                else if (v.ModelRecords > 1)
                    report.Add(AGR_Severity.Warning, "Несколько записей о модели", v, v.ModelPath, $"StorageModel: {v.ModelRecords} шт., берётся первая");

                if (v.ComponentType == AGR_ComponentType_e.Assembly && v.Children.Count == 0)
                    report.Add(AGR_Severity.Warning, "Сборка без структуры", v, v.ModelPath, "В AssemblyStructures нет дочерних");
            }
        }

        private void CheckStorageFiles(AGR_MigrationData data, AGR_MigrationReport report)
        {
            var root = NormalizeDir(AGR_Options.StorageRootFolderPath);
            report.Progress($"Хранилище: {root}");

            foreach (var v in data.Versions.Where(v => v.ModelPath != null))
            {
                var path = v.ModelPath!;
                if (!IsUnder(path, root))
                    report.Add(AGR_Severity.Warning, "Модель вне корня хранилища", v, path,
                        IsUnder(path, NormalizeDir(AGR_Options.OldStorageRootFolderPath)) ? "старый корень хранилища" : "");

                v.ModelExists = SafeExists(path);
                if (!v.ModelExists)
                {
                    report.Add(AGR_Severity.Error, "Файл модели не найден", v, path, "");
                    continue;
                }

                var fileName = Path.GetFileName(path);
                var legacy = Path.Combine(AGR_Options.StorageRootFolderPath, v.HashSum.ToString("D10"), fileName);
                if (SamePath(path, v.NewModelPath))
                    report.Add(AGR_Severity.Info, "Уже в новой схеме", v, path, "");
                else
                {
                    if (!SamePath(path, legacy))
                        report.Add(AGR_Severity.Warning, "Нестандартный путь модели", v, path, $"ожидался {legacy}");
                    if (SafeExists(v.NewModelPath!))
                        report.Add(AGR_Severity.Warning, "Целевой файл уже есть", v, v.NewModelPath, "в папке hash\\версия уже лежит файл");
                }

                var drawingNearModel = Path.ChangeExtension(path, ".SLDDRW");
                if (v.DrawingPath != null)
                {
                    v.DrawingExists = SafeExists(v.DrawingPath);
                    if (!v.DrawingExists)
                        report.Add(AGR_Severity.Warning, "Файл чертежа не найден", v, v.DrawingPath, "");
                    else if (!SamePath(v.DrawingPath, drawingNearModel))
                        report.Add(AGR_Severity.Warning, "Чертёж не рядом с моделью", v, v.DrawingPath, "");
                }
                else if (SafeExists(drawingNearModel))
                {
                    v.DrawingPath = drawingNearModel;
                    v.DrawingExists = true;
                    report.Add(AGR_Severity.Info, "Чертёж без записи в БД", v, drawingNearModel, "будет перенесён вместе с моделью");
                }
            }

            // Один старый файл на несколько версий: хеш совпал, файл перезаписан, геометрия старой версии потеряна
            foreach (var g in data.Versions.Where(v => v.ModelPath != null)
                         .GroupBy(v => NormalizePath(v.ModelPath!), StringComparer.OrdinalIgnoreCase)
                         .Where(g => g.Count() > 1))
            {
                var list = string.Join(", ", g.Select(v => $"{v.PartNumber} v{v.Version}"));
                foreach (var v in g)
                    report.Add(AGR_Severity.Warning, "Один файл на несколько версий", v, v.ModelPath, list);
            }

            // Две версии в одну папку hash\версия с одним именем файла
            foreach (var g in data.Versions.Where(v => v.ModelPath != null)
                         .GroupBy(v => NormalizePath(v.NewModelPath!), StringComparer.OrdinalIgnoreCase)
                         .Where(g => g.Count() > 1))
            {
                var list = string.Join(", ", g.Select(v => $"{v.PartNumber} v{v.Version}"));
                foreach (var v in g)
                    report.Add(AGR_Severity.Error, "Совпадение нового пути", v, v.NewModelPath, list);
            }

            CollectOrphans(data, report, root);
        }

        private void CollectOrphans(AGR_MigrationData data, AGR_MigrationReport report, string root)
        {
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var v in data.Versions)
            {
                if (v.ModelPath != null) known.Add(NormalizePath(v.ModelPath));
                if (v.DrawingPath != null) known.Add(NormalizePath(v.DrawingPath));
            }

            try
            {
                foreach (var file in Directory.EnumerateFiles(root, "*.sld*", SearchOption.AllDirectories))
                {
                    var name = Path.GetFileName(file);
                    if (name.StartsWith("~$")) continue;
                    var ext = Path.GetExtension(file);
                    if (!ext.Equals(".sldprt", StringComparison.OrdinalIgnoreCase)
                        && !ext.Equals(".sldasm", StringComparison.OrdinalIgnoreCase)
                        && !ext.Equals(".slddrw", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!known.Contains(NormalizePath(file))) report.Orphans.Add(file);
                }
            }
            catch (Exception ex)
            {
                report.Add(AGR_Severity.Error, "Ошибка обхода хранилища", null, root, ex.Message);
            }
        }

        #endregion

        #region Уровень 1: ссылки сборок и чертежей

        private void CheckReferences(AGR_MigrationData data, AGR_MigrationReport report)
        {
            var byPath = data.Versions.Where(v => v.ModelPath != null)
                .GroupBy(v => NormalizePath(v.ModelPath!), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var assemblies = data.Versions
                .Where(v => v.ModelExists && IsAssembly(v.ModelPath!))
                .ToList();
            var drawings = data.Versions.Where(v => v.ModelExists && v.DrawingExists).ToList();
            int total = assemblies.Count + drawings.Count, done = 0;

            foreach (var asm in assemblies)
            {
                if (report.CheckStop()) return;
                report.Progress($"Ссылки {++done}/{total}: {asm.ModelPath}", onlyEvery: 50, index: done);
                foreach (var refPath in GetReferences(asm, asm.ModelPath!, report))
                    ClassifyReference(asm, "Сборка", refPath, asm.Children, byPath, report);
            }

            foreach (var v in drawings)
            {
                if (report.CheckStop()) return;
                report.Progress($"Ссылки {++done}/{total}: {v.DrawingPath}", onlyEvery: 50, index: done);
                // Чертёж компонента должен ссылаться на модель этой же версии
                foreach (var refPath in GetReferences(v, v.DrawingPath!, report))
                    ClassifyReference(v, "Чертёж", refPath, new List<AGR_MigrationVersion> { v }, byPath, report);
            }
        }

        private List<string> GetReferences(AGR_MigrationVersion owner, string path, AGR_MigrationReport report)
        {
            var result = new List<string>();
            try
            {
                // CHECK: traverse=false (только прямые ссылки), searchflag=false (пути, как сохранены в файле, без правил поиска).
                // Результат - пары «имя, путь»; берём только полные пути моделей.
                var deps = _swApp.Sw.GetDocumentDependencies2(path, false, false, false) as object[];
                if (deps == null) return result;
                foreach (var d in deps.OfType<string>())
                {
                    if (!Path.IsPathRooted(d) || !IsModel(d)) continue;
                    if (SamePath(d, path)) continue;
                    if (!result.Contains(d, StringComparer.OrdinalIgnoreCase)) result.Add(d);
                }
            }
            catch (Exception ex)
            {
                report.Add(AGR_Severity.Error, "Ошибка чтения ссылок", owner, path, ex.Message);
            }
            return result;
        }

        private static void ClassifyReference(
            AGR_MigrationVersion owner, string ownerKind, string refPath,
            IReadOnlyList<AGR_MigrationVersion> expected,
            Dictionary<string, List<AGR_MigrationVersion>> byPath,
            AGR_MigrationReport report)
        {
            // Встроенные/виртуальные компоненты
            if (refPath.Contains("Local\\Temp", StringComparison.OrdinalIgnoreCase)) return;

            var key = NormalizePath(refPath);
            if (expected.Any(c => c.ModelPath != null && SamePath(c.ModelPath, key)))
            {
                report.Count($"{ownerKind}: ссылка на версию из структуры");
                return;
            }

            var byName = expected
                .Where(c => c.ModelPath != null
                    && string.Equals(Path.GetFileName(c.ModelPath), Path.GetFileName(refPath), StringComparison.OrdinalIgnoreCase))
                .ToList();
            var mapped = byName.Count == 1 ? $"{byName[0].PartNumber} v{byName[0].Version}" : "";

            if (byPath.TryGetValue(key, out var others))
            {
                report.Add(AGR_Severity.Warning, $"{ownerKind}: ссылка на версию вне структуры", owner, refPath,
                    "файл версии " + string.Join(", ", others.Select(o => $"{o.PartNumber} v{o.Version}"))
                    + (mapped.Length > 0 ? $"; по имени в структуре: {mapped}" : "; по имени в структуре не найдено"));
                return;
            }

            if (byName.Count == 1)
                report.Add(AGR_Severity.Info, $"{ownerKind}: ссылка найдена по имени", owner, refPath, "в структуре: " + mapped);
            else if (byName.Count > 1)
                report.Add(AGR_Severity.Error, $"{ownerKind}: ссылка по имени неоднозначна", owner, refPath,
                    string.Join(", ", byName.Select(c => $"{c.PartNumber} v{c.Version}")));
            else
                report.Add(AGR_Severity.Error, $"{ownerKind}: ссылка не разрешена", owner, refPath,
                    (SafeExists(refPath) ? "файл существует" : "файла нет")
                    + (IsUnder(refPath, NormalizeDir(AGR_Options.StorageRootFolderPath)) ? ", в хранилище" : ", вне хранилища"));
        }

        #endregion

        #region Уровень 2: открытие моделей

        private void CheckModels(AGR_MigrationData data, AGR_MigrationReport report)
        {
            var groups = data.Versions.Where(v => v.ModelExists)
                .GroupBy(v => NormalizePath(v.ModelPath!), StringComparer.OrdinalIgnoreCase)
                .ToList();
            int done = 0;

            foreach (var g in groups)
            {
                if (report.CheckStop()) return;
                var path = g.Key;
                var first = g.First();
                report.Progress($"Модели {++done}/{groups.Count}: {path}", onlyEvery: 20, index: done);

                // Потерянные ссылки при открытии: сначала среди файлов дочерних версий (на всю глубину)
                _missingRefs.TakeEvents();
                _missingRefs.PreferredPaths = DescendantPaths(g);

                ISwDocument? doc = null;
                try
                {
                    var state = DocumentState_e.Silent | DocumentState_e.ReadOnly | DocumentState_e.Hidden;
                    if (IsAssembly(path)) state |= DocumentState_e.Lightweight; // CHECK: скрытая облегчённая сборка
                    doc = _swApp.Documents.Open(path, state) as ISwDocument;
                    if (doc == null)
                    {
                        report.Add(AGR_Severity.Error, "Модель не открылась", first, path, "");
                        continue;
                    }

                    CheckPartNumber(g.ToList(), doc.Model, report);

                    switch (_markers.TryRead(doc, out var marker, out var error))
                    {
                        case AGR_MarkerReadResult_e.Ok:
                            report.Add(AGR_Severity.Info, "Маркер уже есть", first, path,
                                $"{marker!.PartNumber} v{marker.Version} (Id {marker.ComponentId})");
                            break;
                        case AGR_MarkerReadResult_e.Invalid:
                            report.Add(AGR_Severity.Info, "Маркер повреждён или тестовый", first, path, error ?? "");
                            break;
                        default:
                            report.Count("Модель: маркера нет");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    report.Add(AGR_Severity.Error, "Модель не открылась", first, path, ex.Message);
                }
                finally
                {
                    try { doc?.Close(); } catch (Exception ex) { _logger.LogDebug(ex, $"Миграция: не закрылся {path}"); }
                    _missingRefs.PreferredPaths = null;
                    AddMissingReferences(first, report);
                }
            }
        }

        /// <summary>Файлы всех дочерних версий (на всю глубину AssemblyStructure) версий этого файла.</summary>
        private static List<string> DescendantPaths(IEnumerable<AGR_MigrationVersion> versions)
        {
            var result = new List<string>();
            var visited = new HashSet<int>();
            var stack = new Stack<AGR_MigrationVersion>(versions);
            while (stack.Count > 0)
            {
                var v = stack.Pop();
                if (!visited.Add(v.VersionId)) continue;
                foreach (var child in v.Children)
                {
                    if (child.ModelPath != null) result.Add(child.ModelPath);
                    stack.Push(child);
                }
            }
            return result;
        }

        /// <summary>События ReferenceNotFound при открытии модели - в отчёт.</summary>
        private void AddMissingReferences(AGR_MigrationVersion owner, AGR_MigrationReport report)
        {
            foreach (var e in _missingRefs.TakeEvents())
            {
                switch (e.How)
                {
                    case AGR_MissingReferenceHow_e.Preferred:
                        report.Add(AGR_Severity.Info, "Открытие: потерянная ссылка найдена среди дочерних версий", owner, e.Missing, e.Resolved ?? "");
                        break;
                    case AGR_MissingReferenceHow_e.ByNameUnique:
                        report.Add(AGR_Severity.Warning, "Открытие: потерянная ссылка найдена по имени в хранилище", owner, e.Missing, e.Resolved ?? "");
                        break;
                    case AGR_MissingReferenceHow_e.ByNameAmbiguous:
                        report.Add(AGR_Severity.Warning, "Открытие: потерянная ссылка по имени неоднозначна", owner, e.Missing,
                            $"взят первый из {e.Candidates}: {e.Resolved}");
                        break;
                    default:
                        report.Add(AGR_Severity.Error, "Открытие: потерянная ссылка не найдена", owner, e.Missing, "компонент остался потерянным");
                        break;
                }
            }
        }

        private static void CheckPartNumber(List<AGR_MigrationVersion> versions, IModelDoc2 model, AGR_MigrationReport report)
        {
            var first = versions[0];
            var path = first.ModelPath;
            var dbPn = first.PartNumber;

            var fileLevel = AGR_PropertyReader.Read(model, "", AGR_PropertyNames.Partnumber);
            var byConfig = AGR_PropertyReader.ReadByConfigurations(model, AGR_PropertyNames.Partnumber);
            var configValues = byConfig.Where(c => !string.IsNullOrWhiteSpace(c.Value))
                .Select(c => c.Value!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var configText = string.Join(" | ", byConfig.Select(c => $"{c.Config}={c.Value ?? "(нет)"}"));

            if (byConfig.Count > 1) report.Count("Модель: несколько конфигураций");

            if (!string.IsNullOrWhiteSpace(fileLevel))
            {
                if (string.Equals(fileLevel.Trim(), dbPn, StringComparison.OrdinalIgnoreCase))
                    report.Count("Обозначение: уже на уровне файла");
                else
                    report.Add(AGR_Severity.Warning, "Обозначение файла не совпадает с БД", first, path, $"файл={fileLevel}, БД={dbPn}");
            }

            if (configValues.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(fileLevel))
                    report.Add(AGR_Severity.Info, "Обозначения нет в файле", first, path, $"будет записано из БД: {dbPn}");
            }
            else if (configValues.Count > 1)
                report.Add(AGR_Severity.Info, "Обозначение различается в конфигурациях", first, path, configText + $"; БД={dbPn}");
            else if (!string.Equals(configValues[0], dbPn, StringComparison.OrdinalIgnoreCase))
                report.Add(AGR_Severity.Warning, "Обозначение конфигурации не совпадает с БД", first, path, configText + $"; БД={dbPn}");
            else
                report.Count("Обозначение: в конфигурациях совпадает с БД");
        }

        #endregion

        #region Пути

        internal static string NormalizePath(string path)
        {
            try { return Path.GetFullPath(path).TrimEnd('\\'); }
            catch { return path.Trim().TrimEnd('\\'); }
        }

        private static string NormalizeDir(string? dir) => string.IsNullOrWhiteSpace(dir) ? "" : NormalizePath(dir) + "\\";

        private static bool SamePath(string? a, string? b)
            => a != null && b != null && string.Equals(NormalizePath(a), NormalizePath(b), StringComparison.OrdinalIgnoreCase);

        private static bool IsUnder(string path, string dir)
            => dir.Length > 0 && NormalizePath(path).StartsWith(dir, StringComparison.OrdinalIgnoreCase);

        private static bool SafeExists(string path)
        {
            try { return File.Exists(path); } catch { return false; }
        }

        private static bool IsAssembly(string path) => Path.GetExtension(path).Equals(".sldasm", StringComparison.OrdinalIgnoreCase);

        private static bool IsModel(string path)
        {
            var ext = Path.GetExtension(path);
            return ext.Equals(".sldprt", StringComparison.OrdinalIgnoreCase) || ext.Equals(".sldasm", StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }

    /// <summary>Чтение свойств через interop без создания свойства (AGR_TryGetProp создаёт пустое).</summary>
    public static class AGR_PropertyReader
    {
        /// <param name="config">"" = вкладка Custom уровня файла.</param>
        public static string? Read(IModelDoc2 model, string config, string name)
        {
            var mgr = model.Extension.get_CustomPropertyManager(config);
            if (mgr == null) return null;
            var names = mgr.GetNames() as object[];
            var actual = names?.OfType<string>().FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            if (actual == null) return null;
            mgr.Get5(actual, false, out string value, out _, out _);
            return value;
        }

        public static List<(string Config, string? Value)> ReadByConfigurations(IModelDoc2 model, string name)
        {
            var result = new List<(string, string?)>();
            if (model.GetConfigurationNames() is object[] configs)
                foreach (var c in configs.OfType<string>())
                    result.Add((c, Read(model, c, name)));
            return result;
        }
    }

    #region Данные и запись отчёта

    public enum AGR_Severity { Error, Warning, Info }

    internal class AGR_MigrationVersion
    {
        public int VersionId { get; set; }
        public int ComponentId { get; set; }
        public string PartNumber { get; set; } = "";
        public int Version { get; set; }
        public int HashSum { get; set; }
        public string Name { get; set; } = "";
        public AGR_ComponentType_e ComponentType { get; set; }
        public string? ModelPath { get; set; }
        public int ModelRecords { get; set; }
        public string? DrawingPath { get; set; }
        public bool ModelExists { get; set; }
        public bool DrawingExists { get; set; }
        public List<AGR_MigrationVersion> Children { get; } = new();

        public string? NewModelPath => ModelPath == null ? null : AGR_StoragePaths.GetFilePath(HashSum, Version, ModelPath);
        public string? NewDrawingPath => DrawingPath == null || NewModelPath == null ? null : Path.ChangeExtension(NewModelPath, ".SLDDRW");
    }

    internal class AGR_MigrationData
    {
        public List<AGR_MigrationVersion> Versions { get; } = new();
        public int StructureRows { get; set; }
        public int EditLocks { get; set; }
        public List<string> ComponentsWithoutVersions { get; set; } = new();
    }

    internal class AGR_MigrationReport
    {
        private readonly string _folder;
        private readonly List<string[]> _issues = new();
        private readonly Dictionary<(AGR_Severity, string), int> _byCategory = new();
        private readonly Dictionary<string, int> _counters = new();
        public List<string> Orphans { get; } = new();
        public bool Stopped { get; private set; }

        public AGR_MigrationReport(string folder) => _folder = folder;

        public void Add(AGR_Severity severity, string category, AGR_MigrationVersion? v, string? path, string details)
        {
            _byCategory[(severity, category)] = _byCategory.TryGetValue((severity, category), out var n) ? n + 1 : 1;
            _issues.Add(new[]
            {
                SeverityText(severity), category,
                v?.ComponentId.ToString() ?? "", v?.PartNumber ?? "", v?.Version.ToString() ?? "",
                v?.VersionId.ToString() ?? "", v?.HashSum.ToString() ?? "", v?.ComponentType.ToString() ?? "",
                path ?? "", details
            });
        }

        public void Count(string counter) => _counters[counter] = _counters.TryGetValue(counter, out var n) ? n + 1 : 1;

        /// <summary>Файл STOP в папке отчёта прерывает проход (отчёт пишется по уже собранному).</summary>
        public bool CheckStop()
        {
            if (!Stopped && File.Exists(Path.Combine(_folder, AGR_MigrationReportService.StopFileName))) Stopped = true;
            return Stopped;
        }

        public void Progress(string text, int onlyEvery = 1, int index = 0)
        {
            if (onlyEvery > 1 && index % onlyEvery != 0) return;
            try { File.AppendAllText(Path.Combine(_folder, "progress.txt"), $"{DateTime.Now:HH:mm:ss} {text}\r\n", Encoding.UTF8); }
            catch { /* прогресс не критичен */ }
        }

        public void WriteAll(AGR_MigrationData data, TimeSpan elapsed, bool openFiles)
        {
            WriteCsv("issues.csv",
                new[] { "Важность", "Категория", "ComponentId", "Обозначение", "Версия", "VersionId", "HashSum", "Тип", "Путь", "Подробности" },
                _issues.OrderBy(r => r[0] == "Ошибка" ? 0 : r[0] == "Предупреждение" ? 1 : 2).ThenBy(r => r[1]));

            WriteCsv("plan.csv",
                new[] { "VersionId", "ComponentId", "Обозначение", "Версия", "HashSum", "Тип", "Модель есть", "Старый путь модели", "Новый путь модели", "Старый путь чертежа", "Новый путь чертежа" },
                data.Versions.OrderBy(v => v.PartNumber).ThenBy(v => v.Version).Select(v => new[]
                {
                    v.VersionId.ToString(), v.ComponentId.ToString(), v.PartNumber, v.Version.ToString(), v.HashSum.ToString(),
                    v.ComponentType.ToString(), v.ModelExists ? "да" : "нет",
                    v.ModelPath ?? "", v.NewModelPath ?? "", v.DrawingPath ?? "", v.NewDrawingPath ?? ""
                }));

            WriteCsv("orphans.csv", new[] { "Файл в хранилище без записи в БД" }, Orphans.Select(o => new[] { o }));

            var sb = new StringBuilder();
            sb.AppendLine($"Отчёт перед миграцией хранилища, {DateTime.Now:dd.MM.yyyy HH:mm}");
            sb.AppendLine($"Хранилище: {AGR_Options.StorageRootFolderPath}");
            sb.AppendLine($"Уровень: {(openFiles ? "БД + папки + ссылки + открытие моделей" : "БД + папки + ссылки")}");
            if (Stopped) sb.AppendLine("ПРЕРВАНО файлом STOP: данные неполные.");
            sb.AppendLine($"Время: {elapsed:hh\\:mm\\:ss}");
            sb.AppendLine();
            sb.AppendLine($"Версий: {data.Versions.Count}, с файлом модели: {data.Versions.Count(v => v.ModelExists)}");
            sb.AppendLine($"Сборок (версий): {data.Versions.Count(v => v.ModelPath != null && v.ModelPath.EndsWith(".sldasm", StringComparison.OrdinalIgnoreCase))}");
            sb.AppendLine($"Чертежей: {data.Versions.Count(v => v.DrawingExists)}");
            sb.AppendLine($"Строк AssemblyStructures: {data.StructureRows}");
            sb.AppendLine($"Замков редактирования: {data.EditLocks}");
            sb.AppendLine($"Файлов без записи в БД: {Orphans.Count} (orphans.csv)");
            sb.AppendLine();
            sb.AppendLine("Замечания (issues.csv):");
            foreach (var kv in _byCategory.OrderBy(k => k.Key.Item1).ThenBy(k => k.Key.Item2))
                sb.AppendLine($"  {SeverityText(kv.Key.Item1),-15} {kv.Key.Item2}: {kv.Value}");
            sb.AppendLine();
            sb.AppendLine("Без замечаний (счётчики):");
            foreach (var kv in _counters.OrderBy(k => k.Key))
                sb.AppendLine($"  {kv.Key}: {kv.Value}");

            File.WriteAllText(Path.Combine(_folder, "summary.txt"), sb.ToString(), new UTF8Encoding(true));
        }

        private void WriteCsv(string name, string[] header, IEnumerable<string[]> rows)
        {
            using var w = new StreamWriter(Path.Combine(_folder, name), false, new UTF8Encoding(true)); // BOM для Excel
            w.WriteLine(string.Join(";", header.Select(Escape)));
            foreach (var r in rows) w.WriteLine(string.Join(";", r.Select(Escape)));
        }

        private static string Escape(string s)
        {
            if (s.IndexOfAny(new[] { ';', '"', '\n', '\r' }) < 0) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        private static string SeverityText(AGR_Severity s) => s switch
        {
            AGR_Severity.Error => "Ошибка",
            AGR_Severity.Warning => "Предупреждение",
            _ => "Инфо"
        };
    }

    #endregion
}
