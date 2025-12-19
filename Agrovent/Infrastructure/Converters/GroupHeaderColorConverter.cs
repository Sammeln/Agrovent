using System;
using System.Globalization;
using System.Windows.Data;
using Agrovent.Infrastructure.Enums;

namespace Agrovent.Infrastructure.Converters
{
    public class GroupHeaderColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AGR_ComponentType_e componentType)
            {
                return componentType switch
                {
                    AGR_ComponentType_e.Assembly => "#E8F5E9",      // Светло-зеленый
                    AGR_ComponentType_e.SheetMetallPart => "#FFF3E0", // Светло-оранжевый
                    AGR_ComponentType_e.Part => "#F3E5F5",         // Светло-фиолетовый
                    AGR_ComponentType_e.Purchased => "#E3F2FD",    // Светло-голубой
                    _ => "#F5F5F5"
                };
            }
            return "#F5F5F5";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}