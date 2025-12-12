using Agrovent.Infrastructure.Enums;
using Agrovent.Infrastructure.Extensions;
using Agrovent.Infrastructure.Interfaces.Properties;
using Xarial.XCad.Data;
using Xarial.XCad.Documents;
using Xarial.XCad.SolidWorks.Documents;

namespace Agrovent.ViewModels.Properties
{
    internal class AGR_SheetPartPropertiesCollection : AGR_BasePropertiesCollection, IAGR_SheetMetallPropertiesCollection
    {
        public IXProperty SheetMetall_Length
        {
            get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankLen);
            set => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankLen).Value = value;
        }
        public IXProperty SheetMetall_Width
        {
            get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankWid);
            set => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankWid).Value = value;
        }
        public IXProperty SheetMetall_Thickness
        {
            get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankThick);
            set => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankThick).Value = value;
        }
        public IXProperty SheetMetall_SurfaceArea
        {
            get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankArea);
            set => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankArea).Value = value;
        }
        public IXProperty SheetMetall_Bends
        {
            get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankBends);
            set => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankBends).Value = value;
        }

        //public IXProperty SheetMetall_Holes { get => mProperties[AGR_PropertyNames.BlankVolume]; set => throw new NotImplementedException(); }
        //public IXProperty SheetMetall_PlateArea { get => mProperties[AGR_PropertyNames.BlankVolume]; set => throw new NotImplementedException(); }
        //public IXProperty SheetMetall_OuterContour { get => mProperties[AGR_PropertyNames.BlankVolume]; set => throw new NotImplementedException(); }
        //public IXProperty SheetMetall_InnerContour { get => mProperties[AGR_PropertyNames.BlankVolume]; set => throw new NotImplementedException(); }
        public AGR_SheetPartPropertiesCollection(ISwDocument3D document3D) : base(document3D)
        {
            InitProperties();
            if (!string.IsNullOrEmpty(SheetMetall_Length.Value.ToString())) Properties.Add(SheetMetall_Length);
            if (!string.IsNullOrEmpty(SheetMetall_Width.Value.ToString())) Properties.Add(SheetMetall_Width);
            if (!string.IsNullOrEmpty(SheetMetall_Thickness.Value.ToString())) Properties.Add(SheetMetall_Thickness);
            if (!string.IsNullOrEmpty(SheetMetall_SurfaceArea.Value.ToString())) Properties.Add(SheetMetall_SurfaceArea);
            if (!string.IsNullOrEmpty(SheetMetall_Bends.Value.ToString())) Properties.Add(SheetMetall_Bends);
        }
    }
}
