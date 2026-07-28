// File: Infrastructure/Interfaces/IAGR_CommandService.cs
using System.Threading.Tasks;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.Infrastructure.Interfaces
{
    public interface IAGR_CommandService
    {
        Task<bool> UpdatePropertiesAsync();
        Task<bool> OpenComponentRegistryAsync();
        Task<bool> OpenProjectExplorerWindowAsync();
        Task<bool> SaveActiveComponentAsync();
        Task<bool> CopyFilesToStorageAsync();
        Task<bool> CopyFilesToProdAsync();
        Task<bool> UpdateDrawingsAsync();
        Task<bool> GetSheetMetallPartsAssmbly();
        Task<bool> PackNGoAsync();
    }
}