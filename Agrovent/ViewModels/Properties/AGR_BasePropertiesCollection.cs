using System.Collections.ObjectModel;
using Agrovent.DAL.Infrastructure.Enums;
using Agrovent.Infrastructure.Extensions;
using Xarial.XCad.Data;
using Xarial.XCad.SolidWorks.Data;
using Xarial.XCad.SolidWorks.Documents;
using Agrovent.DAL.Infrastructure.Interfaces.Properties;

namespace Agrovent.ViewModels.Properties
{
    internal class AGR_BasePropertiesCollection : IAGR_BasePropertiesCollection
    {
        private ISwDocument3D mDocument;
        private ISwConfiguration mConfiguration;
        internal ISwCustomPropertiesCollection mProperties;

        public IXProperty Volume { get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankVolume); set => throw new NotImplementedException(); }
        public IXProperty Mass { get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankMass); set => throw new NotImplementedException(); }
        public IXProperty SurfaceArea { get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankArea); set => throw new NotImplementedException(); }
        public ICollection<IXProperty> Properties { get; set; }

        internal void InitProperties()
        {
            Properties = new ObservableCollection<IXProperty> { Volume, Mass, SurfaceArea };
        }
        public AGR_BasePropertiesCollection(ISwDocument3D document3D)
        {
            mDocument = document3D;
            mConfiguration = mDocument.Configurations.Active;
            mProperties = mConfiguration.Properties;

            InitProperties();
        }
    }
}
