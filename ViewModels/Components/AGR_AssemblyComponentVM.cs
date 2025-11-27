using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agrovent.ViewModels.Base;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels.Components
{
    public class AGR_AssemblyComponentVM : AGR_FileComponent
    {
        public AGR_AssemblyComponentVM(ISwDocument3D swDocument3D) : base(swDocument3D)
        {
        }
    }
}
