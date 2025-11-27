using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using Agrovent.DAL.Infrastructure.Enums;
using Agrovent.Infrastructure.Enums;

namespace Agrovent.Infrastructure.Converters
{
    internal class AGR_AvaTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != "")
            {
                Enum type = (AvaType_e)value;
                switch (type)
                {
                    case AvaType_e.Production:
                        return "Продукция";
                    case AvaType_e.Component:
                        return "Произведено";
                    case AvaType_e.Purchased:
                        return "Покупное";
                    case AvaType_e.VirtualComponent:
                        return "Виртуальный компонент";
                    case AvaType_e.DontBuy:
                        return "Не покупать";
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
