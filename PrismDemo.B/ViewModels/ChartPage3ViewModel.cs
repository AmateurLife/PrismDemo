using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Ioc;
using Prism.Regions;
using PrismDemo.Core.Interfaces;
using PrismDemo.Core.Models;
using PrismDemo.B.Interfaces;
using PrismDemo.B.Models;
using PrismDemo.B.Services;
using PrismDemo.B.Views;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Media;
using static PrismDemo.Core.Services.AlertLogger;

namespace PrismDemo.B.ViewModels
{
    public class ChartPage3ViewModel : BindableBase, INavigationAware, IDisposable
    {
        private static readonly (string Field, string Title, string Color, int Axis)
            FlowCfg   = ("TankFlow_2_1_1",        "滤后流量",       "#1976D2", 0),
            OutTurbidityCfg  = ("TankB_2_1_N_1",       "调节池前端B剂浓度",  "#1E90FF", 1),
            InTurbidityCfg   = ("TankB_2_1_N_2",       "调节池后端B剂浓度",  "#FF9800", 1),
            BackACfg = ("MainBManualLL_2_1",   "实时加药量",     "#4CAF50", 2),
            PredACfg = ("PredictMainB_2_1",    "智能加药量",     "#8BC34A", 2),
            PredUnitRateCfg = ("PredictMainBUnitRate_2_1", "智能投加率",  "#FF5722", 2),
            IsAutoCfg  = ("isAutoMainB_2_1",     "是否智能加药",   "#9C27B0", 2);

        private static readonly (string Field, string Title, string Color, int Axis)[] AllCurves =
            { FlowCfg, OutTurbidityCfg, InTurbidityCfg, BackACfg, PredACfg, PredUnitRateCfg, IsAutoCfg };

        private static readonly string[] _tableFields =
            { "TankB_2_1_N_1", "TankB_2_1_N_2", "TankFlow_2_1_1",
              "PredictMainB_2_1", "MainBManualLL_2_1", "PredictMainBUnitRate_2_1" };

        private readonly IBService _bService;
        private readonly BDataService _bDataService;
        private readonly IContainerProvider _container;
        private readonly Dictionary<string, DateTime> _lastTimestamps = new();
        private Timer _refreshTimer;
        private Timer _tableTimer;
        private readonly INotifyPropertyChanged _serviceNpc;
        private readonly PropertyChangedEventHandler _servicePropertyChangedHandler;
        private bool _disposed;
        private bool _updatePending;
        private bool _refreshPending;
        private bool _tablePending;

        /// <summary>曲线最多保留的数据点数（分钟级数据约24小时），防止无限增长</summary>
        private const int MaxCurvePoints = 1440;

        public BData? CurrentData => _bService?.CurrentData;

        public IWriteableService? WriteableService => _bService as IWriteableService;

        public SeriesCollection Series { get; set; } = new();

        public Func<double, string> XLabelFormatter { get; set; }

        public Func<double, string> FlowFormatter { get; set; }

        public Func<double, string> TurbidityFormatter { get; set; }

        public Func<double, string> MixFormatter { get; set; }

        public bool IsFlowChecked     { get => _isFlow;      set => ToggleProp(ref _isFlow,      value, FlowCfg); }
        public bool IsFrontBChecked   { get => _isFrontB;    set => ToggleProp(ref _isFrontB,    value, OutTurbidityCfg); }
        public bool IsBackBChecked    { get => _isBackB;     set => ToggleProp(ref _isBackB,     value, InTurbidityCfg); }
        public bool IsActualBChecked  { get => _isActualB;   set => ToggleProp(ref _isActualB,   value, BackACfg); }
        public bool IsPredictBChecked  { get => _isPredictB;   set => ToggleProp(ref _isPredictB,   value, PredACfg); }
        public bool IsPredictUnitRateChecked { get => _isPredictUnitRate; set => ToggleProp(ref _isPredictUnitRate, value, PredUnitRateCfg); }
        public bool IsAutoChecked   { get => _isAuto;    set => ToggleProp(ref _isAuto,    value, IsAutoCfg); }

