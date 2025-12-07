using System.Collections.ObjectModel;
using Agrovent.DAL.Infrastructure.Interfaces;
using Xarial.XCad.Data;

namespace Agrovent.DAL.Infrastructure.Interfaces.Components;
public interface IAGR_Part
{
    abstract IAGR_Material BaseMaterial { get; set; }
    abstract decimal BaseMaterialCount { get; set; }
    abstract IAGR_Material? Paint { get; set; }
    abstract decimal? PaintCount { get; set; }
    abstract ICollection<IXProperty> BlankPropertiesCollection { get; set; }
}
