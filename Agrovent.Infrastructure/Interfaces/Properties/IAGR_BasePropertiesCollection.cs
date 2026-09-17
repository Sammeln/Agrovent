using Xarial.XCad.Data;

namespace AgroventInfrastructure.Interfaces.Properties
{
    public interface IAGR_BasePropertiesCollection : IAGR_PropertiesCollection
    {
        abstract IXProperty Volume { get; set; }
        abstract IXProperty Mass { get; set; }
        abstract IXProperty SurfaceArea { get; set; }
    }
}
