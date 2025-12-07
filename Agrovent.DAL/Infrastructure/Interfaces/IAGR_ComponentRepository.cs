// IComponentRepository.cs
using Agrovent.DAL.Entities.Components;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Agrovent.Infrastructure.Interfaces.Specification;

namespace Agrovent.DAL.Repositories
{
    public interface IAGR_ComponentRepository
    {
        // Основные операции с компонентами
        Task<Component?> GetComponentByPartNumber(string partNumber);
        Task<ComponentVersion?> GetComponentVersion(string partNumber, int version);
        Task<ComponentVersion?> GetLatestComponentVersion(string partNumber);

        // Создание/обновление компонента
        Task<ComponentVersion> SaveComponent(IAGR_BaseComponent component, int hashSum);

        // Поиск компонента по хешу
        Task<ComponentVersion?> FindComponentByHash(int hashSum);

        // Получение структуры сборки
        Task<List<AssemblyStructure>> GetAssemblyStructure(string assemblyPartNumber, int version);
        Task SaveAssemblyStructure(ComponentVersion assemblyVersion, IEnumerable<IAGR_SpecificationItem> components);

        // Статистика
        Task<int> GetComponentCount();
        Task<int> GetComponentVersionCount();
    }
}