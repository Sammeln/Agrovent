using System;
using System.Collections.Generic;
using System.Text;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using System.Threading.Tasks;
using Agrovent.ViewModels.Components;
using Agrovent.DAL.Entities.Components;

namespace Agrovent.Infrastructure.Interfaces
{
    public interface IAGR_ComponentVersionService
    {
        Task<bool> CheckAndSaveComponentAsync(IAGR_BaseComponent component);
        Task<bool> CheckAndSaveAssemblyAsync(AGR_AssemblyComponentVM assembly);
        Task<ComponentVersion?> GetComponentVersionAsync(string partNumber, int version);
        Task<bool> HasComponentChangedAsync(IAGR_BaseComponent component);

        // Дополнительные методы (опционально)
        Task<ComponentVersion?> GetLatestComponentVersionAsync(string partNumber);
        Task<List<AssemblyStructure>> GetAssemblyStructureAsync(string assemblyPartNumber, int version);
    }
}