        private bool _isFlow, _isFrontB, _isBackB, _isActualB, _isPredictB, _isPredictUnitRate, _isAuto;

        #region 绑定属性

        private bool _ringIsAuto = false;
        public bool RingIsAuto
        {
            get => _ringIsAuto;
            set => SetProperty(ref _ringIsAuto, value);
        }

        private double _complianceRate = 80;
        public double ComplianceRate
        {
            get => _complianceRate;
            set => SetProperty(ref _complianceRate, value);
        }

        private double _complianceRateChange = 0;
        public double ComplianceRateChange
        {
            get => _complianceRateChange;
            set => SetProperty(ref _complianceRateChange, value);
        }

        private double _frontBResidual = 0;
        public double FrontBResidual
        {
            get => _frontBResidual;
            set => SetProperty(ref _frontBResidual, value);
        }

        private double _frontBResidualChange = 0;
        public double FrontBResidualChange
        {
            get => _frontBResidualChange;
            set => SetProperty(ref _frontBResidualChange, value);
        }

        private double _backBResidual = 0;
        public double BackBResidual
        {
            get => _backBResidual;
            set => SetProperty(ref _backBResidual, value);
        }

        private double _backBResidualChange = 0;
        public double BackBResidualChange
        {
            get => _backBResidualChange;
            set => SetProperty(ref _backBResidualChange, value);
        }

        private double _predictB = 0;
        public double PredictB
        {
            get => _predictB;
            set => SetProperty(ref _predictB, value);
        }

        private double _actualB = 0;
        public double ActualB
        {
            get => _actualB;
            set => SetProperty(ref _actualB, value);
        }

        private double _predictBUnitRate = 0;
        public double PredictBUnitRate
        {
            get => _predictBUnitRate;
            set => SetProperty(ref _predictBUnitRate, value);
        }

        private ISeries[] _turbidityStackedSeries = Array.Empty<ISeries>();
        public ISeries[] TurbidityStackedSeries
        {
            get => _turbidityStackedSeries;
            set => SetProperty(ref _turbidityStackedSeries, value);
        }

        private LiveChartsCore.SkiaSharpView.Axis[] _turbidityStackedXAxes = Array.Empty<LiveChartsCore.SkiaSharpView.Axis>();
        public LiveChartsCore.SkiaSharpView.Axis[] TurbidityStackedXAxes
        {
            get => _turbidityStackedXAxes;
            set => SetProperty(ref _turbidityStackedXAxes, value);
        }

        private LiveChartsCore.SkiaSharpView.Axis[] _turbidityStackedYAxes = Array.Empty<LiveChartsCore.SkiaSharpView.Axis>();
        public LiveChartsCore.SkiaSharpView.Axis[] TurbidityStackedYAxes
        {
            get => _turbidityStackedYAxes;
            set => SetProperty(ref _turbidityStackedYAxes, value);
        }

        private ISeries[] _settledTurbiditySeries = Array.Empty<ISeries>();
        public ISeries[] SettledTurbiditySeries
        {
            get => _settledTurbiditySeries;
            set => SetProperty(ref _settledTurbiditySeries, value);
        }

        private LiveChartsCore.SkiaSharpView.Axis[] _settledTurbidityXAxes = Array.Empty<LiveChartsCore.SkiaSharpView.Axis>();
        public LiveChartsCore.SkiaSharpView.Axis[] SettledTurbidityXAxes
        {
            get => _settledTurbidityXAxes;
            set => SetProperty(ref _settledTurbidityXAxes, value);
        }

        private LiveChartsCore.SkiaSharpView.Axis[] _settledTurbidityYAxes = Array.Empty<LiveChartsCore.SkiaSharpView.Axis>();
        public LiveChartsCore.SkiaSharpView.Axis[] SettledTurbidityYAxes
        {
            get => _settledTurbidityYAxes;
            set => SetProperty(ref _settledTurbidityYAxes, value);
        }

