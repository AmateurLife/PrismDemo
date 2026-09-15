using PrismDemo.Core.Models;
using System;

namespace PrismDemo.Core.Interfaces
{
    /// <summary>
    /// 可写入服务接口
    /// 定义 OPC 数据写入的通用契约
    /// </summary>
    public interface IWriteableService
    {
        /// <summary>
        /// 判断指定字段是否可写入
        /// </summary>
        /// <param name="fieldName">字段名</param>
        /// <returns>可写入返回 true，否则返回 false</returns>
        bool CanWrite(string fieldName);

        /// <summary>
        /// 获取字段的中文显示名称
        /// </summary>
        /// <param name="fieldName">字段名</param>
        /// <returns>中文显示名，如未匹配则返回原名</returns>
        string GetDisplayName(string fieldName);

        /// <summary>
        /// 写入值到 OPC 服务器
        /// </summary>
        /// <param name="data">ConnectData 对象</param>
        /// <param name="value">要写入的值</param>
        bool WriteValue(ConnectData data, object value);

        /// <summary>
        /// 切换布尔值
        /// </summary>
        /// <param name="data">ConnectData 对象</param>
        void ToggleBoolValue(ConnectData data);
    }
}