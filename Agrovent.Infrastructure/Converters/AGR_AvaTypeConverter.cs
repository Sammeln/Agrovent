using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using Agrovent.Infrastructure.Enums;
using AgroventInfrastructure.Enums;

namespace AgroventInfrastructure.AGR_Converters
{
    public class AGR_AvaTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && value != "")
            {
                if (value is string)
                {
                    switch (value.ToString())
                    {
                        //case "Production":
                        //    return AGR_AvaType_e.Production;
                        //case "Component":
                        //    return AGR_AvaType_e.Component;
                        //case "Purchased":
                        //    return AGR_AvaType_e.Purchased;
                        //case "VirtualComponent":
                        //    return AGR_AvaType_e.VirtualComponent;
                        //case "DontBuy":
                        //    return AGR_AvaType_e.DontBuy;
                        //case "NA":
                        //    return AGR_AvaType_e.NA;
                        case "Production":
                        return AGR_AvaTypeNames.Production;
                        case "Component":
                        return AGR_AvaTypeNames.Component;
                        case "Purchased":
                        return AGR_AvaTypeNames.Purchased;
                        case "VirtualComponent":
                        return AGR_AvaTypeNames.VirtualComponent;
                        case "DontBuy":
                        return AGR_AvaTypeNames.DontBuy;
                        case "NA":
                        return AGR_AvaTypeNames.NA;
                    }
                }
                if ((AGR_AvaType_e)value != null)
                {
                    Enum type = (AGR_AvaType_e)value;
                    switch (type)
                    {
                        case AGR_AvaType_e.Production:
                            return AGR_AvaTypeNames.Production;
                        case AGR_AvaType_e.Component:
                            return AGR_AvaTypeNames.Component;
                        case AGR_AvaType_e.Purchased:
                            return AGR_AvaTypeNames.Purchased;
                        case AGR_AvaType_e.VirtualComponent:
                            return AGR_AvaTypeNames.VirtualComponent;
                        case AGR_AvaType_e.DontBuy:
                            return AGR_AvaTypeNames.DontBuy;
                        case AGR_AvaType_e.NA:
                            return AGR_AvaTypeNames.NA;
                    }
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not null)
            {
                return value;
            }
            return null;
        }
    }
}
