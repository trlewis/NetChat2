using System;
using System.Globalization;
using System.Windows.Data;

namespace NetChat2Client.Code.Converters
{
    public class Int2String : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var num = value as int?;
            return num == null ? string.Empty : num.Value.ToString(CultureInfo.InvariantCulture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var str = value as string;
            if (str == null)
            {
                return null;
            }

            int i;
            if (int.TryParse(str, out i))
            {
                return i;
            }
            return null;
        }
    }
}