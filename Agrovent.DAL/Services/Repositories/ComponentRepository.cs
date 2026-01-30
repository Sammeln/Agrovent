using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Agrovent.DAL.Entities.Components;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces.Components;
using Agrovent.Infrastructure.Interfaces.Specification;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.Infrastructure.Interfaces.Properties;
using Agrovent.Infrastructure.Interfaces;
using Microsoft.VisualBasic.FileIO;
using Xarial.XCad.Documents;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.DAL.Repositories
{
    public class ComponentRepository : IAGR_ComponentRepository
    {
        private readonly DataContext _context;
        private readonly ILogger<ComponentRepository> _logger;

        public ComponentRepository(DataContext context, ILogger<ComponentRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region Основные операции с компонентами

        public async Task<Component?> GetComponentByPartNumber(string partNumber)
        {
            try
            {
                _logger.LogDebug($"Запрос компонента по PartNumber: {partNumber}");

                return await _context.Components
                    .Include(c => c.Versions)
                        .ThenInclude(v => v.Properties)
                    .Include(c => c.Versions)
                        .ThenInclude(v => v.Material)
                    .Include(c => c.Versions)
                        .ThenInclude(v => v.Files)
                    .FirstOrDefaultAsync(c => c.PartNumber == partNumber);
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

                return component.Versions
                    .OrderByDescending(v => v.Version)
                    .FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении последней версии компонента: {partNumber}");
                throw;
            }
        }

        #endregion

        #region Создание/обновление компонента

        public async Task<ComponentVersion> SaveComponent(IAGR_BaseComponent component, int hashSum)
        {
            try
            {
                var pn = component.PartNumber;
                _logger.LogInformation($"Подготовка к сохранению компонента: {component.PartNumber}, HashSum: {hashSum}");

                // Генерация PartNumber, если он пуст 
                if (string.IsNullOrWhiteSpace(component.PartNumber))
                {
                    _logger.LogDebug("PartNumber компонента пуст. Генерация нового...");
                    component.PartNumber = await GenerateNewPartNumberAsync();
                    _logger.LogInformation($"Сгенерирован PartNumber: {component.PartNumber}");
                }
                //else
                //{
                //    // Проверяем формат PartNumber, если он был задан
                //    if (!IsValidPartNumberFormat(component.PartNumber))
                //    {
                //        _logger.LogWarning($"Предоставленный PartNumber '{component.PartNumber}' имеет неверный формат. Ожидается 7 цифр без точек.");
                //        // В зависимости от требований, можно выбросить исключение или попытаться исправить
                //        // throw new ArgumentException($"Invalid PartNumber format: {component.PartNumber}. Expected 7 digits without dots.");
                //        // Или, например, попытаться очистить:
                //        component.PartNumber = SanitizePartNumber(component.PartNumber);
                //        if (!IsValidPartNumberFormat(component.PartNumber))
                //        {
                //            _logger.LogError($"Не удалось исправить формат PartNumber '{component.PartNumber}'.");
                //            throw new ArgumentException($"Invalid or unfixable PartNumber format: {component.PartNumber}");
                //        }
                //    }
                //}

                // 1. Проверяем существование компонента по PartNumber
                var existingComponent = await GetComponentByPartNumber(component.PartNumber);

                if (existingComponent == null)
                {

                    // Проверяем, нет ли компонента с таким PartNumber в локальном контексте
                    var localComponent = _context.Components.Local
                        .FirstOrDefault(c => c.PartNumber == component.PartNumber);

                    if (localComponent != null)
                    {
                        _logger.LogInformation($"Компонент найден в локальном контексте: {component.PartNumber}");
                        existingComponent = localComponent;
                    }
                    else
                    {
                        _logger.LogInformation($"Создание нового компонента: {component.PartNumber}");
                        existingComponent = new Component
                        {
                            PartNumber = component.PartNumber,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Components.Add(existingComponent);
                        // Сохраняем сразу, чтобы получить Id
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"Создан компонент с Id: {existingComponent.Id}");
                    }
                }

                // 2. Проверяем существование версии по хешу
                var existingVersion = await FindComponentByHash(hashSum);
                if (existingVersion != null)
                {
                    _logger.LogInformation($"Версия уже существует: {component.PartNumber} v{existingVersion.Version}");
                    return existingVersion;
                }

                // 3. Определяем следующую версию
                var nextVersion = existingComponent.Versions.Any()
                    ? existingComponent.Versions.Max(v => v.Version) + 1
                    : 1;

                _logger.LogInformation($"Создание новой версии: {component.Name}_{component.PartNumber} v{nextVersion}");

                // 4. Создаем новую версию компонента
                var componentVersion = new ComponentVersion
                {
                    Component = existingComponent,
                    Version = nextVersion,
                    HashSum = hashSum,
                    PreviewImage = component.Preview,
                    Name = component.Name,
                    ConfigName = component.ConfigName,
                    AvaArticle = component.AvaArticle as AvaArticleModel,
                    ComponentType = component.ComponentType,
                    AvaType = component.AvaType,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ComponentVersions.Add(componentVersion);

                // 5. Сохраняем свойства компонента
                await SaveComponentProperties(componentVersion, component);

                // 6. Сохраняем материал и покраску для деталей
                await SaveMaterialData(componentVersion, component);

                // 7. Сохраняем информацию о файлах
                await SaveFileData(componentVersion, component);

                _logger.LogInformation($"Компонент подготовлен к сохранению: {component.PartNumber} v{nextVersion}");
                return componentVersion;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при подготовке сохранения компонента: {component.PartNumber}");
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

                return await _context.AssemblyStructures
                    .Include(s => s.AssemblyVersion)
                        .ThenInclude(av => av.Component)
                    .Include(s => s.ComponentVersion)
                        .ThenInclude(cv => cv.Component)
                    .Include(s => s.ComponentVersion)
                        .ThenInclude(cv => cv.Properties)
                    .Include(s => s.ComponentVersion)
                        .ThenInclude(cv => cv.Material)
                    .Where(s => s.AssemblyVersionId == assemblyVersion.Id)
                    .OrderBy(s => s.Level)
                    .ThenBy(s => s.OrderIndex)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении структуры сборки: {assemblyPartNumber}");
                throw;
            }
        }

        public async Task SaveAssemblyStructure(IAGR_BaseComponent assembly, IEnumerable<IAGR_SpecificationItem> components)
        {
            try
            {
                _logger.LogInformation($"Подготовка к сохранению структуры сборки: {assembly.PartNumber}");

                // 1. Сохраняем сборку как компонент
                var assemblyHash = assembly.CalculateComponentHash(); //(assembly as IAGR_BaseComponent);
                var assemblyVersion = await SaveComponent(assembly as IAGR_BaseComponent, assemblyHash);

                // 2. Удаляем старую структуру
                //var oldStructures = await _context.AssemblyStructures
                //    .Where(s => s.AssemblyVersionId == assemblyVersion.Id)
                //    .ToListAsync();

                //if (oldStructures.Any())
                //{
                //    _context.AssemblyStructures.RemoveRange(oldStructures);
                //    _logger.LogInformation($"Удалена старая структура для сборки: {assemblyVersion.Name}");
                //}

                // 3. Сохраняем новую структуру рекурсивно
                await SaveAssemblyStructureRecursive(assemblyVersion, components.ToList(), null, 0, 0);

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
            int level,
            int orderIndex)
        {
            foreach (var item in components)
            {
                // Сохраняем компонент
                var componentHash = item.Component.CalculateComponentHash();
                var componentVersion = await SaveComponent(item.Component, componentHash);

                // Создаем запись в структуре
                var structure = new AssemblyStructure
                {
                    AssemblyVersion = assemblyVersion,
                    ComponentVersion = componentVersion,
                    Quantity = item.Quantity,
                    Level = level,
                    ParentStructure = parent,
                    OrderIndex = orderIndex++
                };

                _context.AssemblyStructures.Add(structure);

                // Если компонент - сборка, рекурсивно обрабатываем его структуру
                if (item.Component is IAGR_Assembly childAssembly)
                {
                    var childComponents = childAssembly.GetChildComponents().ToList();
                    await SaveAssemblyStructureRecursive(assemblyVersion, childComponents, structure, level + 1, 0);
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

        #endregion

        #region Управление версиями

        public async Task<bool> HasComponentChanged(IAGR_BaseComponent component)
        {
            try
            {
                _logger.LogDebug($"Проверка изменений компонента: {component.PartNumber}");

                var hash = component.CalculateComponentHash();
                var existingVersion = await FindComponentByHash(hash);
                return existingVersion == null;
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

                var hash = component.CalculateComponentHash();
                return await FindComponentByHash(hash);
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
                    Value = property.Value?.ToString() ?? string.Empty,
                    Type = MapPropertyType(property.Value)
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
                Paint = paint?.Name,
                PaintCount = paintCount
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
            if (!string.IsNullOrEmpty(fileComponent.CurrentModelFilePath))
            {
                files.Add(new ComponentFile
                {
                    ComponentVersion = componentVersion,
                    FileType = AGR_FileType_e.CurrentModel,
                    FilePath = fileComponent.CurrentModelFilePath,
                    LastModified = File.GetLastWriteTimeUtc(fileComponent.CurrentModelFilePath),
                    FileSize = new FileInfo(fileComponent.CurrentModelFilePath).Length
                });
            }

            // Текущий чертеж
            if (!string.IsNullOrEmpty(fileComponent.CurrentDrawFilePath))
            {
                files.Add(new ComponentFile
                {
                    ComponentVersion = componentVersion,
                    FileType = AGR_FileType_e.CurrentDrawing,
                    FilePath = fileComponent.CurrentDrawFilePath,
                    LastModified = File.GetLastWriteTimeUtc(fileComponent.CurrentDrawFilePath),
                    FileSize = new FileInfo(fileComponent.CurrentDrawFilePath).Length
                });
            }

            // Модель в хранилище
            if (!string.IsNullOrEmpty(fileComponent.StorageModelFilePath))
            {
                files.Add(new ComponentFile
                {
                    ComponentVersion = componentVersion,
                    FileType = AGR_FileType_e.StorageModel,
                    FilePath = fileComponent.StorageModelFilePath,
                    LastModified = File.GetLastWriteTimeUtc(fileComponent.StorageModelFilePath),
                    FileSize = new FileInfo(fileComponent.StorageModelFilePath).Length
                });
            }

            // Чертеж в хранилище
            if (!string.IsNullOrEmpty(fileComponent.StorageDrawFilePath))
            {
                files.Add(new ComponentFile
                {
                    ComponentVersion = componentVersion,
                    FileType = AGR_FileType_e.StorageDrawing,
                    FilePath = fileComponent.StorageDrawFilePath,
                    LastModified = File.GetLastWriteTimeUtc(fileComponent.StorageDrawFilePath),
                    FileSize = new FileInfo(fileComponent.StorageDrawFilePath).Length
                });
            }

            // Модель в производстве
            if (!string.IsNullOrEmpty(fileComponent.ProductionModelFilePath))
            {
                files.Add(new ComponentFile
                {
                    ComponentVersion = componentVersion,
                    FileType = AGR_FileType_e.ProductionModel,
                    FilePath = fileComponent.ProductionModelFilePath,
                    LastModified = File.GetLastWriteTimeUtc(fileComponent.ProductionModelFilePath),
                    FileSize = new FileInfo(fileComponent.ProductionModelFilePath).Length
                });
            }

            // Чертеж в производстве
            if (!string.IsNullOrEmpty(fileComponent.ProductionDrawFilePath))
            {
                files.Add(new ComponentFile
                {
                    ComponentVersion = componentVersion,
                    FileType = AGR_FileType_e.ProductionDrawing,
                    FilePath = fileComponent.ProductionDrawFilePath,
                    LastModified = File.GetLastWriteTimeUtc(fileComponent.ProductionDrawFilePath),
                    FileSize = new FileInfo(fileComponent.ProductionDrawFilePath).Length
                });
            }

            foreach (var file in files)
            {
                _context.ComponentFiles.Add(file);
            }

            _logger.LogDebug($"Сохранено {files.Count} файлов");
        }

        private AGR_PropertyType_e MapPropertyType(object? value)
        {
            if (value == null)
                return AGR_PropertyType_e.String;

            return value switch
            {
                bool _ => AGR_PropertyType_e.Boolean,
                sbyte or byte or short or ushort or int or uint or long or ulong => AGR_PropertyType_e.Number,
                float or double or decimal => AGR_PropertyType_e.Number,
                DateTime => AGR_PropertyType_e.Date,
                _ => AGR_PropertyType_e.String
            };
        }


        #endregion

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
    }
}