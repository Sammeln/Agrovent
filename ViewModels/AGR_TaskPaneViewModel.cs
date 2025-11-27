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
using Agrovent.ViewModels.Components;
using Agrovent.Infrastructure.Interfaces.Components;
using Xarial.XCad.Geometry;

namespace Agrovent.ViewModels
{
    public class AGR_TaskPaneViewModel : BaseViewModel
    {


        #region Property - ISwAssembly ActiveComponent
        private ISwDocument3D _ActiveComponent;
        public ISwDocument3D ActiveComponent
        {
            get => _ActiveComponent;
            set => Set(ref _ActiveComponent, value);
        }
        #endregion


        #region Property - 
        private IAGR_BaseComponent _BaseComponent;
        public IAGR_BaseComponent BaseComponent
        {
            get => _BaseComponent;
            set => Set(ref _BaseComponent, value);
        }
        #endregion 

        #region CTOR
        public AGR_TaskPaneViewModel()
        {
            var app = AGR_ServiceContainer.GetService<AgroventAddin>();
            app.Application.Documents.DocumentActivated += Documents_DocumentActivated;
        }


        #endregion

        private void Documents_DocumentActivated(IXDocument doc)
        {


            if (ActiveComponent != null)
            {
                ActiveComponent.Selections.NewSelection -= Selections_NewSelection;
                ActiveComponent.Selections.ClearSelection -= Selections_ClearSelection;
                //(ActiveComponent.Assembly as AssemblyDoc).ComponentVisibleChangeNotify -= TaskPaneVM_ComponentVisibleChangeNotify;
            }

            doc.Selections.NewSelection += Selections_NewSelection;
            doc.Selections.ClearSelection += Selections_ClearSelection;
            if (doc is ISwAssembly assembly)
            {
                ActiveComponent = assembly;
                BaseComponent = new AGR_AssemblyComponentVM(assembly);
                return;
            }
            if (doc is ISwPart part)
            {
                ActiveComponent = part;
                BaseComponent = new AGR_PartComponentVM(part);
                return;
            }
        }
        private void Selections_NewSelection(IXDocument doc, Xarial.XCad.IXSelObject selObject)
        {
            if (selObject is IXFace face)
            {
                if (doc is ISwAssembly assembly)
                {
                    if (face.Component.ReferencedDocument is ISwPart part)
                    {
                        BaseComponent = new AGR_PartComponentVM(part);
                        return;
                    }
                    BaseComponent = new AGR_AssemblyComponentVM(face.Component.ReferencedDocument as ISwDocument3D);
                }
                else if (doc is ISwPart part)
                {
                    BaseComponent = new AGR_PartComponentVM(part);
                }
            }
        }
        private void Selections_ClearSelection(IXDocument doc)
        {
            if (doc is ISwAssembly assembly)
            {
                ActiveComponent = assembly;
                BaseComponent = new AGR_AssemblyComponentVM(assembly);
            }
            if (doc is ISwPart part)
            {
                ActiveComponent = part;
                BaseComponent = new AGR_PartComponentVM(part);
            }
        }
    }
}
