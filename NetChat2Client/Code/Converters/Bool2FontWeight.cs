using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NetChat2Client.Code.Converters
{
    public class Bool2FontWeight : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var b = value as bool?;
            if (b == null)
            {
                return FontWeights.Regular;
            }

            return b == true ? FontWeights.Bold : FontWeights.Regular;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}