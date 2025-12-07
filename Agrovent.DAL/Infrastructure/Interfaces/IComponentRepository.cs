// IComponentRepository.cs
using Agrovent.DAL.Entities.Components;
using Agrovent.DAL.Infrastructure.Enums;
using Agrovent.DAL.Infrastructure.Interfaces.Components;
using Agrovent.ViewModels.Specification;

namespace Agrovent.DAL.Repositories
{
    public interface IComponentRepository
    {
        // Основные операции с компонентами
        Task<Component?> GetComponentByPartNumber(string partNumber);
        Task<ComponentVersion?> GetComponentVersion(string partNumber, int version);
        Task<ComponentVersion?> GetLatestComponentVersion(string partNumber);

        // Создание/обновление компонента
        Task<ComponentVersion> SaveComponent(IAGR_BaseComponent component, int hashSum);

        // Поиск компонента по хешу (проверка существования версии)
        Task<ComponentVersion?> FindComponentByHash(int hashSum);

        // Получение структуры сборки
        Task<List<AssemblyStructure>> GetAssemblyStructure(string assemblyPartNumber, int version);
        Task SaveAssemblyStructure(ComponentVersion assemblyVersion, List<IAGR_SpecificationItem> components);

        // Статистика
        Task<int> GetComponentCount();
        Task<int> GetComponentVersionCount();
    }
}