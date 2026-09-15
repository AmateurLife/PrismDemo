using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using PrismDemo.Core.Interfaces;
using PrismDemo.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PrismDemo.APP.ViewModels
{
    public class HistoricalAlarmViewModel : BindableBase, INavigationAware
    {
        private readonly IRegionManager _regionManager;
        private readonly IDatabaseService _databaseService;

        public DelegateCommand<AlarmRecord> ShowAlarmDetailCmd { get; set; }
        public DelegateCommand ChangeHomePageCmd { get; set; }
        public DelegateCommand QueryCmd { get; set; }
        public DelegateCommand PreviousPageCmd { get; set; }
        public DelegateCommand NextPageCmd { get; set; }

        private DateTime? _startTime;
        public DateTime? StartTime
        {
            get => _startTime;
            set => SetProperty(ref _startTime, value);
        }

        private DateTime? _endTime;
        public DateTime? EndTime
        {
            get => _endTime;
            set => SetProperty(ref _endTime, value);
        }

        private string _selectedAlarmType = "全部";
        public string SelectedAlarmType
        {
            get => _selectedAlarmType;
            set => SetProperty(ref _selectedAlarmType, value);
        }

        public ObservableCollection<string> AlarmTypes { get; set; }

        private ObservableCollection<AlarmRecord> _alarmRecords;
        public ObservableCollection<AlarmRecord> AlarmRecords
        {
            get => _alarmRecords;
            set => SetProperty(ref _alarmRecords, value);
        }

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                if (SetProperty(ref _currentPage, value))
                    RaisePropertyChanged(nameof(PageInfo));
            }
        }

        private int _totalPages;
        public int TotalPages
        {
            get => _totalPages;
            set
            {
                if (SetProperty(ref _totalPages, value))
                    RaisePropertyChanged(nameof(PageInfo));
            }
        }

        private const int PAGE_SIZE = 20;

        public string PageInfo => TotalPages > 0
            ? $"第 {CurrentPage} 页 / 共 {TotalPages} 页"
            : "暂无数据";

        public HistoricalAlarmViewModel(IRegionManager regionManager, IDatabaseService databaseService)
        {
            _regionManager = regionManager;
            _databaseService = databaseService;

            ChangeHomePageCmd = new DelegateCommand(ChangeHomePage);
            QueryCmd = new DelegateCommand(async () => await LoadAlarmsAsync());
            ShowAlarmDetailCmd = new DelegateCommand<AlarmRecord>(ShowAlarmDetail);
            PreviousPageCmd = new DelegateCommand(async () => await GoToPageAsync(CurrentPage - 1),
                () => CurrentPage > 1);
            NextPageCmd = new DelegateCommand(async () => await GoToPageAsync(CurrentPage + 1),
                () => CurrentPage < TotalPages);

            AlarmRecords = new ObservableCollection<AlarmRecord>();
            AlarmTypes = new ObservableCollection<string> { "全部", "越上限", "越下限", "突变", "不变" };

            EndTime = DateTime.Now;
            StartTime = DateTime.Now.AddDays(-7);

            LoadAlarmsAsync();
        }

        private async Task LoadAlarmsAsync()
        {
            try
            {
                Debug.WriteLine("[HistoricalAlarm] 开始加载报警记录...");
                var adjustedEnd = EndTime?.Date.AddDays(1).AddSeconds(-1);
                var typeFilter = SelectedAlarmType == "全部" ? null : SelectedAlarmType;
                var (records, totalCount) = await _databaseService.QueryAlarmHistoryAsync(
                    StartTime, adjustedEnd, typeFilter, CurrentPage, PAGE_SIZE);

                AlarmRecords = new ObservableCollection<AlarmRecord>(records);
                TotalPages = (int)Math.Ceiling((double)totalCount / PAGE_SIZE);

                Debug.WriteLine($"[HistoricalAlarm] 加载完成，共 {totalCount} 条，第 {CurrentPage}/{TotalPages} 页");
                RefreshPagingCommandState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HistoricalAlarm] 加载失败: {ex.Message}");
            }
        }

        private async Task GoToPageAsync(int page)
        {
            if (page < 1 || page > TotalPages) return;
            CurrentPage = page;
            await LoadAlarmsAsync();
        }

        private void RefreshPagingCommandState()
        {
            PreviousPageCmd.RaiseCanExecuteChanged();
            NextPageCmd.RaiseCanExecuteChanged();
        }

        private void ChangeHomePage()
        {
            _regionManager.RequestNavigate("ContentRegion", "HomePage");
        }

        private void ShowAlarmDetail(AlarmRecord record)
        {
            if (record == null) return;
            var parameters = new NavigationParameters { { "AlarmRecord", record } };
            _regionManager.RequestNavigate("ContentRegion", "AlarmDetail", parameters);
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            Debug.WriteLine("导航到历史报警页面");
            LoadAlarmsAsync();
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
    }
}
