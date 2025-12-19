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
using Agrovent.DAL.Infrastructure.Interfaces;

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
                _logger.LogInformation($"Подготовка к сохранению компонента: {component.PartNumber}, HashSum: {hashSum}");

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

                _logger.LogInformation($"Создание новой версии: {component.PartNumber} v{nextVersion}");

                // 4. Создаем новую версию компонента
                var componentVersion = new ComponentVersion
                {
                    Component = existingComponent,
                    Version = nextVersion,
                    HashSum = hashSum,
                    Name = component.Name,
                    ConfigName = component.ConfigName,
                    MatchModel = component.MatchModel,
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
                var assemblyHash = CalculateComponentHash(assembly as IAGR_BaseComponent);
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
                var componentHash = CalculateComponentHash(item.Component);
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

                var hash = CalculateComponentHash(component);
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

                var hash = CalculateComponentHash(component);
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

        private int CalculateComponentHash(IAGR_BaseComponent component)
        {
            // Вычисляем хеш на основе важных свойств
            unchecked
            {
                int hash = 17;


                return hash;


                //int hash = 17;
                //hash = hash * 23 + (component.Name?.GetHashCode(StringComparison.Ordinal) ?? 0);
                //hash = hash * 23 + (component.ConfigName?.GetHashCode(StringComparison.Ordinal) ?? 0);
                //hash = hash * 23 + (component.PartNumber?.GetHashCode(StringComparison.Ordinal) ?? 0);
                //hash = hash * 23 + component.ComponentType.GetHashCode();
                //hash = hash * 23 + component.AvaType.GetHashCode();
                //return hash;
            }
        }

        #endregion
    }
}