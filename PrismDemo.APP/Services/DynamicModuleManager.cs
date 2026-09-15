// PrismDemo.APP/Services/DynamicModuleManager.cs
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
    public class DynamicModuleManager : IModuleLifecycle
    {
        private readonly ConcurrentDictionary<string, IModule> _moduleInstances = new();
        private readonly ConcurrentDictionary<string, IServiceProvider> _moduleContainers = new();
        private readonly IContainerExtension _containerExtension;
        private readonly IRegionManager _regionManager;
        private readonly ConcurrentDictionary<string, WeakReference<ModuleLoadContext>> _contexts = new();
        private readonly ConcurrentDictionary<string, AssemblyLoadContext> _loadedModules
            = new ConcurrentDictionary<string, AssemblyLoadContext>();

        public DynamicModuleManager(IContainerExtension containerExtension, IRegionManager regionManager)
        {
            _containerExtension = containerExtension;
            _regionManager = regionManager;
        }

        /// <summary>
        /// 首次加载模块（模块必须未被加载过）
        /// </summary>
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

        /// <summary>
        /// 更新/重新加载模块（先卸载旧版本，再加载新版本）
        /// </summary>
        public async Task UpdateModuleAsync(string moduleName, string sourceDllPath)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                throw new ArgumentException("模块名称不能为空", nameof(moduleName));

            if (!File.Exists(sourceDllPath))
                throw new FileNotFoundException($"模块文件不存在: {sourceDllPath}", sourceDllPath);

            Debug.WriteLine($"[Update] 开始更新模块 {moduleName}...");

            await UnloadModuleInternalAsync(moduleName);

            Debug.WriteLine($"[Update] 卸载完成，_contexts={_contexts.ContainsKey(moduleName)}" +
                            $" _instances={_moduleInstances.ContainsKey(moduleName)}");

            await LoadModuleInternalAsync(moduleName, sourceDllPath);
        }

        /// <summary>
        /// 热重载模块（完整流程：卸载旧版本 → 加载新版本 → 替换代理目标）
        /// 
        /// 与 UpdateModuleAsync 的区别：
        ///   - 模块内部会自动复用代理实例
        ///   - 适用于开发期快速刷新和生产期版本升级
        /// </summary>
        public async Task ReloadModuleAsync(string moduleName, string sourceDllPath)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                throw new ArgumentException("模块名称不能为空", nameof(moduleName));

            if (!File.Exists(sourceDllPath))
                throw new FileNotFoundException($"模块文件不存在: {sourceDllPath}", sourceDllPath);

            Debug.WriteLine($"[Reload] ═══ 开始热重载模块 {moduleName} ═══");
            Debug.WriteLine($"[Reload] 新 DLL 路径: {sourceDllPath}");

            Debug.WriteLine($"[Reload] 步骤 1/3: 卸载旧版本...");
            await UnloadModuleInternalAsync(moduleName);

            Debug.WriteLine($"[Reload] 步骤 2/3: 强制 GC 回收...");
            for (int i = 0; i < 5; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(30);
            }

            Debug.WriteLine($"[Reload] 步骤 3/3: 加载新版本...");
            await LoadModuleInternalAsync(moduleName, sourceDllPath);

            Debug.WriteLine($"[Reload] ═══ 模块 {moduleName} 热重载完成 ═══");
        }

        /// <summary>
        /// 卸载模块
        /// </summary>
        public async Task UnloadModuleAsync(string moduleName)
        {
            await UnloadModuleInternalAsync(moduleName);
        }

        private async Task LoadModuleInternalAsync(string moduleName, string sourceDllPath)
        {
            if (_moduleInstances.TryRemove(moduleName, out var oldModule))
            {
                Debug.WriteLine($"[Load] 清理旧模块实例: {moduleName}");
                if (oldModule is IDisposable disposable)
                    disposable.Dispose();
            }
            Debug.WriteLine($"[Load] >>> LoadModuleInternalAsync 开始 Hash={GetHashCode()}");

            var tempBaseDir = Path.Combine(Path.GetTempPath(), "PrismDemo", "Modules", moduleName);
            Directory.CreateDirectory(tempBaseDir);
            var tempDll = Path.Combine(tempBaseDir, $"{moduleName}_{Guid.NewGuid():N}.dll");
            File.Copy(sourceDllPath, tempDll, overwrite: true);

            var context = new ModuleLoadContext(tempDll);
            _contexts[moduleName] = new WeakReference<ModuleLoadContext>(context);

            Debug.WriteLine($"[Load] 创建新 ALC，_contexts 已更新");

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
        }

        /// <summary>
        /// 初始化模块 —— 将根容器传给模块的 OnInitialized
        /// </summary>
        public void InitializeModule(string moduleName, IContainerProvider rootContainer)
        {
            Debug.WriteLine($"[DynamicModule] InitializeModule {moduleName}");

            if (!_moduleInstances.TryGetValue(moduleName, out var module))
            {
                Debug.WriteLine($"[DynamicModule] {moduleName} 未找到模块实例，跳过");
                return;
            }

            module.OnInitialized(rootContainer);
            Debug.WriteLine($"[DynamicModule] 模块 {moduleName} 初始化完成");
        }

        /// <summary>
        /// 判断模块是否已加载
        /// </summary>
        public bool IsModuleLoaded(string moduleName)
        {
            if (_moduleInstances.ContainsKey(moduleName))
            {
                Debug.WriteLine($"[DynamicModule] IsModuleLoaded({moduleName}) = True（_moduleInstances 中存在）");
                return true;
            }

            if (_contexts.ContainsKey(moduleName))
            {
                Debug.WriteLine($"[DynamicModule] IsModuleLoaded({moduleName}) = True（_contexts 中存在）");
                return true;
            }

            Debug.WriteLine($"[DynamicModule] IsModuleLoaded({moduleName}) = False（未在 _moduleInstances 或 _contexts 中找到）");
            return false;
        }


        /// <summary>
        /// 核心卸载逻辑（移除 Region 视图 → 清理实例 → 卸载 ALC → GC 回收）
        /// </summary>
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
                    try
                    {
                        disposable.Dispose();
                        Debug.WriteLine($"[Unload] Dispose 完成");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Unload] Dispose 异常: {ex.Message}");
                    }
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

        /// <summary>
        /// 用于根据 View 类型推断对应的 ViewModel 类型
        /// </summary>
        private static Type? GetViewModelTypeForView(Type viewType, Assembly moduleAssembly)
        {
            var ns = viewType.Namespace;
            if (ns == null) return null;

            var vmNamespace = ns.Replace(".Views", ".ViewModels");
            var vmTypeName = viewType.Name + "ViewModel";

            return moduleAssembly.GetType($"{vmNamespace}.{vmTypeName}");
        }

        private static IEnumerable<Type> GetViewTypesFromAssembly(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes()
                    .Where(t => t.Namespace?.EndsWith(".Views", StringComparison.OrdinalIgnoreCase) == true
                                && typeof(System.Windows.FrameworkElement).IsAssignableFrom(t)
                                && !t.IsAbstract);
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null
                                           && t.Namespace?.EndsWith(".Views", StringComparison.OrdinalIgnoreCase) == true
                                           && typeof(System.Windows.FrameworkElement).IsAssignableFrom(t)
                                           && !t.IsAbstract);
            }
        }

        /// <summary>
        /// 用于获取模块的 DI 容器
        /// </summary>
        public IServiceProvider GetModuleContainer(string moduleName)
        {
            return (_containerExtension as DryIocContainerExtension)?.Instance
                   ?? _containerExtension.Resolve<IServiceProvider>();
        }

        /// <summary>
        /// 获取模块中指定名称的 View 类型
        /// </summary>
        public Type GetModuleViewType(string moduleName, string viewTypeName)
        {
            return GetModuleType(moduleName, $"Views.{viewTypeName}");
        }

        /// <summary>
        /// 获取模块中指定名称的 ViewModel 类型
        /// </summary>
        public Type GetModuleViewModelType(string moduleName, string viewModelTypeName)
        {
            return GetModuleType(moduleName, $"ViewModels.{viewModelTypeName}");
        }

        /// <summary>
        /// 获取模块中的类型
        /// </summary>
        private Type GetModuleType(string moduleName, string fullTypeName)
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
            Debug.WriteLine($"[GetModuleType] 查找类型: {typeName}");

            var type = assembly.GetType(typeName);
            Debug.WriteLine($"[GetModuleType] 结果: {(type != null ? type.FullName : "null")}");
            return type;
        }

    }
}
