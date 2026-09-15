// PrismDemo.APP/ViewModels/ChartPageViewModel.cs
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using PrismDemo.APP.Services;
using PrismDemo.Core.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace PrismDemo.APP.ViewModels
{
    public class ChartPageViewModel : BindableBase, INavigationAware
    {
        #region 常量
        private const string TargetRegionName = "ChartPageRegion";
        private const string ContentRegionName = "ContentRegion";
        #endregion

        #region 内部类型（用于 ComboBox 数据绑定）
        public class ModuleOption
        {
            public string DisplayName { get; set; }
            public string ModuleName { get; set; }

            public ModuleOption(string displayName, string moduleName)
            {
                DisplayName = displayName;
                ModuleName = moduleName;
            }
        }
        #endregion

        #region 字段
        private readonly IRegionManager _regionManager;
        //private readonly DynamicModuleManager _moduleManager;
        private readonly IModuleSwitch _moduleSwitch;

        // 模块导航串行化：RequestNavigate 异步，防止快速切换模块时视图/VM 竞态泄漏
        private bool _moduleNavInProgress;
        private string _pendingModule;

        // 页面是否处于激活状态（离开=页面在导航期间被切走，需清理孤儿模块视图）
        private bool _pageActive;

        // 模块优先级顺序（自动加载时使用）
        private static readonly string[] ModulePriorityOrder =
        {
            "A",
            "B"
            // 可继续添加："OtherModule", ...
        };
        #endregion

        #region 命令
        public DelegateCommand ChangeHomePageCmd { get; }
        public DelegateCommand<string> SwitchModuleCmd { get; }
        #endregion

        #region 属性（UI 绑定）

        // 模块选项列表（供 ComboBox 使用）
        public ObservableCollection<ModuleOption> ModuleOptions { get; } = new()
        {
            new("A剂系统", "A"),
            new("B剂系统", "B")
            // 同步更新此处以支持新模块
        };

        // 当前选中的模块选项（双向绑定）
        private ModuleOption _selectedModuleOption;
        private string _lastSwitchedModule;
        public ModuleOption SelectedModuleOption
        {
            get => _selectedModuleOption;
            set
            {
                if (!SetProperty(ref _selectedModuleOption, value)) return;
                if (value != null && value.ModuleName != _lastSwitchedModule)
                {
                    _lastSwitchedModule = value.ModuleName;
                    SwitchToModule(value.ModuleName);
                }
            }
        }

        // 用于控制“无模块”提示的可见性（可选）
        private bool _hasActiveModule;
        public bool HasActiveModule
        {
            get => _hasActiveModule;
            private set => SetProperty(ref _hasActiveModule, value);
        }
        #endregion

        #region 构造函数
        public ChartPageViewModel(
            IRegionManager regionManager,
            //DynamicModuleManager moduleManager,
            IModuleSwitch moduleSwitch)
        {
            _regionManager = regionManager;
            //_moduleManager = moduleManager;
            _moduleSwitch = moduleSwitch;

            _moduleSwitch.SwitchChanged += OnModuleStateChanged;
            _moduleSwitch.ModuleDisabled += OnModuleDisabled;
            ChangeHomePageCmd = new DelegateCommand(ChangeHomePage);
            SwitchModuleCmd = new DelegateCommand<string>(SwitchToModule);

            Debug.WriteLine($"[ChartPage] ViewModel 构造完成");
        }
        #endregion

        #region 模块状态变更回调
        private void OnModuleStateChanged(string moduleName, bool enabled)
        {
            if (!ModulePriorityOrder.Contains(moduleName)) return;

            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                UpdateActiveView(); // 自动切换回优先级最高的启用模块
            });
        }

        private void OnModuleDisabled(string moduleName)
        {
            if (!ModulePriorityOrder.Contains(moduleName)) return;

            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                Debug.WriteLine($"[ChartPage] 收到模块禁用通知: {moduleName}");
                _moduleNavInProgress = false;
                _pendingModule = null;
                RemoveAllViews();
                HasActiveModule = false;
                SelectedModuleOption = null;
            });
        }
        #endregion

        #region 视图管理核心逻辑

        /// <summary>
        /// 自动激活优先级最高的已启用模块
        /// </summary>
        private void UpdateActiveView()
        {
            var activeModule = FindFirstEnabledModule();
            ActivateModuleInternal(activeModule);
        }

        /// <summary>
        /// 强制激活指定模块（用于手动切换）
        /// </summary>
        private void ForceActivateModule(string moduleName)
        {
            // 可选：确保模块已启用（根据业务需求决定是否取消注释）
            // _moduleSwitch.SetModuleEnabled(moduleName, true);

            ActivateModuleInternal(moduleName);
        }

        /// <summary>
        /// 内部统一激活逻辑（导航串行化：进行中记录待激活模块，完成后追赶到最新目标）
        /// </summary>
        private void ActivateModuleInternal(string moduleName)
        {
            if (_moduleNavInProgress)
            {
                _pendingModule = moduleName;
                return;
            }

            RemoveAllViews();

            if (!string.IsNullOrEmpty(moduleName))
            {
                TryRegisterModuleView(moduleName);
                HasActiveModule = true;

                // 同步更新 SelectedModuleOption（保持 UI 一致）
                var matchedOption = ModuleOptions.FirstOrDefault(opt => opt.ModuleName == moduleName);
                if (matchedOption != null)
                    SelectedModuleOption = matchedOption;
            }
            else
            {
                HasActiveModule = false;
                SelectedModuleOption = null;
            }
        }

        private void FinishModuleNavigation()
        {
            if (_pendingModule != null)
            {
                var next = _pendingModule;
                _pendingModule = null;
                ActivateModuleInternal(next);
            }
        }

        private void RemoveAllViews()
        {
            if (!_regionManager.Regions.ContainsRegionWithName(TargetRegionName))
                return;

            var region = _regionManager.Regions[TargetRegionName];
            foreach (var view in region.Views.ToList())
            {
                var dataContext = (view as FrameworkElement)?.DataContext;
                if (dataContext is IDisposable disposable)
                    disposable.Dispose();
                region.Remove(view);
            }
        }

        private void TryRegisterModuleView(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                FinishModuleNavigation();
                return;
            }

            if (!_moduleSwitch.IsModuleEnabled(moduleName))
            {
                Debug.WriteLine($"[ChartPage] 模块 {moduleName} 已禁用，跳过注册视图");
                RemoveAllViews();
                HasActiveModule = false;
                FinishModuleNavigation();
                return;
            }

            string viewName = $"{moduleName}Chart";

            Debug.WriteLine($"[ChartPage] 尝试导航到: {viewName} 在 Region: {TargetRegionName}");

            _moduleNavInProgress = true;
            _pendingModule = null;
            try
            {
                _regionManager.RequestNavigate(TargetRegionName, viewName, navigationResult =>
                {
                    // 页面已离开后才完成导航：视图是孤儿，加入 region 后无人清理，
                    // 其整个模块视图树（ChartViewModel + 各控制点 VM）会持有完整数据泄漏。
                    // 立即清掉并复位。
                    if (!_pageActive)
                    {
                        RemoveAllViews();
                        _moduleNavInProgress = false;
                        _pendingModule = null;
                        return;
                    }

                    _moduleNavInProgress = false;
                    if (navigationResult.Result.HasValue &&
                        navigationResult.Result.Value &&
                        navigationResult.Error == null)
                    {
                        Debug.WriteLine($"[ChartPage] ✅ 成功激活模块视图: {viewName}");
                    }
                    else
                    {
                        Debug.WriteLine($"[ChartPage] ❌ 导航失败: 视图 '{viewName}' 未注册或发生错误。");
                        if (navigationResult.Error != null)
                        {
                            Debug.WriteLine($"    异常类型: {navigationResult.Error.GetType().Name}");
                            Debug.WriteLine($"    异常消息: {navigationResult.Error.Message}");
                        }
                    }
                    FinishModuleNavigation();
                });
            }
            catch (Exception ex)
            {
                _moduleNavInProgress = false;
                Debug.WriteLine($"[ChartPage] 导航异常: {ex.Message}");
                FinishModuleNavigation();
            }
        }

        private string FindFirstEnabledModule()
        {
            foreach (var name in ModulePriorityOrder)
            {
                if (_moduleSwitch.IsModuleEnabled(name))
                    return name;
            }
            return null;
        }
        #endregion

        #region 页面导航与命令
        private void ChangeHomePage()
        {
            _regionManager.RequestNavigate(ContentRegionName, "HomePage");
        }

        private void SwitchToModule(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName) || !ModulePriorityOrder.Contains(moduleName))
                return;

            ForceActivateModule(moduleName);
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _pageActive = true;
            // 首次进入：按优先级加载首个启用模块
            UpdateActiveView();
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            // 离开图表页时清理模块视图，停止子页定时器，防止后台持续运行泄漏
            _pageActive = false;
            RemoveAllViews();
        }
        #endregion
    }
}