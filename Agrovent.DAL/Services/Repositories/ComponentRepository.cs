using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AgroventInfrastructure.Enums;
using AgroventInfrastructure.Interfaces.Components;
using AgroventInfrastructure.Interfaces.Specification;
using AgroventInfrastructure.Interfaces.Components.Base;
using AgroventInfrastructure.Interfaces.Properties;
using Agrovent.Infrastructure.Interfaces;
using Microsoft.VisualBasic.FileIO;
using Xarial.XCad.Documents;
using Xarial.XCad.SolidWorks.Documents;
using System.Windows.Forms;
using AgroventInfrastructure.Interfaces.Entities.Components;
using AgroventInfrastructure.Interfaces.Entities;
using AgroventInfrastructure.Interfaces.Entities.TechProcess;
using AgroventInfrastructure.Entities.Base;
using AgroventInfrastructure.Entities.Components;
using AgroventInfrastructure.Entities.TechProcess;
using Agrovent.Infrastructure;
using System.Data;
using AgroventInfrastructure;
using AgroventInfrastructure.Interfaces;

namespace Agrovent.DAL.Services.Repositories
{
    public interface IAGR_ComponentRepository
    {
        // Основные операции с компонентами
        Task<Component?> GetComponentByPartNumber(string partNumber);
        Task<ComponentVersion?> GetComponentVersion(string partNumber, int version);
        Task<ComponentVersion?> GetLatestComponentVersion(string partNumber);
        Task<string?> GetLatestComponentVersionByName(string name);


        // Создание/обновление компонента (без транзакций - Unit of Work управляет транзакциями)
        Task<Component> CreateNewComponent(IAGR_BaseComponent component);
        Task<bool> CreateNewComponents(List<IAGR_BaseComponent> components);

        // Сохранение (этап 4): решение «новая / существующая версия» принимает AGR_SavePlan, не хеш
        Task<ComponentVersion> ResolvePlannedVersion(IAGR_BaseComponent component, AGR_SavePlan plan);
        Task<ComponentVersion> CreateNewVersion(IAGR_BaseComponent component, int? basedOnVersion);
        Task<ComponentVersion> GetVersionForReference(IAGR_BaseComponent component, int componentId, int version);
        Task RemoveEditLocksAsync(IEnumerable<int> componentIds, int userId);
        Task<int> UpdateReferencedVersionAsync(ComponentVersion componentVersion, IAGR_BaseComponent component, IReadOnlyDictionary<string, string> fileValues);

        // 6.5: поиск существующего покупного перед выдачей нового номера
        Task<List<AGR_PurchasedCandidate>> FindPurchasedCandidatesAsync(int? article, string name, string? geometryFingerprint);

        // Получение структуры сборки
        Task<List<AssemblyStructure>> GetAssemblyStructure(string assemblyPartNumber, int version);
        Task<ComponentVersion> SaveAssemblyStructure(IAGR_BaseComponent assembly, IEnumerable<IAGR_SpecificationItem> components, AGR_SavePlan plan);
        Task<List<AssemblyStructure>> GetAssemblyStructureRecursive(string assemblyPartNumber, int version);
        Task<AssemblyStructure?> GetExistingAssemblyStructure(ComponentVersion parentComponentVersion, ComponentVersion childComponentVersion, int quantity);
        Task<List<ComponentVersion>> GetRootAssembliesForChildAsync(ComponentVersion childCV);

        // Статистика
        Task<int> GetComponentCount();
        Task<int> GetComponentVersionCount();
        Task<List<ComponentVersion>> GetAssembliesUsedComponent(ComponentVersion childComponent);


        // Управление версиями
        Task<IEnumerable<ComponentVersion>> GetAllLatestComponentVersionsAsync();

        //Проекты
        Task<IEnumerable<ComponentVersion>> GetTopLevelAssembliesNotInProjectsAsync();

        //Техпроцессы
        Task<TechnologicalProcess?> GetComponentTechnologyProcessByPartNumberAsync(string partNumber);
        Task<List<Operation>> GetComponentOperationsByPartNumberAsync(string partNumber);
        Task<List<TemplateOperation>> GetAllTemplateOperationsAsync();
        Task<Dictionary<string, AvaArticleModel>> GetAvaArticlesByNameAsync(List<string> names);
        Task<AvaArticleModel?> GetAvaArticleByArticleNumberAsync(int articleNumber);

        Task UpdateComponentVersionEditAsync(AGR_ComponentEditData data);
        Task UpdateComponentVersionEditsAsync(IEnumerable<AGR_ComponentEditData> data);

        Task<ComponentVersion?> GetComponentVersionForEdit(string partNumber, int version);
        Task AddComponentFileAsync(ComponentFile file);
        Task RemoveComponentFileAsync(int componentFileId);

        // Плоский состав сборки: один компонент — одна строка, количество
        // пересчитано с учётом всех путей и множителей родительских сборок.
        Task<List<AGR_FlatAssemblyComponent>> GetFlatAssemblyComponents(string assemblyPartNumber, int version);
    }
    public class ComponentRepository : IAGR_ComponentRepository
    {
        private readonly DataContext _context;
        private readonly ILogger<ComponentRepository> _logger;
        private readonly IAGR_SaveProgressVM _saveProgress;
        private readonly IAGR_User _currentUser;

        public ComponentRepository(DataContext context, ILogger<ComponentRepository> logger, IAGR_SaveProgressVM saveProgress)
        {
            _context = context;
            _logger = logger;
            _saveProgress = saveProgress;
        }
        public ComponentRepository(DataContext context, ILogger<ComponentRepository> logger)
        {
            _context = context;
            _logger = logger;
        }
        public ComponentRepository(DataContext context, ILogger<ComponentRepository> logger, IAGR_SaveProgressVM saveProgress, IAGR_User currentUser)
        {
            _context = context;
            _logger = logger;
            _saveProgress = saveProgress;
            _currentUser = currentUser;
        }

        #region Основные операции с компонентами

        public async Task<Component?> GetComponentByPartNumber(string partNumber)
        {
            try
            {
                _logger.LogDebug($"Запрос компонента по PartNumber: {partNumber}");

                var comp = await _context.Components
                    .Include(c => c.Versions)
                        .ThenInclude(v => v.Properties)
                    .Include(c => c.Versions)
                        .ThenInclude(p => p.AvaArticle)
                    .Include(c => c.Versions)
                        .ThenInclude(v => v.Material)
                        .ThenInclude(m => m.MaterialAvaArticle)
                    .Include(c => c.Versions)
                        .ThenInclude(v => v.Material)
                        .ThenInclude(m => m.PaintAvaArticle)
                    .Include(c => c.Versions)
                        .ThenInclude(v => v.Files)
                    .Include(c => c.Versions)
                    .Include(c => c.TechnologicalProcess)
                        .ThenInclude(tp => tp.Operations)
                    .FirstOrDefaultAsync(c => c.PartNumber == partNumber);

                return comp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении компонента по PartNumber: {partNumber}");
                throw;
            }
        }

        public async Task<ComponentVersion?> GetComponentVersion(string partNumber, int version)
        {
            try
            {
                _logger.LogDebug($"Запрос версии компонента: {partNumber} v{version}");

                return await _context.ComponentVersions
                    .Include(v => v.Component)
                    .Include(v => v.Properties)
                    .Include(v => v.Material)
                    .Include(v => v.Files)
                    .FirstOrDefaultAsync(v => v.Component.PartNumber == partNumber && v.Version == version);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении версии компонента: {partNumber} v{version}");
                throw;
            }
        }

