using System;
using System.Globalization;
using System.Windows.Data;

namespace Hallupa.Library.UI.Converters
{
    public class InvObjectToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return value != parameter;
            }

            return !value.Equals(parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? null : parameter;
        }
    }
}