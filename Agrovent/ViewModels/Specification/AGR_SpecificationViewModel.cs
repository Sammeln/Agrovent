using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.ViewModels.Base;
using Agrovent.ViewModels.Components;

namespace Agrovent.ViewModels.Specification
{
    public class AGR_SpecificationViewModel : BaseViewModel
    {
        private AGR_AssemblyComponentVM _baseComponent;
        private CollectionViewSource _componentsCVS = new CollectionViewSource();
        public ICollectionView ComponentsView => _componentsCVS.View;

        #region Property - ObservableCollection<SpecificationItemVM> Components
        private ObservableCollection<AGR_SpecificationItemVM> _Components;
        public ObservableCollection<AGR_SpecificationItemVM> Components
        {
            get => _Components;
            set => Set(ref _Components, value);
        }
        #endregion

        #region Property - 
        private ObservableCollection<Tuple<IAGR_Material, decimal>> _Materials;
        public ObservableCollection<Tuple<IAGR_Material, decimal>> Materials
        {
            get => new(_baseComponent.AGR_FlatComponents
                        .Where(x => x.Component is AGR_PartComponentVM)
                        .GroupBy(x => (x.Component as AGR_PartComponentVM).BaseMaterial.Name)
                        .Select(x => new Tuple<IAGR_Material, decimal>
                            ((x.First().Component as AGR_PartComponentVM).BaseMaterial,
                            x.Sum(d => (d.Component as AGR_PartComponentVM).BaseMaterialCount)
                            ))
                        );
            //set => Set(ref _Materials, value);
        }
        #endregion 
        public AGR_SpecificationViewModel(AGR_AssemblyComponentVM baseComponent)
        {
            _baseComponent = baseComponent;
            Components = baseComponent.AGR_FlatComponents;
            Components.Add(new AGR_SpecificationItemVM(baseComponent,1));

            _componentsCVS.Source = Components;
        }
    }
}