        public async Task<ComponentVersion?> GetLatestComponentVersion(string partNumber)
        {
            try
            {
                _logger.LogDebug($"Запрос последней версии компонента: {partNumber}");

                var component = await GetComponentByPartNumber(partNumber);
                if (component == null)
                {
                    _logger.LogDebug($"Компонент не найден: {partNumber}");
                    return null;
                }

                var cv = component.Versions
                    .OrderByDescending(v => v.Version)
                    .FirstOrDefault();

                if (cv != null)
                {
                    cv.ParentAssemblies = await GetAssembliesUsedComponent(cv);
                }

                return cv;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении последней версии компонента: {partNumber}");
                throw;
            }
        }
        public async Task<string?> GetLatestComponentVersionByName(string name)
        {
            try
            {
                _logger.LogDebug($"Запрос последней версии компонента: {name}");
                string fileModel = string.Empty;

                var cv = _context.ComponentVersions
                    .Include(cv => cv.Files)
                    .Where(v => v.Name == name)
                    .OrderByDescending(v => v.Version)
                    .FirstOrDefault();

                if (cv != null)
                {
                    fileModel = cv.Files.FirstOrDefault(x => x.FileType == AGR_FileType_e.StorageModel).FilePath;
                }

                return fileModel ?? null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении последней версии компонента: {name}");
                throw;
            }
        }

        #endregion

        #region Создание/обновление компонента
        private async Task<string> GenerateNewPartNumberAsync_Seq()
        {
            const int maxNumber = 9999999;

            // Получаем физическое соединение с БД из EF Core контекста
            var connection = _context.Database.GetDbConnection();

            // Открываем соединение, если оно закрыто (EF Core обычно сам управляет этим, 
            // но для явных ADO.NET запросов лучше подстраховаться)
            bool needClose = _context.Database.GetDbConnection().State != ConnectionState.Open ? true : false;
            if (needClose)
            {
                await connection.OpenAsync();
            }

            try
            {
                using var command = connection.CreateCommand();
                // Запрашиваем следующее значение из нашей последовательности
                command.CommandText = "SELECT nextval('part_number_seq')";

                var result = await command.ExecuteScalarAsync();

                if (result == null || result == DBNull.Value)
                {
                    throw new InvalidOperationException("БД вернула пустое значение для Sequence.");
                }

                int nextNumber = Convert.ToInt32(result);

                if (nextNumber > maxNumber)
                {
                    throw new InvalidOperationException("Достигнут максимальный лимит 9999999.");
                }

                // Форматируем в 7-значную строку с нулями
                return nextNumber.ToString("D7");
            }
            finally
            {
                // Закрываем соединение, если мы открывали его вручную
                if (needClose)
                {
                    await connection.CloseAsync();
                }
            }
        }
        private static readonly SemaphoreSlim _dbLock = new SemaphoreSlim(1, 1);

        public async Task<Component> CreateNewComponent(IAGR_BaseComponent component)
        {
            var _component = new Component
            {
                CreatedAt = DateTime.UtcNow,
            };
            _context.Components.Add(_component);
            await _context.SaveChangesAsync();

            return _component;
        }
        public async Task<bool> CreateNewComponents(List<IAGR_BaseComponent> components)
        {
            foreach (var item in components)
            {
                var _component = new Component
                {
                    CreatedAt = DateTime.UtcNow,
                };
                _context.Components.Add(_component);
            }
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>Версия компонента по плану: новая (max+1) или существующая.</summary>
        public async Task<ComponentVersion> ResolvePlannedVersion(IAGR_BaseComponent component, AGR_SavePlan plan)
        {
            var item = plan.Get(component);
            item.Saved = item.Action == AGR_VersionAction_e.NewVersion
                ? await CreateNewVersion(component, item.BasedOnVersion)
                : await GetVersionForReference(component, item.ComponentId, item.Version);
            return item.Saved;
        }

        /// <summary>
        /// Новая версия компонента: всегда max+1, даже если хеш не изменился.
        /// Компонент (по PartNumber) создаётся, если его ещё нет. Повторный вызов в том же контексте
        /// для того же компонента возвращает уже созданную версию (компонент встречается в нескольких подсборках).
        /// </summary>
        public async Task<ComponentVersion> CreateNewVersion(IAGR_BaseComponent component, int? basedOnVersion)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(component.PartNumber))
                    throw new InvalidOperationException($"У компонента {component.Name} нет PartNumber: его нужно сгенерировать до сохранения.");

                var pn = component.PartNumber;

                var created = _context.ComponentVersions.Local
                    .FirstOrDefault(v => v.Id == 0 && v.Component?.PartNumber == pn && v.ConfigName == component.ConfigName);
                if (created != null)
                {
                    component.Version = created.Version;
                    component.StorageGuid = created.Component.StorageGuid;
                    return created;
                }

                var existingComponent = _context.Components.Local.FirstOrDefault(c => c.PartNumber == pn)
                    ?? await _context.Components.FirstOrDefaultAsync(c => c.PartNumber == pn);

                if (existingComponent == null)
                {
                    _logger.LogInformation($"Создание нового компонента: {pn}");
                    _saveProgress.AddLogMessage($"Создание нового компонента: {pn}");
                    existingComponent = new Component
                    {
                        PartNumber = pn,
                        StorageGuid = Guid.NewGuid(),
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Components.Add(existingComponent);
                }
                else if (existingComponent.StorageGuid == Guid.Empty)
                {
                    // Защита: колонку заполняет SQL-скрипт этапа 6, но пустой GUID дал бы общую папку
                    existingComponent.StorageGuid = Guid.NewGuid();
                }

                var maxInDb = existingComponent.Id == 0
                    ? null
                    : await _context.ComponentVersions
                        .Where(v => v.ComponentId == existingComponent.Id)
                        .MaxAsync(v => (int?)v.Version);
                var maxLocal = _context.ComponentVersions.Local
                    .Where(v => v.Component == existingComponent)
                    .Max(v => (int?)v.Version);
                var nextVersion = Math.Max(maxInDb ?? 0, maxLocal ?? 0) + 1;

                _logger.LogInformation($"Создание новой версии: {component.Name}_{pn} v{nextVersion} (основа v{basedOnVersion?.ToString() ?? "-"})");
                _saveProgress.AddLogMessage($"Создание новой версии: {component.Name}_{pn} v{nextVersion}");

                var mAvaArticle = component.AvaArticle as AvaArticleModel;

                var componentVersion = new ComponentVersion
                {
                    Component = existingComponent,
                    Version = nextVersion,
                    BasedOnVersion = basedOnVersion,
                    PreviewImage = component.Preview,
                    Name = component.Name,
                    ConfigName = component.ConfigName,
                    AvaArticleArticle = mAvaArticle?.Article,
                    ComponentType = component.ComponentType,
                    AvaType = component.AvaType,
                    CreatedAt = DateTime.UtcNow,
                    SavedByUser = _currentUser != null
                        ? await GetOrCreateUserAsync(_currentUser)
                        : null
                };

                _context.ComponentVersions.Add(componentVersion);

                await SaveComponentProperties(componentVersion, component);
                await SaveMaterialData(componentVersion, component);
                await SaveFileData(componentVersion, component); // путь hash\версия: версия уже известна

                component.StorageGuid = existingComponent.StorageGuid;
                component.Version = nextVersion;

                _saveProgress.AddLogMessage("============================================================");
                return componentVersion;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при создании версии компонента: {component.PartNumber}");
                _saveProgress.AddLogMessage($"Ошибка при создании версии компонента: {component.PartNumber}.\n{ex.InnerException}");
                throw;
            }
        }

