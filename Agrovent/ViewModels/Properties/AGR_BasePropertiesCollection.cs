using System.Collections.ObjectModel;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Extensions;
using Agrovent.Infrastructure.Interfaces.Properties;
using Xarial.XCad.Data;
using Xarial.XCad.SolidWorks.Data;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels.Properties
{
    internal class AGR_BasePropertiesCollection : IAGR_BasePropertiesCollection
    {
        private ISwDocument3D mDocument;
        private ISwConfiguration mConfiguration;
        internal ISwCustomPropertiesCollection mProperties;

        public IXProperty Volume 
        {
            get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankVolume); 
            set => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankVolume).Value = value; 
        }
        public IXProperty Mass 
        {
            get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankMass);
            set => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankMass).Value = value; 
        }
        public IXProperty SurfaceArea 
        {
            get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankArea);
            set => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankArea).Value = value;
        }
        public ICollection<IXProperty> Properties { get; set; }

        internal void InitProperties()
        {
            Properties = new ObservableCollection<IXProperty>();
            if (!string.IsNullOrEmpty(Volume.Value.ToString())) Properties.Add(Volume);
            if (!string.IsNullOrEmpty(Mass.Value.ToString())) Properties.Add(Mass);
            if (!string.IsNullOrEmpty(SurfaceArea.Value.ToString())) Properties.Add(SurfaceArea);
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
