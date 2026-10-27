// File: Services/Migration/AGR_StorageRebaseService.cs
using System.IO;
using System.Text;
using AgroventInfrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xarial.XCad.SolidWorks;

namespace Agrovent.Services.Migration
{
    /// <summary>
    /// Смена корня хранилища после миграции (временно, ветка MarkerMigrationTest).
    /// Миграция выполнялась в локальную папку (Migration:RebaseFrom), затем папки guid скопированы в боевой
    /// StorageRootFolderPath. Ссылки в файлах сборок, чертежей и деталей (производные) остались на старый корень:
    /// каждая ссылка «RebaseFrom\...» заменяется на «StorageRoot\...» (ReplaceReferencedDocument по закрытому файлу).
    /// БД не меняется: ComponentFiles.FilePath переводится SQL-скриптом. Повторный запуск безопасен (замен уже не найдёт).
    /// </summary>
    public interface IAGR_StorageRebaseService
    {
        /// <summary>Вызывать с главного потока SolidWorks, все документы закрыты.</summary>
        AGR_StorageRebaseRun Run();

        /// <summary>Migration:RebaseFrom из настроек; null - не задан.</summary>
        string? RebaseFrom { get; }
    }

    public record AGR_StorageRebaseRun(string Folder, int Files, int Replaced, int Failed, int Missing, int OtherRoots,
        bool Stopped, TimeSpan Elapsed);

    public class AGR_StorageRebaseService : IAGR_StorageRebaseService
    {
        private readonly ISwApplication _swApp;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AGR_StorageRebaseService> _logger;

        public AGR_StorageRebaseService(ISwApplication swApp, IConfiguration configuration, ILogger<AGR_StorageRebaseService> logger)
        {
            _swApp = swApp;
            _configuration = configuration;
            _logger = logger;
        }

        public string? RebaseFrom
        {
            get
            {
                var value = _configuration["Migration:RebaseFrom"];
                return string.IsNullOrWhiteSpace(value) ? null : AGR_MigrationReportService.NormalizeDir(value);
            }
        }

