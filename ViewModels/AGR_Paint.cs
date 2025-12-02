using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agrovent.DAL.Entities;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces;
using Xarial.XCad.Data;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels
{
    internal class AGR_Paint : IAGR_Material
    {
        public string Name { get; set; }
        public string Article { get; set; }
        public string UOM { get; set; }
        public AvaArticleModel AvaModel { get; set; }

        public AGR_Paint(ISwDocument3D doc3D)
        {
            var colorProp = doc3D.Configurations.Active.Properties.GetOrPreCreate(AGR_PropertyNames.Color);
            if (!colorProp.IsCommitted) colorProp.Commit(CancellationToken.None);
            Name = colorProp.Value.ToString();

        }
    }
}
