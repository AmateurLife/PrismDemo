using DryIoc;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using PrismDemo.B.Configuration;
using PrismDemo.B.Interfaces;
using PrismDemo.B.Services;
using PrismDemo.B.ViewModels;
using PrismDemo.B.Views;
using PrismDemo.Core.Interfaces;
using PrismDemo.Core.Models;
using System;
using static PrismDemo.Core.Services.Log;

namespace PrismDemo.B
{
    /// <summary>模块 B（框架演示版，对应真实工程的 NaClOModule/PACModule）。</summary>
    public class BModule : IModule, IDisposable
    {
        private IContainerExtension _rootContainerExtension;
        private bool _disposed;

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            Write("[B] ══ RegisterTypes 开始 ══");

            Config.Initialize();

            var container = ((DryIocContainerExtension)containerRegistry).Instance;
            container.Register<BDataService>(ifAlreadyRegistered: IfAlreadyRegistered.Replace);

            containerRegistry.RegisterForNavigation<BHomeView, BHomeViewModel>("ModuleB_Home");
            Write("[B] 已注册导航页面: BHomeView → ModuleB_Home");

            Write("[B] ══ RegisterTypes 完成 ══");
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            Write("[B] ══ OnInitialized 开始 ══");

            try
            {
                _rootContainerExtension = containerProvider is IContainerExtension ext
                    ? ext
                    : containerProvider.Resolve<IContainerExtension>();

                RegisterMainService(containerProvider, _rootContainerExtension);

                var moduleSwitch = containerProvider.Resolve<IModuleSwitch>();
                if (!moduleSwitch.IsModuleEnabled("B"))
                {
                    moduleSwitch.SetModuleEnabledAsync("B", true).GetAwaiter().GetResult();
                    Write("[B] 模块开关已自动启用");
                }

                InitializeService(containerProvider);

                Write($"[B] ══ OnInitialized 完成 v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version} ══");
            }
            catch (Exception ex)
            {
                Write($"[B] ❌ OnInitialized 异常: {ex}");
            }
        }

        private void RegisterMainService(IContainerProvider containerProvider, IContainerExtension containerExtension)
        {
            var container = ((DryIocContainerExtension)containerExtension).Instance;

            var dataService = containerProvider.Resolve<BDataService>();
            var moduleSwitch = containerProvider.Resolve<IModuleSwitch>();
            var sharedData = containerProvider.Resolve<SharedDataModel>();

            var service = new BMainService(moduleSwitch, sharedData, dataService);

            // ServiceProxy 作为"当前服务实例持有者"接缝（见 PrismDemo.Core.Interfaces.ServiceProxy<T>）。
            var proxy = new ServiceProxy<IBService>();
            proxy.SetTarget(service);

            container.RegisterInstance(proxy, IfAlreadyRegistered.Replace);
            container.RegisterInstance<IBService>(service, IfAlreadyRegistered.Replace);
            Write($"[B] 注册 IBService, Hash={service.GetHashCode()}");
        }

        private void InitializeService(IContainerProvider containerProvider)
        {
            var service = containerProvider.Resolve<IBService>();
            service.Start();
            Write($"[B] 服务已解析并启动 Hash={service.GetHashCode()}");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                var service = _rootContainerExtension?.Resolve<IBService>();
                if (service is IDisposable disposable)
                {
                    disposable.Dispose();
                    Write("[B] 已释放 IBService");
                }
                Write("[B] Module.Dispose 完成");
            }
            catch (Exception ex)
            {
                Write($"[B] Module.Dispose 异常: {ex.Message}");
            }
        }
    }
}