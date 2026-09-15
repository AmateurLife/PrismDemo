using System;
using System.Globalization;
using System.Windows.Data;

namespace PrismDemo.Core.Converters
{
    public class BoolToIconFontConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked)
                return isChecked ? "\ue601" : "\ue600";
            return "\ue600";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
