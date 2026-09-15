using PrismDemo.Core.Interfaces;
using PrismDemo.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace PrismDemo.A.Models
{
    /// <summary>
    /// A 字段配置
    /// </summary>
    public class AFieldConfig : IFilterConfig
    {
        public string Name { get; set; }
        public string ConnectName { get; set; }
        public bool IsStore { get; set; }
        public bool IsFilter { get; set; }
        public int? FilterLength { get; set; }
    }

    /// <summary>
    /// A剂数据集合 (用字典替代 AData 类)
    /// 根据 name 可以立即找到对应的 ConnectData
    /// </summary>
    public class AData : Dictionary<string, ConnectData>
    {
        /// <summary>
        /// 根据字段名获取 ConnectData，如果没有则返回 null
        /// </summary>
        public ConnectData? GetField(string name) => TryGetValue(name, out var data) ? data : null;

        /// <summary>
        /// 根据字段名设置 ConnectData 的值，如果字段不存在则忽略
        /// </summary>
        public void SetField(string name, object value)
        {
            if (TryGetValue(name, out var data))
                data.Value = value;
        }

        /// <summary>
        /// 获取需要保存到数据库的字段配置列表
        /// </summary>
        public IEnumerable<AFieldConfig> GetStoreFields(IEnumerable<AFieldConfig> configs)
            => configs.Where(c => c.IsStore);
    }
}
