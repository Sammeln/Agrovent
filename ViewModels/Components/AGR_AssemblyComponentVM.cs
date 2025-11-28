using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        private IGrouping<string, IXComponent> _SelectedItem;
        public IGrouping<string, IXComponent> SelectedItem
        {
            get => _SelectedItem;
            set
            {
                mDocument.Selections.Clear();

                Set(ref _SelectedItem, value);
                foreach (var item in value)
                {
                    item.Select(true);
                }
            }
        }
        #endregion

        #region Property - 
        private ObservableCollection<IGrouping<string, IXComponent>> _Components;
        public ObservableCollection<IGrouping<string, IXComponent>> Components
        {
            get => _Components;
            set => Set(ref _Components, value);
        }
        #endregion

        #region Property - 
        private ObservableCollection<IAGR_BaseComponent> _AGR_Components;
        public ObservableCollection<IAGR_BaseComponent> AGR_Components
        {
            get => _AGR_Components;
            set => Set(ref _AGR_Components, value);
        }
        #endregion 

        public AGR_AssemblyComponentVM(ISwDocument3D swDocument3D) : base(swDocument3D)
        {
            var assem = swDocument3D as ISwAssembly;
            var comps = assem.Configurations.Active.Components.GroupBy(x => x.ReferencedDocument.Title + x.ReferencedConfiguration.Name);
            Components = new ObservableCollection<IGrouping<string, IXComponent>>(comps);

            var agrComps = assem.Configurations.Active.Components.AGR_BaseComponents();

            AGR_Components = new ObservableCollection<IAGR_BaseComponent>(agrComps);
        }
    }
}
