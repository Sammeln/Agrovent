using Xarial.XCad.Data;

namespace Agrovent.Infrastructure.Interfaces.Properties
{
    public interface IAGR_BasePropertiesCollection : IAGR_PropertiesCollection
    {
        abstract IXProperty Volume { get; set; }
        abstract IXProperty Mass { get; set; }
        abstract IXProperty SurfaceArea { get; set; }
    }
}
