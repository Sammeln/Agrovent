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

namespace Agrovent.ViewModels
{
    public class AGR_SpecificationViewModel : BaseViewModel
    {
        private AGR_AssemblyComponentVM _baseComponent;


        private CollectionViewSource _componentsCVS = new CollectionViewSource();
        public ICollectionView ComponentsView => _componentsCVS.View;

        #region Property - 
        private ObservableCollection<Tuple<IAGR_BaseComponent, int>> _Components;
        public  ObservableCollection<Tuple<IAGR_BaseComponent, int>> Components
        {
            get => _Components;
            set => Set(ref _Components, value);
        }
        #endregion

        #region Property - 
        private ObservableCollection<Tuple<IAGR_BaseComponent, int>> _Assemblies;
        public ObservableCollection<Tuple<IAGR_BaseComponent, int>> Assemblies
        {
            get => new(_baseComponent.AGR_Components
                        .Where(x => x.ComponentType == AGR_ComponentType_e.Assembly)
                        .GroupBy(x => x.Name + x.ConfigName)
                        .Select(x => new Tuple<IAGR_BaseComponent, int>(x.First(), x.Count()))
                );
            //set => Set(ref _Assemblies, value);
        }
        #endregion

        #region Property - 
        private ObservableCollection<Tuple<IAGR_BaseComponent, int>> _SheetParts;
        public ObservableCollection<Tuple<IAGR_BaseComponent, int>> SheetParts
        {
            get => new(_baseComponent.AGR_Components
               .Where(x => x.ComponentType == AGR_ComponentType_e.SheetMetallPart)
               .GroupBy(x => x.Name + x.ConfigName)
               .Select(x => new Tuple<IAGR_BaseComponent, int>(x.First(), x.Count()))
       );
            //set => Set(ref _SheetParts, value);
        }
        #endregion

        #region Property - 
        private ObservableCollection<Tuple<IAGR_BaseComponent, int>> _Parts;
        public ObservableCollection<Tuple<IAGR_BaseComponent, int>> Parts
        {
            get => new(_baseComponent.AGR_Components
               .Where(x => x.ComponentType == AGR_ComponentType_e.Part)
               .GroupBy(x => x.Name + x.ConfigName)
               .Select(x => new Tuple<IAGR_BaseComponent, int>(x.First(), x.Count()))
       );
            //set => Set(ref _Parts, value);
        }
        #endregion

        #region Property - 
        private ObservableCollection<Tuple<IAGR_BaseComponent, int>> _Purchased;
        public ObservableCollection<Tuple<IAGR_BaseComponent, int>> Purchased
        {
            get => new(_baseComponent.AGR_Components
               .Where(x => x.ComponentType == AGR_ComponentType_e.Purchased)
               .GroupBy(x => x.Name + x.ConfigName)
               .Select(x => new Tuple<IAGR_BaseComponent, int>(x.First(), x.Count()))
       );
            //set => Set(ref _Purchased, value);
        }
        #endregion
        #region Property - 
        private ObservableCollection<Tuple<IAGR_Material, decimal>> _Materials;
        public ObservableCollection<Tuple<IAGR_Material, decimal>> Materials
        {
            get => new(_baseComponent.AGR_Components
                        .Where(x => x.ComponentType == AGR_ComponentType_e.Part
                                 || x.ComponentType == AGR_ComponentType_e.SheetMetallPart)
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
            Components = new ObservableCollection<Tuple<IAGR_BaseComponent, int>>(
                    baseComponent.AGR_Components
                    .GroupBy(x => x.Name + x.ConfigName)
                    .Select(x => new Tuple<IAGR_BaseComponent, int>(x.First(), x.Count()))
                    );
        }
    }
}
