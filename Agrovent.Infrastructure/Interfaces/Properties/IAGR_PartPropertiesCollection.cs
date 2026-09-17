using Xarial.XCad.Data;

namespace AgroventInfrastructure.Interfaces.Properties
{
    public interface IAGR_PartPropertiesCollection : IAGR_PropertiesCollection
    {
        abstract IXProperty Length { get; set; }
        abstract IXProperty Width { get; set; }
    }
}
