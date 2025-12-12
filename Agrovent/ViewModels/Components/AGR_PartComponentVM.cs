using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.Infrastructure.Interfaces.Components;
using Agrovent.Infrastructure.Interfaces.Properties;
using Agrovent.Services;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Properties;
using Xarial.XCad.Data;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels.Components
{
    public class AGR_PartComponentVM : AGR_FileComponent, IAGR_HasMaterial, IAGR_HasPaint
    {
        public IAGR_Material BaseMaterial { get; set; }
        public decimal BaseMaterialCount { get; set; }
        public IAGR_Material? Paint { get; set; }
        public decimal? PaintCount { get; set; }
        //public IAGR_BasePropertiesCollection PropertiesCollection { get; set; }

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

            //switch (ComponentType)
            //{
            //    case AGR_ComponentType_e.Assembly:
            //        PropertiesCollection = new AGR_BasePropertiesCollection(mDocument);
            //        break;
            //    case AGR_ComponentType_e.Part:
            //        PropertiesCollection = new AGR_PartPropertiesCollection(mDocument);
            //        break;
            //    case AGR_ComponentType_e.SheetMetallPart:
            //        PropertiesCollection = new AGR_SheetPartPropertiesCollection(mDocument);
            //        break;
            //    case AGR_ComponentType_e.Purchased:
            //        PropertiesCollection?.Properties.Clear();
            //        break;
            //    case AGR_ComponentType_e.NA:
            //        PropertiesCollection = new AGR_BasePropertiesCollection(mDocument);
            //        break;
            //    default:
            //        break;
            //}

            //ComponentTypeChanged += AGR_PartComponentVM_ComponentTypeChanged;

        }

        private void AGR_PartComponentVM_ComponentTypeChanged(AGR_ComponentType_e type)
        {
            switch (type)
            {
                case AGR_ComponentType_e.Assembly:
                    PropertiesCollection = new AGR_BasePropertiesCollection(mDocument);
                    break;
                case AGR_ComponentType_e.Part:
                    PropertiesCollection = new AGR_PartPropertiesCollection(mDocument);
                    break;
                case AGR_ComponentType_e.SheetMetallPart:
                    PropertiesCollection = new AGR_SheetPartPropertiesCollection(mDocument);
                    break;
                case AGR_ComponentType_e.Purchased:
                    PropertiesCollection?.Properties.Clear();
                    break;
                case AGR_ComponentType_e.NA:
                    PropertiesCollection = new AGR_BasePropertiesCollection(mDocument);
                    break;
                default:
                    break;
            }

        }
    }
}
