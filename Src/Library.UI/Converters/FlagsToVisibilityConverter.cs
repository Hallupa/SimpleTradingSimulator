using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Hallupa.Library.UI.Converters
{
    public class FlagsToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var expectedFlag = (int)parameter;
            var flagValue = (int)value;

            if ((expectedFlag & flagValue) == expectedFlag)
            {
                return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