        /// <summary>Существующая версия для ссылки из структуры (неотмеченный компонент). Ничего не меняет.</summary>
        public async Task<ComponentVersion> GetVersionForReference(IAGR_BaseComponent component, int componentId, int version)
        {
            var cv = _context.ComponentVersions.Local.FirstOrDefault(v => v.ComponentId == componentId && v.Version == version)
                ?? await _context.ComponentVersions
                    .Include(v => v.Component)
                    .FirstOrDefaultAsync(v => v.ComponentId == componentId && v.Version == version);

            if (cv == null)
                throw new InvalidOperationException(
                    $"Версия v{version} компонента {component.Name} (Id {componentId}) не найдена в БД.");

            // StorageGuid нужен для пути файла версии (AGR_StorageWriter)
            if (cv.Component == null)
                await _context.Entry(cv).Reference(v => v.Component).LoadAsync();

            component.Version = cv.Version;
            component.StorageGuid = cv.Component.StorageGuid;
            _saveProgress.AddLogMessage($"Без изменений, ссылка на версию: {component.Name} {component.PartNumber} v{cv.Version}");
            return cv;
        }

        #endregion

        #region Генерация нового Partnumber

        private async Task<string> GenerateNewPartNumberAsync()
        {
            const int maxNumber = 9999999;
            const int minNumber = 1;

            // Загружаем все занятые PartNumbers из таблицы Components и Article из AvaArticles
            // Преобразуем их в числа
            var usedComponentsQuery = _context.Components
                .Select(c => c.PartNumber)
                .AsQueryable();

            var usedArticlesQuery = _context.AvaArticles
                //.Where(a => a.Type == "Комплектующие" || a.Type == "Продукция")
                //.Where(a => a.PartNumber.Count() == 7)
                .Select(a => a.PartNumber)
                .AsQueryable();

            // Объединяем обе коллекии строк PartNumber/Article
            var allUsedStringsQuery = usedComponentsQuery;//.Union(usedArticlesQuery);

            // --- КРИТИЧЕСКОЕ ИЗМЕНЕНИЕ: Выполняем ToListAsync() ПОСЛЕ Union ---
            // Это загружает все строки в память
            var allUsedStringList = await allUsedStringsQuery.ToListAsync();

            // Теперь фильтруем и преобразуем в int на стороне .NET
            var usedNumbers = new HashSet<int>(
                allUsedStringList
                    .Where(s => !string.IsNullOrEmpty(s) && s.Length == 7 && s.All(char.IsDigit)) // Фильтрация в .NET
                    .Select(s => int.Parse(s)) // Преобразование в .NET
            );

            //// Преобразуем строки в числа и собираем в HashSet для быстрого поиска
            //var usedNumbers = new HashSet<int>(
            //    await allUsedStringsQuery
            //        .Where(s => !string.IsNullOrEmpty(s) && s.All(char.IsDigit) && s.Length == 7) // Убедимся, что строка состоит из 7 цифр
            //        .Select(s => int.Parse(s)) // Преобразуем в int
            //        .ToListAsync()
            //);

            // Ищем первое неиспользованное число
            for (int i = minNumber; i <= maxNumber; i++)
            {
                if (!usedNumbers.Contains(i))
                {
                    // Найдено! Форматируем как 7-значную строку
                    return i.ToString("D7");
                }
            }

            // Если все числа заняты (в теории невозможно при maxNumber = 9999999)
            throw new InvalidOperationException("Не удалось сгенерировать уникальный PartNumber: достигнут максимальный лимит.");
        }
        #endregion

        #region  ВСПОМОГАТЕЛЬНЫЙ МЕТОД: Проверка формата PartNumber 
        private static bool IsValidPartNumberFormat(string partNumber)
        {
            // Проверяем, что строка не пустая, состоит из 7 цифр и не содержит точек
            return !string.IsNullOrEmpty(partNumber) &&
                   partNumber.Length == 7 &&
                   partNumber.All(char.IsDigit); // Все символы - цифры, точки нет
        }
        #endregion

        #region ВСПОМОГАТЕЛЬНЫЙ МЕТОД: Попытка очистки PartNumber (опционально)
        private static string SanitizePartNumber(string partNumber)
        {
            // Убираем точки и пробелы, оставляем только цифры
            var cleaned = new string(partNumber.Where(char.IsDigit).ToArray());
            // Обрезаем до 7 символов, если больше
            if (cleaned.Length > 7)
            {
                cleaned = cleaned.Substring(0, 7);
            }
            // Добавляем ведущие нули, если меньше 7
            return cleaned.PadLeft(7, '0');
        }
        #endregion

        #region Поиск покупных (6.5)

        /// <summary>
        /// Покупные компоненты (ComponentType или AvaType = покупной), подходящие под файл без маркера и без «Обозначения»:
        /// по артикулу ERP (он уникален для покупного), а если артикула нет - по имени файла и отпечатку геометрии.
        /// Один компонент - одна строка с последней из подходящих версий. Только чтение.
        /// </summary>
        public async Task<List<AGR_PurchasedCandidate>> FindPurchasedCandidatesAsync(int? article, string name, string? geometryFingerprint)
        {
            var query = _context.ComponentVersions.AsNoTracking()
                .Where(v => v.ComponentType == AGR_ComponentType_e.Purchased || v.AvaType == AGR_AvaType_e.Purchased);

            if (article.HasValue)
                query = query.Where(v => v.AvaArticleArticle == article.Value);
            else if (!string.IsNullOrEmpty(geometryFingerprint))
                query = query.Where(v => v.Name == name && v.GeometryFingerprint == geometryFingerprint);
            else
                return new List<AGR_PurchasedCandidate>();

            var rows = await query
                .Select(v => new AGR_PurchasedCandidate
                {
                    ComponentId = v.ComponentId,
                    PartNumber = v.Component.PartNumber,
                    Version = v.Version,
                    Name = v.Name,
                    Article = v.AvaArticleArticle,
                    ArticleName = v.AvaArticle != null ? v.AvaArticle.Name : null
                })
                .ToListAsync();

            return rows
                .GroupBy(r => r.ComponentId)
                .Select(g => g.OrderByDescending(r => r.Version).First())
                .OrderBy(r => r.ComponentId)
                .ToList();
        }

        #endregion

        #region Структура сборки

