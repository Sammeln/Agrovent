using System;
using System.Collections.Generic;
using System.Text;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using System.Threading.Tasks;
using Agrovent.ViewModels.Components;
using AgroventInfrastructure.Interfaces.Entities.Components;
using AgroventInfrastructure.Entities.Components;

namespace Agrovent.Infrastructure.Interfaces
{
    public interface IAGR_ComponentVersionService
    {
        Task<bool> CheckAndSaveComponentAsync(IAGR_BaseComponent component);
        Task<bool> CheckAndSaveAssemblyAsync(AGR_AssemblyComponentVM assembly);
        Task<ComponentVersion?> GetComponentVersionAsync(string partNumber, int version);
        Task<bool> HasComponentChangedAsync(IAGR_BaseComponent component);
        Task<Component> CreateNewComponent(IAGR_BaseComponent component);
        Task<bool> CreateNewComponents(List<IAGR_BaseComponent> components);

        // Дополнительные методы (опционально)
        Task<ComponentVersion?> GetLatestComponentVersionAsync(string partNumber);
        Task<List<AssemblyStructure>> GetAssemblyStructureAsync(string assemblyPartNumber, int version);
        Task CopyFilesToStorageAsync(IAGR_BaseComponent rootComponent, int rootHashSum);
        Task CopyFilesToProdAsync(IAGR_BaseComponent rootComponent, int rootHashSum);
        
    }
}