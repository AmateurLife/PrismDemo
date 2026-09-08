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
using System.Diagnostics;

namespace PrismDemo.B
{
    /// <summary>模块 B（框架演示版，对应真实工程的 NaClOModule/PACModule）。</summary>
    public class BModule : IModule, IDisposable
    {
        private IContainerExtension _rootContainerExtension;
        private bool _disposed;

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            Debug.WriteLine("[B] ══ RegisterTypes 开始 ══");

            Config.Initialize();

            containerRegistry.Register<BDataService>();

            containerRegistry.RegisterForNavigation<BHomeView, BHomeViewModel>("ModuleB_Home");
            Debug.WriteLine("[B] 已注册导航页面: BHomeView → ModuleB_Home");

            Debug.WriteLine("[B] ══ RegisterTypes 完成 ══");
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            Debug.WriteLine("[B] ══ OnInitialized 开始 ══");

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
                    Debug.WriteLine("[B] 模块开关已自动启用");
                }

                InitializeService(containerProvider);

                Debug.WriteLine("[B] ══ OnInitialized 完成 ══");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[B] ❌ OnInitialized 异常: {ex}");
            }
        }

        private void RegisterMainService(IContainerProvider containerProvider, IContainerExtension containerExtension)
        {
            var dataService = containerProvider.Resolve<BDataService>();
            var moduleSwitch = containerProvider.Resolve<IModuleSwitch>();
            var sharedData = containerProvider.Resolve<SharedDataModel>();

            var service = new BMainService(moduleSwitch, sharedData, dataService);

            var proxy = new ServiceProxy<IBService>();
            proxy.SetTarget(service);

            containerExtension.RegisterInstance<ServiceProxy<IBService>>(proxy);
            containerExtension.RegisterInstance<IBService>(service);
            Debug.WriteLine($"[B] 注册 IBService, Hash={service.GetHashCode()}");
        }

        private void InitializeService(IContainerProvider containerProvider)
        {
            var service = containerProvider.Resolve<IBService>();
            service.Start();
            Debug.WriteLine($"[B] 服务已解析并启动 Hash={service.GetHashCode()}");
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
                    Debug.WriteLine("[B] 已释放 IBService");
                }

                _rootContainerExtension?.Resolve<ServiceProxy<IBService>>();
                Debug.WriteLine("[B] Module.Dispose 完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[B] Module.Dispose 异常: {ex.Message}");
            }
        }
    }
}