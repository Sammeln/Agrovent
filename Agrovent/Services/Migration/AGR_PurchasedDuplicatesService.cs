// File: Services/Migration/AGR_PurchasedDuplicatesService.cs
using System.Data.Common;
using System.IO;
using System.Text;
using Agrovent.DAL;
using AgroventInfrastructure.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agrovent.Services.Migration
{
    /// <summary>
    /// Слияние дублей покупных (шаг перед миграцией хранилища, временно).
    /// Группа = покупные компоненты с одинаковым ключом по последней версии: «артикул + HashSum», без артикула «имя + HashSum».
    /// Основной = наименьший Id. AssemblyStructures и ProjectComponents переводятся с версий дублей на последнюю версию основного,
    /// дубли удаляются. Соответствие «версия дубля → версия основного» и старые пути файлов дубля пишутся в MigrationLog
    /// (Status = Merged): по ним миграция перенаправит ссылки сборок на файл основного.
    /// </summary>
    public interface IAGR_PurchasedDuplicatesService
    {
        /// <summary>Поиск групп, БД не меняется. Отчёт пишется в папку результата.</summary>
        Task<AGR_DuplicatesResult> AnalyzeAsync();

        /// <summary>Слияние групп из AnalyzeAsync (кроме пропущенных) одной транзакцией. Ошибка = откат всего.</summary>
        Task<AGR_DuplicatesApplied> ApplyAsync(AGR_DuplicatesResult result);
    }

    public class AGR_DupVersion
    {
        public int VersionId { get; set; }
        public int ComponentId { get; set; }
        public string PartNumber { get; set; } = "";
        public int Version { get; set; }
        public string? Name { get; set; }
        public int? Article { get; set; }
        public AGR_ComponentType_e ComponentType { get; set; }
        public AGR_AvaType_e AvaType { get; set; }
        public string? ModelPath { get; set; }
        public string? DrawingPath { get; set; }
        public long? HashSum { get; set; }
        public int ParentRows { get; set; }
        public int ProjectRows { get; set; }
    }

    public class AGR_DupComponent
    {
        public int ComponentId { get; init; }
        public string PartNumber { get; init; } = "";
        public List<AGR_DupVersion> Versions { get; init; } = new();
        public AGR_DupVersion Latest => Versions.OrderByDescending(v => v.Version).First();
    }

    public class AGR_DuplicateGroup
    {
        public string Key { get; init; } = "";
        public AGR_DupComponent Main { get; init; } = null!;
        public List<AGR_DupComponent> Duplicates { get; init; } = new();
        /// <summary>Причина, по которой группа не сливается; null - сливается.</summary>
        public string? Blocked { get; set; }
        public List<string> Warnings { get; } = new();
    }

    public class AGR_DuplicatesResult
    {
        public string Folder { get; init; } = "";
        public List<AGR_DuplicateGroup> Groups { get; } = new();
        public int PurchasedComponents { get; set; }
        public int WithoutHash { get; set; }

        public IEnumerable<AGR_DuplicateGroup> ToMerge => Groups.Where(g => g.Blocked == null);
        public int ComponentsToDelete => ToMerge.Sum(g => g.Duplicates.Count);
        public int VersionsToDelete => ToMerge.Sum(g => g.Duplicates.Sum(d => d.Versions.Count));
    }

    public record AGR_DuplicatesApplied(int Groups, int Components, int Versions, int StructureRows, int ProjectRows,
        int StructureRowsMerged, int ProjectRowsMerged);

    public class AGR_PurchasedDuplicatesService : IAGR_PurchasedDuplicatesService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AGR_PurchasedDuplicatesService> _logger;

        public AGR_PurchasedDuplicatesService(IServiceScopeFactory scopeFactory, ILogger<AGR_PurchasedDuplicatesService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        #region Поиск групп

        public async Task<AGR_DuplicatesResult> AnalyzeAsync()
        {
            var folder = Path.Combine(AGR_MigrationReportService.ReportsRoot, "Duplicates_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(folder);
            var result = new AGR_DuplicatesResult { Folder = folder };

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();

            var versions = await db.ComponentVersions.AsNoTracking()
                .Select(v => new AGR_DupVersion
                {
                    VersionId = v.Id,
                    ComponentId = v.ComponentId,
                    PartNumber = v.Component.PartNumber,
                    Version = v.Version,
                    Name = v.Name,
                    Article = v.AvaArticleArticle,
                    ComponentType = v.ComponentType,
                    AvaType = v.AvaType,
                    ModelPath = v.Files.Where(f => f.FileType == AGR_FileType_e.StorageModel).Select(f => f.FilePath).FirstOrDefault(),
                    DrawingPath = v.Files.Where(f => f.FileType == AGR_FileType_e.StorageDrawing).Select(f => f.FilePath).FirstOrDefault(),
                })
                .ToListAsync();

            // HashSum в EF-модели больше нет (этап 6.1), колонка в БД осталась до конца миграции
            var hashes = await ReadHashesAsync(db);
            var parentRows = await db.AssemblyStructures.AsNoTracking()
                .GroupBy(s => s.ChildComponentVersionId).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
            var projectRows = await db.ProjectComponents.AsNoTracking()
                .GroupBy(p => p.ComponentVersionId).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
            var techProcesses = (await db.TechProcesses.AsNoTracking().Select(t => t.PartNumber).ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var v in versions)
            {
                v.HashSum = hashes.TryGetValue(v.VersionId, out var h) ? h : null;
                v.ParentRows = parentRows.TryGetValue(v.VersionId, out var p) ? p : 0;
                v.ProjectRows = projectRows.TryGetValue(v.VersionId, out var r) ? r : 0;
            }

            // Ключ по последней версии компонента; покупной = ComponentType или AvaType покупной (как в 6.5)
            var keyed = new List<(string Key, AGR_DupComponent Component)>();
            foreach (var g in versions.GroupBy(v => v.ComponentId))
            {
                var component = new AGR_DupComponent { ComponentId = g.Key, PartNumber = g.First().PartNumber, Versions = g.ToList() };
                var latest = component.Latest;
                if (latest.ComponentType != AGR_ComponentType_e.Purchased && latest.AvaType != AGR_AvaType_e.Purchased) continue;
                result.PurchasedComponents++;

                if (latest.HashSum == null) { result.WithoutHash++; continue; }
                var key = latest.Article.HasValue
                    ? $"артикул {latest.Article} + хеш {latest.HashSum}"
                    : $"имя «{NormalizeName(latest.Name)}» + хеш {latest.HashSum}";
                keyed.Add((key, component));
            }

            foreach (var g in keyed.GroupBy(k => k.Key).Where(g => g.Count() > 1).OrderBy(g => g.Key))
            {
                var ordered = g.Select(k => k.Component).OrderBy(c => c.ComponentId).ToList();
                var group = new AGR_DuplicateGroup { Key = g.Key, Main = ordered[0], Duplicates = ordered.Skip(1).ToList() };

                // Техпроцесс привязан к PartNumber дубля: удаление сломает его, решать вручную
                var withTech = group.Duplicates.Where(d => techProcesses.Contains(d.PartNumber)).Select(d => d.PartNumber).ToList();
                if (withTech.Count > 0) group.Blocked = "у дубля есть техпроцесс: " + string.Join(", ", withTech);

                if (string.IsNullOrWhiteSpace(group.Main.Latest.ModelPath))
                    group.Warnings.Add("у основного нет записи о файле модели");
                if (ordered.Select(c => NormalizeName(c.Latest.Name)).Distinct().Count() > 1)
                    group.Warnings.Add("имена различаются");
                if (group.Duplicates.Any(d => d.Versions.Count > 1))
                    group.Warnings.Add("у дубля несколько версий");

                result.Groups.Add(group);
            }

            WriteReport(result);
            _logger.LogInformation($"Дубли покупных: групп {result.Groups.Count}, к удалению компонентов {result.ComponentsToDelete}. {folder}");
            return result;
        }

        private static string NormalizeName(string? name) => (name ?? "").Trim().ToUpperInvariant();

        private static async Task<Dictionary<int, long?>> ReadHashesAsync(DataContext db)
        {
            var result = new Dictionary<int, long?>();
            var conn = db.Database.GetDbConnection();
            await db.Database.OpenConnectionAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT \"Id\", \"HashSum\" FROM public.\"ComponentVersions\"";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    result[reader.GetInt32(0)] = reader.IsDBNull(1) ? null : Convert.ToInt64(reader.GetValue(1));
            }
            finally
            {
                await db.Database.CloseConnectionAsync();
            }
            return result;
        }

        private static void WriteReport(AGR_DuplicatesResult result)
        {
            var rows = new List<string[]>();
            foreach (var g in result.Groups)
            {
                var status = g.Blocked != null ? "пропущена: " + g.Blocked : "слить";
                var warnings = string.Join("; ", g.Warnings);
                foreach (var (c, role) in g.Duplicates.Select(d => (d, "дубль")).Prepend((g.Main, "основной")))
                    foreach (var v in c.Versions.OrderBy(v => v.Version))
                        rows.Add(new[]
                        {
                            g.Key, status, role, c.ComponentId.ToString(), c.PartNumber, v.Version.ToString(), v.VersionId.ToString(),
                            v.Name ?? "", v.Article?.ToString() ?? "", v.HashSum?.ToString() ?? "",
                            v.ParentRows.ToString(), v.ProjectRows.ToString(),
                            role == "дубль" ? $"{g.Main.PartNumber} v{g.Main.Latest.Version} (VersionId {g.Main.Latest.VersionId})" : "",
                            v.ModelPath ?? "", warnings
                        });
            }
            AGR_MigrationReport.WriteCsv(Path.Combine(result.Folder, "duplicates.csv"),
                new[] { "Группа", "Статус", "Роль", "ComponentId", "Обозначение", "Версия", "VersionId", "Наименование", "Артикул", "HashSum",
                        "Строк в сборках", "Строк в проектах", "Переводится на", "Файл модели", "Замечания" },
                rows);

            var sb = new StringBuilder();
            sb.AppendLine($"Дубли покупных, {DateTime.Now:dd.MM.yyyy HH:mm}");
            sb.AppendLine("Ключ: последняя версия, «артикул + HashSum», без артикула «имя + HashSum». Основной = наименьший Id.");
            sb.AppendLine();
            sb.AppendLine($"Покупных компонентов: {result.PurchasedComponents}, без HashSum (не группируются): {result.WithoutHash}");
            sb.AppendLine($"Групп: {result.Groups.Count}, к слиянию: {result.ToMerge.Count()}, пропущено: {result.Groups.Count(g => g.Blocked != null)}");
            sb.AppendLine($"Будет удалено компонентов: {result.ComponentsToDelete}, версий: {result.VersionsToDelete}");
            sb.AppendLine($"Строк AssemblyStructures к переводу: {result.ToMerge.Sum(g => g.Duplicates.Sum(d => d.Versions.Sum(v => v.ParentRows)))}");
            sb.AppendLine($"Строк ProjectComponents к переводу: {result.ToMerge.Sum(g => g.Duplicates.Sum(d => d.Versions.Sum(v => v.ProjectRows)))}");
            foreach (var w in result.Groups.SelectMany(g => g.Warnings).GroupBy(w => w).OrderBy(w => w.Key))
                sb.AppendLine($"  Замечание «{w.Key}»: групп {w.Count()}");
            foreach (var g in result.Groups.Where(g => g.Blocked != null))
                sb.AppendLine($"  Пропущена {g.Key}: {g.Blocked}");
            File.WriteAllText(Path.Combine(result.Folder, "summary.txt"), sb.ToString(), new UTF8Encoding(true));
        }

        #endregion

        #region Слияние

        public async Task<AGR_DuplicatesApplied> ApplyAsync(AGR_DuplicatesResult result)
        {
            var groups = result.ToMerge.ToList();
            if (groups.Count == 0) return new AGR_DuplicatesApplied(0, 0, 0, 0, 0, 0, 0);

            var dupVersionIds = groups.SelectMany(g => g.Duplicates).SelectMany(d => d.Versions).Select(v => v.VersionId).ToArray();
            var dupComponentIds = groups.SelectMany(g => g.Duplicates).Select(d => d.ComponentId).ToArray();
            var mainVersionIds = groups.Select(g => g.Main.Latest.VersionId).Distinct().ToArray();

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var conn = db.Database.GetDbConnection();
            var tx = transaction.GetDbTransaction();

            try
            {
                // Затронутые сборки и проекты - до перевода ссылок: только в них потом склеиваются строки
                var parents = await QueryIntsAsync(conn, tx,
                    "SELECT DISTINCT \"ParentComponentVersionId\" FROM public.\"AssemblyStructures\" WHERE \"ChildComponentVersionId\" = ANY(@ids)",
                    ("ids", dupVersionIds));
                var projects = await QueryIntsAsync(conn, tx,
                    "SELECT DISTINCT \"ProjectId\" FROM public.\"ProjectComponents\" WHERE \"ComponentVersionId\" = ANY(@ids)",
                    ("ids", dupVersionIds));

                int structureRows = 0, projectRows = 0;
                var machine = System.Environment.MachineName;
                foreach (var g in groups)
                {
                    var main = g.Main.Latest;
                    foreach (var dup in g.Duplicates)
                    {
                        var ids = dup.Versions.Select(v => v.VersionId).ToArray();
                        structureRows += await ExecAsync(conn, tx,
                            "UPDATE public.\"AssemblyStructures\" SET \"ChildComponentVersionId\" = @main WHERE \"ChildComponentVersionId\" = ANY(@ids)",
                            ("main", main.VersionId), ("ids", ids));
                        projectRows += await ExecAsync(conn, tx,
                            "UPDATE public.\"ProjectComponents\" SET \"ComponentVersionId\" = @main WHERE \"ComponentVersionId\" = ANY(@ids)",
                            ("main", main.VersionId), ("ids", ids));

                        foreach (var v in dup.Versions)
                            await ExecAsync(conn, tx,
                                "INSERT INTO public.\"MigrationLog\" (\"ComponentVersionId\", \"Status\", \"OldModelPath\", \"OldDrawingPath\", " +
                                "\"MergedIntoVersionId\", \"Message\", \"MachineName\", \"StartedAt\", \"FinishedAt\") " +
                                "VALUES (@v, 'Merged', @model, @drawing, @main, @msg, @machine, now(), now()) " +
                                "ON CONFLICT (\"ComponentVersionId\") DO UPDATE SET \"Status\" = 'Merged', " +
                                "\"OldModelPath\" = EXCLUDED.\"OldModelPath\", \"OldDrawingPath\" = EXCLUDED.\"OldDrawingPath\", " +
                                "\"MergedIntoVersionId\" = EXCLUDED.\"MergedIntoVersionId\", \"Message\" = EXCLUDED.\"Message\", " +
                                "\"MachineName\" = EXCLUDED.\"MachineName\", \"FinishedAt\" = now()",
                                ("v", v.VersionId), ("model", v.ModelPath), ("drawing", v.DrawingPath), ("main", main.VersionId),
                                ("msg", $"дубль {dup.PartNumber} v{v.Version} -> {g.Main.PartNumber} v{main.Version} ({g.Key})"),
                                ("machine", machine));
                    }
                }

                // Сборка содержала и дубль, и основной: одна строка, количество суммируется
                var structureMerged = await ExecAsync(conn, tx,
                    "UPDATE public.\"AssemblyStructures\" a SET \"Quantity\" = s.q " +
                    "FROM (SELECT min(\"Id\") AS id, sum(\"Quantity\") AS q FROM public.\"AssemblyStructures\" " +
                    "      WHERE \"ParentComponentVersionId\" = ANY(@parents) AND \"ChildComponentVersionId\" = ANY(@mains) " +
                    "      GROUP BY \"ParentComponentVersionId\", \"ChildComponentVersionId\" HAVING count(*) > 1) s " +
                    "WHERE a.\"Id\" = s.id",
                    ("parents", parents), ("mains", mainVersionIds));
                if (structureMerged > 0)
                    await ExecAsync(conn, tx,
                        "DELETE FROM public.\"AssemblyStructures\" a " +
                        "WHERE a.\"ParentComponentVersionId\" = ANY(@parents) AND a.\"ChildComponentVersionId\" = ANY(@mains) " +
                        "AND EXISTS (SELECT 1 FROM public.\"AssemblyStructures\" b WHERE b.\"ParentComponentVersionId\" = a.\"ParentComponentVersionId\" " +
                        "            AND b.\"ChildComponentVersionId\" = a.\"ChildComponentVersionId\" AND b.\"Id\" < a.\"Id\")",
                        ("parents", parents), ("mains", mainVersionIds));

                var projectMerged = await ExecAsync(conn, tx,
                    "DELETE FROM public.\"ProjectComponents\" a " +
                    "WHERE a.\"ProjectId\" = ANY(@projects) AND a.\"ComponentVersionId\" = ANY(@mains) " +
                    "AND EXISTS (SELECT 1 FROM public.\"ProjectComponents\" b WHERE b.\"ProjectId\" = a.\"ProjectId\" " +
                    "            AND b.\"ComponentVersionId\" = a.\"ComponentVersionId\" AND b.\"Id\" < a.\"Id\")",
                    ("projects", projects), ("mains", mainVersionIds));

                // Удаление дублей явно, не полагаясь на каскады в БД
                await ExecAsync(conn, tx, "DELETE FROM public.\"ComponentProperties\" WHERE \"ComponentVersionId\" = ANY(@ids)", ("ids", dupVersionIds));
                await ExecAsync(conn, tx, "DELETE FROM public.\"ComponentMaterials\" WHERE \"ComponentVersionId\" = ANY(@ids)", ("ids", dupVersionIds));
                await ExecAsync(conn, tx, "DELETE FROM public.\"ComponentFiles\" WHERE \"ComponentVersionId\" = ANY(@ids)", ("ids", dupVersionIds));
                // Структура покупной сборки-дубля (она сама родитель)
                await ExecAsync(conn, tx, "DELETE FROM public.\"AssemblyStructures\" WHERE \"ParentComponentVersionId\" = ANY(@ids)", ("ids", dupVersionIds));
                await ExecAsync(conn, tx, "DELETE FROM public.\"ComponentEditLocks\" WHERE \"ComponentId\" = ANY(@ids)", ("ids", dupComponentIds));
                var versionsDeleted = await ExecAsync(conn, tx, "DELETE FROM public.\"ComponentVersions\" WHERE \"Id\" = ANY(@ids)", ("ids", dupVersionIds));
                var componentsDeleted = await ExecAsync(conn, tx, "DELETE FROM public.\"Components\" WHERE \"Id\" = ANY(@ids)", ("ids", dupComponentIds));

                await transaction.CommitAsync();

                var applied = new AGR_DuplicatesApplied(groups.Count, componentsDeleted, versionsDeleted, structureRows, projectRows,
                    structureMerged, projectMerged);
                File.WriteAllText(Path.Combine(result.Folder, "applied.txt"),
                    $"Слияние выполнено {DateTime.Now:dd.MM.yyyy HH:mm}\r\n" +
                    $"Групп: {applied.Groups}\r\nУдалено компонентов: {applied.Components}, версий: {applied.Versions}\r\n" +
                    $"Переведено строк AssemblyStructures: {applied.StructureRows}, склеено: {applied.StructureRowsMerged}\r\n" +
                    $"Переведено строк ProjectComponents: {applied.ProjectRows}, удалено повторов: {applied.ProjectRowsMerged}\r\n" +
                    "Соответствие версий и старые пути файлов дублей: MigrationLog, Status = 'Merged'.\r\n",
                    new UTF8Encoding(true));
                _logger.LogInformation($"Дубли покупных слиты: {applied}");
                return applied;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Дубли покупных: ошибка слияния, транзакция откатана.");
                throw;
            }
        }

        private static async Task<int> ExecAsync(DbConnection conn, DbTransaction tx, string sql, params (string Name, object? Value)[] args)
        {
            using var cmd = CreateCommand(conn, tx, sql, args);
            return await cmd.ExecuteNonQueryAsync();
        }

        private static async Task<int[]> QueryIntsAsync(DbConnection conn, DbTransaction tx, string sql, params (string Name, object? Value)[] args)
        {
            using var cmd = CreateCommand(conn, tx, sql, args);
            var result = new List<int>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) result.Add(reader.GetInt32(0));
            return result.ToArray();
        }

        private static DbCommand CreateCommand(DbConnection conn, DbTransaction tx, string sql, (string Name, object? Value)[] args)
        {
            var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            foreach (var (name, value) in args)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = name;
                p.Value = value ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }
            return cmd;
        }

        #endregion
    }
}
