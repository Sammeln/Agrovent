using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.Infrastructure.Interfaces.Components;
using Agrovent.Infrastructure.Interfaces.Properties;
using Agrovent.Services;
using Agrovent.ViewModels.Base;
using Xarial.XCad.Data;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels.Components
{
    public class AGR_PartComponentVM : AGR_FileComponent, IAGR_Part
    {
        public IAGR_Material BaseMaterial { get; set; }
        public decimal BaseMaterialCount { get; set; }
        public IAGR_Material? Paint { get; set; }
        public decimal? PaintCount { get; set; }
        public IAGR_BasePropertiesCollection PropertiesCollection { get; set; }

        public async Task SaveToDatabaseAsync()
        {
            var versionService = AGR_ServiceContainer.GetService<IAGR_ComponentVersionService>();
            await versionService.CheckAndSaveComponentAsync(this);
        }
        public AGR_PartComponentVM(ISwDocument3D swDocument3D) : base(swDocument3D)
        {
            if (ComponentType == AGR_ComponentType_e.Purchased) return;
         
            BaseMaterial = new AGR_Material(swDocument3D);
            Paint = new AGR_Paint(swDocument3D);
        }
    }
}
