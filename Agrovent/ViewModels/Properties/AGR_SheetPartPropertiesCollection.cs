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
            set
            {
                mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankLen).Value = value;
                OnPropertyChanged(nameof(SheetMetall_Length));
            }
        }
        public IXProperty SheetMetall_Width
        {
            get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankWid);
            set
            {
                mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankWid).Value = value;
                OnPropertyChanged(nameof(SheetMetall_Width));
            }
        }
        public IXProperty SheetMetall_Thickness
        {
            get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankThick);
            set
            {
                mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankThick).Value = value;
                OnPropertyChanged(nameof(SheetMetall_Thickness));
            }
        }
        public IXProperty SheetMetall_SurfaceArea
        {
            get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankArea);
            set
            {
                mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankArea).Value = value;
                OnPropertyChanged(nameof(SheetMetall_SurfaceArea));
            }
        }
        public IXProperty SheetMetall_Bends
        {
            get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankBends);
            set
            {
                mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankBends).Value = value;
                OnPropertyChanged(nameof(SheetMetall_Bends));
            }
        }
        public IXProperty SheetMetall_Holes
        {
            get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankHoles);
            set
            {
                mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankHoles).Value = value;
                OnPropertyChanged(nameof(SheetMetall_Holes));
            }
        }
        public IXProperty SheetMetall_PlateArea
        {
            get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankPlateArea);
            set
            {
                mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankPlateArea).Value = value;
                OnPropertyChanged(nameof(SheetMetall_PlateArea));
            }
        }
        public IXProperty SheetMetall_OuterContour
        {
            get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankOuterContour);
            set
            {
                mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankOuterContour).Value = value;
                OnPropertyChanged(nameof(SheetMetall_OuterContour));
            }
        }
        public IXProperty SheetMetall_InnerContour
        {
            get => mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankInnerContour);
            set
            {
                mProperties.AGR_TryGetProp(AGR_PropertyNames.BlankInnerContour).Value = value;
                OnPropertyChanged(nameof(SheetMetall_InnerContour));
            }
        }

        new public void UpdateProperties()
        {
            base.UpdateProperties();

            IXProperty property = default;
            var cutlist = (mConfiguration as IXPartConfiguration).CutLists.First();
            if (cutlist != null)
            {
                try
                {
                    //Вычисление длины развертки
                    property = cutlist.Properties.AGR_TryGetProp(AGR_SheetMetallPropNames.SM_BlankLen);
                    if (property != null && !string.IsNullOrEmpty(property.Value.ToString()))
                    {
                        SheetMetall_Length.Value = property.Value;
                    }

                    //Вычисление ширины развертки
                    property = cutlist.Properties.AGR_TryGetProp(AGR_SheetMetallPropNames.SM_BlankWid);
                    if (property != null && !string.IsNullOrEmpty(property.Value.ToString()))
                    {
                        SheetMetall_Width.Value = property.Value;
                    }

                    //Вычисление толщины развертки
                    property = cutlist.Properties.AGR_TryGetProp(AGR_SheetMetallPropNames.SM_BlankThick);
                    if (property != null && !string.IsNullOrEmpty(property.Value.ToString()))
                    {
                        SheetMetall_Thickness.Value = property.Value;
                    }

                    //Вычисление площади развертки
                    property = cutlist.Properties.AGR_TryGetProp(AGR_SheetMetallPropNames.SM_BlankArea);
                    if (property != null && !string.IsNullOrEmpty(property.Value.ToString()))
                    {
                        double area = double.Parse(property.Value.ToString()
                                            .Replace('.', ',')
                                            );
                        //Площадь в кв.мм переводим в кв.м
                        area = area / 1000000;

                        SheetMetall_SurfaceArea.Value = Math.Round(area, 3, MidpointRounding.ToPositiveInfinity);
                    }

                    //Вычисление количества сгибов
                    property = cutlist.Properties.AGR_TryGetProp(AGR_SheetMetallPropNames.SM_BlankBends);
                    if (property != null && !string.IsNullOrEmpty(property.Value.ToString()))
                    {
                        SheetMetall_Bends.Value = property.Value;
                    }

                    //Вычисление количества отверстий
                    property = cutlist.Properties.AGR_TryGetProp(AGR_SheetMetallPropNames.SM_BlankHoles);
                    if (property != null && !string.IsNullOrEmpty(property.Value.ToString()))
                    {
                        SheetMetall_Holes.Value = property.Value;
                    }

                    //Вычисление площади пластины
                    property = cutlist.Properties.AGR_TryGetProp(AGR_SheetMetallPropNames.SM_BlankPlateArea);
                    if (property != null && !string.IsNullOrEmpty(property.Value.ToString()))
                    {
                        double area = double.Parse(property.Value.ToString()
                                            .Replace('.', ',')
                                            );
                        //Площадь в кв.мм переводим в кв.м
                        area = area / 1000000;

                        SheetMetall_PlateArea.Value = Math.Round(area, 3, MidpointRounding.ToPositiveInfinity);
                    }
                    //Вычисление внешнего контура
                    property = cutlist.Properties.AGR_TryGetProp(AGR_SheetMetallPropNames.SM_BlankOuterContour);
                    if (property != null && !string.IsNullOrEmpty(property.Value.ToString()))
                    {
                        double contVal = double.Parse(property.Value.ToString()
                                            .Replace('.', ',')
                                            );

                        SheetMetall_OuterContour.Value = Math.Round(contVal, 3, MidpointRounding.ToPositiveInfinity);
                    }
                    //Вычисление внутреннего контура
                    property = cutlist.Properties.AGR_TryGetProp(AGR_SheetMetallPropNames.SM_BlankInnerContour);
                    if (property != null && !string.IsNullOrEmpty(property.Value.ToString()))
                    {
                        double contVal = double.Parse(property.Value.ToString()
                                               .Replace('.', ',')
                                               );

                        SheetMetall_InnerContour.Value = Math.Round(contVal, 3, MidpointRounding.ToPositiveInfinity);

                    }

                }
                catch (Exception)
                {

                    throw;
                }
            }
        }

        public AGR_SheetPartPropertiesCollection(ISwDocument3D document3D) : base(document3D)
        {
            InitProperties();
            if (!string.IsNullOrEmpty(SheetMetall_Length.Value.ToString())) Properties.Add(SheetMetall_Length);
            if (!string.IsNullOrEmpty(SheetMetall_Width.Value.ToString())) Properties.Add(SheetMetall_Width);
            if (!string.IsNullOrEmpty(SheetMetall_Thickness.Value.ToString())) Properties.Add(SheetMetall_Thickness);
            if (!string.IsNullOrEmpty(SheetMetall_SurfaceArea.Value.ToString())) Properties.Add(SheetMetall_SurfaceArea);
            if (!string.IsNullOrEmpty(SheetMetall_Bends.Value.ToString())) Properties.Add(SheetMetall_Bends);
            if (!string.IsNullOrEmpty(SheetMetall_Holes.Value.ToString())) Properties.Add(SheetMetall_Holes);
            if (!string.IsNullOrEmpty(SheetMetall_PlateArea.Value.ToString())) Properties.Add(SheetMetall_PlateArea);
            if (!string.IsNullOrEmpty(SheetMetall_OuterContour.Value.ToString())) Properties.Add(SheetMetall_OuterContour);
            if (!string.IsNullOrEmpty(SheetMetall_InnerContour.Value.ToString())) Properties.Add(SheetMetall_InnerContour);
        }
    }
}
