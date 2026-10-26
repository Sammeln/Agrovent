// File: Services/Migration/AGR_MissingReferenceResolver.cs
using System.IO;
using AgroventInfrastructure;
using Microsoft.Extensions.Logging;
using Xarial.XCad.SolidWorks;

namespace Agrovent.Services.Migration
{
    /// <summary>Одно событие ReferenceNotFound: что искали, что подставили, как нашли.</summary>
    public record AGR_MissingReference(string Missing, string? Resolved, AGR_MissingReferenceHow_e How, int Candidates);

    public enum AGR_MissingReferenceHow_e
    {
        /// <summary>Среди файлов дочерних версий открываемой сборки (AssemblyStructure).</summary>
        Preferred,
        /// <summary>По имени файла в хранилище, единственный.</summary>
        ByNameUnique,
        /// <summary>По имени файла в хранилище, несколько - взят первый.</summary>
        ByNameAmbiguous,
        NotFound
    }

    /// <summary>
    /// Временный обработчик ReferenceNotFound (ветка тестов и миграции; в этапе 7 его заменит поиск по структуре версии из БД).
    /// Единственная подписка на событие (раньше их было две: пустая в AgroventAddin и заглушка в реестре).
    /// Порядок поиска: PreferredPaths (дети открываемой версии, задаёт вызывающий) → точное имя файла в хранилище.
    /// Не найдено - SolidWorks получает пустой путь, компонент остаётся потерянным, окно поиска не показывается (CHECK).
    /// </summary>
    public class AGR_MissingReferenceResolver
    {
        private readonly ISwApplication _app;
        private readonly ILogger<AGR_MissingReferenceResolver> _logger;
        private readonly List<AGR_MissingReference> _events = new();
        private Dictionary<string, List<string>>? _index;
        private string? _indexRoot;

        public AGR_MissingReferenceResolver(ISwApplication app, ILogger<AGR_MissingReferenceResolver> logger)
        {
            _app = app;
            _logger = logger;
        }

        /// <summary>Файлы, среди которых искать в первую очередь (полные пути). null - только по хранилищу.</summary>
        public IReadOnlyCollection<string>? PreferredPaths { get; set; }

        /// <summary>События с прошлого вызова; список очищается.</summary>
        public List<AGR_MissingReference> TakeEvents()
        {
            var result = new List<AGR_MissingReference>(_events);
            _events.Clear();
            return result;
        }

        /// <summary>Обработчик ISldWorks.ReferenceNotFoundNotify.</summary>
        public int OnReferenceNotFound(string fileName)
        {
            AGR_MissingReference result;
            try
            {
                result = Resolve(fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ReferenceNotFound: ошибка поиска {fileName}");
                result = new AGR_MissingReference(fileName, null, AGR_MissingReferenceHow_e.NotFound, 0);
            }

            _events.Add(result);
            _logger.LogInformation($"ReferenceNotFound: {fileName} -> {result.Resolved ?? "(не найден)"} [{result.How}]");

            // CHECK: пустая строка = «оставить потерянным» без окна поиска; проверить на одной сломанной сборке до ночного прогона
            _app.Sw.SetMissingReferencePathName(result.Resolved ?? "");
            return 1;
        }

        private AGR_MissingReference Resolve(string fileName)
        {
            var name = Path.GetFileName(fileName);

            var preferred = PreferredPaths?
                .Where(p => string.Equals(Path.GetFileName(p), name, StringComparison.OrdinalIgnoreCase) && File.Exists(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (preferred is { Count: > 0 })
                return new AGR_MissingReference(fileName, preferred[0], AGR_MissingReferenceHow_e.Preferred, preferred.Count);

            if (GetIndex().TryGetValue(name, out var found) && found.Count > 0)
                return new AGR_MissingReference(fileName, found[0],
                    found.Count == 1 ? AGR_MissingReferenceHow_e.ByNameUnique : AGR_MissingReferenceHow_e.ByNameAmbiguous, found.Count);

            return new AGR_MissingReference(fileName, null, AGR_MissingReferenceHow_e.NotFound, 0);
        }

        /// <summary>Индекс «имя файла → пути» по хранилищу, строится при первом событии (и заново при смене корня).</summary>
        private Dictionary<string, List<string>> GetIndex()
        {
            var root = AGR_Options.StorageRootFolderPath;
            if (_index != null && string.Equals(_indexRoot, root, StringComparison.OrdinalIgnoreCase)) return _index;

            var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
            {
                foreach (var file in Directory.EnumerateFiles(root, "*.sld*", SearchOption.AllDirectories))
                {
                    var ext = Path.GetExtension(file);
                    if (!ext.Equals(".sldprt", StringComparison.OrdinalIgnoreCase)
                        && !ext.Equals(".sldasm", StringComparison.OrdinalIgnoreCase)) continue;
                    var name = Path.GetFileName(file);
                    if (!index.TryGetValue(name, out var list)) index[name] = list = new List<string>();
                    list.Add(file);
                }
            }
            _logger.LogInformation($"ReferenceNotFound: индекс хранилища {root}: {index.Count} имён");
            _index = index;
            _indexRoot = root;
            return index;
        }
    }
}
