using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Globalization;

namespace NetChat2Client.Converters
{
    public class String2Int : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(!(value is string))
            {
                return null;
            }

            try
            {
                return int.Parse(value as string);
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(!(value is int))
            {
                return null;
            }

            return ((int)value).ToString();
        }
    }
}
