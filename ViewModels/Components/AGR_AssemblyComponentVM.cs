using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Extensions;
using Agrovent.Infrastructure.Interfaces.Components;
using Agrovent.ViewModels.Base;
using Xarial.XCad.Documents;
using Xarial.XCad.SolidWorks.Documents;

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
        private ObservableCollection<IAGR_BaseComponent> _AGR_TopComponents;
        public ObservableCollection<IAGR_BaseComponent> AGR_TopComponents
        {
            get => _AGR_TopComponents;
            set => Set(ref _AGR_TopComponents, value);
        }
        #endregion

        #region Property - ObservableCollection<IAGR_BaseComponent> _AGR_FlatComponents
        private ObservableCollection<IAGR_BaseComponent> _AGR_FlatComponents;
        public ObservableCollection<IAGR_BaseComponent> AGR_FlatComponents
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
            var comps = assem.Configurations.Active.Components
                .AGR_ActiveComponents()
                .GroupBy(x => x.ReferencedDocument.Title + x.ReferencedConfiguration.Name);

             AGR_TopComponents = new(assem.Configurations.Active.Components
                .AGR_ActiveComponents().AGR_BaseComponents());

            AGR_FlatComponents = new(assem.Configurations.Active.Components
                .AGR_TryFlatten().AGR_BaseComponents());
        }
    }
}
