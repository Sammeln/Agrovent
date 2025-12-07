using Agrovent.DAL.Infrastructure.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace Agrovent.Infrastructure.Converters
{
    public class AGR_ComponentTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AGR_ComponentType_e componentType)
            {
                // Для purchased компонентов показываем только определенные секции
                if (componentType == AGR_ComponentType_e.Purchased)
                {
                    if (parameter.ToString().Contains("true", StringComparison.OrdinalIgnoreCase))
                    {
                        return Visibility.Visible;

                    }
                    else return Visibility.Collapsed;
                }

                // Для не-purchased показываем все
                else if (parameter.ToString().Contains("true", StringComparison.OrdinalIgnoreCase))
                {
                    return Visibility.Collapsed;
                }
            }

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
