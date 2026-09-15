using System;
using System.Globalization;
using System.Windows.Data;

namespace PrismDemo.Core.Converters
{
    /// <summary>
    /// 布尔值显示转换器
    /// 将 bool 类型的 Value 转换为 "是"/"否" 显示
    /// </summary>
    public class BoolConverter : IMultiValueConverter
    {
        /// <summary>
        /// 正向转换：values[0] = Value, values[1] = Type
        /// </summary>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] != null && values[1] is string type)
            {
                var lowerType = type.ToLowerInvariant();
                if (lowerType == "bool" || lowerType == "boolean")
                {
                    if (values[0] is bool b)
                    {
                        return b ? "是" : "否";
                    }
                }
            }
            return values[0]?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// 反向转换（未实现，仅用于单向显示）
        /// </summary>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
