using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using PrismDemo.APP.Services;
using PrismDemo.Core.Configuration;
using PrismDemo.Core.Interfaces;
using PrismDemo.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using static PrismDemo.Core.Services.AlertLogger;

namespace PrismDemo.APP.ViewModels
{
    public class SettingViewModel : BindableBase, INavigationAware
    {
        private readonly IRegionManager _regionManager;
        private readonly IModuleSwitch _moduleSwitch;
        private readonly DynamicModuleManager _dynamicModuleManager;
        private readonly IModuleDeploymentService _deploymentService;
        private readonly AppReloadService _appReloadService;
        private readonly IDatabaseService _databaseService;

        public DelegateCommand BrowseCommand { get; }
        public DelegateCommand ReloadSystemCommand { get; }
        public DelegateCommand BackupDatabaseCommand { get; }
        public DelegateCommand BrowseBackupPathCommand { get; }

        private string _moduleSourcePath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Modules"));
        public string ModuleSourcePath
        {
            get => _moduleSourcePath;
            set => SetProperty(ref _moduleSourcePath, value);
        }
        public ObservableCollection<AvailableModuleInfo> DiscoveredModules { get; } = new();

        public DelegateCommand ScanModulesCommand { get; }
        public DelegateCommand<AvailableModuleInfo> DeployModuleCommand { get; }
        public DelegateCommand<ModuleInfo> ReloadModuleCommand { get; }
        private UserControl? _view;

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

        private string _buildTime;
        public string BuildTime
        {
            get => _buildTime;
            set => SetProperty(ref _buildTime, value);
        }

        private string _backupPath = "";
        public string BackupPath
        {
            get => _backupPath;
            set => SetProperty(ref _backupPath, value);
        }

        private string _filenameFormat = "{数据库名称}_{时间戳}.bak";
        public string FilenameFormat
        {
            get => _filenameFormat;
            set => SetProperty(ref _filenameFormat, value);
        }

        private int _retentionDays = 7;
        public int RetentionDays
        {
            get => _retentionDays;
            set => SetProperty(ref _retentionDays, value);
        }

        private bool _enableCompression = false;
        public bool EnableCompression
        {
            get => _enableCompression;
            set => SetProperty(ref _enableCompression, value);
        }

        private bool _enableVerification = true;
        public bool EnableVerification
        {
            get => _enableVerification;
            set => SetProperty(ref _enableVerification, value);
        }

        private string _backupStatus = "";
        public string BackupStatus
        {
            get => _backupStatus;
            set => SetProperty(ref _backupStatus, value);
        }

        private bool _isBackingUp = false;
        public bool IsBackingUp
        {
            get => _isBackingUp;
            set => SetProperty(ref _isBackingUp, value);
        }

        public void SetView(UserControl view) => _view = view;

        public ObservableCollection<ModuleInfo> AvailableModules { get; } = new();

        public DelegateCommand<string> NavigateToSectionCommand { get; }
        public DelegateCommand ChangeHomePageCmd { get; }
        public DelegateCommand ChangeSettingConnectCmd { get; }
        public DelegateCommand ChangeSettingAlarmCmd { get; }

        public SettingViewModel(
            IRegionManager regionManager,
            IModuleSwitch moduleSwitch,
            IModuleDeploymentService deploymentService,
            DynamicModuleManager dynamicModuleManager,
            AppReloadService appReloadService,
            IDatabaseService databaseService)
        {
            _regionManager = regionManager;
            _moduleSwitch = moduleSwitch;
            _deploymentService = deploymentService;
            _dynamicModuleManager = dynamicModuleManager;
            _appReloadService = appReloadService;
            _databaseService = databaseService;

            NavigateToSectionCommand = new DelegateCommand<string>(ScrollToSection);
            BrowseCommand = new DelegateCommand(OnBrowseCommand);
            ReloadSystemCommand = new DelegateCommand(async () => await ReloadSystemAsync());
            BackupDatabaseCommand = new DelegateCommand(async () => await ExecuteBackupAsync());
            BrowseBackupPathCommand = new DelegateCommand(OnBrowseBackupPath);
            ChangeHomePageCmd = new DelegateCommand(() =>
                _regionManager.RequestNavigate("ContentRegion", "HomePage"));
            ChangeSettingConnectCmd = new DelegateCommand(() =>
                _regionManager.RequestNavigate("ContentRegion", "SettingConnect"));
            ChangeSettingAlarmCmd = new DelegateCommand(() =>
                _regionManager.RequestNavigate("ContentRegion", "SettingAlarm"));

            ScanModulesCommand = new DelegateCommand(async () => await ScanForNewModulesAsync());
            DeployModuleCommand = new DelegateCommand<AvailableModuleInfo>(async (module) => await DeployModuleAsync(module));
            ReloadModuleCommand = new DelegateCommand<ModuleInfo>(async (module) => await ReloadModuleAsync(module));

            BuildTime = GetBuildTime();
            InitializeAvailableModules();
            InitializeBackupSettings();
        }

