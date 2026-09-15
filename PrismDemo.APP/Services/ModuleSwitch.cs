using Prism.Ioc;
using PrismDemo.APP.Services;
using PrismDemo.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PrismDemo.APP.Services
{
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

        public async Task SetModuleEnabledAsync(string moduleName, bool enabled)
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
        }

        public async Task RequestReloadAsync(string moduleName, string newDllPath)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                throw new ArgumentException("模块名称不能为空", nameof(moduleName));

            if (string.IsNullOrWhiteSpace(newDllPath))
                throw new ArgumentException("DLL路径不能为空", nameof(newDllPath));

            Debug.WriteLine($"[ModuleSwitch] 请求热重载模块 {moduleName}，路径: {newDllPath}");
            ReloadRequested?.Invoke(moduleName, newDllPath);
        }
    }
}
