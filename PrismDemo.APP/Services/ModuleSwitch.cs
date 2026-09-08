using Prism.Ioc;
using PrismDemo.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PrismDemo.APP.Services
{
    /// <summary>
    /// 模块开关与热重载控制（内存状态，真实工程可持久化）。
    /// </summary>
    public class ModuleSwitch : IModuleSwitch
    {
        private readonly Dictionary<string, bool> _switches = new();

        public event Action<string, bool> SwitchChanged;
        public event Action<string> ModuleDisabled;
        public event Action<string, string> ReloadRequested;

        public bool IsModuleEnabled(string moduleName)
        {
            return _switches.TryGetValue(moduleName, out bool enabled) && enabled;
        }

        public Task SetModuleEnabledAsync(string moduleName, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                throw new ArgumentException("模块名称不能为空", nameof(moduleName));

            bool previousState = _switches.TryGetValue(moduleName, out bool prev) && prev;
            _switches[moduleName] = enabled;
            SwitchChanged?.Invoke(moduleName, enabled);

            if (!enabled && previousState)
            {
                Debug.WriteLine($"[ModuleSwitch] 模块 {moduleName} 已禁用，触发视图清理事件");
                ModuleDisabled?.Invoke(moduleName);
            }

            return Task.CompletedTask;
        }

        public Task RequestReloadAsync(string moduleName, string newDllPath)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                throw new ArgumentException("模块名称不能为空", nameof(moduleName));
            if (string.IsNullOrWhiteSpace(newDllPath))
                throw new ArgumentException("DLL路径不能为空", nameof(newDllPath));

            Debug.WriteLine($"[ModuleSwitch] 请求热重载模块 {moduleName}，路径: {newDllPath}");
            ReloadRequested?.Invoke(moduleName, newDllPath);
            return Task.CompletedTask;
        }
    }
}