        private ISeries[] _rawWaterTurbiditySeries = Array.Empty<ISeries>();
        public ISeries[] RawWaterTurbiditySeries
        {
            get => _rawWaterTurbiditySeries;
            set => SetProperty(ref _rawWaterTurbiditySeries, value);
        }

        private LiveChartsCore.SkiaSharpView.Axis[] _rawWaterTurbidityXAxes = Array.Empty<LiveChartsCore.SkiaSharpView.Axis>();
        public LiveChartsCore.SkiaSharpView.Axis[] RawWaterTurbidityXAxes
        {
            get => _rawWaterTurbidityXAxes;
            set => SetProperty(ref _rawWaterTurbidityXAxes, value);
        }

        private LiveChartsCore.SkiaSharpView.Axis[] _rawWaterTurbidityYAxes = Array.Empty<LiveChartsCore.SkiaSharpView.Axis>();
        public LiveChartsCore.SkiaSharpView.Axis[] RawWaterTurbidityYAxes
        {
            get => _rawWaterTurbidityYAxes;
            set => SetProperty(ref _rawWaterTurbidityYAxes, value);
        }

        #endregion

        public ObservableCollection<RealtimeRow> RealtimeDataRows { get; } = new();

        public DelegateCommand OpenHistoryChartCmd { get; }

        public ChartPage3ViewModel(IBService bService, BDataService bDataService, IContainerProvider container)
        {
            _bService = bService;
            _bDataService = bDataService;
            _container = container;

            OpenHistoryChartCmd = new DelegateCommand(() =>
            {
                var view = _container.Resolve<HistoryChart3>();
                var vm = _container.Resolve<HistoryChart3ViewModel>();
                view.DataContext = vm;
                var window = new Window
                {
                    Content = view,
                    Title = "B剂历史曲线 - 3#控制点",
                    Width = 1200,
                    Height = 800,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Application.Current.MainWindow,
                    ResizeMode = ResizeMode.CanResize,
                    MinWidth = 800,
                    MinHeight = 600,
                    ShowInTaskbar = true,
                    WindowStyle = WindowStyle.SingleBorderWindow
                };
                window.Show();
            });

            FlowFormatter = v => v.ToString("F0");
            TurbidityFormatter  = v => v.ToString("F2");
            MixFormatter  = v => v.ToString("F2");
            XLabelFormatter = d => new DateTime((long)d).ToString("HH:mm");

            GenerateTurbidityStaticData();
            GenerateTurbidityLineChartsData();

            if (_bService is INotifyPropertyChanged npc)
            {
                _serviceNpc = npc;
                _servicePropertyChangedHandler = (_, e) =>
                {
                    if (e.PropertyName == nameof(CurrentData))
                    {
                        if (_updatePending) return;
                        _updatePending = true;
                        Application.Current?.Dispatcher.BeginInvoke(
                            System.Windows.Threading.DispatcherPriority.Background, () =>
                        {
                            try
                            {
                                if (_disposed) return;
                                UpdateFromCurrentData();
                            }
                            finally
                            {
                                _updatePending = false;
                            }
                        });
                    }
                };
                _serviceNpc.PropertyChanged += _servicePropertyChangedHandler;
            }
        }

        private void UpdateFromCurrentData()
        {
            try
            {
                var isAuto = CurrentData?.GetField("isAutoMainB_2_1")?.Value;
                RingIsAuto = isAuto is int i && i == 3;

                FrontBResidual = Convert.ToDouble(CurrentData?.GetField("TankB_2_1_N_1")?.Value ?? 0);
                BackBResidual = Convert.ToDouble(CurrentData?.GetField("TankB_2_1_N_2")?.Value ?? 0);
                PredictB = Convert.ToDouble(CurrentData?.GetField("PredictMainB_2_1")?.Value ?? 0);
                ActualB = Convert.ToDouble(CurrentData?.GetField("MainBManualLL_2_1")?.Value ?? 0);
                PredictBUnitRate = Convert.ToDouble(CurrentData?.GetField("PredictMainBUnitRate_2_1")?.Value ?? 0);

                RaisePropertyChanged(nameof(CurrentData));
            }
            catch (Exception ex)
            {
                WriteLog($"[B ChartPage3] UpdateFromCurrentData 异常：{ex.Message}");
            }
        }