        public async Task<List<AssemblyStructure>> GetAssemblyStructure(string assemblyPartNumber, int version)
        {
            try
            {
                _logger.LogDebug($"Запрос структуры сборки: {assemblyPartNumber} v{version}");

                var assemblyVersion = await GetComponentVersion(assemblyPartNumber, version);
                if (assemblyVersion == null)
                {
                    _logger.LogWarning($"Версия сборки не найдена: {assemblyPartNumber} v{version}");
                    return new List<AssemblyStructure>();
                }



                var list = await _context.AssemblyStructures
                    .Include(s => s.ParentComponentVersion)
                        .ThenInclude(pv => pv.Component)
                    .Include(s => s.ChildComponentVersion)
                        .ThenInclude(cv => cv.Component)
                    .Include(s => s.ChildComponentVersion)
                        .ThenInclude(cv => cv.Properties)
                    .Include(s => s.ParentComponentVersion)
                        .ThenInclude(cv => cv.Material)
                    .Where(s => s.ParentComponentVersionId == assemblyVersion.Id)
                    .OrderBy(s => s.Order)
                    .ToListAsync();

                foreach (AssemblyStructure structure in list.Where(s => s.ChildComponentVersion.ComponentType == 0))
                {
                    list.AddRange(await GetAssemblyStructure(structure.ChildComponentVersion.Component.PartNumber, structure.ChildComponentVersion.Version));
                }

                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении структуры сборки: {assemblyPartNumber}");
                throw;
            }
        }

        public async Task<AssemblyStructure?> GetExistingAssemblyStructure(ComponentVersion parentComponentVersion, ComponentVersion childComponentVersion, int quantity)
        {
            var structure = await _context.AssemblyStructures.FirstOrDefaultAsync(
                c => c.ParentComponentVersionId == parentComponentVersion.Id
                    && c.ChildComponentVersionId == childComponentVersion.Id
                    && c.Quantity == quantity);

            return structure;

        }
        public async Task<List<AssemblyStructure>> GetAssemblyStructureRecursive(string assemblyPartNumber, int version)
        {


            var directChildren = await _context.AssemblyStructures
                .Include(s => s.ParentComponentVersion)
                    .ThenInclude(cv => cv.Component)
                .Include(s => s.ParentComponentVersion)
                    .ThenInclude(cv => cv.Material)
                    .ThenInclude(m => m.PaintAvaArticle)
                .Include(s => s.ChildComponentVersion)
                    .ThenInclude(cv => cv.Component)
                .Include(s => s.ChildComponentVersion)
                    .ThenInclude(cv => cv.Properties)
                .Include(s => s.ChildComponentVersion)
                    .ThenInclude(cv => cv.Files)
                .Include(s => s.ChildComponentVersion)
                    .ThenInclude(cv => cv.Material)
                    .ThenInclude(m => m.MaterialAvaArticle)
                .Include(s => s.ChildComponentVersion)
                    .ThenInclude(cv => cv.Material)
                    .ThenInclude(m => m.PaintAvaArticle)
                .Include(s => s.ChildComponentVersion)
                    .ThenInclude(cv => cv.AvaArticle)
                .Where(s => s.ParentComponentVersion.Component.PartNumber == assemblyPartNumber
                        && s.ParentComponentVersion.Version == version)
                .OrderBy(s => s.Order)
                .ToListAsync();

            var allEntries = new List<AssemblyStructure>(directChildren);

            foreach (var child in directChildren)
            {
                if (child.ChildComponentVersion.ComponentType == 0)
                {
                    var subTreeEntries = await GetAssemblyStructureRecursive(child.ChildComponentVersion.Component.PartNumber, child.ChildComponentVersion.Version);
                    allEntries.AddRange(subTreeEntries);
                }
            }

            return allEntries;
        }

        public async Task<ComponentVersion> SaveAssemblyStructure(
            IAGR_BaseComponent assembly, IEnumerable<IAGR_SpecificationItem> components, AGR_SavePlan plan)
        {
            try
            {
                _logger.LogInformation($"Подготовка к сохранению структуры сборки: {assembly.PartNumber}");

                var assemblyVersion = await ResolvePlannedVersion(assembly, plan);
                if (plan.Get(assembly).Action != AGR_VersionAction_e.NewVersion)
                {
                    // Структура существующей версии уже в БД и меняться не должна
                    _logger.LogInformation($"Сборка {assembly.PartNumber} не на редактировании: структура не сохраняется.");
                    return assemblyVersion;
                }

                var expanded = new HashSet<ComponentVersion> { assemblyVersion };
                await SaveAssemblyStructureRecursive(assemblyVersion, components.ToList(), plan, expanded);

                _logger.LogInformation($"Структура сборки подготовлена к сохранению: {assemblyVersion.Name} v{assemblyVersion.Version}");
                return assemblyVersion;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при подготовке сохранения структуры сборки");
                throw;
            }
        }

        /// <summary>Строки структуры пишутся только для НОВЫХ версий сборок (каждая один раз).
        /// Неотмеченные дочерние сборки ссылаются на свою версию, их структура не трогается.</summary>
        private async Task SaveAssemblyStructureRecursive(
            ComponentVersion assemblyVersion,
            IList<IAGR_SpecificationItem> components,
            AGR_SavePlan plan,
            HashSet<ComponentVersion> expanded)
        {
            int orderIndex = 0;
            foreach (var item in components)
            {
                var componentVersion = await ResolvePlannedVersion(item.Component, plan);

                var existStructure = _context.AssemblyStructures.Local
                    .FirstOrDefault(s => s.ParentComponentVersion == assemblyVersion
                                      && s.ChildComponentVersion == componentVersion);
                if (existStructure != null)
                {
                    // Один и тот же компонент дважды в списке (разные конфигурации одного PartNumber и т.п.)
                    existStructure.Quantity += item.Quantity;
                }
                else
                {
                    _context.AssemblyStructures.Add(new AssemblyStructure
                    {
                        ParentComponentVersion = assemblyVersion,
                        ChildComponentVersion = componentVersion,
                        Quantity = item.Quantity,
                        Order = orderIndex++
                    });
                }

                if (item.Component is IAGR_Assembly childAssembly
                    && plan.Get(item.Component).Action == AGR_VersionAction_e.NewVersion
                    && expanded.Add(componentVersion))
                {
                    var childComponents = childAssembly.GetChildComponents().ToList();
                    await SaveAssemblyStructureRecursive(componentVersion, childComponents, plan, expanded);
                }
            }
        }

        #endregion

        #region Статистика

        public async Task<int> GetComponentCount()
        {
            try
            {
                _logger.LogDebug("Запрос количества компонентов");
                return await _context.Components.CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении количества компонентов");
                throw;
            }
        }
        public async Task<int> GetComponentVersionCount()
        {
            try
            {
                _logger.LogDebug("Запрос количества версий компонентов");
                return await _context.ComponentVersions.CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении количества версий компонентов");
                throw;
            }
        }
        public async Task<List<ComponentVersion>> GetAssembliesUsedComponent(ComponentVersion childComponent)
        {

            return await _context.AssemblyStructures
                .Include(c => c.ParentComponentVersion.Component)
                .Include(c => c.ParentComponentVersion.SavedByUser)
                .Include(c => c.ParentComponentVersion.Files)
                .Where(c => c.ChildComponentVersionId == childComponent.Id)
                .Select(x => x.ParentComponentVersion)
                .ToListAsync();
        }
        public async Task<List<ComponentVersion>> GetRootAssembliesForChildAsync(ComponentVersion childCV)
        {
            var visitedNodes = new HashSet<ComponentVersion>();

            var rootAssemblyIds = new HashSet<ComponentVersion>();

            await FindRootsRecursiveAsync(childCV, visitedNodes, rootAssemblyIds);

            if (!rootAssemblyIds.Any())
            {
                return new List<ComponentVersion>();
            }

            var roots = rootAssemblyIds.ToList();
            //await _context.ComponentVersions
            //.Where(c => rootAssemblyIds.Contains(c))
            //.ToListAsync();

            return roots;
        }

