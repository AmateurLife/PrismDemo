using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Linq;
using System.Windows;
using static PrismDemo.Core.Services.AlertLogger;

namespace PrismDemo.A.ViewModels
{
    public class ChartViewModel : BindableBase, INavigationAware, IDisposable
    {
        private readonly IRegionManager _regionManager;
        private bool _disposed;

        public DelegateCommand<string> SwitchControlPointCmd { get; }

        private int _selectedControlPointIndex;
        public int SelectedControlPointIndex
        {
            get => _selectedControlPointIndex;
            set
            {
                if (SetProperty(ref _selectedControlPointIndex, value))
                    NavigateToControlPoint($"ChartPage{value + 1}");
            }
        }

        public ChartViewModel(IRegionManager regionManager)
        {
            _regionManager = regionManager;

            SwitchControlPointCmd = new DelegateCommand<string>(viewName =>
            {
                switch (viewName)
                {
                    case "ChartPage1": SelectedControlPointIndex = 0; break;
                    case "ChartPage2": SelectedControlPointIndex = 1; break;
                    case "ChartPage3": SelectedControlPointIndex = 2; break;
                    case "ChartPage4": SelectedControlPointIndex = 3; break;
                }
            });
        }

        private bool _navigationInProgress;
        private string _pendingControlPoint;

        private void NavigateToControlPoint(string viewName)
        {
            // 串行化导航：RequestNavigate 是异步的，快速点击会让多次导航交错，
            // 导致新视图在"上一次 ClearControlPointViews"之后才加入 region，其 VM 从未被 Dispose（泄漏）。
            // 导航进行中则记录最新请求，完成后追赶到最新目标。
            if (_navigationInProgress)
            {
                _pendingControlPoint = viewName;
                return;
            }

            var region = _regionManager.Regions["ControlPointRegion"];
            if (region == null) return;

            _navigationInProgress = true;
            _pendingControlPoint = null;
            ClearControlPointViews();

            try
            {
                _regionManager.RequestNavigate("ControlPointRegion", $"A_{viewName}", _ =>
                {
                    // 页面已离开/ChartViewModel 已被 Dispose 后，导航才完成：
                    // 该视图是"孤儿"，加入 region 后无人清理，其 VM 会持有完整数据泄漏。
                    // 这里立即清掉并复位，杜绝孤儿。
                    if (_disposed)
                    {
                        ClearControlPointViews();
                        _navigationInProgress = false;
                        _pendingControlPoint = null;
                        return;
                    }

                    _navigationInProgress = false;
                    if (_pendingControlPoint != null)
                    {
                        var next = _pendingControlPoint;
                        _pendingControlPoint = null;
                        NavigateToControlPoint(next);
                    }
                });
            }
            catch (Exception ex)
            {
                _navigationInProgress = false;
                WriteLog($"[A Chart] 控制点导航失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 移除 ControlPointRegion 中的所有视图，并释放其 ViewModel（停止定时器、退订事件），
        /// 防止 region.Remove 绕过导航生命周期导致的幽灵定时器与 ViewModel 泄漏
        /// </summary>
        private void ClearControlPointViews()
        {
            var region = _regionManager.Regions["ControlPointRegion"];
            if (region == null) return;

            foreach (var view in region.Views.ToList())
            {
                var dataContext = (view as FrameworkElement)?.DataContext;
                if (dataContext is IDisposable disposable)
                    disposable.Dispose();
                region.Remove(view);
            }
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            var index = navigationContext.Parameters.TryGetValue<int>("ControlPointIndex", out var idx) ? idx : 0;
            _selectedControlPointIndex = index;
            RaisePropertyChanged(nameof(SelectedControlPointIndex));
            NavigateToControlPoint($"ChartPage{index + 1}");
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            ClearControlPointViews();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _navigationInProgress = false;
            _pendingControlPoint = null;
            ClearControlPointViews();
            WriteLog("[A Chart] ViewModel Dispose，已级联释放控制点视图");
        }
    }
}
