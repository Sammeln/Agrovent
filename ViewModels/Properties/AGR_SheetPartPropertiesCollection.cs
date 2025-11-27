using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Interfaces.Properties;
using Xarial.XCad.Data;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels.Properties
{
    internal class AGR_SheetPartPropertiesCollection : AGR_BasePropertiesCollection, AGR_ISheetMetallPropertiesCollection
    {
        public IXProperty SheetMetall_Length { get => mProperties[AGR_PropertyNames.BlankVolume]; set => throw new NotImplementedException(); }
        public IXProperty SheetMetall_Width { get => mProperties[AGR_PropertyNames.BlankVolume]; set => throw new NotImplementedException(); }
        public IXProperty SheetMetall_Thickness { get => mProperties[AGR_PropertyNames.BlankThick]; set => throw new NotImplementedException(); }
        //public IXProperty SheetMetall_Holes { get => mProperties[AGR_PropertyNames.BlankVolume]; set => throw new NotImplementedException(); }
        public IXProperty SheetMetall_SurfaceArea { get => mProperties[AGR_PropertyNames.BlankVolume]; set => throw new NotImplementedException(); }
        //public IXProperty SheetMetall_PlateArea { get => mProperties[AGR_PropertyNames.BlankVolume]; set => throw new NotImplementedException(); }
        public IXProperty SheetMetall_OuterContour { get => mProperties[AGR_PropertyNames.BlankVolume]; set => throw new NotImplementedException(); }
        //public IXProperty SheetMetall_InnerContour { get => mProperties[AGR_PropertyNames.BlankVolume]; set => throw new NotImplementedException(); }
        public IXProperty SheetMetall_Bends { get => mProperties[AGR_PropertyNames.BlankVolume]; set => throw new NotImplementedException(); }
            
        public AGR_SheetPartPropertiesCollection(ISwDocument3D document3D) : base(document3D)
        {
            InitProperties();
            Properties.Add(SheetMetall_Length);
            Properties.Add(SheetMetall_Width);
            Properties.Add(SheetMetall_Thickness);
            //Properties.Add(SheetMetall_Holes);
            Properties.Add(SheetMetall_SurfaceArea);
            //Properties.Add(SheetMetall_PlateArea);
            Properties.Add(SheetMetall_OuterContour);
            //Properties.Add(SheetMetall_InnerContour);
            Properties.Add(SheetMetall_Bends);
        }
    }
}