        private async Task FindRootsRecursiveAsync(ComponentVersion currentChildCV, HashSet<ComponentVersion> visitedNodes, HashSet<ComponentVersion> rootAssemblyIds)
        {
            if (visitedNodes.Contains(currentChildCV))
            {
                return;
            }

            visitedNodes.Add(currentChildCV);

            var parents = await _context.AssemblyStructures
                .Include(x => x.ParentComponentVersion)
                    .ThenInclude(x => x.Component)
                .Where(assem => assem.ChildComponentVersion == currentChildCV)
                .Select(assem => assem.ParentComponentVersion)
                .ToListAsync();

            // Если родителей нет, значит текущий узел (currentChildId) является корневой сборкой
            if (!parents.Any())
            {
                rootAssemblyIds.Add(currentChildCV);
                return;
            }

            // Если родители есть, рекурсивно идем вверх для каждого родителя
            foreach (var parentId in parents)
            {
                await FindRootsRecursiveAsync(parentId, visitedNodes, rootAssemblyIds);
            }
        }


        #endregion

        #region Замки редактирования (в транзакции сохранения)

        /// <summary>Помечает к удалению замки текущего пользователя на указанных компонентах.
        /// Удаляются вместе с новыми версиями при CompleteAsync/Commit (одна транзакция).</summary>
        public async Task RemoveEditLocksAsync(IEnumerable<int> componentIds, int userId)
        {
            var ids = componentIds.Distinct().ToList();
            if (ids.Count == 0) return;

            var locks = await _context.ComponentEditLocks
                .Where(l => ids.Contains(l.ComponentId) && l.UserId == userId)
                .ToListAsync();
            _context.ComponentEditLocks.RemoveRange(locks);
            _logger.LogInformation($"Снятие замков в транзакции сохранения: {locks.Count} шт.");
        }

        #endregion

        #region Вспомогательные методы

        private async Task SaveComponentProperties(ComponentVersion componentVersion, IAGR_BaseComponent component)
        {
            if (component.PropertiesCollection?.Properties == null || !component.PropertiesCollection.Properties.Any())
                return;

            _logger.LogDebug($"Сохранение свойств для компонента: {component.PartNumber}");

            foreach (var property in component.PropertiesCollection.Properties)
            {
                var prop = new ComponentProperty
                {
                    ComponentVersion = componentVersion,
                    Name = property.Name,
                    Value = property.Value?.ToString() ?? string.Empty
                };
                _context.ComponentProperties.Add(prop);
            }

            _logger.LogDebug($"Сохранено {component.PropertiesCollection.Properties.Count} свойств");
        }
        /// <summary>
        /// 6.3: неотмеченный компонент, геометрия совпала с маркером, а отпечаток свойств нет - пользователь поправил
        /// свойства в файле. Файл новее БД, поэтому существующая версия обновляется из него (новая версия не создаётся):
        /// - свойства (ComponentProperties) по составу VM, значение из файла: новые добавляются, изменённые
        ///   перезаписываются, отсутствующие в файле не удаляются;
        /// - «Признак» (AvaType) и «Артикул» (если такой артикул есть в AvaArticles);
        /// - материал и покраска деталей (по наименованию; артикул ищется по наименованию в AvaArticles).
        /// «Обозначение» не меняется: это идентичность компонента. SaveChanges - через CompleteAsync снаружи.
        /// fileValues - AGR_PropertiesFingerprint.ReadEffective (конфигурация, иначе Custom файла).
        /// </summary>
        public async Task<int> UpdateReferencedVersionAsync(ComponentVersion componentVersion, IAGR_BaseComponent component,
            IReadOnlyDictionary<string, string> fileValues)
        {
            if (componentVersion.Id == 0) return 0;
            string? FileValue(string name) => fileValues.TryGetValue(name, out var v) ? v.Trim() : null;
            int changed = 0;

            // Свойства
            var vmProps = component.PropertiesCollection?.Properties;
            if (vmProps != null && vmProps.Count > 0)
            {
                var dbProps = await _context.ComponentProperties
                    .Where(p => p.ComponentVersionId == componentVersion.Id)
                    .ToListAsync();

                foreach (var property in vmProps)
                {
                    var value = FileValue(property.Name) ?? property.Value?.ToString() ?? string.Empty;
                    var db = dbProps.FirstOrDefault(p => p.Name == property.Name);
                    if (db == null)
                    {
                        _context.ComponentProperties.Add(new ComponentProperty
                        {
                            ComponentVersionId = componentVersion.Id,
                            Name = property.Name,
                            Value = value
                        });
                        changed++;
                    }
                    else if (db.Value != value)
                    {
                        db.Value = value;
                        changed++;
                    }
                }
            }

            // Признак
            var avaTypeText = FileValue(AGR_PropertyNames.AvaType);
            if (int.TryParse(avaTypeText, out var avaTypeRaw) && Enum.IsDefined(typeof(AGR_AvaType_e), avaTypeRaw)
                && componentVersion.AvaType != (AGR_AvaType_e)avaTypeRaw)
            {
                componentVersion.AvaType = (AGR_AvaType_e)avaTypeRaw;
                changed++;
            }

            // Артикул
            var articleText = FileValue(AGR_PropertyNames.Article);
            if (string.IsNullOrEmpty(articleText))
            {
                if (componentVersion.AvaArticleArticle != null) { componentVersion.AvaArticleArticle = null; changed++; }
            }
            else if (int.TryParse(articleText, out var article) && componentVersion.AvaArticleArticle != article)
            {
                if (await _context.AvaArticles.AnyAsync(a => a.Article == article))
                {
                    componentVersion.AvaArticleArticle = article;
                    changed++;
                }
                else
                {
                    _saveProgress.AddLogMessage($"{component.Name}: артикула {article} нет в справочнике, артикул в БД не изменён.");
                }
            }

            // Материал и покраска (только детали, как SaveMaterialData)
            if (component.ComponentType == AGR_ComponentType_e.Part || component.ComponentType == AGR_ComponentType_e.SheetMetallPart)
                changed += await UpdateMaterialFromFileAsync(componentVersion, component,
                    FileValue(AGR_PropertyNames.Material), FileValue(AGR_PropertyNames.Color));

            if (changed > 0)
            {
                _logger.LogInformation($"Версия {component.PartNumber} v{componentVersion.Version} обновлена из файла: {changed} изменений");
                _saveProgress.AddLogMessage($"Обновлены свойства {component.Name} {component.PartNumber} v{componentVersion.Version}: {changed}");
            }
            return changed;
        }

