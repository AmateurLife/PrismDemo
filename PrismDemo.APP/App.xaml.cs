using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using PrismDemo.APP.Services;
using PrismDemo.APP.Views;
using PrismDemo.Core.Interfaces;
using PrismDemo.Core.Models;
using PrismDemo.Core.Configuration;
using PrismDemo.Core.Services;
using static PrismDemo.Core.Services.AlertLogger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace PrismDemo.APP
{
    /// <summary>
    /// WPF 应用程序入口
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
        /// <summary>
        /// 模块 DLL 所在目录
        /// </summary>
        private static string ModulesDirectory =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modules");

        // ═══════════════════════════════════════════════════════
        //  第一步：注册 DI 依赖
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 注册全局 DI 依赖
        /// 只注册 APP 层的依赖，模块的依赖在各自的 Module.RegisterTypes() 中注册
        /// </summary>
        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            Debug.WriteLine("[App] ══ RegisterTypes 开始 ══");

            Config.Initialize();
            Debug.WriteLine("[App] 核心配置已加载");

            // ── 导航页面注册 ──
            containerRegistry.RegisterForNavigation<HomePage>();
            containerRegistry.RegisterForNavigation<ReportData>();
            containerRegistry.RegisterForNavigation<ChartPage>();
            containerRegistry.RegisterForNavigation<HistoricalAlarm>();
            containerRegistry.RegisterForNavigation<Setting>();
            containerRegistry.RegisterForNavigation<AlarmDetail>();
            containerRegistry.RegisterForNavigation<SettingConnect>();
            containerRegistry.RegisterForNavigation<SettingAlarm>();
            Debug.WriteLine("[App] 导航页面注册完成");

            // ── 核心服务注册 ──
            containerRegistry.RegisterSingleton<DynamicModuleManager>();
            containerRegistry.RegisterSingleton<IModuleSwitch, ModuleSwitch>();
            containerRegistry.RegisterSingleton<IModuleDeploymentService, ModuleDeploymentService>();

            // 注册数据库服务
            containerRegistry.RegisterSingleton<IDatabaseService, DatabaseService>();

            // 注册OPC服务
            // 2026-04-07 修复：先注册 OpcServices 为单例，再让 IOpcService 解析为同一实例
            // 原因：APPMainService 通过 OpcServices 类型注入，各功能模块主服务通过 IOpcService 注入
            // 如果只注册 RegisterSingleton<IOpcService, OpcServices>()，两者会得到不同的实例
            // 使用 Config.OpcEndpoint 作为连接地址，使 core.config.json 的 Opc:Endpoint 配置生效
            containerRegistry.RegisterSingleton<OpcServices>(c => new OpcServices(Config.OpcEndpoint));
            containerRegistry.RegisterSingleton<IOpcService>(c => c.Resolve<OpcServices>());

            // 注册共享数据类
            containerRegistry.RegisterSingleton<SharedDataModel>();

            // 注册APP主服务
            containerRegistry.RegisterSingleton<APPMainService>();

            // 注册报警服务
            containerRegistry.RegisterSingleton<AlarmService>();
            containerRegistry.RegisterSingleton<IAlarmConfigProvider>(c => c.Resolve<AlarmService>());

            // 注册系统重新加载服务
            containerRegistry.RegisterSingleton<AppReloadService>();

            Debug.WriteLine("[App] 核心服务注册完成");

            Debug.WriteLine("[App] ══ RegisterTypes 完成 ══");
        }

        // ═══════════════════════════════════════════════════════
        //  第二步：创建主窗口
        // ═══════════════════════════════════════════════════════

        protected override Window CreateShell()
        {
            Debug.WriteLine("[App] CreateShell 开始");
            var shell = Container.Resolve<MainWindow>();
            Debug.WriteLine("[App] CreateShell 完成");
            return shell;
        }

        // ═══════════════════════════════════════════════════════
        //  第四步：应用初始化
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 应用初始化
        ///
        /// base.OnInitialized() 内部会：
        ///   1. 加载 ConfigureModuleCatalog 中注册的所有模块
        ///   2. 依次调用每个模块的 RegisterTypes()
        ///   3. 依次调用每个模块的 OnInitialized()
        /// </summary>
        protected override void OnInitialized()
        {
            Debug.WriteLine("═══════════════════════════════════════════════════");
            Debug.WriteLine("[App] OnInitialized 开始");
            Debug.WriteLine("═══════════════════════════════════════════════════");

            try
            {
                CleanupOldModuleVersions(keepLast: 3);
                Debug.WriteLine("[App] 调用 base.OnInitialized()（加载所有模块）");
                base.OnInitialized();

                // 启动APP主服务（OPC连接和数据采集）
                var appMainService = Container.Resolve<APPMainService>();
                appMainService.Start();

                AutoLoadExistingModules();

                // ── 诊断输出 ──
                Debug.WriteLine("[App] ── 模块加载后诊断 ──");
                var switchService = Container.Resolve<IModuleSwitch>();
                Debug.WriteLine($"[App] A 模块启用状态: {switchService.IsModuleEnabled("A")}");

                Debug.WriteLine("═══════════════════════════════════════════════════");
                Debug.WriteLine("[App] OnInitialized 完成");
                Debug.WriteLine("═══════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] ❌ OnInitialized 异常: {ex}");
                ShowMessageBox($"应用初始化失败:\n{ex.Message}", "启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += (_, args) =>
                WriteLog($"[App] DispatcherUnhandledException: {args.Exception}");

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                WriteLog($"[App] AppDomainUnhandledException: {args.ExceptionObject}");

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                WriteLog($"[App] UnobservedTaskException: {args.Exception}");
                args.SetObserved();
            };

            base.OnStartup(e);
        }

        private void CleanupOldModuleVersions(int keepLast = 3)
        {
            if (!Directory.Exists(ModulesDirectory)) return;

            var groups = Directory.GetFiles(ModulesDirectory, "PrismDemo.*.dll")
                .GroupBy(file =>
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    var parts = name.Split('.');
                    return parts.Length >= 3 ? parts[1] : null; // 模块名
                })
                .Where(g => g.Key != null);

            foreach (var group in groups)
            {
                var sorted = group
                    .Select(f =>
                    {
                        var name = Path.GetFileNameWithoutExtension(f);
                        var parts = name.Split('.');
                        var tsStr = parts.Length >= 3 ? parts[^1] : "";
                        return new { File = f, Time = DateTime.TryParseExact(tsStr, "yyyyMMddHHmm", null, DateTimeStyles.None, out var t) ? t : (DateTime?)null };
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
                        Debug.WriteLine($"[Cleanup] 删除旧模块: {Path.GetFileName(old.File)}");
                    }
                    catch { /* 忽略正在使用的 */ }
                }
            }
        }

        private void AutoLoadExistingModules()
        {
            if (!Directory.Exists(ModulesDirectory))
            {
                Debug.WriteLine("[App] modules 目录不存在，跳过自动加载");
                return;
            }

            var moduleManager = Container.Resolve<DynamicModuleManager>();
            var latestModules = new Dictionary<string, (string Dll, DateTime Time)>();

            foreach (var dll in Directory.GetFiles(ModulesDirectory, "PrismDemo.*.dll"))
            {
                var name = Path.GetFileNameWithoutExtension(dll);
                var parts = name.Split('.');
                if (parts.Length < 3) continue;
                var tsStr = parts[^1];
                if (tsStr.Length != 12 || !long.TryParse(tsStr, out _)) continue;
                var moduleName = string.Join(".", parts.Skip(1).Take(parts.Length - 2));

                if (DateTime.TryParseExact(tsStr, "yyyyMMddHHmm", null, DateTimeStyles.None, out var ts))
                {
                    if (!latestModules.TryGetValue(moduleName, out var existing) || ts > existing.Time)
                        latestModules[moduleName] = (dll, ts);
                }
            }

            foreach (var kvp in latestModules)
            {
                try
                {
                    Debug.WriteLine($"[App] 自动加载模块: {kvp.Key}");
                    moduleManager.LoadModuleAsync(kvp.Key, kvp.Value.Dll).GetAwaiter().GetResult();
                    Debug.WriteLine($"[App] 模块 {kvp.Key} 加载完成");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[App] 模块 {kvp.Key} 自动加载失败: {ex.Message}");
                }
            }
        }
    }
}
