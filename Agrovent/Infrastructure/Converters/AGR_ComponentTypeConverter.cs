using System.Globalization;
using System.Windows.Data;
using Agrovent.Infrastructure.Enums;

namespace Agrovent.Infrastructure.Converters
{
    internal class AGR_ComponentTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != "")
            {
                Enum type = (AGR_ComponentType_e)value;
                switch (type)
                {
                    case AGR_ComponentType_e.Assembly:
                        return "Сборочные единицы";
                    case AGR_ComponentType_e.Part:
                        return "Детали";
                    case AGR_ComponentType_e.Purchased:
                        return "Покупное";
                    case AGR_ComponentType_e.SheetMetallPart:
                        return "Листовые детали";
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
