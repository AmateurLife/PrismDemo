using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PrismDemo.Core.Models
{
    /// <summary>
    /// 数据库connect数据
    /// </summary>
    public class ConnectData : INotifyPropertyChanged
    {
        private object _value;

        // 数据是否有效，主要用于A剂等模块对输入数据进行验证
        public bool isvalid = false;

        public string Name { get; set; }
        public string Type { get; set; }

        public object Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = FormatValue(value, Type);
                    OnPropertyChanged();
                }
            }
        }

        public string OpcAddress { get; set; }
        public string MockOpcAddress { get; set; }
        public string Description { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static object FormatValue(object value, string type)
        {
            if (value == null) return null;

            return type?.ToLowerInvariant() switch
            {
                "float" or "double" => Math.Round(Convert.ToDouble(value), 3),
                "int" or "int32" or "short" or "word" => Convert.ToInt32(value),
                "bool" or "boolean" => Convert.ToBoolean(value),
                _ => value
            };
        }
    }
}
