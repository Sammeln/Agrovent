using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SolidWorks.Interop.swpublished;
using Xarial.XCad.Base.Enums;
using Xarial.XCad.SolidWorks;

namespace Agrovent.Infrastructure.Helpers
{
    internal static class AGR_Helper
    {
        public static void ShowMessage(string message, MessageBoxIcon_e icon, MessageBoxButtons_e buttons)
        { 
            var _app = AGR_ServiceContainer.GetService<ISwApplication>();
            _app.ShowMessageBox(message, icon, buttons);
        }
    }
}
