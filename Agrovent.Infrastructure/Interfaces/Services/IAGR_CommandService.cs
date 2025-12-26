// File: Infrastructure/Interfaces/IAGR_CommandService.cs
using System.Threading.Tasks;
using Agrovent.Infrastructure.Interfaces.Components.Base;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.Infrastructure.Interfaces
{
    public interface IAGR_CommandService
    {
        Task<bool> UpdatePropertiesAsync();
        // Добавьте сюда методы для других команд, когда они будут реализованы
        // Task<bool> SomeOtherCommandAsync(ISwDocument3D document);
    }
}