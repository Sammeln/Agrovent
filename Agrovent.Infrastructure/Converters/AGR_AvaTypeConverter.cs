using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using Agrovent.Infrastructure.Enums;

namespace Agrovent.Infrastructure.AGR_Converters
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
                        case "Production":
                            return "Продукция";
                        case "Component":
                            return "Комплектующие";
                        case "Purchased":
                            return "Покупное";
                        case "VirtualComponent":
                            return "Виртуальный компонент";
                        case "DontBuy":
                            return "Не покупать";
                    }
                }
                if ((AGR_AvaType_e)value != null)
                {
                    Enum type = (AGR_AvaType_e)value;
                    switch (type)
                    {
                        case AGR_AvaType_e.Production:
                            return "Продукция";
                        case AGR_AvaType_e.Component:
                            return "Комплектующие";
                        case AGR_AvaType_e.Purchased:
                            return "Покупное";
                        case AGR_AvaType_e.VirtualComponent:
                            return "Виртуальный компонент";
                        case AGR_AvaType_e.DontBuy:
                            return "Не покупать";
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
