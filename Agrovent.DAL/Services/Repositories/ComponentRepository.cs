using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces.Components;
using Agrovent.Infrastructure.Interfaces.Specification;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.Infrastructure.Interfaces.Properties;
using Agrovent.Infrastructure.Interfaces;
using Microsoft.VisualBasic.FileIO;
using Xarial.XCad.Documents;
using Xarial.XCad.SolidWorks.Documents;
using Agrovent.ViewModels.Windows;
using System.Windows.Forms;
using AgroventInfrastructure.Interfaces.Entities.Components;
using AgroventInfrastructure.Interfaces.Entities;
using AgroventInfrastructure.Interfaces.Entities.TechProcess;
using AgroventInfrastructure.Entities.Base;
using AgroventInfrastructure.Entities.Components;
using AgroventInfrastructure.Entities.TechProcess;
using Agrovent.Infrastructure;
using System.Data;

namespace Agrovent.DAL.Services.Repositories
{
    public interface IAGR_ComponentRepository
    {
        // Основные операции с компонентами
        Task<Component?> GetComponentByPartNumber(string partNumber);
        Task<ComponentVersion?> GetComponentVersion(string partNumber, int version);
        Task<ComponentVersion?> GetLatestComponentVersion(string partNumber);

        // Создание/обновление компонента (без транзакций - Unit of Work управляет транзакциями)
        Task<Component> CreateNewComponent(IAGR_BaseComponent component);
        Task<bool> CreateNewComponents(List<IAGR_BaseComponent> components);

        Task<ComponentVersion> SaveComponent(IAGR_BaseComponent component, int hashSum);

        // Поиск компонента по хешу
        Task<ComponentVersion?> FindComponentByHash(int hashSum);
        Task<ComponentVersion?> FindComponentByHash(int hashSum, string partnumber);

        // Получение структуры сборки
        Task<List<AssemblyStructure>> GetAssemblyStructure(string assemblyPartNumber, int version);
        Task SaveAssemblyStructure(IAGR_BaseComponent assembly, IEnumerable<IAGR_SpecificationItem> components);
        Task<List<AssemblyStructure>> GetAssemblyStructureRecursive(string assemblyPartNumber, int version);
        Task<AssemblyStructure?> GetExistingAssemblyStructure(ComponentVersion parentComponentVersion, ComponentVersion childComponentVersion, int quantity);
        Task<List<ComponentVersion>> GetRootAssembliesForChildAsync(ComponentVersion childCV);

        // Статистика
        Task<int> GetComponentCount();
        Task<int> GetComponentVersionCount();
        Task<List<ComponentVersion>> GetAssembliesUsedComponent(ComponentVersion childComponent);


        // Управление версиями
        Task<bool> HasComponentChanged(IAGR_BaseComponent component);
        Task<ComponentVersion?> GetExistingVersion(IAGR_BaseComponent component);
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

