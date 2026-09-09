using DryIoc;
using Prism.DryIoc;
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
using static PrismDemo.Core.Services.Log;

namespace PrismDemo.A
{
    /// <summary>
    /// 模块 A（框架演示版，对应真实工程的 NaClOModule/PACModule）。
    /// 热重载支撑：所有服务注册采用 IfAlreadyRegistered.Replace 语义，
    /// 使模块被第二次加载时幂等；ServiceProxy 复用容器中既有代理并更新 Target。
    /// </summary>
    public class AModule : IModule, IDisposable
    {
        private IContainerExtension _rootContainerExtension;
        private bool _disposed;

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            Write("[A] ══ RegisterTypes 开始 ══");

            Config.Initialize();

            var container = ((DryIocContainerExtension)containerRegistry).Instance;
            container.Register<SampleDataService>(ifAlreadyRegistered: IfAlreadyRegistered.Replace);

            containerRegistry.RegisterForNavigation<AHomeView, AHomeViewModel>("ModuleA_Home");
            Write("[A] 已注册导航页面: AHomeView → ModuleA_Home");

            Write("[A] ══ RegisterTypes 完成 ══");
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            Write("[A] ══ OnInitialized 开始 ══");

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
                    Write("[A] 模块开关已自动启用");
                }

                InitializeService(containerProvider);

                Write($"[A] ══ OnInitialized 完成 v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version} ══");
            }
            catch (Exception ex)
            {
                Write($"[A] ❌ OnInitialized 异常: {ex}");
            }
        }

        /// <summary>
        /// 聚合根容器提供的依赖，构造模块主服务并注册。
        /// ServiceProxy 为跨程序集加载上下文持有的稳定接缝：
        /// 热重载时复用既有代理并 SetTarget 新实例，宿主引用无需重新解析。
        /// </summary>
        private void RegisterMainService(IContainerProvider containerProvider, IContainerExtension containerExtension)
        {
            var container = ((DryIocContainerExtension)containerExtension).Instance;

            var dataService = containerProvider.Resolve<SampleDataService>();
            var moduleSwitch = containerProvider.Resolve<IModuleSwitch>();
            var sharedData = containerProvider.Resolve<SharedDataModel>();

            var service = new SampleMainService(moduleSwitch, sharedData, dataService);

            // ServiceProxy 作为"当前服务实例持有者"接缝：
            // 常规 DI 消费者经 IASampleService 获取本代实例；
            // 需要长期持有/轮询当前实例的一方经 ServiceProxy 拿到 Target。
            var proxy = new ServiceProxy<IASampleService>();
            proxy.SetTarget(service);

            container.RegisterInstance(proxy, IfAlreadyRegistered.Replace);
            container.RegisterInstance<IASampleService>(service, IfAlreadyRegistered.Replace);
            Write($"[A] 注册 IASampleService, Hash={service.GetHashCode()}");
        }

        private void InitializeService(IContainerProvider containerProvider)
        {
            var service = containerProvider.Resolve<IASampleService>();
            service.Start();
            Write($"[A] 服务已解析并启动 Hash={service.GetHashCode()}");
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
                    Write("[A] 已释放 IASampleService");
                }
                Write("[A] Module.Dispose 完成");
            }
            catch (Exception ex)
            {
                Write($"[A] Module.Dispose 异常: {ex.Message}");
            }
        }
    }
}