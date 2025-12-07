using Agrovent.Services;
using Agrovent.ViewModels.Base;
using Xarial.XCad.Documents;
using Xarial.XCad.SolidWorks.Documents;
using Agrovent.ViewModels.Components;
using Xarial.XCad.Geometry;
using Agrovent.Infrastructure.Extensions;
using Xarial.XCad.SolidWorks;
using Agrovent.Infrastructure.Interfaces.Components.Base;

namespace Agrovent.ViewModels.TaskPane
{
    public class AGR_TaskPaneViewModel : BaseViewModel
    {
        private readonly ISwApplication _app = AGR_ServiceContainer.GetService<AgroventAddin>().Application;

        #region Property - ISwAssembly ActiveComponent
        private ISwDocument3D _ActiveComponent;
        public ISwDocument3D ActiveComponent
        {
            get => _ActiveComponent;
            set => Set(ref _ActiveComponent, value);
        }
        #endregion


        #region Property - 
        private IAGR_PageView? _BaseComponent;
        public IAGR_PageView? BaseComponent
        {
            get => _BaseComponent;
            set => Set(ref _BaseComponent, value);
        }
        #endregion 

        #region CTOR
        public AGR_TaskPaneViewModel()
        {
            BaseComponent = new AGR_HomePageVM();
            _app.Documents.DocumentActivated += Documents_DocumentActivated;
            _app.Idle += _app_Idle;

        }

        private void _app_Idle(Xarial.XCad.IXApplication app)
        {
            if (_app.Documents.Count == 0 && ActiveComponent != null)
            {
                BaseComponent = null;
            }
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

            BaseComponent = (doc as ISwDocument3D).AGR_BaseComponent();

            if (doc is ISwAssembly assembly)
            {
                ActiveComponent = assembly;
                //BaseComponent = new AGR_AssemblyComponentVM(assembly);
                return;
            }
            if (doc is ISwPart part)
            {
                ActiveComponent = part;
                //BaseComponent = new AGR_PartComponentVM(part);
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
