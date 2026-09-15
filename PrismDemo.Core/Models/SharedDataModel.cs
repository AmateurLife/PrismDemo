using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Prism.Mvvm;

namespace PrismDemo.Core.Models
{
    public class SharedDataModel : BindableBase
    {
        public ObservableDictionary<string, object> Data { get; } = new();
        
        //根据数据库配置生成的连接数据列表
        public List<ConnectData> ConnectDataList { get; set; }

        //由ConnectDataList及其name生成的字典，方便根据name快速访问ConnectData对象
        private Dictionary<string, ConnectData> _collectedData = new();
        public Dictionary<string, ConnectData> CollectedData
        {
            get => _collectedData;
            set => SetProperty(ref _collectedData, value);
        }

        // ✅ 泛型设置方法（推荐）
        public void SetValue<T>(string key, T value)
        {
            Data[key] = value;
        }

        // ✅ 安全获取：支持默认值和类型转换
        public T GetValue<T>(string key, T defaultValue = default)
        {
            if (Data.TryGetValue(key, out object value))
            {
                // 尝试安全转换
                try
                {
                    if (value is T tValue)
                        return tValue;

                    // 尝试类型转换（如 string → double）
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    // 转换失败，返回默认值
                }
            }
            return defaultValue;
        }

        // 批量更新：接受 Dictionary<string, object>
        public void UpdateBatch(Dictionary<string, object> updates)
        {
            Data.UpdateBatch(updates);
        }
    }
}
