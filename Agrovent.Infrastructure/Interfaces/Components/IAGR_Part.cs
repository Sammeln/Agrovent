using Agrovent.Infrastructure.Interfaces.Properties;
using System.Collections.Generic;
using Xarial.XCad.Data;

namespace Agrovent.Infrastructure.Interfaces.Components
{
    public interface IAGR_Part
    {
        abstract IAGR_Material BaseMaterial { get; set; }
        abstract decimal BaseMaterialCount { get; set; }
        abstract IAGR_Material? Paint { get; set; }
        abstract decimal? PaintCount { get; set; }
        abstract IAGR_BasePropertiesCollection PropertiesCollection { get; set; }
    }
}
