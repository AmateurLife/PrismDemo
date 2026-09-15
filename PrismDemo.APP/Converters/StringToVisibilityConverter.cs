using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PrismDemo.APP.Converters
{
    /// <summary>
    /// 将非空字符串转换为 Visibility.Visible，空字符串转换为 Visibility.Collapsed
    /// </summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
