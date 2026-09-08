using System;
using System.Threading.Tasks;

namespace PrismDemo.Core.Interfaces
{
    /// <summary>
    /// 模块开关与热重载控制接口。
    /// </summary>
    public interface IModuleSwitch
    {
        bool IsModuleEnabled(string moduleName);
        Task SetModuleEnabledAsync(string moduleName, bool enabled);

        event Action<string, bool> SwitchChanged;

        /// <summary>模块禁用时触发，用于通知视图清理。</summary>
        event Action<string> ModuleDisabled;

        /// <summary>热重载请求事件，参数：(moduleName, newDllPath)。</summary>
        event Action<string, string> ReloadRequested;

        Task RequestReloadAsync(string moduleName, string newDllPath);
    }
}