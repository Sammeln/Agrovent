using System.Collections.ObjectModel;
using Agrovent.DAL.Infrastructure.Enums;
using Agrovent.Infrastructure.Extensions;
using Agrovent.ViewModels.Base;
using Xarial.XCad.SolidWorks.Documents;
using Agrovent.DAL.Infrastructure.Enums;
using Agrovent.DAL.Infrastructure.Interfaces.Components;
using Agrovent.ViewModels.Specification;

namespace Agrovent.ViewModels.Components
{
    public class AGR_AssemblyComponentVM : AGR_FileComponent
    {


        #region Property - 
        private IAGR_BaseComponent _SelectedItem;
        public IAGR_BaseComponent SelectedItem
        {
            get => _SelectedItem;
            set => Set(ref _SelectedItem, value);
        }
        #endregion

        #region Property - ObservableCollection<IAGR_BaseComponent> _AGR_TopComponents
        private ObservableCollection<SpecificationItemVM> _AGR_TopComponents;
        public ObservableCollection<SpecificationItemVM> AGR_TopComponents
        {
            get => _AGR_TopComponents;
            set => Set(ref _AGR_TopComponents, value);
        }
        #endregion

        #region Property - ObservableCollection<IAGR_BaseComponent> _AGR_FlatComponents
        private ObservableCollection<SpecificationItemVM> _AGR_FlatComponents;
        public ObservableCollection<SpecificationItemVM> AGR_FlatComponents
        {
            get => _AGR_FlatComponents;
            set => Set(ref _AGR_FlatComponents, value);
        }
        #endregion 
        public int TotalComponentsCount => AGR_TopComponents?.Count ?? 0;
        public int PartsCount => AGR_TopComponents?.Count(c => c.ComponentType == AGR_ComponentType_e.Part) ?? 0;
        public int AssembliesCount => AGR_TopComponents?.Count(c => c.ComponentType == AGR_ComponentType_e.Assembly) ?? 0;
        public int PurchasedCount => AGR_TopComponents?.Count(c => c.ComponentType == AGR_ComponentType_e.Purchased) ?? 0;
        public int SheetMetalPartsCount => AGR_TopComponents?.Count(c => c.ComponentType == AGR_ComponentType_e.SheetMetallPart) ?? 0;
        
        public AGR_AssemblyComponentVM(ISwDocument3D swDocument3D) : base(swDocument3D)
        {
            var assem = swDocument3D as ISwAssembly;

            // Получаем компоненты верхнего уровня
            var topComponents = assem.Configurations.Active.Components.AGR_ActiveComponents().AGR_BaseComponents();
            // Группируем и создаем SpecificationItemVM для верхнего уровня
            var groupedTop = topComponents
                .GroupBy(c => new { c.Name, c.ConfigName })
                .Select(g => new SpecificationItemVM(g.First(), g.Count()));
            AGR_TopComponents = new ObservableCollection<SpecificationItemVM>(groupedTop);

            // Получаем все компоненты (плоский список)
            var flatComponents = assem.Configurations.Active.Components.AGR_TryFlatten().AGR_BaseComponents();
            // Группируем и создаем SpecificationItemVM для плоского списка
            var groupedFlat = flatComponents
                .GroupBy(c => new { c.Name, c.ConfigName })
                .Select(g => new SpecificationItemVM(g.First(), g.Count()));
            AGR_FlatComponents = new ObservableCollection<SpecificationItemVM>(groupedFlat);
        }
    }
}
