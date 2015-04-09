using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NetChat2Client.Code.Converters
{
    public class String2Visibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var str = value as string;
            if (str == null)
            {
                return Visibility.Collapsed;
            }

            //might have to make some kind of static class to do all this checking and casting and whatnot
            //if i end up having a ton of converters
            if (parameter != null && (parameter as string) != null)
            {
                var param = parameter as string;

                if (param == "inverse")
                {
                    return string.IsNullOrWhiteSpace(str) ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            return string.IsNullOrWhiteSpace(str) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            //who would use this it makes no sense why omg (watch i'll need it at some point...)
            return null;
        }
    }
}