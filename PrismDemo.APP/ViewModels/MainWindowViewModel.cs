using Prism.Regions;
using Prism.Commands;
using Prism.Mvvm;

namespace PrismDemo.APP.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        private readonly IRegionManager _regionManager;
        private string _title = "Prism Framework Demo";

        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        public DelegateCommand NavigateHomeCommand { get; }
        public DelegateCommand NavigateModuleACommand { get; }
        public DelegateCommand NavigateModuleBCommand { get; }
        public DelegateCommand NavigateSettingCommand { get; }

        public MainWindowViewModel(IRegionManager regionManager)
        {
            _regionManager = regionManager;

            NavigateHomeCommand = new DelegateCommand(() => _regionManager.RequestNavigate("ContentRegion", nameof(Views.HomePage)));
            NavigateModuleACommand = new DelegateCommand(() => _regionManager.RequestNavigate("ContentRegion", "ModuleA_Home"));
            NavigateModuleBCommand = new DelegateCommand(() => _regionManager.RequestNavigate("ContentRegion", "ModuleB_Home"));
            NavigateSettingCommand = new DelegateCommand(() => _regionManager.RequestNavigate("ContentRegion", nameof(Views.Setting)));
        }
    }
}