using Xarial.XCad.Data;

namespace Agrovent.DAL.Infrastructure.Interfaces.Properties
{
    public interface AGR_ISheetMetallPropertiesCollection : AGR_IPropertiesCollection
    {
        abstract IXProperty SheetMetall_Length { get; set; }
        abstract IXProperty SheetMetall_Width { get; set; }
        abstract IXProperty SheetMetall_Thickness { get; set; }
        //abstract IXProperty SheetMetall_Holes { get; set; }
        abstract IXProperty SheetMetall_SurfaceArea { get; set; }
        //abstract IXProperty SheetMetall_PlateArea { get; set; }
        abstract IXProperty SheetMetall_OuterContour { get; set; }
        //abstract IXProperty SheetMetall_InnerContour { get; set; }
        abstract IXProperty SheetMetall_Bends { get; set; }
    }
}
