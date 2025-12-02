using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.Infrastructure.Interfaces.Components;
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
        private ObservableCollection<SpecificationItemVM> _Components;
        public ObservableCollection<SpecificationItemVM> Components
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
                        .Where(x => x is AGR_PartComponentVM)
                        .GroupBy(x => (x as AGR_PartComponentVM).BaseMaterial.Name)
                        .Select(x => new Tuple<IAGR_Material, decimal>
                            ((x.First() as AGR_PartComponentVM).BaseMaterial,
                            x.Sum(d => (d as AGR_PartComponentVM).BaseMaterialCount)
                            ))
                        );
            //set => Set(ref _Materials, value);
        }
        #endregion 
        public AGR_SpecificationViewModel(AGR_AssemblyComponentVM baseComponent)
        {
            _baseComponent = baseComponent;
            Components = new ObservableCollection<SpecificationItemVM>(
            _baseComponent.AGR_FlatComponents
               //.Where(x => x.ComponentType == AGR_ComponentType_e.Part)
               .GroupBy(x => x.Name + x.ConfigName)
               .Select(x => new SpecificationItemVM(x.First(), x.Count()))
            );
            Components.Add(new SpecificationItemVM(baseComponent,1));

            _componentsCVS.Source = Components;
        }
    }
}