        private static string GetBuildTime()
        {
            try
            {
                var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var fileInfo = System.IO.File.GetLastWriteTime(assemblyPath);
                return fileInfo.ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch
            {
                return "未知";
            }
        }

        private void OnBrowseCommand()
        {
            // 使用 WinForms FolderBrowserDialog（.NET 8 WPF 支持）
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

            Debug.WriteLine($"[Scan] 扫描路径: {ModuleSourcePath}");
            var modules = await _deploymentService.GetAvailableModulesAsync(ModuleSourcePath);

            Debug.WriteLine($"[Scan] 找到 {modules.Count} 个可部署模块");
            foreach (var m in modules)
            {
                Debug.WriteLine($"  - {m.ModuleName} @ {m.SourcePath}");
            }

            foreach (var m in modules)
                DiscoveredModules.Add(m);
        }

        private async Task DeployModuleAsync(AvailableModuleInfo? module)
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
                ShowMessageBox($"部署失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ReloadModuleAsync(ModuleInfo? module)
        {
            if (module == null) return;

            try
            {
                ReloadStatus = $"正在热重载 {module.Name}...";
                Debug.WriteLine($"[Reload] 开始热重载模块 {module.Name}");

                var dllPath = module.DllPath;
                if (!File.Exists(dllPath))
                {
                    ShowMessageBox($"模块文件不存在: {dllPath}\n请确保模块已编译并部署到 modules/ 目录",
                        "热重载失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ReloadStatus = "热重载失败: 文件不存在";
                    return;
                }

                await _dynamicModuleManager.ReloadModuleAsync(module.Name, dllPath);

                ReloadStatus = $"✅ {module.Name} 热重载成功 ({module.AssemblyVersion})";
                Debug.WriteLine($"[Reload] 模块 {module.Name} 热重载完成");

                InitializeAvailableModules();

                await Task.Delay(3000);
                ReloadStatus = string.Empty;
            }
            catch (Exception ex)
            {
                ReloadStatus = $"热重载失败: {ex.Message}";
                Debug.WriteLine($"[Reload] 热重载异常: {ex}");
                ShowMessageBox($"热重载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ReloadSystemAsync()
        {
            if (IsReloading) return;

            var result = ShowMessageBox(
                "确定要重新加载系统吗？",
                "重新加载系统", MessageBoxButton.YesNo, MessageBoxImage.Question);
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
                Debug.WriteLine($"[Reload] 系统重新加载异常: {ex}");
                ShowMessageBox($"重新加载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsReloading = false;
                await Task.Delay(3000);
                SystemReloadStatus = string.Empty;
            }
        }

        private void InitializeAvailableModules()
        {
            AvailableModules.Clear();

            var modulesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modules");
            if (!Directory.Exists(modulesDir)) return;

            var latestModules = new Dictionary<string, (string FullPath, DateTime Timestamp)>();

            // 第一步：扫描所有 PrismDemo.*.dll，解析模块名和时间戳
            foreach (var dll in Directory.GetFiles(modulesDir, "PrismDemo.*.dll"))
            {
                string fileName = Path.GetFileNameWithoutExtension(dll); // e.g., "PrismDemo.A.202604071556"
                var parts = fileName.Split('.');

                if (parts.Length < 3) continue; // 至少: PrismDemo + ModuleName + Timestamp

                // 提取模块基础名（去掉 PrismDemo. 前缀，但保留中间点，如 "Report.Advanced"）
                // 时间戳必须是最后一部分，且为12位数字
                string lastPart = parts[^1];
                if (lastPart.Length != 12 || !long.TryParse(lastPart, out _)) continue;

                string moduleName = string.Join(".", parts.Skip(1).Take(parts.Length - 2)); // 去掉首(PrismDemo)和尾(时间戳)

                if (DateTime.TryParseExact(lastPart, "yyyyMMddHHmm", null, DateTimeStyles.None, out var ts))
                {
                    // 保留最新版本
                    if (!latestModules.TryGetValue(moduleName, out var existing) || ts > existing.Timestamp)
                    {
                        latestModules[moduleName] = (dll, ts);
                    }
                }
            }

            // 第二步：只为最新版本创建 ModuleInfo
            foreach (var kvp in latestModules)
            {
                string moduleName = kvp.Key;
                string dllPath = kvp.Value.FullPath;

                bool isEnabled = _moduleSwitch.IsModuleEnabled(moduleName);
                var info = new ModuleInfo(moduleName, dllPath, isEnabled);

                // 防重复触发机制（保持不变）
                bool isProcessing = false;
                info.IsEnabledChanged += async (name, enabled) =>
                {
                    if (isProcessing) return;
                    try
                    {
                        isProcessing = true;
                        Debug.WriteLine($"[SettingViewModel] 开始处理模块 {name} 切换，enabled={enabled}");
                        await _moduleSwitch.SetModuleEnabledAsync(name, enabled);

                        // 刷新状态（注意：这里 info 对应的是最新 DLL）
                        info.IsLoaded = _moduleSwitch.IsModuleEnabled(name);
                        info.Status = info.IsLoaded ? "已加载" : "未加载";

                        Debug.WriteLine($"[SettingViewModel] 模块 {name} 处理完成，IsLoaded={info.IsLoaded}");
                    }
                    finally
                    {
                        isProcessing = false;
                    }
                };

                AvailableModules.Add(info);
            }
        }


        // ✅ 删除 LoadModuleAsync 和 UnloadModuleAsync 方法

        private void InitializeBackupSettings()
        {
            BackupPath = Config.DatabaseBackupPath;
        }

        private void OnBrowseBackupPath()
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                SelectedPath = BackupPath,
                Description = "请选择数据库备份目录"
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BackupPath = dialog.SelectedPath;
            }
        }

        private async Task ExecuteBackupAsync()
        {
            if (IsBackingUp) return;

            IsBackingUp = true;
            BackupStatus = "正在备份...";
            try
            {
                var (success, message, filePath) = await Task.Run(() =>
                    _databaseService.BackupDatabaseAsync(
                        BackupPath,
                        EnableCompression,
                        EnableVerification,
                        RetentionDays,
                        FilenameFormat));

                BackupStatus = message;

                if (success)
                {
                    var result = ShowMessageBox($"{message}\n\n是否打开备份文件所在文件夹？", "备份成功", MessageBoxButton.YesNo, MessageBoxImage.Information);
                    if (result == MessageBoxResult.Yes && !string.IsNullOrEmpty(filePath))
                    {
                        var folderPath = Path.GetDirectoryName(filePath);
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = folderPath,
                            UseShellExecute = true
                        });
                    }
                }
                else
                {
                    ShowMessageBox(message, "备份失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                BackupStatus = $"备份异常: {ex.Message}";
                ShowMessageBox($"备份异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBackingUp = false;
            }
        }

        private void ScrollToSection(string sectionName)
        {
            if (_view == null) return;

            var contentRoot = _view.FindName("ContentRoot") as Panel;
            var target = contentRoot?.FindName(sectionName) as UIElement;
            var scroller = _view.FindName("ContentScroller") as ScrollViewer;

            if (target != null && scroller != null)
            {
                var transform = target.TransformToVisual(scroller);
                var position = transform.Transform(new Point(0, 0));
                scroller.ScrollToVerticalOffset(position.Y);
            }
        }

        public void OnNavigatedTo(NavigationContext navigationContext) { }
        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
    }


    // ============================================================
    // ModuleInfo
    // ============================================================
    public class ModuleInfo : BindableBase
    {
        private bool _suppressEvent = false;
        public string Name { get; }
        public string DllPath { get; }

        public event Action<string, bool>? IsEnabledChanged;

        private bool _isLoaded;
        public bool IsLoaded
        {
            get => _isLoaded;
            set => SetProperty(ref _isLoaded, value);
        }

        public string DeployTime { get; }

        private string _assemblyVersion;
        public string AssemblyVersion
        {
            get => _assemblyVersion;
            set => SetProperty(ref _assemblyVersion, value);
        }

        private bool? _isEnabled;
        public bool? IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_suppressEvent)
                {
                    _isEnabled = value;
                    return;
                }

                if (SetProperty(ref _isEnabled, value))
                {
                    Debug.WriteLine($"[ModuleInfo] {Name} IsEnabled changed to {value}");
                    IsEnabledChanged?.Invoke(Name, value ?? false);
                }
            }
        }

        private string _status;
        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public ModuleInfo(string name, string dllPath, bool isEnabled)
        {
            Name = name;
            DllPath = dllPath;

            _suppressEvent = true;
            _isEnabled = isEnabled;
            _isLoaded = isEnabled;
            _status = isEnabled ? "已加载" : "未加载";
            _suppressEvent = false;
            DeployTime = ExtractDeployTimeFromPath(dllPath);
            AssemblyVersion = ReadAssemblyVersion(dllPath);

            RaisePropertyChanged(nameof(IsEnabled));
            RaisePropertyChanged(nameof(IsLoaded));
            RaisePropertyChanged(nameof(Status));
        }

        private static string ExtractDeployTimeFromPath(string dllPath)
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(dllPath);
                var parts = fileName.Split('.');
                if (parts.Length >= 3)
                {
                    string lastPart = parts[^1];
                    if (lastPart.Length == 12 && long.TryParse(lastPart, out _))
                    {
                        if (DateTime.TryParseExact(lastPart, "yyyyMMddHHmm", null, DateTimeStyles.None, out var dt))
                        {
                            return dt.ToString("yyyy-MM-dd HH:mm");
                        }
                    }
                }
            }
            catch
            {
            }
            return "Unknown";
        }

        private static string ReadAssemblyVersion(string dllPath)
        {
            try
            {
                var runtimeAssemblies = Directory.GetFiles(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
                using var mlc = new System.Reflection.MetadataLoadContext(new System.Reflection.PathAssemblyResolver(runtimeAssemblies));
                var assembly = mlc.LoadFromAssemblyPath(dllPath);
                return assembly.GetName().Version?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
    }

}
