// PrismDemo.APP/ViewModels/MainWindowViewModel.cs
// 此类是主窗口的ViewModel，负责处理主窗口的逻辑和数据绑定。
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using PrismDemo.APP.Views;
using PrismDemo.Core.Configuration;
using PrismDemo.Core.Interfaces;

namespace PrismDemo.APP.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {

        private readonly IRegionManager _regionManager;
        private readonly IModuleSwitch _moduleSwitch;
        private string _title = "工业智能控制平台";
        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        /// <summary>
        /// 首页窗口绑定
        /// </summary>
        public DelegateCommand ChangeHomePageCmd { get; set; }
        /// <summary>
        /// 数据报表窗口绑定
        /// </summary>
        public DelegateCommand ChangeReportDataCmd { get; set; }
        /// <summary>
        /// 历史报警窗口绑定
        /// </summary>
        public DelegateCommand ChangeHistoricalAlarmCmd { get; set; }
        /// <summary>
        /// 加药控制窗口绑定
        /// </summary>
        public DelegateCommand ChangeChartPageCmd { get; set; }
        /// <summary>
        /// 设置窗口绑定
        /// </summary>
        public DelegateCommand ChangeSettingCmd { get; set; }

        public MainWindowViewModel(IRegionManager regionManager, IModuleSwitch moduleSwitch)
        {
            _regionManager = regionManager;
            _moduleSwitch = moduleSwitch;

            ChangeHomePageCmd = new DelegateCommand(ChangeHomePage);
            ChangeReportDataCmd = new DelegateCommand(ChangeReportData);
            ChangeHistoricalAlarmCmd = new DelegateCommand(ChangeHistoricalAlarm);
            ChangeChartPageCmd = new DelegateCommand(ChangeChartPage);
            ChangeSettingCmd = new DelegateCommand(ChangeSetting);
        }

        /// <summary>
        /// 切换到首页
        /// </summary>
        private void ChangeHomePage()
        {
            this._regionManager.RequestNavigate("ContentRegion", "HomePage");
        }
        /// <summary>
        /// 切换到数据报表
        /// </summary>
        private void ChangeReportData()
        {
            this._regionManager.RequestNavigate("ContentRegion", "ReportData");
        }
        /// <summary>
        /// 切换到历史报警
        /// </summary>
        private void ChangeHistoricalAlarm()
        {
            this._regionManager.RequestNavigate("ContentRegion", "HistoricalAlarm");
        }
        /// <summary>
        /// 切换到加药控制
        /// </summary>
        private void ChangeChartPage()
        {
            this._regionManager.RequestNavigate("ContentRegion", "ChartPage");
        }
        /// <summary>
        /// 切换到设置
        /// </summary>
        private void ChangeSetting()
        {
            var dialog = new PasswordDialog(Config.SettingPassword);
            if (dialog.ShowDialog() == true)
            {
                this._regionManager.RequestNavigate("ContentRegion", "Setting");
            }
        }
    }
}
