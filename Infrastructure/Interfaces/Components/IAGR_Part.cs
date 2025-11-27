using System.Collections.ObjectModel;
using Agrovent.Infrastructure.Interfaces;
using Xarial.XCad.Data;

public interface IAGR_Part
{
    abstract IAGR_Material BaseMaterial { get; set; }
    abstract decimal BaseMaterialCount { get; set; }
    abstract IAGR_Material? Paint { get; set; }
    abstract decimal? PaintCount { get; set; }
    abstract ICollection<IXProperty> BlankPropertiesCollection { get; set; }
}
