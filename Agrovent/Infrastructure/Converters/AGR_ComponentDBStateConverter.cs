using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Agrovent.Infrastructure.Enums;

namespace Agrovent.Infrastructure.Converters
{
    public class AGR_ComponentDBStateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var EnumVal = (AGR_ComponentDatabaseState_e)value;

            if (EnumVal != null)
            {
                if (parameter != null && !string.IsNullOrEmpty(parameter.ToString()))
                {

                    if (parameter.ToString().Equals("SavedInDataBase"))
                    {
                        switch (EnumVal)
                        {
                            case AGR_ComponentDatabaseState_e.SavedInDataBase:
                            return Visibility.Visible;
                            case AGR_ComponentDatabaseState_e.NotLoaded:
                            return Visibility.Visible;
                            case AGR_ComponentDatabaseState_e.NotSavedInDB:
                            return Visibility.Collapsed;
                        }
                    }
                    if (parameter.ToString().Equals("NotSavedInDB"))
                    {
                        switch (EnumVal)
                        {
                            case AGR_ComponentDatabaseState_e.NotSavedInDB:
                            return Visibility.Visible;
                            default:
                            return Visibility.Collapsed;
                        }
                    }
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
