using System.Collections.ObjectModel;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Extensions;
using Agrovent.Infrastructure.Interfaces.Properties;
using Agrovent.ViewModels.Base;
using Xarial.XCad.Data;
using Xarial.XCad.SolidWorks.Data;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels.Properties
{
    internal class AGR_BasePropertiesCollection : BaseViewModel, IAGR_BasePropertiesCollection
    {
        internal ISwDocument3D mDocument;
        internal ISwConfiguration mConfiguration;
        internal ISwCustomPropertiesCollection mProperties;

        public IXProperty Volume
        {
            get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankVolume);
            set
            {
                mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankVolume).Value = value;
                OnPropertyChanged(nameof(Volume));
            }
        }
        public IXProperty Mass
        {
            get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankMass);
            set
            {
                mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankMass).Value = value;
                OnPropertyChanged(nameof(Mass));
            }
        }
        public IXProperty SurfaceArea
        {
            get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankArea);
            set
            {
                mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankArea).Value = value;
                OnPropertyChanged(nameof(SurfaceArea));
            }
        }
        public ICollection<IXProperty> Properties { get; set; }

        public void UpdateProperties()
        {
            try
            {
                    // Вычисление массы
                    var evaluation = mDocument.Evaluation.PreCreateMassProperty();
                    evaluation.Commit(CancellationToken.None);
                    var mass = evaluation.Mass;
                    Mass.Value = Math.Round(mass, 3, MidpointRounding.ToPositiveInfinity).ToString();

                    // Вычисление объёма
                    var _box = mDocument.Evaluation.PreCreateBoundingBox();
                    _box.Commit(CancellationToken.None);
                    var volume = _box.Box.Width * _box.Box.Height * _box.Box.Length;
                    Volume.Value = Math.Round(volume, 3, MidpointRounding.ToPositiveInfinity).ToString();

                    // Вычисление площади поверхности
                    var surfaceArea = evaluation.SurfaceArea;
                    SurfaceArea.Value = Math.Round(surfaceArea, 3, MidpointRounding.ToPositiveInfinity).ToString();
            }
            catch (Exception)
            {

            }
        }

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