        private async Task<int> UpdateMaterialFromFileAsync(ComponentVersion componentVersion, IAGR_BaseComponent component,
            string? materialName, string? paintName)
        {
            var material = await _context.ComponentMaterials.FirstOrDefaultAsync(m => m.ComponentVersionId == componentVersion.Id);
            materialName = string.IsNullOrEmpty(materialName) ? null : materialName;
            paintName = string.IsNullOrEmpty(paintName) ? null : paintName;

            bool materialChanged = material?.BaseMaterial != materialName;
            bool paintChanged = material?.Paint != paintName;
            if (!materialChanged && !paintChanged) return 0;

            if (material == null)
            {
                material = new ComponentMaterial { ComponentVersionId = componentVersion.Id };
                _context.ComponentMaterials.Add(material);
            }

            var names = new[] { materialName, paintName }.Where(n => n != null).Select(n => n!).ToList();
            var articles = await GetAvaArticlesByNameAsync(names);
            int changed = 0;

            if (materialChanged)
            {
                material.BaseMaterial = materialName;
                material.MaterialAvaArticleID = materialName != null && articles.TryGetValue(materialName, out var m) ? m.Article : null;
                if (materialName != null && material.MaterialAvaArticleID == null)
                    _saveProgress.AddLogMessage($"{component.Name}: материал «{materialName}» не найден в справочнике, артикул материала пуст.");
                changed++;
            }
            if (paintChanged)
            {
                material.Paint = paintName;
                material.PaintAvaArticleID = paintName != null && articles.TryGetValue(paintName, out var p) ? p.Article : null;
                if (paintName != null && material.PaintAvaArticleID == null)
                    _saveProgress.AddLogMessage($"{component.Name}: покраска «{paintName}» не найдена в справочнике, артикул покраски пуст.");
                changed++;
            }
            return changed;
        }

        private async Task SaveMaterialData(ComponentVersion componentVersion, IAGR_BaseComponent component)
        {
            // Сохраняем материал только для деталей и листовых деталей
            if (component.ComponentType != AGR_ComponentType_e.Part &&
                component.ComponentType != AGR_ComponentType_e.SheetMetallPart)
                return;

            // Проверяем, реализует ли компонент интерфейсы материала и покраски
            IAGR_Material? baseMaterial = null;
            decimal baseMaterialCount = 0;
            IAGR_Material? paint = null;
            decimal? paintCount = null;

            if (component is IAGR_HasMaterial partMaterial)
            {
                baseMaterial = partMaterial.BaseMaterial;
                baseMaterialCount = partMaterial.BaseMaterialCount;
            }
            if (component is IAGR_HasPaint partPaint)
            {
                paint = partPaint.Paint;
                paintCount = partPaint.PaintCount;
            }

            _logger.LogDebug($"Сохранение материала для компонента: {component.PartNumber}");

            var material = new ComponentMaterial
            {
                ComponentVersion = componentVersion,
                BaseMaterial = baseMaterial?.Name,
                BaseMaterialCount = baseMaterialCount,
                MaterialAvaArticleID = baseMaterial?.AvaModel?.Article,

                Paint = paint?.Name,
                PaintCount = paintCount,
                PaintAvaArticleID = paint?.AvaModel?.Article

            };

            _context.ComponentMaterials.Add(material);
        }
        private async Task SaveFileData(ComponentVersion componentVersion, IAGR_BaseComponent component)
        {
            // Проверяем, реализует ли компонент интерфейс файлов
            if (!(component is IAGR_HasFile fileComponent))
                return;

            _logger.LogDebug($"Сохранение информации о файлах для компонента: {component.PartNumber}");

            var files = new List<ComponentFile>();

            // Текущая модель
            //if (!string.IsNullOrEmpty(fileComponent.CurrentModelFilePath))
            //{
            //    files.Add(new ComponentFile
            //    {
            //        ComponentVersion = componentVersion,
            //        FileType = AGR_FileType_e.CurrentModel,
            //        FilePath = fileComponent.CurrentModelFilePath,
            //        LastModified = File.GetLastWriteTimeUtc(fileComponent.CurrentModelFilePath),
            //        FileSize = new FileInfo(fileComponent.CurrentModelFilePath).Length
            //    });
            //}

            //// Текущий чертеж
            //if (!string.IsNullOrEmpty(fileComponent.CurrentDrawFilePath))
            //{
            //    files.Add(new ComponentFile
            //    {
            //        ComponentVersion = componentVersion,
            //        FileType = AGR_FileType_e.CurrentDrawing,
            //        FilePath = fileComponent.CurrentDrawFilePath,
            //        LastModified = File.GetLastWriteTimeUtc(fileComponent.CurrentDrawFilePath),
            //        FileSize = new FileInfo(fileComponent.CurrentDrawFilePath).Length
            //    });
            //}

            // Модель в хранилище
            //if (!string.IsNullOrEmpty(fileComponent.StorageModelFilePath))
            //{
            //    files.Add(new ComponentFile
            //    {
            //        ComponentVersion = componentVersion,
            //        FileType = AGR_FileType_e.StorageModel,
            //        FilePath = fileComponent.StorageModelFilePath,
            //        LastModified = File.GetLastWriteTimeUtc(fileComponent.StorageModelFilePath),
            //        FileSize = new FileInfo(fileComponent.StorageModelFilePath).Length
            //    });
            //}

            // Модель в хранилище
            var storageModelName = component.Name + component.Extension;
            var storageModelPath = AGR_StoragePaths.GetFilePath(componentVersion.Component.StorageGuid, componentVersion.Version, storageModelName);
            if (!string.IsNullOrEmpty(fileComponent.CurrentModelFilePath))
            {

                files.Add(new ComponentFile
                {
                    ComponentVersion = componentVersion,
                    FileType = AGR_FileType_e.StorageModel,
                    FilePath = storageModelPath,
                    LastModified = File.GetLastWriteTimeUtc(fileComponent.CurrentModelFilePath),
                    FileSize = new FileInfo(fileComponent.CurrentModelFilePath).Length
                });
            }

            // Чертеж в хранилище
            if (!string.IsNullOrEmpty(fileComponent.CurrentDrawFilePath))
            {
                files.Add(new ComponentFile
                {
                    ComponentVersion = componentVersion,
                    FileType = AGR_FileType_e.StorageDrawing,
                    FilePath = Path.ChangeExtension(storageModelPath, "SLDDRW"),
                    LastModified = File.GetLastWriteTimeUtc(fileComponent.CurrentDrawFilePath),
                    FileSize = new FileInfo(fileComponent.CurrentDrawFilePath).Length
                });
            }

            // Модель в производстве

            var prodModelName = component.Name + component.Extension;
            var prodModelPath = Path.Combine(AGR_Options.ProductionRootFolderPath, component.PartNumber, prodModelName);
            if (!string.IsNullOrEmpty(fileComponent.CurrentModelFilePath))
            {
                files.Add(new ComponentFile
                {
                    ComponentVersion = componentVersion,
                    FileType = AGR_FileType_e.ProductionModel,
                    FilePath = prodModelPath,
                    LastModified = File.GetLastWriteTimeUtc(fileComponent.CurrentModelFilePath),
                    FileSize = new FileInfo(fileComponent.CurrentModelFilePath).Length
                });
            }

            // Чертеж в производстве
            if (!string.IsNullOrEmpty(fileComponent.CurrentDrawFilePath))
            {
                files.Add(new ComponentFile
                {
                    ComponentVersion = componentVersion,
                    FileType = AGR_FileType_e.ProductionDrawing,
                    FilePath = Path.ChangeExtension(prodModelPath, "SLDDRW"),
                    LastModified = File.GetLastWriteTimeUtc(fileComponent.CurrentDrawFilePath),
                    FileSize = new FileInfo(fileComponent.CurrentDrawFilePath).Length
                });
            }

            foreach (var file in files)
            {
                _context.ComponentFiles.Add(file);
            }

            _logger.LogDebug($"Сохранено {files.Count} файлов");
        }
        public async Task<IEnumerable<ComponentVersion>> GetAllLatestComponentVersionsAsync()
        {
            try
            {
                _logger.LogDebug("Запрос всех последних версий компонентов");

                // Запрос для получения последней версии для каждого компонента
                // Группируем версии по ComponentId и выбираем версию с максимальным номером
                var latestVersions = await _context.ComponentVersions
                    .Include(v => v.Component) // Подгружаем связанный компонент
                    .Include(v => v.Files)     // Подгружаем связанный файл
                    .Include(v => v.SavedByUser) //Загружаем пользователя, который сохранил версию
                    .AsNoTracking() // Оптимизация для чтения
                    .GroupBy(v => v.ComponentId)
                    .Select(g => g.OrderByDescending(v => v.Version).First())
                    .ToListAsync();

                return latestVersions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении всех последних версий компонентов");
                throw;
            }
        }
        public async Task<IEnumerable<ComponentVersion>> GetTopLevelAssembliesNotInProjectsAsync()
        {
            try
            {
                _logger.LogDebug("Запрос сборок верхнего уровня, не входящих ни в один проект");

                // "Верхний уровень" = сборка никогда не встречается как дочерний компонент
                // в составе другой сборки (AssemblyStructure.ChildComponentVersionId)
                var usedAsChild = _context.AssemblyStructures
                    .Select(a => a.ChildComponentVersionId)
                    .Distinct();

                var assembliesInProjects = _context.ProjectComponents
                    .Select(pc => pc.ComponentVersionId)
                    .Distinct();

                var topLevelAssemblies = await _context.ComponentVersions
                    .Include(cv => cv.Component)
                    .Include(cv => cv.Files)
                    .Where(cv => cv.ComponentType == AGR_ComponentType_e.Assembly
                                && !usedAsChild.Contains(cv.Id)
                                && !assembliesInProjects.Contains(cv.Id))
                    .AsNoTracking()
                    .ToListAsync();

                return topLevelAssemblies;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении сборок верхнего уровня, не входящих в проекты");
                throw;
            }
        }
        #endregion

