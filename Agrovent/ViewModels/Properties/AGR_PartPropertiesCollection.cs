using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Extensions;
using Agrovent.Infrastructure.Interfaces.Properties;
using Xarial.XCad.Data;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels.Properties
{
    internal class AGR_PartPropertiesCollection : AGR_BasePropertiesCollection, IAGR_PartPropertiesCollection
    {
        public IXProperty Length
        {
            get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankLen);
            set => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankLen).Value = value;
        }
        public IXProperty Width
        {
            get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankWid);
            set => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankWid).Value = value;
        }
        public AGR_PartPropertiesCollection(ISwDocument3D document3D) : base(document3D)
        {
            InitProperties();
            if (!string.IsNullOrEmpty(Length.Value.ToString())) Properties.Add(Length);
            if (!string.IsNullOrEmpty(Width.Value.ToString())) Properties.Add(Width);
        }
    }

}