        public AGR_StorageRebaseRun Run()
        {
            var from = RebaseFrom ?? throw new InvalidOperationException("Не задан Migration:RebaseFrom в appsettings.json.");
            var to = AGR_MigrationReportService.NormalizeDir(AGR_Options.StorageRootFolderPath);
            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Migration:RebaseFrom совпадает с хранилищем: {to}");
            if (!Directory.Exists(to))
                throw new InvalidOperationException($"Хранилище не найдено: {to}");

            var folder = Path.Combine(AGR_MigrationReportService.ReportsRoot, "Rebase_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(folder);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var events = new List<string[]>();
            int files = 0, replaced = 0, failed = 0, missing = 0, otherRoots = 0;
            bool stopped = false;

            void Progress(string text)
            {
                try { File.AppendAllText(Path.Combine(folder, "progress.txt"), $"{DateTime.Now:HH:mm:ss} {text}\r\n", Encoding.UTF8); }
                catch { /* прогресс не критичен */ }
            }

            Progress($"Старый корень: {from}");
            Progress($"Новый корень: {to}");

            var list = EnumerateStorageFiles(to).ToList();
            Progress($"Файлов в папках guid: {list.Count}");

            foreach (var file in list)
            {
                if (File.Exists(Path.Combine(folder, AGR_MigrationReportService.StopFileName))) { stopped = true; break; }
                if (++files % 100 == 0) Progress($"{files}/{list.Count}: {file}");

                List<string> references;
                try
                {
                    // traverse=false (только прямые), searchflag=false (пути, как записаны в файле)
                    references = (_swApp.Sw.GetDocumentDependencies2(file, false, false, false) as object[])?
                        .OfType<string>()
                        .Where(d => Path.IsPathRooted(d) && !AGR_MigrationReportService.SamePath(d, file))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList() ?? new List<string>();
                }
                catch (Exception ex)
                {
                    failed++;
                    events.Add(new[] { "Ошибка", "Ошибка чтения ссылок", file, "", ex.Message });
                    continue;
                }

                foreach (var reference in references)
                {
                    if (reference.Contains("Local\\Temp", StringComparison.OrdinalIgnoreCase)) continue; // встроенные/виртуальные
                    var normalized = AGR_MigrationReportService.NormalizePath(reference);
                    if (!normalized.StartsWith(from, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!AGR_MigrationReportService.IsUnder(normalized, to))
                        {
                            otherRoots++;
                            events.Add(new[] { "Предупреждение", "Ссылка вне хранилища", file, reference, "" });
                        }
                        continue;
                    }

                    var target = to + normalized.Substring(from.Length);
                    if (!File.Exists(target))
                    {
                        missing++;
                        events.Add(new[] { "Ошибка", "Нет файла в новом корне", file, reference, target });
                        continue;
                    }

                    bool ok;
                    string error = "";
                    try { ok = _swApp.Sw.ReplaceReferencedDocument(file, reference, target); }
                    catch (Exception ex) { ok = false; error = ex.Message; }

                    if (ok) replaced++;
                    else
                    {
                        failed++;
                        events.Add(new[] { "Ошибка", "Замена ссылки не выполнена", file, reference, target + " " + error });
                    }
                }
            }

            AGR_MigrationReport.WriteCsv(Path.Combine(folder, "refs.csv"),
                new[] { "Важность", "Категория", "Файл", "Ссылка", "Подробности" },
                events.OrderBy(e => e[0] == "Ошибка" ? 0 : 1).ThenBy(e => e[1]));

            var sb = new StringBuilder();
            sb.AppendLine($"Смена корня хранилища, {DateTime.Now:dd.MM.yyyy HH:mm}");
            sb.AppendLine($"Старый корень: {from}");
            sb.AppendLine($"Новый корень:  {to}");
            if (stopped) sb.AppendLine("ПРЕРВАНО файлом STOP: повторный запуск продолжит (заменённые ссылки уже не найдутся).");
            sb.AppendLine($"Время: {timer.Elapsed:hh\\:mm\\:ss}");
            sb.AppendLine();
            sb.AppendLine($"Файлов просмотрено: {files} из {list.Count}");
            sb.AppendLine($"Ссылок заменено: {replaced}");
            sb.AppendLine($"Нет файла в новом корне: {missing}");
            sb.AppendLine($"Ошибок чтения и замены: {failed}");
            sb.AppendLine($"Ссылок вне хранилища (не меняются, были не разрешены миграцией): {otherRoots}");
            File.WriteAllText(Path.Combine(folder, "summary.txt"), sb.ToString(), new UTF8Encoding(true));

            _logger.LogInformation($"Смена корня: заменено {replaced}, нет файла {missing}, ошибок {failed}. {folder}");
            return new AGR_StorageRebaseRun(folder, files, replaced, failed, missing, otherRoots, stopped, timer.Elapsed);
        }

        /// <summary>Модели и чертежи в папках новой схемы: StorageRoot\&lt;guid&gt;\&lt;версия&gt;\файл.
        /// Старые папки хеша рядом не трогаются.</summary>
        private static IEnumerable<string> EnumerateStorageFiles(string root)
        {
            foreach (var componentDir in Directory.EnumerateDirectories(root))
            {
                if (!Guid.TryParse(Path.GetFileName(componentDir), out _)) continue;
                foreach (var file in Directory.EnumerateFiles(componentDir, "*.sld*", SearchOption.AllDirectories))
                {
                    if (Path.GetFileName(file).StartsWith("~$")) continue;
                    var ext = Path.GetExtension(file);
                    if (AGR_StorageMigrationService.SolidWorksExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase)))
                        yield return file;
                }
            }
        }
    }
}
