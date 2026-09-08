using Prism.Mvvm;
using PrismDemo.APP.Services;
using PrismDemo.Core.Interfaces;
using System.Collections.ObjectModel;

namespace PrismDemo.APP.ViewModels
{
    public class HomePageViewModel : BindableBase
    {
        private readonly DynamicModuleManager _moduleManager;
        private readonly IModuleSwitch _moduleSwitch;

        public ObservableCollection<ModuleStatusItem> Modules { get; } = new();

        public HomePageViewModel(DynamicModuleManager moduleManager, IModuleSwitch moduleSwitch)
        {
            _moduleManager = moduleManager;
            _moduleSwitch = moduleSwitch;

            foreach (var name in _moduleManager.GetLoadedModuleNames())
            {
                Modules.Add(new ModuleStatusItem
                {
                    Name = name,
                    IsLoaded = true,
                    IsEnabled = _moduleSwitch.IsModuleEnabled(name)
                });
            }
        }
    }

    public class ModuleStatusItem
    {
        public string Name { get; set; }
        public bool IsLoaded { get; set; }
        public bool IsEnabled { get; set; }

        public string State => IsLoaded ? (IsEnabled ? "已加载 / 已启用" : "已加载 / 已禁用") : "未加载";
    }
}