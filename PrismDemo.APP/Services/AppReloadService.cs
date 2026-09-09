using Prism.Regions;
using PrismDemo.Core.Configuration;
using PrismDemo.Core.Models;
using PrismDemo.Core.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using static PrismDemo.Core.Services.Log;

namespace PrismDemo.APP.Services
{
    /// <summary>
    /// 整系统重载服务（框架演示版，对应真实工程 AppReloadService）。
    /// 真实工程还会重连 OPC/IGS、重读数据库连接配置等业务步骤；
    /// Demo 蒸馏为：重读 core.config.json → 清理共享数据 → 强制 GC → 重建界面回首页。
    /// 注意：请在后台线程（Task.Run）中调用 ReloadAsync，避免阻塞 UI 线程。
    /// </summary>
    public class AppReloadService
    {
        private readonly SharedDataModel _sharedData;
        private readonly IRegionManager _regionManager;
        private readonly SemaphoreSlim _reloadLock = new(1, 1);

        public AppReloadService(SharedDataModel sharedData, IRegionManager regionManager)
        {
            _sharedData = sharedData ?? throw new ArgumentNullException(nameof(sharedData));
            _regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
        }

        /// <summary>执行系统重新加载，返回错误信息列表（空即全部成功）。</summary>
        public async Task<List<string>> ReloadAsync(IProgress<string> progress)
        {
            var errors = new List<string>();
            await _reloadLock.WaitAsync();
            try
            {
                Report(progress, "正在重新加载配置文件 (core.config.json)...");
                try
                {
                    ConfigLoader.Initialize();
                }
                catch (Exception ex)
                {
                    errors.Add("重新加载配置失败: " + ex.Message);
                }

                Report(progress, "正在清理共享数据...");
                try { _sharedData.Data.Clear(); }
                catch { }

                Report(progress, "正在释放旧资源...");
                for (int i = 0; i < 3; i++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    await Task.Delay(50);
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
                        Write($"[Reload] 界面刷新异常: {ex.Message}");
                    }
                });

                Report(progress, errors.Count > 0 ? "重新加载完成（部分步骤异常）" : "重新加载完成");
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
            Write("[Reload] " + message);
        }
    }
}