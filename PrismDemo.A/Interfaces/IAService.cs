using PrismDemo.Core.Models;
using PrismDemo.A.Models;
using System;
using System.Threading.Tasks;

namespace PrismDemo.A.Interfaces
{
    /// <summary>
    /// A剂服务接口
    /// 负责A剂数据的采集、存储和 OPC 写入
    /// </summary>
    public interface IAService
    {
        /// <summary>
        /// 服务是否正在运行
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 当前A剂数据
        /// </summary>
        AData? CurrentData { get; }

        /// <summary>
        /// 启动服务
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// 判断指定字段是否可写入
        /// </summary>
        /// <param name="name">字段名（ADataCollection 的 Key）</param>
        /// <returns>可写入返回 true，否则返回 false</returns>
        bool CanWrite(string name);

        /// <summary>
        /// 获取字段的中文显示名称
        /// </summary>
        /// <param name="name">字段名（ADataCollection 的 Key）</param>
        /// <returns>中文显示名，如未匹配则返回原名</returns>
        string GetDisplayName(string name);

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
