using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrismDemo.Core.Interfaces
{
    /// <summary>
    /// 模块开关与热重载控制接口
    /// </summary>
    public interface IModuleSwitch
    {
        bool IsModuleEnabled(string moduleName);
        Task SetModuleEnabledAsync(string moduleName, bool enabled);
        event Action<string, bool> SwitchChanged;

        /// <summary>
        /// 模块禁用时触发，用于通知视图清理
        /// </summary>
        event Action<string> ModuleDisabled;

        /// <summary>
        /// 热重载请求事件
        /// 
        /// 触发方式：用户在 UI 点击「热重载」按钮
        /// 参数：(moduleName, newDllPath)
        /// </summary>
        event Action<string, string> ReloadRequested;

        /// <summary>
        /// 请求热重载指定模块
        /// </summary>
        /// <param name="moduleName">模块名称</param>
        /// <param name="newDllPath">新 DLL 路径</param>
        Task RequestReloadAsync(string moduleName, string newDllPath);
    }
}
