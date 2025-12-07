// ComponentRepository.cs
using Microsoft.EntityFrameworkCore;
using Agrovent.DAL.Entities.Components;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.Infrastructure.Interfaces.Components;

namespace Agrovent.DAL.Repositories
{
    public class ComponentRepository : IAGR_ComponentRepository
    {
        private readonly DataContext _context;

        public ComponentRepository(DataContext context)
        {
            _context = context;
        }

        public async Task<Component?> GetComponentByPartNumber(string partNumber)
        {
            return await _context.Components
                .Include(c => c.Versions)
                    .ThenInclude(v => v.Properties)
                .Include(c => c.Versions)
                    .ThenInclude(v => v.Material)
                .Include(c => c.Versions)
                    .ThenInclude(v => v.Files)
                .FirstOrDefaultAsync(c => c.PartNumber == partNumber);
        }

        public async Task<ComponentVersion?> GetLatestComponentVersion(string partNumber)
        {
            var component = await GetComponentByPartNumber(partNumber);
            return component?.GetLatestVersion();
        }

        public async Task<ComponentVersion> SaveComponent(IAGR_BaseComponent component, int hashSum)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Проверяем существование компонента по PartNumber
                var existingComponent = await GetComponentByPartNumber(component.PartNumber);

                if (existingComponent == null)
                {
                    // Создаем новый компонент
                    existingComponent = new Component
                    {
                        PartNumber = component.PartNumber,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Components.Add(existingComponent);
                    await _context.SaveChangesAsync();
                }

                // Проверяем существование версии по хешу
                var existingVersion = await FindComponentByHash(hashSum);
                if (existingVersion != null)
                {
                    // Версия уже существует
                    return existingVersion;
                }

                // Определяем следующую версию
                var nextVersion = existingComponent.Versions.Any()
                    ? existingComponent.Versions.Max(v => v.Version) + 1
                    : 1;

                // Создаем новую версию
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

                // Сохраняем свойства
                if (component.PropertiesCollection?.Properties != null)
                {
                    foreach (var property in component.PropertiesCollection.Properties)
                    {
                        var prop = new ComponentProperty
                        {
                            ComponentVersionId = componentVersion.Id,
                            Name = property.Name,
                            Value = property.Value?.ToString() ?? "",
                            Type = MapPropertyType(property)
                        };
                        _context.ComponentProperties.Add(prop);
                    }
                }

                // Сохраняем материал (если это деталь)
                if (component.ComponentType == AGR_ComponentType_e.Part ||
                    component.ComponentType == AGR_ComponentType_e.SheetMetallPart)
                {
                    if (component is IAGR_Part partComponent)
                    {
                        var material = new ComponentMaterial
                        {
                            ComponentVersionId = componentVersion.Id,
                            BaseMaterial = partComponent.BaseMaterial?.Name,
                            BaseMaterialCount = partComponent.BaseMaterialCount,
                            Paint = partComponent.Paint?.Name,
                            PaintCount = partComponent.PaintCount
                        };
                        _context.ComponentMaterials.Add(material);
                    }
                }

                // Сохраняем файлы
                if (component is AGR_FileComponent fileComponent)
                {
                    var files = new List<ComponentFile>
                    {
                        new() { ComponentVersionId = componentVersion.Id, FileType = FileType.CurrentModel, FilePath = fileComponent.CurrentModelFilePath },
                        new() { ComponentVersionId = componentVersion.Id, FileType = FileType.CurrentDrawing, FilePath = fileComponent.CurrentDrawFilePath },
                        new() { ComponentVersionId = componentVersion.Id, FileType = FileType.StorageModel, FilePath = fileComponent.StorageModelFilePath },
                        new() { ComponentVersionId = componentVersion.Id, FileType = FileType.StorageDrawing, FilePath = fileComponent.StorageDrawFilePath },
                        new() { ComponentVersionId = componentVersion.Id, FileType = FileType.ProductionModel, FilePath = fileComponent.ProductionModelFilePath },
                        new() { ComponentVersionId = componentVersion.Id, FileType = FileType.ProductionDrawing, FilePath = fileComponent.ProductionDrawFilePath }
                    };

                    // Добавляем только файлы, которые существуют
                    foreach (var file in files.Where(f => !string.IsNullOrEmpty(f.FilePath)))
                    {
                        _context.ComponentFiles.Add(file);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return componentVersion;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private PropertyType MapPropertyType(object property)
        {
            return property switch
            {
                AGR_StringPropertyVM => PropertyType.String,
                AGR_NumberPropertyVM => PropertyType.Number,
                AGR_BooleanPropertyVM => PropertyType.Boolean,
                _ => PropertyType.String
            };
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

        public async Task SaveAssemblyStructure(ComponentVersion assemblyVersion, List<SpecificationItemVM> components)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Удаляем старую структуру
                var oldStructures = await _context.AssemblyStructures
                    .Where(s => s.AssemblyVersionId == assemblyVersion.Id)
                    .ToListAsync();

                _context.AssemblyStructures.RemoveRange(oldStructures);
                await _context.SaveChangesAsync();

                // Сохраняем новую структуру (рекурсивно)
                await SaveAssemblyStructureRecursive(assemblyVersion, components, null, 0, 0);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task SaveAssemblyStructureRecursive(
            ComponentVersion assemblyVersion,
            List<SpecificationItemVM> components,
            AssemblyStructure? parent,
            int level,
            int startOrder)
        {
            int orderIndex = startOrder;

            foreach (var component in components)
            {
                // Сохраняем компонент в базу (если нужно)
                var componentVersion = await SaveComponent(component.Component, component.Component.GetHashCode());

                // Создаем запись в структуре
                var structure = new AssemblyStructure
                {
                    AssemblyVersionId = assemblyVersion.Id,
                    ComponentVersionId = componentVersion.Id,
                    Quantity = component.Quantity,
                    Level = level,
                    ParentStructureId = parent?.Id,
                    OrderIndex = orderIndex++
                };

                _context.AssemblyStructures.Add(structure);
                await _context.SaveChangesAsync();

                // Если компонент - сборка, рекурсивно обрабатываем его структуру
                if (component.Component.ComponentType == AGR_ComponentType_e.Assembly &&
                    component.Component is AGR_AssemblyComponentVM assemblyComponent)
                {
                    // Получаем SpecificationItemVM для компонентов сборки
                    var childComponents = assemblyComponent.AGR_TopComponents?.ToList() ?? new List<SpecificationItemVM>();
                    await SaveAssemblyStructureRecursive(assemblyVersion, childComponents, structure, level + 1, orderIndex);
                }
            }
        }

        public async Task<List<AssemblyStructure>> GetAssemblyStructure(string assemblyPartNumber, int version)
        {
            var assemblyVersion = await _context.ComponentVersions
                .Include(v => v.Component)
                .FirstOrDefaultAsync(v => v.Component.PartNumber == assemblyPartNumber && v.Version == version);

            if (assemblyVersion == null)
                return new List<AssemblyStructure>();

            return await _context.AssemblyStructures
                .Include(s => s.AssemblyVersion)
                .Include(s => s.ComponentVersion)
                    .ThenInclude(cv => cv.Component)
                .Include(s => s.ComponentVersion)
                    .ThenInclude(cv => cv.Properties)
                .Include(s => s.ComponentVersion)
                    .ThenInclude(cv => cv.Material)
                .Include(s => s.ChildStructures)
                .Where(s => s.AssemblyVersionId == assemblyVersion.Id)
                .OrderBy(s => s.Level)
                .ThenBy(s => s.OrderIndex)
                .ToListAsync();
        }

        // Остальные методы реализации...
    }
}