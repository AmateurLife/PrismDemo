using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using PrismDemo.Core.Interfaces;
using PrismDemo.A.Configuration;
using PrismDemo.A.Interfaces;
using PrismDemo.A.Services;
using PrismDemo.A.ViewModels;
using PrismDemo.A.Views;
using System;
using System.Diagnostics;

namespace PrismDemo.A
{
    public class AModule : IModule, IDisposable
    {
        private Prism.Ioc.IContainerExtension? _rootContainerExtension;
        private bool _disposed;

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            Debug.WriteLine("[A] ══ RegisterTypes 开始（子容器）══");

            Config.Initialize();

            try
            {
                var newDataService = new ADataService();
                newDataService.EnsureTableStructure();
                containerRegistry.RegisterInstance<ADataService>(newDataService);
                Debug.WriteLine($"[A] 注册 ADataService, Hash={newDataService.GetHashCode()}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[A] ADataService 注册失败（模块不会完全阻塞）: {ex.Message}");
                containerRegistry.RegisterInstance<ADataService>(new ADataService());
            }

            containerRegistry.RegisterForNavigation<Chart, ChartViewModel>("AChart");
            Debug.WriteLine("[A] 已注册导航页面: Chart → AChart");

            containerRegistry.RegisterForNavigation<ChartPage1, ChartPage1ViewModel>("A_ChartPage1");
            containerRegistry.RegisterForNavigation<ChartPage2, ChartPage2ViewModel>("A_ChartPage2");
            containerRegistry.RegisterForNavigation<ChartPage3, ChartPage3ViewModel>("A_ChartPage3");
            containerRegistry.RegisterForNavigation<ChartPage4, ChartPage4ViewModel>("A_ChartPage4");
            Debug.WriteLine("[A] 已注册控制点导航: ChartPage1~4");

            containerRegistry.Register<HistoryChart1>();
            containerRegistry.Register<HistoryChart1ViewModel>();
            containerRegistry.Register<HistoryChart2>();
            containerRegistry.Register<HistoryChart2ViewModel>();
            containerRegistry.Register<HistoryChart3>();
            containerRegistry.Register<HistoryChart3ViewModel>();
            containerRegistry.Register<HistoryChart4>();
            containerRegistry.Register<HistoryChart4ViewModel>();
            Debug.WriteLine("[A] 已注册历史曲线: HistoryChart1~4");

            Debug.WriteLine("[A] ══ RegisterTypes 完成 ══");
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            Debug.WriteLine("[A] ══ OnInitialized 开始（根容器）══");

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

        private void RegisterMainService(IContainerProvider containerProvider, IContainerExtension containerExtension)
        {
            var dataService = containerProvider.Resolve<ADataService>();
            var moduleSwitch = containerProvider.Resolve<IModuleSwitch>();
            var sharedData = containerProvider.Resolve<PrismDemo.Core.Models.SharedDataModel>();
            var opcService = containerProvider.Resolve<IOpcService>();
            var alarmConfigProvider = containerProvider.Resolve<IAlarmConfigProvider>();

            var newService = new AMainService(moduleSwitch, sharedData, dataService, opcService, alarmConfigProvider);

            var serviceProxy = new ServiceProxy<IAService>();
            serviceProxy.SetTarget(newService);
            containerExtension.RegisterInstance<ServiceProxy<IAService>>(serviceProxy);
            containerExtension.RegisterInstance<IAService>(newService);
            containerExtension.RegisterInstance<IWriteableService>(newService);
            Debug.WriteLine($"[A] 注册 IAService, Hash={newService.GetHashCode()}");
        }

        private void InitializeService(IContainerProvider containerProvider)
        {
            var service = containerProvider.Resolve<IAService>();
            Debug.WriteLine($"[A] 服务已解析 Hash={service.GetHashCode()}");

            if (service is AMainService concreteService)
            {
                concreteService.EnsureInitialized();
                Debug.WriteLine("[A] 服务冷启动完成");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                Debug.WriteLine("[A] Module.Dispose 开始");

                var mainService = _rootContainerExtension?.Resolve<IAService>();
                if (mainService is IDisposable d)
                {
                    d.Dispose();
                    Debug.WriteLine("[A] 已释放 IAService");
                }

                Debug.WriteLine("[A] Module.Dispose 完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[A] Module.Dispose 异常: {ex.Message}");
            }
        }
    }
}
