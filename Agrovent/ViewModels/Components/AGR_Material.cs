using Agrovent.DAL.Entities.Components;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels.Components
{
    public class AGR_Material : IAGR_Material
    {
        public string Name { get; set; }
        public string Article { get; set; }
        public string UOM { get; set; }
        public IAGR_AvaArticleModel AvaModel { get; set; }

        public AGR_Material(ISwDocument3D doc3D)
        {
            Name = doc3D.Configurations.Active.Properties[AGR_PropertyNames.Material].Value.ToString();
        }
    }
}
