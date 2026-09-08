using Prism.Mvvm;
using PrismDemo.A.Configuration;
using PrismDemo.A.Interfaces;
using PrismDemo.A.Models;
using PrismDemo.A.Services;
using PrismDemo.Core.Interfaces;
using PrismDemo.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;

namespace PrismDemo.A.Services
{
    /// <summary>
    /// 模块 A 主服务：聚合各依赖、向 UI/共享数据发布示例数据。
    /// 框架演示版对应真实工程的 ClMainService。
    /// </summary>
    public class SampleMainService : BindableBase, IASampleService, IDisposable
    {
        private readonly IModuleSwitch _moduleSwitch;
        private readonly SharedDataModel _sharedData;
        private readonly SampleDataService _dataService;
        private DispatcherTimer _timer;
        private bool _disposed;

        public string ModuleName => "A";

        public ObservableCollection<SampleItem> Items { get; } = new();

        private double _latestValue;
        public double LatestValue
        {
            get => _latestValue;
            set => SetProperty(ref _latestValue, value);
        }

        public SampleMainService(IModuleSwitch moduleSwitch, SharedDataModel sharedData, SampleDataService dataService)
        {
            _moduleSwitch = moduleSwitch;
            _sharedData = sharedData;
            _dataService = dataService;

            for (int i = 1; i <= 5; i++)
            {
                Items.Add(new SampleItem
                {
                    Index = i,
                    Name = $"示例点 {i}",
                    Value = 0
                });
            }

            _moduleSwitch.SwitchChanged += OnModuleSwitchChanged;
        }

        public void Start()
        {
            if (_timer != null) return;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(Math.Max(1, Config.RefreshSeconds))
            };
            _timer.Tick += OnTimerTick;
            _timer.Start();
            Debug.WriteLine("[A.MainService] 定时刷新已启动");
        }

        public void Stop()
        {
            if (_timer == null) return;

            _timer.Stop();
            _timer = null;
            Debug.WriteLine("[A.MainService] 定时刷新已停止");
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            if (!_moduleSwitch.IsModuleEnabled(ModuleName)) return;
            if (_disposed || Items.Count == 0) return;

            var value = _dataService.NextValue(Config.SampleLower, Config.SampleUpper);
            LatestValue = value;

            var item = Items[new Random().Next(Items.Count)];
            item.Value = value;

            _sharedData.SetValue($"A:LatestValue", value);
        }

        private void OnModuleSwitchChanged(string moduleName, bool enabled)
        {
            if (!string.Equals(moduleName, ModuleName, StringComparison.OrdinalIgnoreCase)) return;

            if (enabled) Start();
            else Stop();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _moduleSwitch.SwitchChanged -= OnModuleSwitchChanged;
            Stop();
            _sharedData.SetValue("A:LatestValue", (object)null);
            Debug.WriteLine("[A.MainService] Dispose 完成");
        }
    }
}