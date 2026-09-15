using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PrismDemo.Core.Models
{
    /// <summary>
    /// 操作类型枚举，使用 Flags 特性支持多选
    /// </summary>
    [Flags]
    public enum OperationType
    {
        None = 0,
        /// <summary>需要保存到数据库</summary>
        DbStore = 1 << 0,
        /// <summary>需要写入 OPC</summary>
        WriteToOPC = 1 << 1,
    }

    /// <summary>
    /// 属性特性标记，用于指定属性所属的操作类型
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class OperationAttribute : Attribute
    {
        /// <summary>
        /// 操作类型
        /// </summary>
        public OperationType Operations { get; }
        /// <summary>
        /// 排序顺序
        /// </summary>
        public int Order { get; set; } = 0;

        public OperationAttribute(OperationType operations)
        {
            Operations = operations;
        }
    }
}