using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xarial.XCad.SolidWorks.Documents;
using Xarial.XCad.SolidWorks.Features.CustomFeature;
using Xarial.XCad.SolidWorks.Geometry;
using Xarial.XCad.SolidWorks;
using Xarial.XCad.Geometry;
using Xarial.XCad.Geometry.Structures;
using Xarial.XCad.Features.CustomFeature.Structures;

namespace Agrovent.TestMacroFeature
{
    public class BoxMacroFeature : SwMacroFeatureDefinition<BoxData, BoxData>
    {
        public override ISwBody[] CreateGeometry(ISwApplication app, ISwDocument model, ISwMacroFeature<BoxData> feat)
        {
            var data = feat.Parameters;

            var body = (ISwBody)app.MemoryGeometryBuilder.CreateSolidBox(new Point(0, 0, 0),
                new Vector(1, 0, 0), new Vector(0, 1, 0),
                data.Width, data.Length, data.Height).Bodies.First();

            return new ISwBody[] { body };
        }

        public override bool OnEditDefinition(ISwApplication app, ISwDocument doc, ISwMacroFeature feature)
        {
            return base.OnEditDefinition(app, doc, feature);
        }
        public override CustomFeatureRebuildResult OnRebuild(ISwApplication app, ISwDocument doc, ISwMacroFeature feature)
        {
            return base.OnRebuild(app, doc, feature);
        }
    }
}
