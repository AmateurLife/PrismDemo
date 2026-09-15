using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using PrismDemo.Core.Interfaces;
using PrismDemo.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using static PrismDemo.Core.Services.AlertLogger;

namespace PrismDemo.APP.ViewModels
{
    public class SettingAlarmViewModel : BindableBase, INavigationAware
    {
        private readonly IRegionManager _regionManager;
        private readonly IDatabaseService _databaseService;

        private ObservableCollection<AlarmConfig> _configs;
        public ObservableCollection<AlarmConfig> Configs
        {
            get => _configs;
            set => SetProperty(ref _configs, value);
        }

        private AlarmConfig _selectedConfig;
        public AlarmConfig SelectedConfig
        {
            get => _selectedConfig;
            set => SetProperty(ref _selectedConfig, value);
        }

        public DelegateCommand GoBackCommand { get; }
        public DelegateCommand AddCommand { get; }
        public DelegateCommand SaveCommand { get; }
        public DelegateCommand DeleteCommand { get; }
        public DelegateCommand RefreshCommand { get; }

        public SettingAlarmViewModel(IRegionManager regionManager, IDatabaseService databaseService)
        {
            _regionManager = regionManager;
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));

            GoBackCommand = new DelegateCommand(() =>
                _regionManager.RequestNavigate("ContentRegion", "Setting"));

            AddCommand = new DelegateCommand(AddConfig);
            SaveCommand = new DelegateCommand(async () => await SaveConfigAsync());
            DeleteCommand = new DelegateCommand(async () => await DeleteConfigAsync());
            RefreshCommand = new DelegateCommand(async () => await LoadConfigsAsync());

            Configs = new ObservableCollection<AlarmConfig>();
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            LoadConfigsAsync();
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }

        private async Task LoadConfigsAsync()
        {
            try
            {
                Debug.WriteLine("[SettingAlarm] 开始加载报警配置...");
                var items = await _databaseService.GetAlarmConfigsAsync();
                Configs = new ObservableCollection<AlarmConfig>(items);
                Debug.WriteLine($"[SettingAlarm] 加载完成，共 {Configs.Count} 条");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingAlarm] 加载失败: {ex.Message}");
            }
        }

        private void AddConfig()
        {
            var newConfig = new AlarmConfig
            {
                ConnectName = "新配置",
                AlarmLevel = 1,
                IsEnabled = 1,
                UpdateTime = DateTime.Now,
                UpperLimitTrigger = false,
                LowerLimitTrigger = false,
                MutationThresholdTrigger = false,
                UnchangedThresholdTrigger = false
            };
            Configs.Add(newConfig);
            SelectedConfig = newConfig;
        }

        private async Task SaveConfigAsync()
        {
            if (SelectedConfig == null)
            {
                ShowMessageBox("请先选择一条配置");
                return;
            }

            try
            {
                SelectedConfig.UpdateTime = DateTime.Now;

                if (SelectedConfig.Id == 0)
                {
                    var newId = await _databaseService.InsertAlarmConfigAsync(SelectedConfig);
                    SelectedConfig.Id = newId;
                    Debug.WriteLine($"[SettingAlarm] 新增配置成功 id={newId}");
                }
                else
                {
                    await _databaseService.UpdateAlarmConfigAsync(SelectedConfig);
                    Debug.WriteLine($"[SettingAlarm] 更新配置成功 id={SelectedConfig.Id}");
                }

                ShowMessageBox("保存成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingAlarm] 保存失败: {ex.Message}");
                ShowMessageBox("保存失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteConfigAsync()
        {
            if (SelectedConfig == null)
            {
                ShowMessageBox("请先选择一条配置");
                return;
            }

            if (SelectedConfig.Id == 0)
            {
                Configs.Remove(SelectedConfig);
                SelectedConfig = null;
                return;
            }

            if (ShowMessageBox($"确认删除配置 \"{SelectedConfig.ConnectName}\"？", "确认", 
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                await _databaseService.DeleteAlarmConfigAsync(SelectedConfig.Id);
                Configs.Remove(SelectedConfig);
                SelectedConfig = null;
                Debug.WriteLine($"[SettingAlarm] 删除配置成功");
                ShowMessageBox("删除成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingAlarm] 删除失败: {ex.Message}");
                ShowMessageBox("删除失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
