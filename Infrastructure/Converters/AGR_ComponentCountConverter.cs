// AGR_ComponentCountConverter.cs
using Agrovent.Infrastructure.Enums;
using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace Agrovent.Infrastructure.Converters
{
    public class AGR_ComponentCountConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count && parameter is string typeParam)
            {
                if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
                    return "0";

                // В реальном приложении здесь будет логика фильтрации
                // Так как мы не можем получить доступ к коллекции из конвертера,
                // лучше сделать это в ViewModel
                return "0";
            }

            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}