#nullable enable
using DryIoc;
using Microsoft.Extensions.DependencyInjection;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using PrismDemo.Core.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using System.Windows;

namespace PrismDemo.APP.Services
{
    /// <summary>
    /// 动态模块加载管理器。
    /// 将模块 DLL 在独立的可回收 ALC 中加载，
    /// 通过根 DryIoc 容器实例化模块类，并依次调用 RegisterTypes / OnInitialized。
    /// </summary>
    public class DynamicModuleManager : IModuleLifecycle
    {
        private readonly ConcurrentDictionary<string, IModule> _moduleInstances = new();
        private readonly ConcurrentDictionary<string, IServiceProvider> _moduleContainers = new();
        private readonly IContainerExtension _containerExtension;
        private readonly IRegionManager _regionManager;
        private readonly ConcurrentDictionary<string, WeakReference<ModuleLoadContext>> _contexts = new();

        public DynamicModuleManager(IContainerExtension containerExtension, IRegionManager regionManager)
        {
            _containerExtension = containerExtension;
            _regionManager = regionManager;
        }

        public async Task LoadModuleAsync(string moduleName, string sourceDllPath)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                throw new ArgumentException("模块名称不能为空", nameof(moduleName));
            if (!File.Exists(sourceDllPath))
                throw new FileNotFoundException($"模块文件不存在: {sourceDllPath}", sourceDllPath);

            if (IsModuleLoaded(moduleName))
            {
                Debug.WriteLine($"[Load] 模块 {moduleName} 已加载，跳过。如需重新加载请调用 UpdateModuleAsync");
                return;
            }

            await LoadModuleInternalAsync(moduleName, sourceDllPath);
        }

        public async Task UpdateModuleAsync(string moduleName, string sourceDllPath)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                throw new ArgumentException("模块名称不能为空", nameof(moduleName));
            if (!File.Exists(sourceDllPath))
                throw new FileNotFoundException($"模块文件不存在: {sourceDllPath}", sourceDllPath);

