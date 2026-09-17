using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;
using AgroventInfrastructure.Enums;

namespace AgroventInfrastructure.AGR_Converters
{
    public class AGR_ComponentTypeToVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var enumVal = (AGR_AvaType_e)value;
            if (enumVal != null)
            {
                return enumVal != AGR_AvaType_e.Purchased;
            }
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
