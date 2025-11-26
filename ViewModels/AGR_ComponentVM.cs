using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agrovent.ViewModels.Base;
using Xarial.XCad;
using Xarial.XCad.Annotations;
using Xarial.XCad.Data;
using Xarial.XCad.Documents;
using Xarial.XCad.Documents.Enums;
using Xarial.XCad.Features;
using Xarial.XCad.Geometry;
using Xarial.XCad.Geometry.Structures;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels
{
    public class AGR_ComponentVM : BaseViewModel
    {
        private IXComponent mComponent;

        #region CTOR
        public AGR_ComponentVM(IXComponent xComponent)
        {
            mComponent = xComponent;
        } 
        #endregion
    }

    public static class XComponentExtension
    {
        public static AGR_ComponentVM AGR_Component(this IXComponent xComponent)
        {
            return new AGR_ComponentVM(xComponent);
        }
    }
}