        public async Task<ComponentVersion> SaveComponent(IAGR_BaseComponent component, int hashSum)
        {
            try
            {
                var pn = component.PartNumber;
                Component existingComponent = default;
                _logger.LogInformation($"Подготовка к сохранению компонента: {component.Name} {component.PartNumber}, HashSum: {hashSum}");
                _saveProgress.AddLogMessage($"Подготовка к сохранению компонента: {component.Name} {component.PartNumber}, HashSum: {hashSum}");
                // Генерация PartNumber, если он пуст 
                if (string.IsNullOrWhiteSpace(component.PartNumber))
                {
                    _logger.LogDebug("PartNumber компонента пуст. Генерация нового...");
                    _saveProgress.AddLogMessage("PartNumber компонента пуст. Генерация нового...");

                    //existingComponent = await CreateNewComponent(component);

                    //component.PartNumber = await GenerateNewPartNumberAsync_Seq();
                    _logger.LogInformation($"Сгенерирован PartNumber: {component.PartNumber}");
                    _saveProgress.AddLogMessage($"Сгенерирован PartNumber: {component.PartNumber}");

                    //component.SwDocument.Save();
                }
                else
                {
                    existingComponent = await GetComponentByPartNumber(component.PartNumber);
                }
                // 1. Проверяем существование компонента по PartNumber

                if (existingComponent == null)
                {

                    // Проверяем, нет ли компонента с таким PartNumber в локальном контексте
                    var localComponent = _context.Components.Local
                        .FirstOrDefault(c => c.PartNumber == component.PartNumber);

                    if (localComponent != null)
                    {
                        _logger.LogInformation($"Компонент найден в локальном контексте: {component.PartNumber}");
                        _saveProgress.AddLogMessage($"Компонент найден в локальном контексте: {component.PartNumber}");
                        existingComponent = localComponent;
                    }
                    else
                    {
                        _logger.LogInformation($"Создание нового компонента: {component.PartNumber}");
                        _saveProgress.AddLogMessage($"Создание нового компонента: {component.PartNumber}");
                        existingComponent = new Component
                        {
                            PartNumber = component.PartNumber,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Components.Add(existingComponent);
                        // Сохраняем сразу, чтобы получить Id
                        //await _context.SaveChangesAsync();
                        _logger.LogInformation($"Создан компонент: {existingComponent.PartNumber}");
                        _saveProgress.AddLogMessage($"Создан компонент: {existingComponent.PartNumber}");
                    }
                }

                // 2. Проверяем существование версии по хешу
                var existingVersion = await FindComponentByHash(hashSum, component.PartNumber);
                if (existingVersion != null)
                {
                    _logger.LogInformation($"Версия уже существует: {component.Name} {component.PartNumber} v{existingVersion.Version}");
                    _saveProgress.AddLogMessage($"Версия уже существует: {component.Name} {component.PartNumber} v{existingVersion.Version}");
                    _saveProgress.AddLogMessage("============================================================");

                    return existingVersion;
                }
                if (existingVersion == null)
                {
                    existingVersion = existingComponent.Versions
                        .OrderByDescending(x => x.Version)
                        .FirstOrDefault(x => x.HashSum == hashSum);

                    if (existingVersion != null)
                    {
                        _logger.LogInformation($"Версия уже существует: {component.Name} {component.PartNumber} v{existingVersion.Version}");
                        _saveProgress.AddLogMessage($"Версия уже существует: {component.Name} {component.PartNumber} v{existingVersion.Version}");
                        _saveProgress.AddLogMessage("============================================================");
                        return existingVersion;
                    }
                }

                // 3. Определяем следующую версию
                var nextVersion = existingComponent.Versions.Any()
                    ? existingComponent.Versions.Max(v => v.Version) + 1
                    : 1;

                _logger.LogInformation($"Создание новой версии: {component.Name}_{component.PartNumber} v{nextVersion}");
                _saveProgress.AddLogMessage($"Создание новой версии: {component.Name}_{component.PartNumber} v{nextVersion}");

                // 4. Создаем новую версию компонента
                if (_currentUser == null)
                {
                }
                var mAvaArticle = component.AvaArticle as AvaArticleModel;

                var componentVersion = new ComponentVersion
                {
                    Component = existingComponent,
                    Version = nextVersion,
                    HashSum = hashSum,
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

                // 5. Сохраняем свойства компонента
                await SaveComponentProperties(componentVersion, component);

                // 6. Сохраняем материал и покраску для деталей
                await SaveMaterialData(componentVersion, component);

                // 7. Сохраняем информацию о файлах
                await SaveFileData(componentVersion, component);

                component.HashSum = hashSum;

                //await _context.SaveChangesAsync();

                _logger.LogInformation($"Компонент сохранен: {component.PartNumber} v{nextVersion}");
                _saveProgress.AddLogMessage($"Компонент сохраненен: {component.PartNumber} v{nextVersion}");
                _saveProgress.AddLogMessage("============================================================");
                return componentVersion;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при сохранении компонента: {component.PartNumber}");
                _saveProgress.AddLogMessage($"Ошибка при сохранении компонента: {component.PartNumber}.\n{ex.InnerException}");
                throw;
            }
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

        #region Поиск компонента по хешу

        public async Task<ComponentVersion?> FindComponentByHash(int hashSum)
        {
            try
            {
                _logger.LogDebug($"Поиск компонента по хешу: {hashSum}");

                return await _context.ComponentVersions
                    .Include(v => v.Component)
                    .Include(v => v.Properties)
                    .Include(v => v.Material)
                    .Include(v => v.Files)
                    .FirstOrDefaultAsync(v => v.HashSum == hashSum);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при поиске компонента по хешу: {hashSum}");
                //System.Windows.Forms.MessageBox.Show(ex.Message);
                throw;

            }
        }
        public async Task<ComponentVersion?> FindComponentByHash(int hashSum, string partnumber)
        {
            try
            {
                _logger.LogDebug($"Поиск компонента по хешу: {hashSum}");

                return await _context.ComponentVersions
                    .Include(v => v.Component)
                    .Include(v => v.Properties)
                    .Include(v => v.Material)
                    .Include(v => v.Files)
                    .Where(c => c.Component.PartNumber == partnumber)
                    .FirstOrDefaultAsync(v => v.HashSum == hashSum);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при поиске компонента по хешу: {hashSum}");
                //System.Windows.Forms.MessageBox.Show(ex.Message);
                throw;

            }
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

        public async Task SaveAssemblyStructure(IAGR_BaseComponent assembly, IEnumerable<IAGR_SpecificationItem> components)
        {

            try
            {
                _logger.LogInformation($"Подготовка к сохранению структуры сборки: {assembly.PartNumber}");

                // 1. Сохраняем сборку как компонент
                var assemblyHash = assembly.CalculateComponentHash(); //(assembly as IAGR_BaseComponent);
                var assemblyVersion = await SaveComponent(assembly, assemblyHash);

                // 3. Сохраняем новую структуру рекурсивно
                await SaveAssemblyStructureRecursive(assemblyVersion, components.ToList(), null, 0);

                _logger.LogInformation($"Структура сборки подготовлена к сохранению: {assemblyVersion.Name} v{assemblyVersion.Version}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при подготовке сохранения структуры сборки");
                throw;
            }
        }

        private async Task SaveAssemblyStructureRecursive(
            ComponentVersion assemblyVersion,
            IList<IAGR_SpecificationItem> components,
            AssemblyStructure? parent,
            int orderIndex)
        {
            foreach (var item in components)
            {
                // Сохраняем компонент
                var componentHash = item.Component.CalculateComponentHash();
                var componentVersion = await SaveComponent(item.Component, componentHash);

                var existStructure = await GetExistingAssemblyStructure(assemblyVersion, componentVersion, item.Quantity);

                if (existStructure == null)
                {

                    var localStructure = _context.AssemblyStructures.Local
                        .FirstOrDefault(c => c.ParentComponentVersion.Name == assemblyVersion.Name
                                        && c.ChildComponentVersion.Name == componentVersion.Name
                                        && c.Quantity == item.Quantity);

                    if (localStructure == null)
                    {
                        _logger.LogInformation($"Создание новой структуры: {assemblyVersion.Component.PartNumber}");
                        _saveProgress.AddLogMessage($"Создание новой структуры: {assemblyVersion.Component.PartNumber}");
                        existStructure = new AssemblyStructure
                        {
                            ParentComponentVersion = assemblyVersion,
                            ChildComponentVersion = componentVersion,
                            Quantity = item.Quantity,
                            Order = orderIndex++
                        };
                        _context.AssemblyStructures.Add(existStructure);
                    }
                    else
                    {
                        _logger.LogInformation($"Структура найдена в локальном контексте: {assemblyVersion.Component.PartNumber}");
                        _saveProgress.AddLogMessage($"Структура найдена в локальном контексте: {assemblyVersion.Component.PartNumber}");
                        existStructure = localStructure;
                    }
                }



                // Если компонент - сборка, рекурсивно обрабатываем его структуру
                if (item.Component is IAGR_Assembly childAssembly)
                {
                    var childComponents = childAssembly.GetChildComponents().ToList();
                    await SaveAssemblyStructureRecursive(componentVersion, childComponents, existStructure, 0);
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

        #region Управление версиями

        public async Task<bool> HasComponentChanged(IAGR_BaseComponent component)
        {
            try
            {
                Component existingComponent = default;
                _logger.LogDebug($"Проверка изменений компонента: {component.PartNumber}");
                var partnumber = component.PartNumber;
                if (!string.IsNullOrEmpty(partnumber))
                {
                    existingComponent = await GetComponentByPartNumber(partnumber);
                    if (existingComponent != null)
                    {
                        var hash = component.CalculateComponentHash();
                        var existingVersion = await FindComponentByHash(hash, partnumber);
                        return existingVersion != null;
                    }
                    else return false;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при проверке изменений компонента: {component.PartNumber}");
                throw;
            }
        }
        public async Task<ComponentVersion?> GetExistingVersion(IAGR_BaseComponent component)
        {
            try
            {
                _logger.LogDebug($"Поиск существующей версии компонента: {component.PartNumber}");

                var componentVersions = await _context.ComponentVersions
                    .Include(c => c.Component)
                    .Where(c => c.Component.PartNumber == component.PartNumber)
                    .ToListAsync();

                if (componentVersions.Count == 0) return null;

                var hash = component.CalculateComponentHash();
                return componentVersions.FirstOrDefault(c => c.HashSum == hash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при поиске существующей версии компонента: {component.PartNumber}");
                throw;
            }
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
            var storageModelPath = Path.Combine(AGR_Options.StorageRootFolderPath, componentVersion.HashSum.ToString("D10"), storageModelName);
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
                _logger.LogDebug("Запрос версий сборок верхнего уровня, не входящих в проекты");

                // Предположим, ComponentType_e.Assembly соответствует 0 (или другому значению enum -> int)
                // и что "верхний уровень" означает, что у компонента нет родителя в структуре сборки (что сложно определить без хранения этой связи)
                // Вместо этого, будем искать сборки, которые НЕ находятся НИ в одном ProjectComponent
                var assembliesInProjects = _context.ProjectComponents
                    .Select(pc => pc.ComponentVersionId)
                    .Distinct();

                var topLevelAssemblies = await _context.ComponentVersions
                    .Include(cv => cv.Component) // Подгружаем связанный компонент
                    .Where(cv => cv.ComponentType == (int)AGR_ComponentType_e.Assembly
                                && !assembliesInProjects.Contains(cv.Id))
                    .ToListAsync();

                return topLevelAssemblies;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении версий сборок верхнего уровня, не входящих в проекты");
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