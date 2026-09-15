using System;
using System.Globalization;
using System.Windows.Data;

namespace PrismDemo.Core.Converters
{
    public class ChangeRateTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                if (Math.Abs(d) < 0.001)
                    return "—";
                if (d > 0)
                    return $"↑ {d:F1}%";
                return $"↓ {Math.Abs(d):F1}%";
            }
            return "—";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