        private void ToggleProp(ref bool field, bool value, (string Field, string Title, string Color, int Axis) cfg)
        {
            if (SetProperty(ref field, value))
                ToggleCurve(cfg.Field, cfg.Title, cfg.Color, cfg.Axis, value);
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            if (_disposed) return;
            RaisePropertyChanged(nameof(CurrentData));
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
            _refreshTimer = new Timer(10000) { AutoReset = true };
            _refreshTimer.Elapsed += (_, _) =>
            {
                if (_refreshPending) return;
                _refreshPending = true;
                Application.Current?.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background, () =>
                {
                    try
                    {
                        RefreshCurves();
                    }
                    finally
                    {
                        _refreshPending = false;
                    }
                });
            };
            _refreshTimer.Start();

            LoadRealtimeInitialData();
            _tableTimer?.Stop();
            _tableTimer?.Dispose();
            _tableTimer = new Timer(60000) { AutoReset = true };
            _tableTimer.Elapsed += (_, _) =>
            {
                if (_tablePending) return;
                _tablePending = true;
                try
                {
                    var latest = _bDataService.GetLatestData(_tableFields);
                    Application.Current?.Dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.Background, () =>
                    {
                        try
                        {
                            if (latest == null) return;
                            RealtimeDataRows.Insert(0, new RealtimeRow
                            {
                                Time = latest.Time.ToString("HH:mm:ss"),
                                FrontB = latest.Values.GetValueOrDefault("TankB_2_1_N_1"),
                                BackB = latest.Values.GetValueOrDefault("TankB_2_1_N_2"),
                                Flow = latest.Values.GetValueOrDefault("TankFlow_2_1_1"),
                                PredictB = latest.Values.GetValueOrDefault("PredictMainB_2_1"),
                                ActualB = latest.Values.GetValueOrDefault("MainBManualLL_2_1"),
                                PredictBUnitRate = latest.Values.GetValueOrDefault("PredictMainBUnitRate_2_1"),
                            });
                            while (RealtimeDataRows.Count > 60)
                                RealtimeDataRows.RemoveAt(RealtimeDataRows.Count - 1);
                        }
                        finally
                        {
                            _tablePending = false;
                        }
                    });
                }
                catch (Exception ex)
                {
                    WriteLog($"[B ChartPage3] _tableTimer 查询异常：{ex.Message}");
                    _tablePending = false;
                }
            };
            _tableTimer.Start();

            IsFrontBChecked = true;
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
            _refreshTimer = null;

            _tableTimer?.Stop();
            _tableTimer?.Dispose();
            _tableTimer = null;

