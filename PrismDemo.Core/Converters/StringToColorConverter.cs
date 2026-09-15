using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PrismDemo.Core.Converters
{
    public class StringToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text)
            {
                return text switch
                {
                    "智能" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#388E3C")),
                    "人工" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB74D")),
                    _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999"))
                };
            }
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
