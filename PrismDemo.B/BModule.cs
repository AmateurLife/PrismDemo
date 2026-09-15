using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using PrismDemo.Core.Interfaces;
using PrismDemo.B.Configuration;
using PrismDemo.B.Interfaces;
using PrismDemo.B.Services;
using PrismDemo.B.ViewModels;
using PrismDemo.B.Views;
using System;
using System.Diagnostics;

namespace PrismDemo.B
{
    public class BModule : IModule, IDisposable
    {
        private Prism.Ioc.IContainerExtension? _rootContainerExtension;
        private bool _disposed;

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            Debug.WriteLine("[B] ══ RegisterTypes 开始（子容器）══");

            Config.Initialize();

            try
            {
                var newDataService = new BDataService();
                newDataService.EnsureTableStructure();
                containerRegistry.RegisterInstance<BDataService>(newDataService);
                Debug.WriteLine($"[B] 注册 BDataService, Hash={newDataService.GetHashCode()}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[B] BDataService 注册失败（模块不会完全阻塞）: {ex.Message}");
                containerRegistry.RegisterInstance<BDataService>(new BDataService());
            }

            containerRegistry.RegisterForNavigation<Chart, ChartViewModel>("BChart");
            Debug.WriteLine("[B] 已注册导航页面: Chart → BChart");

            containerRegistry.RegisterForNavigation<ChartPage1, ChartPage1ViewModel>("B_ChartPage1");
            containerRegistry.RegisterForNavigation<ChartPage2, ChartPage2ViewModel>("B_ChartPage2");
            containerRegistry.RegisterForNavigation<ChartPage3, ChartPage3ViewModel>("B_ChartPage3");
            containerRegistry.RegisterForNavigation<ChartPage4, ChartPage4ViewModel>("B_ChartPage4");
            Debug.WriteLine("[B] 已注册控制点导航: ChartPage1~4");

            containerRegistry.Register<HistoryChart1>();
            containerRegistry.Register<HistoryChart1ViewModel>();
            containerRegistry.Register<HistoryChart2>();
            containerRegistry.Register<HistoryChart2ViewModel>();
            containerRegistry.Register<HistoryChart3>();
            containerRegistry.Register<HistoryChart3ViewModel>();
            containerRegistry.Register<HistoryChart4>();
            containerRegistry.Register<HistoryChart4ViewModel>();
            Debug.WriteLine("[B] 已注册历史曲线: HistoryChart1~4");

            Debug.WriteLine("[B] ══ RegisterTypes 完成 ══");
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            Debug.WriteLine("[B] ══ OnInitialized 开始（根容器）══");

            try
            {
                IContainerExtension containerExtension;
                if (containerProvider is IContainerExtension ext)
                    containerExtension = ext;
                else
                    containerExtension = containerProvider.Resolve<IContainerExtension>();

                _rootContainerExtension = containerExtension;

                RegisterMainService(containerProvider, containerExtension);

                // 自动启用A剂模块开关，使后台循环启动
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
            var sharedData = containerProvider.Resolve<PrismDemo.Core.Models.SharedDataModel>();
            var opcService = containerProvider.Resolve<IOpcService>();
            var alarmConfigProvider = containerProvider.Resolve<IAlarmConfigProvider>();

            var newService = new BMainService(moduleSwitch, sharedData, dataService, opcService, alarmConfigProvider);

            var serviceProxy = new ServiceProxy<IBService>();
            serviceProxy.SetTarget(newService);
            containerExtension.RegisterInstance<ServiceProxy<IBService>>(serviceProxy);
            containerExtension.RegisterInstance<IBService>(newService);
            containerExtension.RegisterInstance<IWriteableService>(newService);
            Debug.WriteLine($"[B] 注册 IBService, Hash={newService.GetHashCode()}");
        }

        private void InitializeService(IContainerProvider containerProvider)
        {
            var service = containerProvider.Resolve<IBService>();
            Debug.WriteLine($"[B] 服务已解析 Hash={service.GetHashCode()}");

            if (service is BMainService concreteService)
            {
                concreteService.EnsureInitialized();
                Debug.WriteLine("[B] 服务冷启动完成");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                Debug.WriteLine("[B] Module.Dispose 开始");

                var mainService = _rootContainerExtension?.Resolve<IBService>();
                if (mainService is IDisposable d)
                {
                    d.Dispose();
                    Debug.WriteLine("[B] 已释放 IBService");
                }

                Debug.WriteLine("[B] Module.Dispose 完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[B] Module.Dispose 异常: {ex.Message}");
            }
        }
    }
}
