using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using Agrovent.Infrastructure.AGR_Converters;

namespace Agrovent.Infrastructure.AGR_Converters

{
    public class AGR_GroupHeaderConverter : IMultiValueConverter
    {
        private readonly AGR_ComponentTypeConverter _typeConverter = new();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var groupName = values[0];
            var groupingMode = values[1];
                // Применяем конвертер ТОЛЬКО при группировке по типу
                if (groupingMode == "По типу")
                    return _typeConverter.Convert(groupName, targetType, parameter, culture);

                return groupName; // Для группировки по материалу возвращаем имя "как есть"
            //return groupName;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
