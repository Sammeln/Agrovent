using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.Infrastructure.Interfaces.Specification;
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
        private ObservableCollection<IAGR_SpecificationItem> _Components;
        public ObservableCollection<IAGR_SpecificationItem> Components
        {
            get => _Components;
            set => Set(ref _Components, value);
        }
        #endregion
        public AGR_SpecificationViewModel(AGR_AssemblyComponentVM baseComponent)
        {
            _baseComponent = baseComponent;
            Components = new ObservableCollection<IAGR_SpecificationItem>(baseComponent.GetFlatComponents());
            Components.Add(new AGR_SpecificationItemVM(baseComponent,1));

            _componentsCVS.Source = Components;
        }
    }
}
