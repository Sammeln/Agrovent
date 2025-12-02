using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Extensions;
using Agrovent.Infrastructure.Interfaces.Properties;
using Xarial.XCad.Data;
using Xarial.XCad.SolidWorks.Data;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels.Properties
{
    internal class AGR_BasePropertiesCollection : AGR_IBasePropertiesCollection
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
