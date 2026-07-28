using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AgroventInfrastructure.AGR_Converters
{
    /// <summary>Преобразует числовой отступ (AGR_PackNGoNodeVM.Indent) в Thickness с левым отступом -
    /// используется для визуального отображения вложенности строк дерева в DataGrid.</summary>
    public class AGR_IndentToMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var indent = value is double d ? d : 0;
            return new Thickness(indent, 0, 0, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>Отображает символ раскрытия/сворачивания узла дерева ("▼"/"▶") по значению IsExpanded.</summary>
    public class AGR_ExpandGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool isExpanded && isExpanded ? "\u25BC" : "\u25B6";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
