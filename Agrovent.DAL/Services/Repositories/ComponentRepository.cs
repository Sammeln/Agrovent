// ComponentRepository.cs в Agrovent.DAL
using Microsoft.EntityFrameworkCore;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces.Components;
using Agrovent.Infrastructure.Interfaces.Specification;
using Agrovent.DAL.Entities.Components;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;

namespace Agrovent.DAL.Repositories
{
    public class ComponentRepository : IComponentRepository
    {
        private readonly DataContext _context;
        private readonly ILogger<ComponentRepository> _logger;

        public ComponentRepository(DataContext context, ILogger<ComponentRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ComponentVersion> SaveComponent(IAGR_BaseComponent component, int hashSum)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _logger.LogInformation($"Сохранение компонента: {component.PartNumber}, HashSum: {hashSum}");

                // 1. Проверяем существование компонента по PartNumber
                var existingComponent = await GetComponentByPartNumber(component.PartNumber);

                if (existingComponent == null)
                {
                    _logger.LogInformation($"Создание нового компонента: {component.PartNumber}");
                    existingComponent = new Component
                    {
                        PartNumber = component.PartNumber,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Components.Add(existingComponent);
                    await _context.SaveChangesAsync();
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
                    ComponentId = existingComponent.Id,
                    Version = nextVersion,
                    HashSum = hashSum,
                    Name = component.Name,
                    ConfigName = component.ConfigName,
                    AvaArticle = component.AvaArticle,
                    ComponentType = component.ComponentType,
                    AvaType = component.AvaType,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ComponentVersions.Add(componentVersion);
                await _context.SaveChangesAsync();

                // 5. Сохраняем свойства компонента
                await SaveComponentProperties(componentVersion, component);

                // 6. Сохраняем материал и покраску для деталей
                await SaveMaterialData(componentVersion, component);

                // 7. Сохраняем информацию о файлах
                await SaveFileData(componentVersion, component);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Компонент успешно сохранен: {component.PartNumber} v{nextVersion}");
                return componentVersion;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Ошибка при сохранении компонента: {component.PartNumber}");
                throw;
            }
        }

        private async Task SaveComponentProperties(ComponentVersion componentVersion, IAGR_BaseComponent component)
        {
            if (component.PropertiesCollection?.Properties == null || !component.PropertiesCollection.Properties.Any())
                return;

            foreach (var property in component.PropertiesCollection.Properties)
            {
                var prop = new ComponentProperty
                {
                    ComponentVersionId = componentVersion.Id,
                    Name = property.Name,
                    Value = property.Value?.ToString() ?? string.Empty,
                    Type = MapPropertyType(property.Value)
                };
                _context.ComponentProperties.Add(prop);
            }

            _logger.LogDebug($"Сохранено {component.PropertiesCollection.Properties.Count} свойств для {component.PartNumber}");
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

            if (component is IAGR_Part partComponent)
            {
                baseMaterial = partComponent.BaseMaterial;
                baseMaterialCount = partComponent.BaseMaterialCount;
                paint = partComponent.Paint;
                paintCount = partComponent.PaintCount;
            }

            var material = new ComponentMaterial
            {
                ComponentVersionId = componentVersion.Id,
                BaseMaterial = baseMaterial?.Name,
                BaseMaterialCount = baseMaterialCount,
                Paint = paint?.Name,
                PaintCount = paintCount
            };

            _context.ComponentMaterials.Add(material);
            _logger.LogDebug($"Сохранен материал для {component.PartNumber}");
        }

        private async Task SaveFileData(ComponentVersion componentVersion, IAGR_BaseComponent component)
        {
            // Проверяем, реализует ли компонент интерфейс файлов
            if (!(component is IAGR_HasFile fileComponent))
                return;

            var files = new List<ComponentFile>();

            // Добавляем файлы, если они существуют
            if (!string.IsNullOrEmpty(fileComponent.CurrentModelFilePath))
            {
                files.Add(new ComponentFile
                {
                    ComponentVersionId = componentVersion.Id,
                    FileType = AGR_FileType_e.CurrentModel,
                    FilePath = fileComponent.CurrentModelFilePath,
                    LastModified = File.GetLastWriteTimeUtc(fileComponent.CurrentModelFilePath),
                    FileSize = new FileInfo(fileComponent.CurrentModelFilePath).Length
                });
            }

            if (!string.IsNullOrEmpty(fileComponent.CurrentDrawFilePath))
            {
                files.Add(new ComponentFile
                {
                    ComponentVersionId = componentVersion.Id,
                    FileType = AGR_FileType_e.CurrentDrawing,
                    FilePath = fileComponent.CurrentDrawFilePath,
                    LastModified = File.GetLastWriteTimeUtc(fileComponent.CurrentDrawFilePath),
                    FileSize = new FileInfo(fileComponent.CurrentDrawFilePath).Length
                });
            }

            // Добавляем остальные файлы аналогично...

            foreach (var file in files)
            {
                _context.ComponentFiles.Add(file);
            }

            _logger.LogDebug($"Сохранено {files.Count} файлов для {component.PartNumber}");
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

        public async Task SaveAssemblyStructure(IAGR_Assembly assembly, IEnumerable<IAGR_SpecificationItem> components)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. Сохраняем сборку как компонент
                var assemblyHash = CalculateComponentHash(assembly as IAGR_BaseComponent);
                var assemblyVersion = await SaveComponent(assembly as IAGR_BaseComponent, assemblyHash);

                // 2. Удаляем старую структуру
                var oldStructures = await _context.AssemblyStructures
                    .Where(s => s.AssemblyVersionId == assemblyVersion.Id)
                    .ToListAsync();

                if (oldStructures.Any())
                {
                    _context.AssemblyStructures.RemoveRange(oldStructures);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Удалена старая структура для сборки: {assemblyVersion.Name}");
                }

                // 3. Сохраняем новую структуру рекурсивно
                await SaveAssemblyStructureRecursive(assemblyVersion, components.ToList(), null, 0, 0);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Структура сборки сохранена: {assemblyVersion.Name} v{assemblyVersion.Version}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Ошибка при сохранении структуры сборки");
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
                    AssemblyVersionId = assemblyVersion.Id,
                    ComponentVersionId = componentVersion.Id,
                    Quantity = item.Quantity,
                    Level = level,
                    ParentStructureId = parent?.Id,
                    OrderIndex = orderIndex++
                };

                _context.AssemblyStructures.Add(structure);
                await _context.SaveChangesAsync();

                // Если компонент - сборка, обрабатываем его дочерние компоненты рекурсивно
                if (item.Component is IAGR_Assembly childAssembly)
                {
                    var childComponents = childAssembly.GetChildComponents().ToList();
                    await SaveAssemblyStructureRecursive(assemblyVersion, childComponents, structure, level + 1, 0);
                }
            }
        }

