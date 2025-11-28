using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agrovent.DAL.Entities;
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
        public AvaArticleModel AvaModel { get; set; }

        public AGR_Material(ISwDocument3D doc3D)
        {
            Name = doc3D.Configurations.Active.Properties[AGR_PropertyNames.Material].Value.ToString();
        }
    }
}
