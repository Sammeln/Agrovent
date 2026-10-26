// File: Services/Migration/AGR_StorageMigrationService.cs
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Text;
using Agrovent.DAL;
using AgroventInfrastructure;
using AgroventInfrastructure.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xarial.XCad.SolidWorks;

namespace Agrovent.Services.Migration
{
    /// <summary>
    /// Миграция хранилища (временно, ветка MarkerMigrationTest). Этап 3: перенос файлов и ссылок.
    /// Каждая версия снизу вверх по AssemblyStructures: модель, чертёж и файлы с тем же именем копируются из
    /// ComponentFiles.FilePath в StorageRoot\guid\версия; в копиях сборок и чертежей ссылки перенаправляются
    /// на файлы версий в новой схеме (ReplaceReferencedDocument по закрытому файлу). Старые файлы не трогаются.
    /// Журнал - MigrationLog (Status = Copied / Error), повторный запуск пропускает Copied и Done.
    /// Открытие, «Обозначение», маркер и запись FilePath в БД - этап 4 (Copied → Done).
    /// </summary>
    public interface IAGR_StorageMigrationService
    {
        /// <summary>Вызывать с главного потока SolidWorks, все документы закрыты. Возвращает папку прогона.</summary>
        Task<AGR_StorageMigrationRun> RunAsync();
    }

    public record AGR_StorageMigrationRun(string Folder, int Total, int Copied, int Skipped, int Errors,
        int ReferencesReplaced, int ReferencesUnresolved, bool Stopped, TimeSpan Elapsed);

    public class AGR_StorageMigrationService : IAGR_StorageMigrationService
    {
        public const string StatusCopied = "Copied";
        public const string StatusDone = "Done";
        public const string StatusError = "Error";
        public const string StatusMerged = "Merged";

        // Модели и чертежи копируются отдельно, остальные файлы с тем же именем (pdf, dxf, step...) - как есть
        internal static readonly string[] SolidWorksExtensions = { ".sldprt", ".sldasm", ".slddrw" };

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ISwApplication _swApp;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AGR_StorageMigrationService> _logger;

