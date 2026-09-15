using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Linq;
using System.Windows;
using static PrismDemo.Core.Services.AlertLogger;

namespace PrismDemo.B.ViewModels
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

        private void NavigateToControlPoint(string viewName)
        {
            var region = _regionManager.Regions["ControlPointRegion"];
            if (region != null)
            {
                ClearControlPointViews();

                _regionManager.RequestNavigate("ControlPointRegion", $"B_{viewName}");
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
                if (view is FrameworkElement fe && fe.DataContext is IDisposable disposable)
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
            ClearControlPointViews();
            WriteLog("[B Chart] ViewModel Dispose，已级联释放控制点视图");
        }
    }
}