        public async Task<Dictionary<string, AvaArticleModel>> GetAvaArticlesByNameAsync(List<string> names)
        {
            if (names == null || !names.Any())
            {
                return new Dictionary<string, AvaArticleModel>(); // Возвращаем пустой словарь, если список имен пуст
            }

            try
            {
                //_logger.LogDebug($"Запрос AvaArticle по {names.Count} наименованиям: [{string.Join(", ", names)}]");

                // Запрос в базу данных: выбрать AvaArticleModel, чьи Name совпадают с именами в списке
                var avaArticlesFromDb = await _context.AvaArticles
                    .Where(aa => names.Contains(aa.Name)) // Фильтрация по списку
                    .ToListAsync();

                // Создаем словарь: ключ - Name, значение - AvaArticleModel
                // Используем ToDictionary, возможно, стоит указать StringComparer.InvariantCultureIgnoreCase
                // или другой, если сравнение чувствительно к регистру/локали.
                var resultDict = avaArticlesFromDb
                    .ToDictionary(aa => aa.Name, aa => aa, StringComparer.OrdinalIgnoreCase); // Используем OrdinalIgnoreCase для гибкости

                //_logger.LogDebug($"Найдено {resultDict.Count} уникальных AvaArticle по наименованиям.");
                return resultDict;
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, $"Ошибка при поиске AvaArticle по наименованиям: [{string.Join(", ", names)}]");
                throw; // Перебрасываем исключение для обработки на более высоком уровне
            }
        }
        public async Task<AvaArticleModel?> GetAvaArticleByArticleNumberAsync(int articleNumber)
        {
            try
            {
                //_logger.LogDebug($"Запрос AvaArticle по Article: {articleNumber}");

                // Запрос в базу данных: выбрать AvaArticleModel, чей Article совпадает
                var avaArticleFromDb = await _context.AvaArticles
                    .FirstOrDefaultAsync(aa => aa.Article == articleNumber); // Предполагаем, что Article - long

                //_logger.LogDebug(avaArticleFromDb != null ? $"Найден AvaArticle по Article {articleNumber}" : $"AvaArticle по Article {articleNumber} НЕ НАЙДЕН");
                return avaArticleFromDb;
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, $"Ошибка при поиске AvaArticle по Article: {articleNumber}");
                throw; // Перебрасываем исключение для обработки на более высоком уровне
            }
        }

        #region Загрузка техпроцесса

        // --- МЕТОД: Получить техпроцесс компонента по PartNumber ---
        // Возвращает TechnologicalProcess с загруженной коллекцией Operations
        public async Task<TechnologicalProcess?> GetComponentTechnologyProcessByPartNumberAsync(string partNumber)
        {
            try
            {
                _logger.LogDebug($"Запрос техпроцесса для компонента по PartNumber: {partNumber}");

                // Ищем TechnologicalProcess по PartNumber
                // Включаем загрузку связанных Operations
                var techProcess = await _context.TechProcesses
                    .Include(tp => tp.Operations) // Подгружаем операции
                    .FirstOrDefaultAsync(tp => tp.PartNumber == partNumber);

                if (techProcess != null)
                {
                    _logger.LogDebug($"Найден техпроцесс для {partNumber} с {techProcess.Operations.Count} операциями.");
                }
                else
                {
                    _logger.LogDebug($"Техпроцесс для компонента {partNumber} не найден.");
                }

                return techProcess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении техпроцесса для компонента {partNumber}");
                throw; // Или возвращаем null, в зависимости от требований
            }
        }

        // --- МЕТОД: Получить только операции компонента по PartNumber ---
        // Возвращает список Operation, связанных с TechnologicalProcess для данного PartNumber
        public async Task<List<Operation>> GetComponentOperationsByPartNumberAsync(string partNumber)
        {
            try
            {
                _logger.LogDebug($"Запрос операций для компонента по PartNumber: {partNumber}");

                // Ищем Operations, связанные с TechnologicalProcess по PartNumber
                // Для этого нужно пройти через TechnologicalProcess
                var operations = await _context.Operations
                    .Include(op => op.TechnologicalProcess) // Подгружаем связанный техпроцесс (может быть полезно для получения PartNumber в логике вызывающего кода)
                    .Where(op => op.TechnologicalProcess.PartNumber == partNumber) // Фильтр через навигационное свойство
                    .OrderBy(op => op.SequenceNumber) // Сортировка по порядку
                    .ToListAsync();

                _logger.LogDebug($"Найдено {operations.Count} операций для компонента {partNumber}.");

                return operations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении операций для компонента {partNumber}");
                throw; // Или возвращаем пустой список, в зависимости от требований
            }
        }

        public async Task<List<TemplateOperation>> GetAllTemplateOperationsAsync()
        {
            try
            {
                _logger.LogDebug("Запрос всех шаблонных операций из БД");

                // Загружаем все TemplateOperation, включая связанный Workstation
                var templateOps = await _context.TemplateOperations
                    .Include(to => to.Workstation) // Подгружаем данные участка
                    .ToListAsync();

                _logger.LogInformation($"Загружено {templateOps.Count} шаблонных операций из БД.");
                return templateOps;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении всех шаблонных операций из БД");
                throw; // Или возвращаем пустой список, в зависимости от требований
            }
        }

        #endregion

        #region Вспомогательные методы для работы с пользователем

        private async Task<UserEntity> GetOrCreateUserAsync(IAGR_User userDto)
        {
            if (userDto == null)
                return null;

            // Пытаемся найти пользователя по полному имени
            var fullName = userDto.FullName;
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.FirstName == userDto.FirstName
                                       && u.LastName == userDto.LastName
                                       && u.Patronymic == userDto.Patronymic);

            if (existingUser != null)
            {
                _logger.LogDebug($"Пользователь найден в БД: {fullName}");
                return existingUser;
            }

            // Создаем нового пользователя
            var newUser = new UserEntity
            {
                FirstName = userDto.FirstName,
                LastName = userDto.LastName,
                Patronymic = userDto.Patronymic
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Создан новый пользователь: {fullName}");
            return newUser;
        }

        #endregion

        public async Task UpdateComponentVersionEditAsync(AGR_ComponentEditData data)
            => await UpdateComponentVersionEditsAsync(new[] { data });
        public async Task UpdateComponentVersionEditsAsync(IEnumerable<AGR_ComponentEditData> edits)
        {
            foreach (var edit in edits)
            {
                var cv = await _context.ComponentVersions
                    .Include(v => v.Material)
                    .FirstOrDefaultAsync(v => v.Id == edit.ComponentVersionId);
                if (cv == null) continue;

                cv.AvaType = edit.AvaType;
                cv.AvaArticleArticle = edit.AvaArticleArticle;

                if (edit.MaterialAvaArticleId.HasValue || edit.PaintAvaArticleId.HasValue
                    || !string.IsNullOrEmpty(edit.MaterialName) || !string.IsNullOrEmpty(edit.PaintName))
                {
                    cv.Material ??= new ComponentMaterial { ComponentVersionId = cv.Id };
                    cv.Material.BaseMaterial = edit.MaterialName;
                    cv.Material.MaterialAvaArticleID = edit.MaterialAvaArticleId;
                    cv.Material.Paint = edit.PaintName;
                    cv.Material.PaintAvaArticleID = edit.PaintAvaArticleId;
                }
            }
            // SaveChanges вызывается через _unitOfWork.CompleteAsync() снаружи
        }
        public async Task<ComponentVersion?> GetComponentVersionForEdit(string partNumber, int version)
        {
            return await _context.ComponentVersions
                .Include(v => v.Component)
                .Include(v => v.Properties)
                .Include(v => v.Files)
                .Include(v => v.AvaArticle)
                .Include(v => v.Material).ThenInclude(m => m.MaterialAvaArticle)
                .Include(v => v.Material).ThenInclude(m => m.PaintAvaArticle)
                .FirstOrDefaultAsync(v => v.Component.PartNumber == partNumber && v.Version == version);
        }

        public async Task AddComponentFileAsync(ComponentFile file)
            => await _context.ComponentFiles.AddAsync(file);

        public async Task RemoveComponentFileAsync(int componentFileId)
        {
            var file = await _context.ComponentFiles.FindAsync(componentFileId);
            if (file != null) _context.ComponentFiles.Remove(file);
        }
        public async Task<List<AGR_FlatAssemblyComponent>> GetFlatAssemblyComponents(string assemblyPartNumber, int version)
        {
            try
            {
                _logger.LogDebug($"Запрос плоского состава сборки: {assemblyPartNumber} v{version}");

                var rootId = await _context.ComponentVersions
                    .AsNoTracking()
                    .Where(v => v.Component.PartNumber == assemblyPartNumber && v.Version == version)
                    .Select(v => v.Id)
                    .FirstOrDefaultAsync();

                if (rootId == 0)
                {
                    _logger.LogWarning($"Версия сборки не найдена: {assemblyPartNumber} v{version}");
                    return new List<AGR_FlatAssemblyComponent>();
                }

                // Итоговое количество для каждой уникальной версии компонента во всей сборке.
                var totals = new Dictionary<int, AGR_FlatAssemblyComponent>();

                // Множитель, с которым конкретная версия компонента встречается "выше по дереву" —
                // нужен, чтобы правильно посчитать количество её собственных потомков.
                var multiplierByVersionId = new Dictionary<int, int> { [rootId] = 1 };

                // Обход по уровням вложенности: один запрос на весь уровень сразу для ВСЕХ
                // родителей этого уровня, а не по одному запросу на каждую под-сборку —
                // это и есть основной резерв производительности на больших сборках.
                var currentLevelParentIds = new List<int> { rootId };
                var visitedAsParent = new HashSet<int> { rootId }; // защита от циклов в структуре

                while (currentLevelParentIds.Count > 0)
                {
                    var levelEntries = await _context.AssemblyStructures
                        .AsNoTracking()
                        .Include(s => s.ChildComponentVersion)
                            .ThenInclude(cv => cv.Component)
                        .Include(s => s.ChildComponentVersion)
                            .ThenInclude(cv => cv.Properties)
                        .Include(s => s.ChildComponentVersion)
                            .ThenInclude(cv => cv.Files)
                        .Include(s => s.ChildComponentVersion)
                            .ThenInclude(cv => cv.Material)
                                .ThenInclude(m => m.MaterialAvaArticle)
                        .Include(s => s.ChildComponentVersion)
                            .ThenInclude(cv => cv.Material)
                                .ThenInclude(m => m.PaintAvaArticle)
                        .Include(s => s.ChildComponentVersion)
                            .ThenInclude(cv => cv.AvaArticle)
                        .Where(s => currentLevelParentIds.Contains(s.ParentComponentVersionId))
                        .OrderBy(s => s.Order)
                        .ToListAsync();

                    var nextLevelParentIds = new List<int>();

                    foreach (var entry in levelEntries)
                    {
                        var parentMultiplier = multiplierByVersionId[entry.ParentComponentVersionId];
                        var contribution = parentMultiplier * entry.Quantity;
                        var childId = entry.ChildComponentVersionId;

                        if (totals.TryGetValue(childId, out var existing))
                        {
                            existing.TotalQuantity += contribution;
                        }
                        else
                        {
                            totals[childId] = new AGR_FlatAssemblyComponent
                            {
                                Entity = entry.ChildComponentVersion,
                                TotalQuantity = contribution
                            };
                        }

                        // Накапливаем множитель для дальнейшего спуска по дереву — важно
                        // накопить его полностью для childId ДО того, как мы будем запрашивать
                        // его собственных потомков на следующей итерации while.
                        multiplierByVersionId[childId] = multiplierByVersionId.TryGetValue(childId, out var m)
                            ? m + contribution
                            : contribution;

                        if (entry.ChildComponentVersion.ComponentType == AGR_ComponentType_e.Assembly
                            && visitedAsParent.Add(childId)) // true только при первом посещении — защита от циклов и дублей в запросе
                        {
                            nextLevelParentIds.Add(childId);
                        }
                    }

                    currentLevelParentIds = nextLevelParentIds;
                }

                return totals.Values.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении плоского состава сборки: {assemblyPartNumber}");
                throw;
            }
        }

    }
}
