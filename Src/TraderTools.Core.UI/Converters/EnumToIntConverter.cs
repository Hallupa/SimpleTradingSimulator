using System;
using System.Globalization;
using System.Windows.Data;

namespace TraderTools.Core.UI.Converters
{
    public class EnumToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null)
            {
                return (int)value;
            }

            return -1;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((int)value != -1)
            {
                return (int)value;
            }

            return null;
        }
    }
}