            Series.Clear();
            _lastTimestamps.Clear();
            RealtimeDataRows.Clear();
        }

        private void ToggleCurve(string field, string title, string color, int axis, bool add)
        {
            try
            {
                if (add)
                {
                    var data = _bDataService.GetHistoricalData(new[] { field });
                    if (data == null || data.Count == 0) return;

                    var values = new ChartValues<DateTimePoint>();
                    foreach (var p in data)
                        values.Add(new DateTimePoint(p.Time, p.Values.GetValueOrDefault(field)));

                    _lastTimestamps[field] = data.Last().Time;

                    Series.Add(new LineSeries
                    {
                        Title = title,
                        Values = values,
                        ScalesYAt = axis,
                        Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(color),
                        Fill = Brushes.Transparent,
                        PointGeometrySize = 0,
                        StrokeThickness = 1
                    });
                }
                else
                {
                    _lastTimestamps.Remove(field);
                    var s = Series.FirstOrDefault(x => x.Title == title);
                    if (s != null) Series.Remove(s);
                }
            }
            catch (Exception ex)
            {
                WriteLog($"[B ChartPage3] ToggleCurve 异常：{ex.Message}");
            }
        }

        private void RefreshCurves()
        {
            try
            {
                foreach (LineSeries series in Series)
                {
                    var cfg = AllCurves.FirstOrDefault(c => c.Title == series.Title);
                    if (cfg.Field == null) continue;

                    if (!_lastTimestamps.TryGetValue(cfg.Field, out var lastTime))
                        continue;

                    var data = _bDataService.GetHistoricalData(new[] { cfg.Field });
                    if (data == null || data.Count == 0) continue;

                    var values = (ChartValues<DateTimePoint>)series.Values;

                    foreach (var p in data)
                    {
                        if (p.Time > lastTime)
                        {
                            values.Add(new DateTimePoint(p.Time, p.Values.GetValueOrDefault(cfg.Field)));
                            lastTime = p.Time;
                        }
                    }

                    while (values.Count > MaxCurvePoints)
                        values.RemoveAt(0);

                    _lastTimestamps[cfg.Field] = lastTime;
                }
            }
            catch (Exception ex)
            {
                WriteLog($"[B ChartPage3] RefreshCurves 异常：{ex.Message}");
            }
        }

        private void GenerateTurbidityStaticData()
        {
            var days = new[] { "05/22", "05/23", "05/24", "05/25", "05/26", "05/27", "05/28" };
            var maxValues = new double[] { 0.85, 0.78, 0.92, 0.70, 0.88, 0.75, 0.82 };
            var avgValues = new double[] { 0.52, 0.48, 0.55, 0.42, 0.50, 0.45, 0.49 };
            var minValues = new double[] { 0.28, 0.22, 0.30, 0.18, 0.25, 0.20, 0.24 };

            TurbidityStackedSeries = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = new ObservableCollection<double>(maxValues),
                    Fill = new SolidColorPaint(new SKColor(244, 67, 54)),
                    Stroke = null,
                    MaxBarWidth = 20,
                    Padding = 2,
                    Name = "最大值"
                },
                new ColumnSeries<double>
                {
                    Values = new ObservableCollection<double>(avgValues),
                    Fill = new SolidColorPaint(new SKColor(33, 150, 243)),
                    Stroke = null,
                    MaxBarWidth = 20,
                    Padding = 2,
                    Name = "平均值"
                },
                new ColumnSeries<double>
                {
                    Values = new ObservableCollection<double>(minValues),
                    Fill = new SolidColorPaint(new SKColor(76, 175, 80)),
                    Stroke = null,
                    MaxBarWidth = 20,
                    Padding = 2,
                    Name = "最小值"
                }
            };

            TurbidityStackedXAxes = new LiveChartsCore.SkiaSharpView.Axis[]
            {
                new LiveChartsCore.SkiaSharpView.Axis
                {
                    Labels = days,
                    LabelsRotation = 0,
                    TextSize = 10
                }
            };

            TurbidityStackedYAxes = new LiveChartsCore.SkiaSharpView.Axis[]
            {
                new LiveChartsCore.SkiaSharpView.Axis
                {
                    MinLimit = 0,
                    Name = "B剂浓度 (mg/L)",
                    NameTextSize = 10,
                    TextSize = 10
                }
            };
        }

        private void GenerateTurbidityLineChartsData()
        {
            var timeLabels = new[] { "00:00", "04:00", "08:00", "12:00", "16:00", "20:00", "24:00" };

            var settledValues = new double[] { 0.35, 0.42, 0.38, 0.45, 0.40, 0.37, 0.43 };
            var settledPaint = new SolidColorPaint(new SKColor(30, 144, 255));
            SettledTurbiditySeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = new ObservableCollection<double>(settledValues),
                    Stroke = settledPaint,
                    GeometryFill = settledPaint,
                    GeometryStroke = settledPaint,
                    GeometrySize = 4,
                    Fill = null,
                    LineSmoothness = 0.3,
                    Name = "滤后流量"
                }
            };
            SettledTurbidityXAxes = new LiveChartsCore.SkiaSharpView.Axis[]
            {
                new LiveChartsCore.SkiaSharpView.Axis
                {
                    Labels = timeLabels,
                    LabelsRotation = 0,
                    TextSize = 9
                }
            };
            SettledTurbidityYAxes = new LiveChartsCore.SkiaSharpView.Axis[]
            {
                new LiveChartsCore.SkiaSharpView.Axis
                {
                    MinLimit = 0,
                    Name = "m³/h",
                    NameTextSize = 9,
                    TextSize = 9
                }
            };

            var rawWaterValues = new double[] { 0.45, 0.52, 0.48, 0.55, 0.50, 0.47, 0.53 };
            var rawWaterPaint = new SolidColorPaint(new SKColor(255, 152, 0));
            RawWaterTurbiditySeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = new ObservableCollection<double>(rawWaterValues),
                    Stroke = rawWaterPaint,
                    GeometryFill = rawWaterPaint,
                    GeometryStroke = rawWaterPaint,
                    GeometrySize = 4,
                    Fill = null,
                    LineSmoothness = 0.3,
                    Name = "调节池前端B剂浓度"
                }
            };
            RawWaterTurbidityXAxes = new LiveChartsCore.SkiaSharpView.Axis[]
            {
                new LiveChartsCore.SkiaSharpView.Axis
                {
                    Labels = timeLabels,
                    LabelsRotation = 0,
                    TextSize = 9
                }
            };
            RawWaterTurbidityYAxes = new LiveChartsCore.SkiaSharpView.Axis[]
            {
                new LiveChartsCore.SkiaSharpView.Axis
                {
                    MinLimit = 0,
                    Name = "mg/L",
                    NameTextSize = 9,
                    TextSize = 9
                }
            };
        }

        private void LoadRealtimeInitialData()
        {
            try
            {
                var fields = new[] { "TankB_2_1_N_1", "TankB_2_1_N_2", "TankFlow_2_1_1", "PredictMainB_2_1", "MainBManualLL_2_1", "PredictMainBUnitRate_2_1" };
                var allData = _bDataService.GetHistoricalData(fields, 1);
                if (allData == null || allData.Count == 0) return;

                var rows = allData.Count > 60 ? allData.Skip(allData.Count - 60).ToList() : allData;
                foreach (var p in rows)
                {
                    RealtimeDataRows.Insert(0, new RealtimeRow
                    {
                        Time = p.Time.ToString("HH:mm:ss"),
                        FrontB = p.Values.GetValueOrDefault("TankB_2_1_N_1"),
                        BackB = p.Values.GetValueOrDefault("TankB_2_1_N_2"),
                        Flow = p.Values.GetValueOrDefault("TankFlow_2_1_1"),
                        PredictB = p.Values.GetValueOrDefault("PredictMainB_2_1"),
                        ActualB = p.Values.GetValueOrDefault("MainBManualLL_2_1"),
                        PredictBUnitRate = p.Values.GetValueOrDefault("PredictMainBUnitRate_2_1"),
                    });
                }
                while (RealtimeDataRows.Count > 60)
                    RealtimeDataRows.RemoveAt(RealtimeDataRows.Count - 1);
            }
            catch (Exception ex)
            {
                WriteLog($"[B ChartPage3] LoadRealtimeInitialData 异常：{ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
            _refreshTimer = null;

            _tableTimer?.Stop();
            _tableTimer?.Dispose();
            _tableTimer = null;

            if (_serviceNpc != null && _servicePropertyChangedHandler != null)
                _serviceNpc.PropertyChanged -= _servicePropertyChangedHandler;
        }
    }

}
