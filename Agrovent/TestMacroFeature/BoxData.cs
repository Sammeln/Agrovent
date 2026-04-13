using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Xarial.XCad.SolidWorks.UI.PropertyPage;

namespace Agrovent.TestMacroFeature
{
    [ComVisible(true)]
    [ProgId("AgroventAddin")]
    [Guid("8864d08d-f77a-47b9-858f-4af5eea4fd77")]
    public class BoxData : SwPropertyManagerPageHandler
    {
        public double Width { get; set; }
        public double Length { get; set; }
        public double Height { get; set; }
    }
}
