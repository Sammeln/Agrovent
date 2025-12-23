using Xarial.XCad.Data;

namespace Agrovent.Infrastructure.Interfaces.Properties
{
    public interface IAGR_SheetMetallPropertiesCollection : IAGR_PropertiesCollection
    {
        abstract IXProperty SheetMetall_Length { get; set; }
        abstract IXProperty SheetMetall_Width { get; set; }
        abstract IXProperty SheetMetall_Thickness { get; set; }
        abstract IXProperty SheetMetall_Holes { get; set; }
        abstract IXProperty SheetMetall_SurfaceArea { get; set; }
        abstract IXProperty SheetMetall_PlateArea { get; set; }
        abstract IXProperty SheetMetall_OuterContour { get; set; }
        abstract IXProperty SheetMetall_InnerContour { get; set; }
        abstract IXProperty SheetMetall_Bends { get; set; }
    }
}