        public AGR_StorageMigrationService(IServiceScopeFactory scopeFactory, ISwApplication swApp,
            IConfiguration configuration, ILogger<AGR_StorageMigrationService> logger)
        {
            _scopeFactory = scopeFactory;
            _swApp = swApp;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<AGR_StorageMigrationRun> RunAsync()
        {
            var folder = Path.Combine(AGR_MigrationReportService.ReportsRoot, "Migration_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(folder);
            var timer = Stopwatch.StartNew();

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();

            // Все чтения БД до работы с SolidWorks; дальше цикл синхронный, журнал пишется синхронно
            var data = await LoadAsync(db);
            var run = new AGR_MigrationRunState(folder, data, ReadAliases(), _swApp, db.Database.GetDbConnection(), _logger);
            run.Progress($"Версий: {data.Versions.Count}, в журнале: {data.Log.Count}, переведено дублей: {data.Merged.Count}. " +
                         $"Хранилище: {AGR_Options.StorageRootFolderPath}");
            foreach (var a in run.Aliases) run.Progress($"Алиас ссылок: {a.From} -> {a.To}");

            db.Database.OpenConnection();
            try
            {
                run.Execute();
            }
            finally
            {
                db.Database.CloseConnection();
                run.WriteFiles(timer.Elapsed);
            }

            _logger.LogInformation($"Миграция: перенос файлов завершён, {folder}");
            return new AGR_StorageMigrationRun(folder, data.Versions.Count, run.CopiedCount, run.SkippedCount, run.ErrorCount,
                run.ReplacedCount, run.UnresolvedCount, run.Stopped, timer.Elapsed);
        }

        /// <summary>Migration:ReferenceAliases - пары From/To: префикс пути, как он записан в ссылках файлов,
        /// и где эти файлы лежат сейчас (на тесте ссылки на \\192.168.10.1\kd\!AgroventStorage, а файлы в копии).</summary>
        private List<AGR_PathAlias> ReadAliases()
        {
            var result = new List<AGR_PathAlias>();
            foreach (var s in _configuration.GetSection("Migration:ReferenceAliases").GetChildren())
            {
                var from = s["From"];
                var to = s["To"];
                if (!string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to))
                    result.Add(new AGR_PathAlias(AGR_MigrationReportService.NormalizeDir(from), AGR_MigrationReportService.NormalizeDir(to)));
            }
            return result;
        }

        private static async Task<AGR_StorageMigrationData> LoadAsync(DataContext db)
        {
            var versions = await db.ComponentVersions.AsNoTracking()
                .Select(v => new AGR_StorageMigrationVersion
                {
                    VersionId = v.Id,
                    ComponentId = v.ComponentId,
                    PartNumber = v.Component.PartNumber,
                    StorageGuid = v.Component.StorageGuid,
                    Version = v.Version,
                    ComponentType = v.ComponentType,
                    OldModelPath = v.Files.Where(f => f.FileType == AGR_FileType_e.StorageModel).Select(f => f.FilePath).FirstOrDefault(),
                    OldDrawingPath = v.Files.Where(f => f.FileType == AGR_FileType_e.StorageDrawing).Select(f => f.FilePath).FirstOrDefault(),
                })
                .ToListAsync();

            var structure = await db.AssemblyStructures.AsNoTracking()
                .Select(s => new { s.ParentComponentVersionId, s.ChildComponentVersionId })
                .ToListAsync();

            var data = new AGR_StorageMigrationData();
            data.Versions.AddRange(versions);
            var byId = versions.ToDictionary(v => v.VersionId);
            foreach (var s in structure)
            {
                if (!byId.TryGetValue(s.ParentComponentVersionId, out var parent)
                    || !byId.TryGetValue(s.ChildComponentVersionId, out var child)) continue;
                if (!parent.Children.Contains(child)) parent.Children.Add(child);
            }

            var conn = db.Database.GetDbConnection();
            await db.Database.OpenConnectionAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT \"ComponentVersionId\", \"Status\", \"NewModelPath\", \"NewDrawingPath\", \"OldModelPath\", " +
                                  "\"MergedIntoVersionId\" FROM public.\"MigrationLog\"";
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var row = new AGR_MigrationLogRow(
                        r.GetInt32(0), r.GetString(1),
                        r.IsDBNull(2) ? null : r.GetString(2),
                        r.IsDBNull(3) ? null : r.GetString(3),
                        r.IsDBNull(4) ? null : r.GetString(4),
                        r.IsDBNull(5) ? null : r.GetInt32(5));
                    if (row.Status == StatusMerged)
                    {
                        if (row.OldModelPath != null && row.MergedIntoVersionId is int main && byId.TryGetValue(main, out var mainVersion))
                            data.Merged[AGR_MigrationReportService.NormalizePath(row.OldModelPath)] = mainVersion;
                    }
                    else data.Log[row.VersionId] = row;
                }
            }
            finally
            {
                await db.Database.CloseConnectionAsync();
            }
            return data;
        }
    }

    #region Данные

    internal record AGR_PathAlias(string From, string To);

    internal record AGR_MigrationLogRow(int VersionId, string Status, string? NewModelPath, string? NewDrawingPath,
        string? OldModelPath, int? MergedIntoVersionId);

    internal class AGR_StorageMigrationVersion
    {
        public int VersionId { get; set; }
        public int ComponentId { get; set; }
        public string PartNumber { get; set; } = "";
        public Guid StorageGuid { get; set; }
        public int Version { get; set; }
        public AGR_ComponentType_e ComponentType { get; set; }
        public string? OldModelPath { get; set; }
        public string? OldDrawingPath { get; set; }
        public List<AGR_StorageMigrationVersion> Children { get; } = new();

        /// <summary>Файл модели, найденный на диске (OldModelPath или вариант без пробелов по краям имени).</summary>
        public string? SourceModelPath { get; set; }
        /// <summary>Файл модели в новой схеме; null - версия не перенесена.</summary>
        public string? NewModelPath { get; set; }
        public string? NewDrawingPath { get; set; }

        public string Title => $"{PartNumber} v{Version}";
        public string VersionFolder => AGR_StoragePaths.GetVersionFolder(StorageGuid, Version);
    }

    internal class AGR_StorageMigrationData
    {
        public List<AGR_StorageMigrationVersion> Versions { get; } = new();
        /// <summary>Журнал по VersionId (кроме Merged).</summary>
        public Dictionary<int, AGR_MigrationLogRow> Log { get; } = new();
        /// <summary>Старый файл дубля покупного → версия основного (этап 2).</summary>
        public Dictionary<string, AGR_StorageMigrationVersion> Merged { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    #endregion

    /// <summary>Один прогон: порядок обхода, копирование, ссылки, журнал и файлы прогона.</summary>
    internal class AGR_MigrationRunState
    {
        private readonly string _folder;
        private readonly AGR_StorageMigrationData _data;
        private readonly ISwApplication _swApp;
        private readonly DbConnection _conn;
        private readonly ILogger _logger;
        private readonly string _machine = System.Environment.MachineName;

        private readonly Dictionary<string, List<AGR_StorageMigrationVersion>> _byOldPath = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<AGR_StorageMigrationVersion>> _byFileName = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, List<string>>? _oldStorageIndex;
        private readonly List<string[]> _events = new();
        private readonly Dictionary<string, int> _counters = new();

        public List<AGR_PathAlias> Aliases { get; }
        public int CopiedCount { get; private set; }
        public int SkippedCount { get; private set; }
        public int ErrorCount { get; private set; }
        public int ReplacedCount { get; private set; }
        public int UnresolvedCount { get; private set; }
        public bool Stopped { get; private set; }

        public AGR_MigrationRunState(string folder, AGR_StorageMigrationData data, List<AGR_PathAlias> aliases,
            ISwApplication swApp, DbConnection conn, ILogger logger)
        {
            _folder = folder;
            _data = data;
            Aliases = aliases;
            _swApp = swApp;
            _conn = conn;
            _logger = logger;

            foreach (var v in data.Versions.Where(v => !string.IsNullOrWhiteSpace(v.OldModelPath)))
            {
                Add(_byOldPath, AGR_MigrationReportService.NormalizePath(v.OldModelPath!), v);
                Add(_byFileName, FileKey(v.OldModelPath!), v);
            }
        }

        public void Execute()
        {
            var order = BottomUp();
            int done = 0;
            foreach (var v in order)
            {
                if (CheckStop()) break;
                Progress($"{++done}/{order.Count}: {v.Title} {v.OldModelPath}", onlyEvery: 25, index: done);
                try
                {
                    ProcessVersion(v);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Миграция: {v.Title}");
                    Fail(v, "Исключение: " + ex.Message);
                }
            }
            Progress($"Готово: перенесено {CopiedCount}, пропущено (уже в журнале) {SkippedCount}, ошибок {ErrorCount}.");
        }

        #region Порядок обхода

        /// <summary>Дети раньше родителей (обход в глубину, post-order). Цикл в структуре - в события, ребро пропускается.</summary>
        private List<AGR_StorageMigrationVersion> BottomUp()
        {
            var result = new List<AGR_StorageMigrationVersion>();
            var state = new Dictionary<int, int>(); // 1 - в обходе, 2 - готово
            foreach (var root in _data.Versions.OrderBy(v => v.PartNumber).ThenBy(v => v.Version))
            {
                if (state.ContainsKey(root.VersionId)) continue;
                var stack = new Stack<(AGR_StorageMigrationVersion V, int Next)>();
                stack.Push((root, 0));
                state[root.VersionId] = 1;
                while (stack.Count > 0)
                {
                    var (v, next) = stack.Pop();
                    if (next < v.Children.Count)
                    {
                        stack.Push((v, next + 1));
                        var child = v.Children[next];
                        if (!state.TryGetValue(child.VersionId, out var s))
                        {
                            state[child.VersionId] = 1;
                            stack.Push((child, 0));
                        }
                        else if (s == 1)
                            Event(AGR_Severity.Error, "Цикл в структуре", v, "", child.Title, "");
                        continue;
                    }
                    state[v.VersionId] = 2;
                    result.Add(v);
                }
            }
            return result;
        }

        #endregion

        #region Версия

        private void ProcessVersion(AGR_StorageMigrationVersion v)
        {
            // Уже перенесена прошлым прогоном
            if (_data.Log.TryGetValue(v.VersionId, out var log)
                && (log.Status == AGR_StorageMigrationService.StatusCopied || log.Status == AGR_StorageMigrationService.StatusDone)
                && log.NewModelPath != null && File.Exists(log.NewModelPath))
            {
                v.NewModelPath = log.NewModelPath;
                v.NewDrawingPath = log.NewDrawingPath;
                SkippedCount++;
                return;
            }

            if (v.StorageGuid == Guid.Empty) { Fail(v, "нет StorageGuid"); return; }
            if (string.IsNullOrWhiteSpace(v.OldModelPath)) { Fail(v, "нет записи о модели (ComponentFiles)"); return; }

            v.SourceModelPath = FindSource(v.OldModelPath!);
            if (v.SourceModelPath == null) { Fail(v, "файл модели не найден: " + v.OldModelPath); return; }

            var folder = v.VersionFolder;
            Directory.CreateDirectory(folder);
            var newModel = Path.Combine(folder, Path.GetFileName(v.SourceModelPath));
            CopyFile(v.SourceModelPath, newModel);

            // Чертёж: из БД, иначе рядом с моделью с тем же именем (15 чертежей без записи - запись появится на этапе 4)
            string? newDrawing = null;
            var drawing = !string.IsNullOrWhiteSpace(v.OldDrawingPath) ? FindSource(v.OldDrawingPath!) : null;
            if (drawing == null && !string.IsNullOrWhiteSpace(v.OldDrawingPath))
                Event(AGR_Severity.Warning, "Чертёж из БД не найден", v, v.OldDrawingPath!, "", "");
            drawing ??= FindSource(Path.ChangeExtension(v.SourceModelPath, ".SLDDRW"));
            if (drawing != null)
            {
                newDrawing = Path.Combine(folder, Path.GetFileName(drawing));
                CopyFile(drawing, newDrawing);
            }

            int extras = 0;
            foreach (var extra in SameNameFiles(v.SourceModelPath))
            {
                CopyFile(extra, Path.Combine(folder, Path.GetFileName(extra)));
                extras++;
            }

            v.NewModelPath = newModel;
            v.NewDrawingPath = newDrawing;

            int unresolvedBefore = UnresolvedCount;
            var handled = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { newModel };
            if (AGR_MigrationReportService.IsAssembly(newModel))
                FixReferences(v, newModel, "Сборка", v.Children, handled);
            if (newDrawing != null)
                FixReferences(v, newDrawing, "Чертёж", v.Children.Prepend(v).ToList(), handled);

            var unresolved = UnresolvedCount - unresolvedBefore;
            var message = $"файлов рядом: {extras}" + (unresolved > 0 ? $"; НЕ разрешено ссылок: {unresolved} (refs.csv)" : "");
            WriteLog(v, AGR_StorageMigrationService.StatusCopied, message);
            CopiedCount++;
            Count(unresolved > 0 ? "Версия: перенесена с неразрешёнными ссылками" : "Версия: перенесена");
        }

        private void Fail(AGR_StorageMigrationVersion v, string message)
        {
            v.NewModelPath = null;
            ErrorCount++;
            Event(AGR_Severity.Error, "Версия не перенесена", v, v.OldModelPath ?? "", "", message);
            try { WriteLog(v, AGR_StorageMigrationService.StatusError, message); }
            catch (Exception ex) { _logger.LogError(ex, $"Миграция: журнал {v.Title}"); }
        }

        /// <summary>Существующий файл: путь как есть, через алиас, или с именем без пробелов по краям
        /// (« VB-800 …» - в БД имя с пробелом в начале).</summary>
        private string? FindSource(string path)
        {
            foreach (var p in new[] { path, ApplyAlias(path) }.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (AGR_MigrationReportService.SafeExists(p)) return p;
                var dir = Path.GetDirectoryName(p);
                var trimmed = dir == null ? null : Path.Combine(dir, Path.GetFileName(p).Trim());
                if (trimmed != null && AGR_MigrationReportService.SafeExists(trimmed)) return trimmed;
            }
            return null;
        }

        private static void CopyFile(string source, string dest)
        {
            if (File.Exists(dest)) File.SetAttributes(dest, File.GetAttributes(dest) & ~FileAttributes.ReadOnly);
            File.Copy(source, dest, true);
            File.SetAttributes(dest, File.GetAttributes(dest) & ~FileAttributes.ReadOnly);
        }

        private static IEnumerable<string> SameNameFiles(string model)
        {
            var dir = Path.GetDirectoryName(model);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) yield break;
            var baseName = Path.GetFileNameWithoutExtension(model);
            foreach (var file in Directory.EnumerateFiles(dir, baseName + ".*"))
            {
                if (!string.Equals(Path.GetFileNameWithoutExtension(file), baseName, StringComparison.OrdinalIgnoreCase)) continue;
                if (AGR_StorageMigrationService.SolidWorksExtensions.Any(e => e.Equals(Path.GetExtension(file), StringComparison.OrdinalIgnoreCase))) continue;
                if (Path.GetFileName(file).StartsWith("~$")) continue;
                yield return file;
            }
        }

        #endregion

        #region Ссылки

        /// <summary>Прямые ссылки скопированного файла → файлы версий в новой схеме.
        /// expected - где искать в первую очередь (дети сборки; для чертежа своя версия и её дети).</summary>
        private void FixReferences(AGR_StorageMigrationVersion owner, string file, string kind,
            IReadOnlyList<AGR_StorageMigrationVersion> expected, HashSet<string> handled)
        {
            foreach (var reference in GetReferences(owner, file))
            {
                if (reference.Contains("Local\\Temp", StringComparison.OrdinalIgnoreCase)) continue; // встроенные/виртуальные

                var (target, how) = Resolve(owner, reference, expected, handled);
                if (target == null)
                {
                    UnresolvedCount++;
                    Event(AGR_Severity.Error, $"{kind}: ссылка не разрешена", owner, file, reference, how);
                    continue;
                }
                if (AGR_MigrationReportService.SamePath(reference, target))
                {
                    Count($"{kind}: ссылка уже верная");
                    continue;
                }

                bool ok;
                try { ok = _swApp.Sw.ReplaceReferencedDocument(file, reference, target); }
                catch (Exception ex) { ok = false; how += "; " + ex.Message; }

                if (ok)
                {
                    ReplacedCount++;
                    Count($"{kind}: {how}");
                    if (!how.StartsWith("по пути из структуры")) Event(AGR_Severity.Info, $"{kind}: {how}", owner, file, reference, target);
                }
                else
                {
                    UnresolvedCount++;
                    Event(AGR_Severity.Error, $"{kind}: замена ссылки не выполнена", owner, file, reference, target + " (" + how + ")");
                }
            }
        }

        private List<string> GetReferences(AGR_StorageMigrationVersion owner, string path)
        {
            var result = new List<string>();
            try
            {
                // traverse=false (только прямые), searchflag=false (пути, как записаны в файле)
                if (_swApp.Sw.GetDocumentDependencies2(path, false, false, false) is not object[] deps) return result;
                foreach (var d in deps.OfType<string>())
                {
                    if (!Path.IsPathRooted(d) || !AGR_MigrationReportService.IsModel(d)) continue;
                    if (AGR_MigrationReportService.SamePath(d, path)) continue;
                    if (!result.Contains(d, StringComparer.OrdinalIgnoreCase)) result.Add(d);
                }
            }
            catch (Exception ex)
            {
                Event(AGR_Severity.Error, "Ошибка чтения ссылок", owner, path, "", ex.Message);
            }
            return result;
        }

        /// <summary>Порядок: точный старый путь среди expected → точный старый путь в БД (один) → дубль покупного →
        /// имя среди expected (один) → имя в БД (один) → файл существует (сирота) → имя в старом хранилище (один).
        /// Сирота копируется в папку версии владельца. Неоднозначность не угадывается.</summary>
        private (string? Target, string How) Resolve(AGR_StorageMigrationVersion owner, string reference,
            IReadOnlyList<AGR_StorageMigrationVersion> expected, HashSet<string> handled)
        {
            var path = AGR_MigrationReportService.NormalizePath(ApplyAlias(reference));
            var key = FileKey(reference);
            string notes = "";

            var exact = expected.Where(c => c.OldModelPath != null && SameOld(c, path)).Distinct().ToList();
            if (exact.Count > 0) return Target(exact[0], "по пути из структуры" + (exact.Count > 1 ? $" (версий на файл {exact.Count}, взята {exact[0].Title})" : ""));

            if (_byOldPath.TryGetValue(path, out var byPath))
            {
                if (byPath.Count == 1) return Target(byPath[0], "по пути, версия вне структуры");
                notes = $"путь у {byPath.Count} версий: {string.Join(", ", byPath.Select(c => c.Title))}";
            }

            if (_data.Merged.TryGetValue(path, out var main)) return Target(main, "дубль покупного -> основной");

            var byName = expected.Where(c => c.OldModelPath != null && FileKey(c.OldModelPath) == key).Distinct().ToList();
            if (byName.Count == 1) return Target(byName[0], "по имени в структуре");
            if (byName.Count > 1) return (null, $"по имени в структуре неоднозначно: {string.Join(", ", byName.Select(c => c.Title))}");

            if (notes.Length == 0 && _byFileName.TryGetValue(key, out var inDb))
            {
                if (inDb.Count == 1) return Target(inDb[0], "по имени в БД");
                notes = $"по имени в БД неоднозначно: {string.Join(", ", inDb.Select(c => c.Title))}";
            }
            if (notes.Length > 0) return (null, notes);

            // Сирота: файла нет в БД (исключённые из спецификации, конверты, старая библиотека покупных).
            // Сначала файл по ссылке в старом хранилище, потом одноимённый там же, последним - локальный файл
            // (C:\Work и т.п. на этой машине может быть другой версией).
            var existing = FindSource(reference);
            if (existing != null && OldStorageRoots().Any(r => AGR_MigrationReportService.IsUnder(existing, r)))
                return (CopyOrphan(owner, existing, handled), "сирота из старого хранилища, скопирована к владельцу");

            var found = OldStorageIndex().TryGetValue(Path.GetFileName(reference).Trim(), out var list) ? list : new List<string>();
            if (found.Count == 1)
                return (CopyOrphan(owner, found[0], handled), "сирота по имени в старом хранилище, скопирована к владельцу");
            if (found.Count > 1)
                return (null, $"в старом хранилище по имени неоднозначно: {string.Join(", ", found.Take(5))}");
            if (existing != null)
                return (CopyOrphan(owner, existing, handled), "сирота: ЛОКАЛЬНЫЙ файл, скопирован к владельцу");
            return (null, "файл не найден ни в БД, ни на диске");
        }

        private (string? Target, string How) Target(AGR_StorageMigrationVersion v, string how)
            => v.NewModelPath != null ? (v.NewModelPath, how) : (null, $"{how}: версия {v.Title} не перенесена");

        private bool SameOld(AGR_StorageMigrationVersion v, string normalizedPath)
            => string.Equals(AGR_MigrationReportService.NormalizePath(v.OldModelPath!), normalizedPath, StringComparison.OrdinalIgnoreCase)
               || (v.SourceModelPath != null && AGR_MigrationReportService.SamePath(v.SourceModelPath, normalizedPath));

        /// <summary>Копия файла-сироты в папку версии владельца; у сироты-сборки ссылки разрешаются по детям владельца.</summary>
        private string CopyOrphan(AGR_StorageMigrationVersion owner, string orphan, HashSet<string> handled)
        {
            var dest = Path.Combine(owner.VersionFolder, Path.GetFileName(orphan));
            if (!handled.Add(dest)) return dest;
            CopyFile(orphan, dest);
            Count("Сирота: скопирована");
            if (AGR_MigrationReportService.IsAssembly(dest))
                FixReferences(owner, dest, "Сирота-сборка", owner.Children, handled);
            return dest;
        }

        /// <summary>Корни старого хранилища: To алиасов, StorageRoot (боевой запуск: старые папки хеша рядом с новыми),
        /// старая библиотека покупных.</summary>
        private List<string> OldStorageRoots()
            => Aliases.Select(a => a.To)
                .Append(AGR_MigrationReportService.NormalizeDir(AGR_Options.StorageRootFolderPath))
                .Append(AGR_MigrationReportService.NormalizeDir(AGR_Options.OldStorageRootFolderPath))
                .Where(r => r.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        /// <summary>Индекс «имя → файлы» по корням старого хранилища (без папок новой схемы).</summary>
        private Dictionary<string, List<string>> OldStorageIndex()
        {
            if (_oldStorageIndex != null) return _oldStorageIndex;
            var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var roots = OldStorageRoots();

            foreach (var root in roots.Where(SafeDirectoryExists))
            {
                try
                {
                    foreach (var file in Directory.EnumerateFiles(root, "*.sld*", SearchOption.AllDirectories))
                    {
                        if (!AGR_MigrationReportService.IsModel(file)) continue;
                        // Новая схема в том же корне (боевой запуск) - не источник
                        if (AGR_MigrationReportService.IsUnder(file, AGR_MigrationReportService.NormalizeDir(AGR_Options.StorageRootFolderPath))
                            && Guid.TryParse(Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(file))), out _)) continue;
                        Add(index, Path.GetFileName(file).Trim(), file);
                    }
                }
                catch (Exception ex)
                {
                    Event(AGR_Severity.Error, "Ошибка обхода старого хранилища", null, root, "", ex.Message);
                }
            }
            Progress($"Индекс старого хранилища: {index.Count} имён ({string.Join(", ", roots)})");
            return _oldStorageIndex = index;
        }

        private static bool SafeDirectoryExists(string dir)
        {
            try { return Directory.Exists(dir); } catch { return false; }
        }

        private string ApplyAlias(string path)
        {
            var normalized = AGR_MigrationReportService.NormalizePath(path);
            foreach (var a in Aliases)
                if (normalized.StartsWith(a.From, StringComparison.OrdinalIgnoreCase))
                    return a.To + normalized.Substring(a.From.Length);
            return path;
        }

        private static string FileKey(string path) => Path.GetFileName(path).Trim().ToUpperInvariant();

        private static void Add<T>(Dictionary<string, List<T>> map, string key, T value)
        {
            if (!map.TryGetValue(key, out var list)) map[key] = list = new List<T>();
            if (!list.Contains(value)) list.Add(value);
        }

        #endregion

        #region Журнал и файлы прогона

        private void WriteLog(AGR_StorageMigrationVersion v, string status, string message)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText =
                "INSERT INTO public.\"MigrationLog\" (\"ComponentVersionId\", \"Status\", \"OldModelPath\", \"NewModelPath\", " +
                "\"OldDrawingPath\", \"NewDrawingPath\", \"Message\", \"Attempts\", \"MachineName\", \"StartedAt\", \"FinishedAt\") " +
                "VALUES (@v, @status, @oldModel, @newModel, @oldDrawing, @newDrawing, @msg, 1, @machine, now(), now()) " +
                "ON CONFLICT (\"ComponentVersionId\") DO UPDATE SET \"Status\" = EXCLUDED.\"Status\", " +
                "\"OldModelPath\" = EXCLUDED.\"OldModelPath\", \"NewModelPath\" = EXCLUDED.\"NewModelPath\", " +
                "\"OldDrawingPath\" = EXCLUDED.\"OldDrawingPath\", \"NewDrawingPath\" = EXCLUDED.\"NewDrawingPath\", " +
                "\"Message\" = EXCLUDED.\"Message\", \"Attempts\" = public.\"MigrationLog\".\"Attempts\" + 1, " +
                "\"MachineName\" = EXCLUDED.\"MachineName\", \"StartedAt\" = now(), \"FinishedAt\" = now()";
            AddParam(cmd, "v", v.VersionId);
            AddParam(cmd, "status", status);
            AddParam(cmd, "oldModel", v.SourceModelPath ?? v.OldModelPath);
            AddParam(cmd, "newModel", v.NewModelPath);
            AddParam(cmd, "oldDrawing", v.OldDrawingPath);
            AddParam(cmd, "newDrawing", v.NewDrawingPath);
            AddParam(cmd, "msg", message);
            AddParam(cmd, "machine", _machine);
            cmd.ExecuteNonQuery();
        }

        private static void AddParam(DbCommand cmd, string name, object? value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        private void Event(AGR_Severity severity, string category, AGR_StorageMigrationVersion? v, string file, string reference, string details)
        {
            Count($"{SeverityText(severity)}: {category}");
            _events.Add(new[]
            {
                SeverityText(severity), category, v?.VersionId.ToString() ?? "", v?.PartNumber ?? "", v?.Version.ToString() ?? "",
                file, reference, details
            });
        }

        private void Count(string counter) => _counters[counter] = _counters.TryGetValue(counter, out var n) ? n + 1 : 1;

        /// <summary>Файл STOP в папке прогона прерывает обход (после текущей версии).</summary>
        private bool CheckStop()
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

        public void WriteFiles(TimeSpan elapsed)
        {
            AGR_MigrationReport.WriteCsv(Path.Combine(_folder, "refs.csv"),
                new[] { "Важность", "Категория", "VersionId", "Обозначение", "Версия", "Файл", "Ссылка", "Подробности / новая ссылка" },
                _events.OrderBy(r => r[0] == "Ошибка" ? 0 : r[0] == "Предупреждение" ? 1 : 2).ThenBy(r => r[1]));

            var sb = new StringBuilder();
            sb.AppendLine($"Миграция хранилища: перенос файлов и ссылок, {DateTime.Now:dd.MM.yyyy HH:mm}");
            sb.AppendLine($"Хранилище: {AGR_Options.StorageRootFolderPath}");
            if (Stopped) sb.AppendLine("ПРЕРВАНО файлом STOP: повторный запуск продолжит с неперенесённых.");
            sb.AppendLine($"Время: {elapsed:hh\\:mm\\:ss}");
            sb.AppendLine();
            sb.AppendLine($"Версий: {_data.Versions.Count}, перенесено: {CopiedCount}, уже в журнале: {SkippedCount}, ошибок: {ErrorCount}");
            sb.AppendLine($"Ссылок заменено: {ReplacedCount}, не разрешено: {UnresolvedCount}");
            sb.AppendLine();
            foreach (var kv in _counters.OrderBy(k => k.Key))
                sb.AppendLine($"  {kv.Key}: {kv.Value}");
            File.WriteAllText(Path.Combine(_folder, "summary.txt"), sb.ToString(), new UTF8Encoding(true));
        }

        private static string SeverityText(AGR_Severity s) => s switch
        {
            AGR_Severity.Error => "Ошибка",
            AGR_Severity.Warning => "Предупреждение",
            _ => "Инфо"
        };

        #endregion
    }
}
