using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using PrismDemo.APP.Services;
using PrismDemo.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace PrismDemo.APP.ViewModels
{
    /// <summary>
    /// 设置页（模块管理 + 关于 + 整系统重载）。
    /// 对应真实工程的 SettingViewModel，砍掉数据库备份/连接配置等业务段。
    /// </summary>
    public class SettingViewModel : BindableBase, INavigationAware
    {
        private readonly IRegionManager _regionManager;
        private readonly IModuleSwitch _moduleSwitch;
        private readonly DynamicModuleManager _dynamicModuleManager;
        private readonly IModuleDeploymentService _deploymentService;
        private readonly AppReloadService _appReloadService;

        public DelegateCommand BrowseCommand { get; }
        public DelegateCommand ScanModulesCommand { get; }
        public DelegateCommand<AvailableModuleInfo> DeployModuleCommand { get; }
        public DelegateCommand<ModuleInfo> ReloadModuleCommand { get; }
        public DelegateCommand ReloadSystemCommand { get; }
        public DelegateCommand ChangeHomePageCmd { get; }
        public DelegateCommand<string> NavigateToSectionCommand { get; }

        private string _moduleSourcePath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Modules"));
        public string ModuleSourcePath
        {
            get => _moduleSourcePath;
            set => SetProperty(ref _moduleSourcePath, value);
        }

        public ObservableCollection<AvailableModuleInfo> DiscoveredModules { get; } = new();
        public ObservableCollection<ModuleInfo> AvailableModules { get; } = new();

        private string _reloadStatus;
        public string ReloadStatus
        {
            get => _reloadStatus;
            set => SetProperty(ref _reloadStatus, value);
        }

        private string _systemReloadStatus;
        public string SystemReloadStatus
        {
            get => _systemReloadStatus;
            set => SetProperty(ref _systemReloadStatus, value);
        }

        private bool _isReloading;
        public bool IsReloading
        {
            get => _isReloading;
            set => SetProperty(ref _isReloading, value);
        }

        public SettingViewModel(
            IRegionManager regionManager,
            IModuleSwitch moduleSwitch,
            IModuleDeploymentService deploymentService,
            DynamicModuleManager dynamicModuleManager,
            AppReloadService appReloadService)
        {
            _regionManager = regionManager;
            _moduleSwitch = moduleSwitch;
            _deploymentService = deploymentService;
            _dynamicModuleManager = dynamicModuleManager;
            _appReloadService = appReloadService;

            ScanModulesCommand = new DelegateCommand(async () => await ScanForNewModulesAsync());
            DeployModuleCommand = new DelegateCommand<AvailableModuleInfo>(async (m) => await DeployModuleAsync(m));
            ReloadModuleCommand = new DelegateCommand<ModuleInfo>(async (m) => await ReloadModuleAsync(m));
            ReloadSystemCommand = new DelegateCommand(async () => await ReloadSystemAsync());
            BrowseCommand = new DelegateCommand(OnBrowse);
            ChangeHomePageCmd = new DelegateCommand(() => _regionManager.RequestNavigate("ContentRegion", nameof(Views.HomePage)));
            NavigateToSectionCommand = new DelegateCommand<string>(ScrollToSection);

            BuildTime = GetBuildTime();
            InitializeAvailableModules();
        }

        private string _buildTime;
        public string BuildTime
        {
            get => _buildTime;
            set => SetProperty(ref _buildTime, value);
        }

        private static string GetBuildTime()
        {
            try
            {
                return File.GetLastWriteTime(System.Reflection.Assembly.GetExecutingAssembly().Location)
                    .ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch
            {
                return "未知";
            }
        }

        private void OnBrowse()
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                SelectedPath = ModuleSourcePath,
                Description = "请选择模块仓库目录"
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ModuleSourcePath = dialog.SelectedPath;
            }
        }

        private async Task ScanForNewModulesAsync()
        {
            DiscoveredModules.Clear();
            var modules = await _deploymentService.GetAvailableModulesAsync(ModuleSourcePath);
            Debug.WriteLine($"[Scan] 找到 {modules.Count} 个可部署模块");
            foreach (var m in modules)
                DiscoveredModules.Add(m);
        }

        private async Task DeployModuleAsync(AvailableModuleInfo module)
        {
            if (module == null) return;

            try
            {
                await _deploymentService.DeployModuleAsync(module.SourcePath, module.ModuleName);

                var deployedModules = _deploymentService.GetLatestDeployedModules();
                var latest = deployedModules.FirstOrDefault(m => m.ModuleName == module.ModuleName);
                if (latest == null) throw new InvalidOperationException("部署后未找到模块");

                await _dynamicModuleManager.UpdateModuleAsync(module.ModuleName, latest.FilePath);

                InitializeAvailableModules();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"部署失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ReloadModuleAsync(ModuleInfo module)
        {
            if (module == null) return;

            try
            {
                ReloadStatus = $"正在热重载 {module.Name}...";
                Debug.WriteLine($"[Reload] 开始热重载模块 {module.Name}");

                if (!File.Exists(module.DllPath))
                {
                    MessageBox.Show($"模块文件不存在: {module.DllPath}\n请确保模块已编译并部署到 modules/ 目录",
                        "热重载失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ReloadStatus = "热重载失败: 文件不存在";
                    return;
                }

                await _dynamicModuleManager.ReloadModuleAsync(module.Name, module.DllPath);

                ReloadStatus = $"✅ {module.Name} 热重载成功 ({module.AssemblyVersion})";
                InitializeAvailableModules();

                await Task.Delay(3000);
                ReloadStatus = string.Empty;
            }
            catch (Exception ex)
            {
                ReloadStatus = $"热重载失败: {ex.Message}";
                MessageBox.Show($"热重载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ReloadSystemAsync()
        {
            if (IsReloading) return;

            var result = MessageBox.Show("确定要重新加载系统吗？", "重新加载系统",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            IsReloading = true;
            SystemReloadStatus = "正在重新加载系统...";
            try
            {
                var progress = new Progress<string>(msg => SystemReloadStatus = msg);
                var errors = await Task.Run(() => _appReloadService.ReloadAsync(progress));

                SystemReloadStatus = errors.Count > 0
                    ? $"重新加载完成，但存在 {errors.Count} 项异常，请查看日志"
                    : "✅ 重新加载完成";

                InitializeAvailableModules();
            }
            catch (Exception ex)
            {
                SystemReloadStatus = $"重新加载失败: {ex.Message}";
                MessageBox.Show($"重新加载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsReloading = false;
                await Task.Delay(3000);
                SystemReloadStatus = string.Empty;
            }
        }

        private void ScrollToSection(string sectionName)
        {
            // 保持简单：demo 不使用大型滚动区域
        }

        /// <summary>
        /// 扫描 modules 目录，为每个模块保留最新版本，生成 ModuleInfo 列表。
        /// </summary>
        private void InitializeAvailableModules()
        {
            AvailableModules.Clear();

            var modulesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modules");
            if (!Directory.Exists(modulesDir)) return;

            var latestModules = new Dictionary<string, (string FullPath, DateTime Timestamp)>();

            foreach (var dll in Directory.GetFiles(modulesDir, "PrismDemo.*.dll"))
            {
                string fileName = Path.GetFileNameWithoutExtension(dll);
                var parts = fileName.Split('.');
                if (parts.Length < 3) continue;

                string lastPart = parts[^1];
                if (lastPart.Length != 12 || !long.TryParse(lastPart, out _)) continue;

                string moduleName = string.Join(".", parts.Skip(1).Take(parts.Length - 2));

                if (DateTime.TryParseExact(lastPart, "yyyyMMddHHmm", null, DateTimeStyles.None, out var ts))
                {
                    if (!latestModules.TryGetValue(moduleName, out var existing) || ts > existing.Timestamp)
                        latestModules[moduleName] = (dll, ts);
                }
            }

            foreach (var kvp in latestModules)
            {
                string moduleName = kvp.Key;
                string dllPath = kvp.Value.FullPath;

                bool isEnabled = _moduleSwitch.IsModuleEnabled(moduleName);
                var info = new ModuleInfo(moduleName, dllPath, isEnabled);

                bool isProcessing = false;
                info.IsEnabledChanged += async (name, enabled) =>
                {
                    if (isProcessing) return;
                    try
                    {
                        isProcessing = true;
                        await _moduleSwitch.SetModuleEnabledAsync(name, enabled);
                        info.IsLoaded = _moduleSwitch.IsModuleEnabled(name);
                        info.Status = info.IsLoaded ? "已加载" : "未加载";
                    }
                    finally
                    {
                        isProcessing = false;
                    }
                };

                AvailableModules.Add(info);
            }
        }

        public void OnNavigatedTo(NavigationContext navigationContext) { }
        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
    }
}