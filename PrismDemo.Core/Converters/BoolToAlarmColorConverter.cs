using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PrismDemo.Core.Converters
{
    public class BoolToAlarmColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked)
                return isChecked ? Brushes.Black : Brushes.Red;
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
