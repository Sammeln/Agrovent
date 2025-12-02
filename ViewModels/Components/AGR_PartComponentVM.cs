using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.ViewModels.Base;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels.Components
{
    public class AGR_PartComponentVM : AGR_FileComponent, IHasMaterial, IHasPaint
    {
        public IAGR_Material BaseMaterial { get; set; }
        public decimal BaseMaterialCount { get; set; }
        public IAGR_Material? Paint { get; set; }
        public decimal? PaintCount { get; set; }
        public AGR_PartComponentVM(ISwDocument3D swDocument3D) : base(swDocument3D)
        {
            if (ComponentType == AGR_ComponentType_e.Purchased) return;
         
            BaseMaterial = new AGR_Material(swDocument3D);
            Paint = new AGR_Paint(swDocument3D);
        }
    }
}
