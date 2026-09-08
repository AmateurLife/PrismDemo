using Prism.Mvvm;
using PrismDemo.B.Interfaces;
using PrismDemo.B.Models;
using PrismDemo.B.Services;
using PrismDemo.Core.Interfaces;
using PrismDemo.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;

namespace PrismDemo.B.Services
{
    /// <summary>模块 B 主服务（框架演示版）。</summary>
    public class BMainService : BindableBase, IBService, IDisposable
    {
        private readonly IModuleSwitch _moduleSwitch;
        private readonly SharedDataModel _sharedData;
        private readonly BDataService _dataService;
        private DispatcherTimer _timer;
        private bool _disposed;

        public string ModuleName => "B";

        public ObservableCollection<SampleEntry> Entries { get; } = new();

        private double _currentValue;
        public double CurrentValue
        {
            get => _currentValue;
            set => SetProperty(ref _currentValue, value);
        }

        public BMainService(IModuleSwitch moduleSwitch, SharedDataModel sharedData, BDataService dataService)
        {
            _moduleSwitch = moduleSwitch;
            _sharedData = sharedData;
            _dataService = dataService;

            for (int i = 1; i <= 4; i++)
            {
                Entries.Add(new SampleEntry { Index = i, Label = $"项 {i}", Progress = 0 });
            }

            _moduleSwitch.SwitchChanged += OnModuleSwitchChanged;
        }

        public void Start()
        {
            if (_timer != null) return;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(100, Configuration.Config.TickMs)) };
            _timer.Tick += OnTimerTick;
            _timer.Start();
            Debug.WriteLine("[B.MainService] 已启动");
        }

        public void Stop()
        {
            if (_timer == null) return;

            _timer.Stop();
            _timer = null;
            Debug.WriteLine("[B.MainService] 已停止");
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            if (_disposed) return;
            if (!_moduleSwitch.IsModuleEnabled(ModuleName)) return;

            var value = _dataService.NextProgress(Configuration.Config.Target);
            CurrentValue = value;

            foreach (var entry in Entries)
            {
                entry.Progress = _dataService.NextProgress(value);
            }

            _sharedData.SetValue($"B:CurrentValue", value);
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
            _sharedData.SetValue("B:CurrentValue", (object)null);
            Debug.WriteLine("[B.MainService] Dispose 完成");
        }
    }
}