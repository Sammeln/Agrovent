using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agrovent.Services;
using Agrovent.ViewModels.Base;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Xarial.XCad.Documents;
using Xarial.XCad.SolidWorks.Documents;
using Xarial.XCad.Documents.Enums;
using System.ComponentModel;
using System.Windows.Data;
using Xarial.XCad.Utils.Reflection;

namespace Agrovent.ViewModels
{
    public class TaskPaneViewModel : BaseViewModel
    {


        #region Property - ISwAssembly ActiveComponent
        private ISwAssembly _ActiveComponent;
        public ISwAssembly ActiveComponent
        {
            get => _ActiveComponent;
            set => Set(ref _ActiveComponent, value);
        }
        #endregion

        #region CTOR
        public TaskPaneViewModel()
        {
            var app = AGR_ServiceContainer.GetService<AgroventAddin>();
            app.Application.Documents.DocumentActivated += Documents_DocumentActivated;
            
        } 
        #endregion

        private void Documents_DocumentActivated(IXDocument doc)
        {
            if (ActiveComponent != null)
            {
                //(ActiveComponent.Assembly as AssemblyDoc).ComponentVisibleChangeNotify -= TaskPaneVM_ComponentVisibleChangeNotify;
            }
            if (doc is ISwAssembly)
            {
                ActiveComponent = doc as ISwAssembly;
            }
        }
    }
}
