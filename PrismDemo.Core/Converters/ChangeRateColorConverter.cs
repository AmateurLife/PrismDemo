using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PrismDemo.Core.Converters
{
    public class ChangeRateColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush UpBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
        private static readonly SolidColorBrush DownBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
        private static readonly SolidColorBrush ZeroBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9E9E9E"));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                if (Math.Abs(d) < 0.001)
                    return ZeroBrush;
                return d > 0 ? UpBrush : DownBrush;
            }
            return ZeroBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
