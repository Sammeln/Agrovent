// File: Infrastructure/Interfaces/IAGR_CommandService.cs
using System.Threading.Tasks;
using AgroventInfrastructure.Interfaces.Components.Base;
using Xarial.XCad.SolidWorks.Documents;

namespace AgroventInfrastructure.Interfaces
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