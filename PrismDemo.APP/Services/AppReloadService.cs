using Prism.Regions;
using PrismDemo.Core.Configuration;
using PrismDemo.Core.Interfaces;
using PrismDemo.Core.Models;
using PrismDemo.Core.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PrismDemo.APP.Services
{
    /// <summary>
    /// 系统重新加载服务
    ///
    /// 在不关闭窗口、不卸载模块的前提下，按"重新启动"的顺序重载核心资源：
    ///   停止采集 → 重读 core.config.json → 切换OPC端点 → 清理共享数据 → 强制GC
    ///   → 重连OPC → 重读 ConnectConfig 表并启动数据循环 → 强制重建首页刷新界面
    ///
    /// 注意：请在后台线程（Task.Run）中调用 ReloadAsync，避免阻塞 UI 线程。
    /// </summary>
    public class AppReloadService
    {
        private readonly APPMainService _appMainService;
        private readonly IDatabaseService _databaseService;
        private readonly SharedDataModel _sharedData;
        private readonly OpcServices _opcService;
        private readonly AlarmService _alarmService;
        private readonly IRegionManager _regionManager;
        private readonly SemaphoreSlim _reloadLock = new SemaphoreSlim(1, 1);

        public AppReloadService(
            APPMainService appMainService,
            IDatabaseService databaseService,
            SharedDataModel sharedData,
            OpcServices opcService,
            AlarmService alarmService,
            IRegionManager regionManager)
        {
            _appMainService = appMainService ?? throw new ArgumentNullException(nameof(appMainService));
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _sharedData = sharedData ?? throw new ArgumentNullException(nameof(sharedData));
            _opcService = opcService ?? throw new ArgumentNullException(nameof(opcService));
            _alarmService = alarmService ?? throw new ArgumentNullException(nameof(alarmService));
            _regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
        }

        /// <summary>
        /// 执行系统重新加载。
        /// </summary>
        /// <param name="progress">进度回调（任意线程）</param>
        /// <returns>错误信息列表，空集合表示全部成功</returns>
        public async Task<List<string>> ReloadAsync(IProgress<string> progress)
        {
            var errors = new List<string>();
            await _reloadLock.WaitAsync();
            try
            {
                Report(progress, "正在停止采集服务...");
                try
                {
                    _appMainService.Stop();
                    await _databaseService.StopMonitoringAsync();
                }
                catch (Exception ex)
                {
                    errors.Add("停止采集失败: " + ex.Message);
                    Debug.WriteLine("[Reload] 停止采集异常: " + ex);
                }

                Report(progress, "正在重新加载配置文件 (core.config.json)...");
                try
                {
                    Config.Initialize();
                    _opcService.Reconfigure(Config.OpcEndpoint);
                }
                catch (Exception ex)
                {
                    errors.Add("重新加载配置失败: " + ex.Message);
                    Debug.WriteLine("[Reload] 重载配置异常: " + ex);
                }

                Report(progress, "正在清理旧数据引用...");
                _sharedData.ConnectDataList = new List<ConnectData>();
                _sharedData.CollectedData = new Dictionary<string, ConnectData>();
                _sharedData.Data.Clear();
                _alarmService.Reset();

                Report(progress, "正在释放旧资源...");
                for (int i = 0; i < 3; i++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    await Task.Delay(50);
                }

                Report(progress, "正在重新连接OPC...");
                try
                {
                    await Task.Run(() => _opcService.Open(30000));
                    Debug.WriteLine("[Reload] OPC 重连完成");
                }
                catch (Exception ex)
                {
                    errors.Add("OPC 重连失败: " + ex.Message);
                    Debug.WriteLine("[Reload] OPC 重连异常: " + ex);
                }

                Report(progress, "正在重新读取 ConnectConfig 配置...");
                try
                {
                    _appMainService.Start();
                }
                catch (Exception ex)
                {
                    errors.Add("启动主服务失败: " + ex.Message);
                    Debug.WriteLine("[Reload] 启动主服务异常: " + ex);
                }

                Report(progress, "正在刷新界面...");
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        var region = _regionManager.Regions["ContentRegion"];
                        region.RemoveAll();
                        _regionManager.RequestNavigate("ContentRegion", "HomePage");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[Reload] 界面刷新异常: " + ex);
                    }
                });

                Report(progress,
                    errors.Count > 0
                        ? "重新加载完成（部分步骤异常，请查看日志）"
                        : "重新加载完成");
                return errors;
            }
            finally
            {
                _reloadLock.Release();
            }
        }

        private static void Report(IProgress<string> progress, string message)
        {
            progress?.Report(message);
            Debug.WriteLine("[Reload] " + message);
        }
    }
}
