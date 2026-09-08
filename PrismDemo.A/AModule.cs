using Prism.Ioc;
using Prism.Modularity;
using PrismDemo.A.Configuration;
using PrismDemo.A.Interfaces;
using PrismDemo.A.Services;
using PrismDemo.A.ViewModels;
using PrismDemo.A.Views;
using PrismDemo.Core.Interfaces;
using PrismDemo.Core.Models;
using System;
using System.Diagnostics;

namespace PrismDemo.A
{
    /// <summary>
    /// 模块 A（框架演示版，对应真实工程的 NaClOModule/PACModule）。
    /// </summary>
    public class AModule : IModule, IDisposable
    {
        private IContainerExtension _rootContainerExtension;
        private bool _disposed;

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            Debug.WriteLine("[A] ══ RegisterTypes 开始 ══");

            Config.Initialize();

            containerRegistry.Register<SampleDataService>();

            containerRegistry.RegisterForNavigation<AHomeView, AHomeViewModel>("ModuleA_Home");
            Debug.WriteLine("[A] 已注册导航页面: AHomeView → ModuleA_Home");

            Debug.WriteLine("[A] ══ RegisterTypes 完成 ══");
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            Debug.WriteLine("[A] ══ OnInitialized 开始 ══");

            try
            {
                _rootContainerExtension = containerProvider is IContainerExtension ext
                    ? ext
                    : containerProvider.Resolve<IContainerExtension>();

                RegisterMainService(containerProvider, _rootContainerExtension);

                var moduleSwitch = containerProvider.Resolve<IModuleSwitch>();
                if (!moduleSwitch.IsModuleEnabled("A"))
                {
                    moduleSwitch.SetModuleEnabledAsync("A", true).GetAwaiter().GetResult();
                    Debug.WriteLine("[A] 模块开关已自动启用");
                }

                InitializeService(containerProvider);

                Debug.WriteLine("[A] ══ OnInitialized 完成 ══");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[A] ❌ OnInitialized 异常: {ex}");
            }
        }

        /// <summary>
        /// 聚合根容器提供的依赖，构造模块主服务并注册。
        /// ServiceProxy 是热重载的无缝切换接缝（见 PrismDemo.Core.Interfaces.ServiceProxy<T>）。
        /// </summary>
        private void RegisterMainService(IContainerProvider containerProvider, IContainerExtension containerExtension)
        {
            var dataService = containerProvider.Resolve<SampleDataService>();
            var moduleSwitch = containerProvider.Resolve<IModuleSwitch>();
            var sharedData = containerProvider.Resolve<SharedDataModel>();

            var service = new SampleMainService(moduleSwitch, sharedData, dataService);

            var proxy = new ServiceProxy<IASampleService>();
            proxy.SetTarget(service);

            containerExtension.RegisterInstance<ServiceProxy<IASampleService>>(proxy);
            containerExtension.RegisterInstance<IASampleService>(service);
            Debug.WriteLine($"[A] 注册 IASampleService, Hash={service.GetHashCode()}");
        }

        private void InitializeService(IContainerProvider containerProvider)
        {
            var service = containerProvider.Resolve<IASampleService>();
            service.Start();
            Debug.WriteLine($"[A] 服务已解析并启动 Hash={service.GetHashCode()}");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                var service = _rootContainerExtension?.Resolve<IASampleService>();
                if (service is IDisposable disposable)
                {
                    disposable.Dispose();
                    Debug.WriteLine("[A] 已释放 IASampleService");
                }

                _rootContainerExtension?.Resolve<ServiceProxy<IASampleService>>();
                Debug.WriteLine("[A] Module.Dispose 完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[A] Module.Dispose 异常: {ex.Message}");
            }
        }
    }
}