        public async Task<bool> HasComponentChanged(IAGR_BaseComponent component)
        {
            var hash = CalculateComponentHash(component);
            var existingVersion = await FindComponentByHash(hash);
            return existingVersion == null;
        }

        public async Task<ComponentVersion?> GetExistingVersion(IAGR_BaseComponent component)
        {
            var hash = CalculateComponentHash(component);
            return await FindComponentByHash(hash);
        }

        private int CalculateComponentHash(IAGR_BaseComponent component)
        {
            // Используем HashSum из компонента, если он задан
            if (component.HashSum != 0)
                return component.HashSum;

            // Иначе вычисляем хеш на основе важных свойств
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (component.Name?.GetHashCode(StringComparison.Ordinal) ?? 0);
                hash = hash * 23 + (component.ConfigName?.GetHashCode(StringComparison.Ordinal) ?? 0);
                hash = hash * 23 + (component.PartNumber?.GetHashCode(StringComparison.Ordinal) ?? 0);
                hash = hash * 23 + component.ComponentType.GetHashCode();
                return hash;
            }
        }

        // Реализация остальных методов интерфейса...
        public async Task<Component?> GetComponentByPartNumber(string partNumber)
        {
            return await _context.Components
                .Include(c => c.Versions)
                .FirstOrDefaultAsync(c => c.PartNumber == partNumber);
        }

        public async Task<ComponentVersion?> GetComponentVersion(string partNumber, int version)
        {
            return await _context.ComponentVersions
                .Include(v => v.Component)
                .Include(v => v.Properties)
                .Include(v => v.Material)
                .Include(v => v.Files)
                .FirstOrDefaultAsync(v => v.Component.PartNumber == partNumber && v.Version == version);
        }

        public async Task<ComponentVersion?> GetLatestComponentVersion(string partNumber)
        {
            var component = await GetComponentByPartNumber(partNumber);
            return component?.Versions.OrderByDescending(v => v.Version).FirstOrDefault();
        }

        public async Task<ComponentVersion?> FindComponentByHash(int hashSum)
        {
            return await _context.ComponentVersions
                .Include(v => v.Component)
                .Include(v => v.Properties)
                .Include(v => v.Material)
                .Include(v => v.Files)
                .FirstOrDefaultAsync(v => v.HashSum == hashSum);
        }

        public async Task<List<AssemblyStructure>> GetAssemblyStructure(string assemblyPartNumber, int version)
        {
            var assemblyVersion = await GetComponentVersion(assemblyPartNumber, version);
            if (assemblyVersion == null)
                return new List<AssemblyStructure>();

            return await _context.AssemblyStructures
                .Include(s => s.ComponentVersion)
                .ThenInclude(cv => cv.Component)
                .Where(s => s.AssemblyVersionId == assemblyVersion.Id)
                .OrderBy(s => s.Level)
                .ThenBy(s => s.OrderIndex)
                .ToListAsync();
        }

        public async Task<int> GetComponentCount()
        {
            return await _context.Components.CountAsync();
        }

        public async Task<int> GetComponentVersionCount()
        {
            return await _context.ComponentVersions.CountAsync();
        }
    }
}