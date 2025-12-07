using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using Xarial.XCad.Documents;

namespace Agrovent.Infrastructure.Converters
{
    public class AGR_XComponentsGroupingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null)
            {
                var group = value as IGrouping<string, IXComponent>;
                return Path.GetFileNameWithoutExtension(group.FirstOrDefault().ReferencedDocument.Path);
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
