using Prism.DryIoc;
using Prism.Ioc;
using PrismDemo.APP.Services;
using PrismDemo.APP.Views;
using PrismDemo.Core.Configuration;
using PrismDemo.Core.Interfaces;
using PrismDemo.Core.Models;
using PrismDemo.Core.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using static PrismDemo.Core.Services.Log;

namespace PrismDemo.APP
{
    /// <summary>
    /// WPF 应用程序入口。
    ///
    /// 模块化架构：
    ///   App 不引用任何模块项目
    ///   所有模块 DLL 放在 modules/ 目录下
    ///   启动时扫描目录，动态发现并加载模块
    ///   新增模块只需放 DLL，无需修改 App 代码
    ///
    /// Prism 启动顺序：
    ///   1. RegisterTypes()           — 注册 DI 依赖
    ///   2. CreateShell()             — 创建主窗口
    ///   3. ConfigureModuleCatalog()  — 扫描目录，注册模块
    ///   4. OnInitialized()           — 加载模块，执行初始化
    /// </summary>
    public partial class App : PrismApplication
    {
        /// <summary>模块 DLL 所在目录。</summary>
        private static string ModulesDirectory =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modules");

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            ConfigLoader.Initialize();
            Log.Initialize();
            Write("[App] 核心配置已加载");

            containerRegistry.RegisterForNavigation<HomePage>();
            Write("[App] 导航页面注册完成");

            containerRegistry.RegisterSingleton<DynamicModuleManager>();
            containerRegistry.RegisterSingleton<IModuleSwitch, ModuleSwitch>();
            containerRegistry.RegisterSingleton<IModuleDeploymentService, ModuleDeploymentService>();
            Write("[App] 动态模块基础设施注册完成");

            containerRegistry.RegisterSingleton<SharedDataModel>();
            Write("[App] 共享数据模型注册完成");
        }

        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        protected override void OnInitialized()
        {
            Write("═══════════════════════════════════════════════════");
            Write("[App] OnInitialized 开始");

            try
            {
                CleanupOldModuleVersions(keepLast: 3);
                base.OnInitialized();

                AutoLoadExistingModules();

                var switchService = Container.Resolve<IModuleSwitch>();
                Write($"[App] 模块 A 启用状态: {switchService.IsModuleEnabled("A")}");
                Write($"[App] 模块 B 启用状态: {switchService.IsModuleEnabled("B")}");

                Write("[App] OnInitialized 完成");
            }
            catch (Exception ex)
            {
                Write($"[App] ❌ OnInitialized 异常: {ex}");
            }
        }

        /// <summary>按模块名分组，只保留每个模块最近 keepLast 个版本。</summary>
        private void CleanupOldModuleVersions(int keepLast = 3)
        {
            if (!Directory.Exists(ModulesDirectory)) return;

            var groups = Directory.GetFiles(ModulesDirectory, "PrismDemo.*.dll")
                .GroupBy(file =>
                {
                    var parts = Path.GetFileNameWithoutExtension(file).Split('.');
                    return parts.Length >= 3 ? parts[1] : null;
                })
                .Where(g => g.Key != null);

            foreach (var group in groups)
            {
                var sorted = group
                    .Select(f =>
                    {
                        var parts = Path.GetFileNameWithoutExtension(f).Split('.');
                        var tsStr = parts.Length >= 3 ? parts[^1] : "";
                        return new { File = f, Time = DateTime.TryParseExact(tsStr, "yyyyMMddHHmm", null, DateTimeStyles.None, out var t) ? (DateTime?)t : null };
                    })
                    .Where(x => x.Time.HasValue)
                    .OrderByDescending(x => x.Time)
                    .Skip(keepLast)
                    .ToList();

                foreach (var old in sorted)
                {
                    try
                    {
                        File.Delete(old.File);
                        Write($"[Cleanup] 删除旧模块: {Path.GetFileName(old.File)}");
                    }
                    catch { }
                }
            }
        }

        private void AutoLoadExistingModules()
        {
            if (!Directory.Exists(ModulesDirectory))
            {
                Write("[App] modules 目录不存在，跳过自动加载");
                return;
            }

            var moduleManager = Container.Resolve<DynamicModuleManager>();
            var latestModules = new Dictionary<string, (string Dll, DateTime Time)>();

            foreach (var dll in Directory.GetFiles(ModulesDirectory, "PrismDemo.*.dll"))
            {
                var parts = Path.GetFileNameWithoutExtension(dll).Split('.');
                if (parts.Length < 2) continue;

                string moduleName;
                DateTime ts;

                var tsStr = parts[^1];
                if (parts.Length >= 3 && tsStr.Length == 12 &&
                    DateTime.TryParseExact(tsStr, "yyyyMMddHHmm", null, DateTimeStyles.None, out ts))
                {
                    moduleName = string.Join(".", parts.Skip(1).Take(parts.Length - 2));
                }
                else
                {
                    // 未带时间戳的常规构建产物（如 PrismDemo.A.dll），直接加载
                    moduleName = string.Join(".", parts.Skip(1));
                    ts = File.GetLastWriteTime(dll);
                }

                if (!latestModules.TryGetValue(moduleName, out var existing) || ts > existing.Time)
                    latestModules[moduleName] = (dll, ts);
            }

            foreach (var kvp in latestModules)
            {
                try
                {
                    Write($"[App] 自动加载模块: {kvp.Key}");
                    moduleManager.LoadModuleAsync(kvp.Key, kvp.Value.Dll).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Write($"[App] 模块 {kvp.Key} 自动加载失败: {ex.Message}");
                }
            }
        }
    }
}