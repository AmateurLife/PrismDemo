using System.Collections.Generic;
using Prism.Mvvm;

namespace PrismDemo.Core.Models
{
    /// <summary>
    /// 全局共享数据模型（框架演示版）。
    /// 真实工程中存放 OPC 采集/数据库派生数据；
    /// Demo 中仅作为模块间共享数据的通道示例。
    /// </summary>
    public class SharedDataModel : BindableBase
    {
        public ObservableDictionary<string, object> Data { get; } = new();

        public void SetValue<T>(string key, T value) => Data[key] = value;

        public T GetValue<T>(string key, T defaultValue = default)
        {
            if (Data.TryGetValue(key, out object value))
            {
                try
                {
                    if (value is T tValue) return tValue;
                    return (T)System.Convert.ChangeType(value, typeof(T));
                }
                catch { }
            }
            return defaultValue;
        }

        public void UpdateBatch(Dictionary<string, object> updates) => Data.UpdateBatch(updates);
    }
}