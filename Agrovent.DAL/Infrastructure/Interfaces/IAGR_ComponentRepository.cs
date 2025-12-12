using Agrovent.DAL.Entities.Components;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces.Components;
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

        // Создание/обновление компонента (без транзакций - Unit of Work управляет транзакциями)
        Task<ComponentVersion> SaveComponent(IAGR_BaseComponent component, int hashSum);

        // Поиск компонента по хешу
        Task<ComponentVersion?> FindComponentByHash(int hashSum);

        // Получение структуры сборки
        Task<List<AssemblyStructure>> GetAssemblyStructure(string assemblyPartNumber, int version);
        Task SaveAssemblyStructure(IAGR_BaseComponent assembly, IEnumerable<IAGR_SpecificationItem> components);

        // Статистика
        Task<int> GetComponentCount();
        Task<int> GetComponentVersionCount();

        // Управление версиями
        Task<bool> HasComponentChanged(IAGR_BaseComponent component);
        Task<ComponentVersion?> GetExistingVersion(IAGR_BaseComponent component);
    }
}