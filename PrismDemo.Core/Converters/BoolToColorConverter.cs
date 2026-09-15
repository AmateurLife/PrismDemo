using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PrismDemo.Core.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isRunning)
                return isRunning ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"))
                                 : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999"));
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
