using System;
using System.Globalization;
using System.Windows.Data;

namespace PrismDemo.APP.Converters
{
    /// <summary>
    /// 将 bool 值取反（用于 IsEnabled 绑定 IsReloading 取反）
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && !b;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && !b;
        }
    }
}