            Debug.WriteLine($"[Update] 开始更新模块 {moduleName}...");
            await UnloadModuleInternalAsync(moduleName);
            await LoadModuleInternalAsync(moduleName, sourceDllPath);
        }

        /// <summary>
        /// 热重载模块：卸载旧版本 → 强制 GC → 加载新版本。
        /// 模块内部通过 ServiceProxy 复用代理实例，实现无缝切换。
        /// </summary>
        public async Task ReloadModuleAsync(string moduleName, string sourceDllPath)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                throw new ArgumentException("模块名称不能为空", nameof(moduleName));
            if (!File.Exists(sourceDllPath))
                throw new FileNotFoundException($"模块文件不存在: {sourceDllPath}", sourceDllPath);

            Debug.WriteLine($"[Reload] ═══ 开始热重载模块 {moduleName} ═══");
            await UnloadModuleInternalAsync(moduleName);

            for (int i = 0; i < 5; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(30);
            }

            await LoadModuleInternalAsync(moduleName, sourceDllPath);
            Debug.WriteLine($"[Reload] ═══ 模块 {moduleName} 热重载完成 ═══");
        }

        public async Task UnloadModuleAsync(string moduleName)
        {
            await UnloadModuleInternalAsync(moduleName);
        }

        private Task LoadModuleInternalAsync(string moduleName, string sourceDllPath)
        {
            if (_moduleInstances.TryRemove(moduleName, out var oldModule))
            {
                if (oldModule is IDisposable disposable)
                    disposable.Dispose();
            }

            Debug.WriteLine($"[Load] >>> LoadModuleInternalAsync 开始");

            var tempBaseDir = Path.Combine(Path.GetTempPath(), "PrismDemo", "Modules", moduleName);
            Directory.CreateDirectory(tempBaseDir);
            var tempDll = Path.Combine(tempBaseDir, $"{moduleName}_{Guid.NewGuid():N}.dll");
            File.Copy(sourceDllPath, tempDll, overwrite: true);

            var context = new ModuleLoadContext(tempDll);
            _contexts[moduleName] = new WeakReference<ModuleLoadContext>(context);

            try
            {
                var assembly = context.LoadFromAssemblyPath(tempDll);

                Type? moduleType = null;
                try
                {
                    moduleType = assembly.GetTypes()
                        .FirstOrDefault(t => typeof(IModule).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);
                }
                catch (ReflectionTypeLoadException ex)
                {
                    moduleType = ex.Types
                        .Where(t => t != null && typeof(IModule).IsAssignableFrom(t) && !t.IsAbstract)
                        .FirstOrDefault();

                    foreach (var loaderEx in ex.LoaderExceptions ?? Array.Empty<Exception>())
                        Debug.WriteLine($"[DynamicModule] 类型加载失败: {loaderEx}");
                }

                if (moduleType == null)
                    throw new InvalidOperationException($"未在程序集 {assembly.FullName} 中找到实现 IModule 的类。");

                var serviceProvider = (_containerExtension as DryIocContainerExtension)?.Instance
                                      ?? _containerExtension.Resolve<IServiceProvider>();

                var module = (IModule)ActivatorUtilities.CreateInstance(serviceProvider, moduleType);

                _moduleInstances[moduleName] = module;

                module.RegisterTypes(_containerExtension);

                var rootProvider = new ModuleContainerProvider(
                    _containerExtension.Resolve<IContainerProvider>(),
                    _regionManager
                );
                module.OnInitialized(rootProvider);

                Debug.WriteLine($"[DynamicModule] 模块 {moduleName} 加载并初始化完成");
            }
            catch (Exception ex)
            {
                context.Unload();
                _contexts.TryRemove(moduleName, out _);
                throw new InvalidOperationException($"加载模块 {moduleName} 失败: {ex.Message}", ex);
            }

            return Task.CompletedTask;
        }

        public void InitializeModule(string moduleName, IContainerProvider rootContainer)
        {
            Debug.WriteLine($"[DynamicModule] InitializeModule {moduleName}");

            if (!_moduleInstances.TryGetValue(moduleName, out var module))
            {
                Debug.WriteLine($"[DynamicModule] {moduleName} 未找到模块实例，跳过");
                return;
            }

            module.OnInitialized(rootContainer);
        }

        public bool IsModuleLoaded(string moduleName)
        {
            if (_moduleInstances.ContainsKey(moduleName)) return true;
            if (_contexts.ContainsKey(moduleName)) return true;
            return false;
        }

        private async Task UnloadModuleInternalAsync(string moduleName)
        {
            if (!_contexts.TryGetValue(moduleName, out var weakRef) || !weakRef.TryGetTarget(out var context))
            {
                Debug.WriteLine($"[Unload] 模块 {moduleName} 未加载，跳过卸载");
                return;
            }

            foreach (var region in _regionManager.Regions)
            {
                var viewsToRemove = region.Views
                    .Where(v => v.GetType().Assembly == context.Assemblies.First())
                    .ToList();

                foreach (var view in viewsToRemove)
                {
                    try
                    {
                        region.Remove(view);
                        if (view is FrameworkElement fe && fe.DataContext is IDisposable disposable)
                            disposable.Dispose();
                        Debug.WriteLine($"[Unload] 已从 {region.Name} 移除 {view.GetType().Name}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Unload] 移除视图失败: {ex.Message}");
                    }
                }
            }

            if (_moduleInstances.TryRemove(moduleName, out var module))
            {
                Debug.WriteLine($"[Unload] 已清理模块实例: {moduleName}");
                if (module is IDisposable disposable)
                {
                    try { disposable.Dispose(); }
                    catch (Exception ex) { Debug.WriteLine($"[Unload] Dispose 异常: {ex.Message}"); }
                }
            }

            context.Unload();
            _contexts.TryRemove(moduleName, out _);
            Debug.WriteLine($"[Unload] 模块 {moduleName} 已卸载");

            for (int i = 0; i < 10; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                if (!weakRef.TryGetTarget(out _)) break;
                await Task.Delay(50);
            }
        }

        /// <summary>获取模块 DI 容器。</summary>
        public IServiceProvider GetModuleContainer(string moduleName)
        {
            return (_containerExtension as DryIocContainerExtension)?.Instance
                   ?? _containerExtension.Resolve<IServiceProvider>();
        }

        public Type? GetModuleViewType(string moduleName, string viewTypeName)
        {
            return GetModuleType(moduleName, $"Views.{viewTypeName}");
        }

        public Type? GetModuleViewModelType(string moduleName, string viewModelTypeName)
        {
            return GetModuleType(moduleName, $"ViewModels.{viewModelTypeName}");
        }

        private Type? GetModuleType(string moduleName, string fullTypeName)
        {
            if (!_contexts.TryGetValue(moduleName, out var weakRef) || !weakRef.TryGetTarget(out var context))
            {
                Debug.WriteLine($"[GetModuleType] ❌ 找不到 {moduleName} 的上下文");
                return null;
            }

            var assembly = context.Assemblies.FirstOrDefault();
            if (assembly == null)
            {
                Debug.WriteLine($"[GetModuleType] ❌ ALC 中没有程序集");
                return null;
            }

            var typeName = $"{assembly.GetName().Name}.{fullTypeName}";
            return assembly.GetType(typeName);
        }

        public IEnumerable<string> GetLoadedModuleNames() => _moduleInstances.Keys;
    }
}