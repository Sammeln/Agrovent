using Agrovent.DAL.Infrastructure.Interfaces.Properties;
using Xarial.XCad.Data;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels.Properties
{
    internal class AGR_PartPropertiesCollection : AGR_BasePropertiesCollection, IAGR_PartPropertiesCollection
    {
        public IXProperty Length { get ; set; }
        public IXProperty Width { get; set; }

        public AGR_PartPropertiesCollection(ISwDocument3D document3D) : base(document3D)
        {
            InitProperties();
            Properties.Add(Length);
            Properties.Add(Width);
        }
    }

}
