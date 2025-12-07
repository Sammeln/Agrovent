using Xarial.XCad.Data;

namespace Agrovent.Infrastructure.Interfaces.Properties
{
    public interface IAGR_PartPropertiesCollection : IAGR_PropertiesCollection
    {
        abstract IXProperty Length { get; set; }
        abstract IXProperty Width { get; set; }
    }
}
