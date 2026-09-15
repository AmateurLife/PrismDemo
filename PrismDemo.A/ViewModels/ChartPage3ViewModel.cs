using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Ioc;
using Prism.Regions;
using PrismDemo.Core.Interfaces;
using PrismDemo.Core.Models;
using PrismDemo.A.Interfaces;
using PrismDemo.A.Models;
using PrismDemo.A.Services;
using PrismDemo.A.Views;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Diagnostics;
using System.IO;
using static PrismDemo.Core.Services.AlertLogger;

namespace PrismDemo.A.ViewModels
{
    public class ChartPage3ViewModel : BindableBase, INavigationAware, IDisposable
    {
        private static readonly (string Field, string Title, string Color, int Axis)
            OutTurbidityCfg  = ("SettledTurbidity_2_2_1",         "沉后水浊度",     "#1E90FF", 1),
            InTurbidityCfg   = ("InTurbidity_2",          "原水浊度",       "#FF9800", 1),
            OutletCfg = ("OutletTurbidity_2",    "出厂水浊度",     "#00BCD4", 1);

        private readonly IAService _aService;
        private readonly ADataService _aDataService;
        private readonly IContainerProvider _container;
        private Timer _subChartTimer;
        private bool _subChartPending;
        private const int MaxTurbidityTrendPoints = 1440;
        private readonly List<LiveChartsCore.Defaults.DateTimePoint> _outletTurbidityPoints = new();
        private readonly List<LiveChartsCore.Defaults.DateTimePoint> _settledTurbidityPoints = new();
        private readonly List<LiveChartsCore.Defaults.DateTimePoint> _rawWaterTurbidityPoints = new();
        private readonly INotifyPropertyChanged _serviceNpc;
        private readonly PropertyChangedEventHandler _servicePropertyChangedHandler;
        private readonly WeakReference<ChartPage3ViewModel> _weakSelf;
        private bool _disposed;
        private bool _updatePending;

        public AData? CurrentData => _aService?.CurrentData;

        public IWriteableService? WriteableService => _aService as IWriteableService;

        #region 绑定属性

         private bool _ringIsAuto = false;
        public bool RingIsAuto
        {
            get => _ringIsAuto;
            set => SetProperty(ref _ringIsAuto, value);
        }

        private double _ringUsePercentage = 60;
        public double RingUsePercentage
        {
            get => _ringUsePercentage;
            set => SetProperty(ref _ringUsePercentage, value);
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

        private double _rawWaterTurbidity = 2.1;
        public double RawWaterTurbidity
        {
            get => _rawWaterTurbidity;
            set => SetProperty(ref _rawWaterTurbidity, value);
        }

        private double _rawWaterTurbidityChange = 0;
        public double RawWaterTurbidityChange
        {
            get => _rawWaterTurbidityChange;
            set => SetProperty(ref _rawWaterTurbidityChange, value);
        }

        private double _settledWaterTurbidity = 0.3;
        public double SettledWaterTurbidity
        {
            get => _settledWaterTurbidity;
            set => SetProperty(ref _settledWaterTurbidity, value);
        }

        private double _settledWaterTurbidityChange = 0;
        public double SettledWaterTurbidityChange
        {
            get => _settledWaterTurbidityChange;
            set => SetProperty(ref _settledWaterTurbidityChange, value);
        }

        private double _predictA = 0;
        public double PredictA
        {
            get => _predictA;
            set => SetProperty(ref _predictA, value);
        }
        private double _frontA = 0;
        public double FrontA
        {
            get => _frontA;
            set => SetProperty(ref _frontA, value);
        }
        private double _backA = 0;
        public double BackA
        {
            get => _backA;
            set => SetProperty(ref _backA, value);
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

        private ISeries[] _outletTurbiditySeries = Array.Empty<ISeries>();
        public ISeries[] OutletTurbiditySeries
        {
            get => _outletTurbiditySeries;
            set => SetProperty(ref _outletTurbiditySeries, value);
        }

        private LiveChartsCore.SkiaSharpView.Axis[] _outletTurbidityXAxes = Array.Empty<LiveChartsCore.SkiaSharpView.Axis>();
        public LiveChartsCore.SkiaSharpView.Axis[] OutletTurbidityXAxes
        {
            get => _outletTurbidityXAxes;
            set => SetProperty(ref _outletTurbidityXAxes, value);
        }

        private LiveChartsCore.SkiaSharpView.Axis[] _outletTurbidityYAxes = Array.Empty<LiveChartsCore.SkiaSharpView.Axis>();
        public LiveChartsCore.SkiaSharpView.Axis[] OutletTurbidityYAxes
        {
            get => _outletTurbidityYAxes;
            set => SetProperty(ref _outletTurbidityYAxes, value);
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

        private void OpenFloc()
        {
            try
            {
                var exePath = Configuration.Config.FlocExePath;
                if (!Path.IsPathRooted(exePath))
                    exePath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, exePath));

                if (!File.Exists(exePath))
                {
                    ShowMessageBox($"絮体观测程序不存在：\n{exePath}", "启动失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var process = new Process();
                process.StartInfo.FileName = exePath;
                process.StartInfo.WorkingDirectory = Path.GetDirectoryName(exePath);
                process.Start();
            }
            catch (Exception ex)
            {
                ShowMessageBox($"打开絮体观测程序失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public DelegateCommand OpenHistoryChartCmd { get; }
        public DelegateCommand OpenFlocCmd { get; }
        public DelegateCommand ToggleAutoCmd { get; }

        private const string IsAutoFieldName = "IsAutoMainA_2_3";

        public ChartPage3ViewModel(IAService aService, ADataService aDataService, IContainerProvider container)
        {
            _aService = aService;
            _aDataService = aDataService;
            _container = container;

            OpenHistoryChartCmd = new DelegateCommand(() =>
            {
                var view = _container.Resolve<HistoryChart3>();
                var vm = _container.Resolve<HistoryChart3ViewModel>();
                view.DataContext = vm;
                var window = new Window
                {
                    Content = view,
                    Title = "A剂历史曲线 - 3#控制点",
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
                window.Closed += (s, e) => vm.Dispose();
                window.Show();
            });
            OpenFlocCmd = new DelegateCommand(OpenFloc);
            ToggleAutoCmd = new DelegateCommand(ToggleAuto);

            _weakSelf = new WeakReference<ChartPage3ViewModel>(this);

            GenerateTurbidityStaticData();

            if (_aService is INotifyPropertyChanged npc)
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
                                RaisePropertyChanged(nameof(CurrentData));
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

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            if (_disposed) return;
            RaisePropertyChanged(nameof(CurrentData));
            _subChartTimer?.Stop();
            _subChartTimer?.Dispose();
            var timer = new Timer(60000) { AutoReset = true };
            _subChartTimer = timer;
            var weakSelf = _weakSelf;
            timer.Elapsed += (_, _) =>
            {
                if (!weakSelf.TryGetTarget(out var vm))
                {
                    timer.Stop();
                    return;
                }
                if (vm._subChartPending) return;
                vm._subChartPending = true;
                Application.Current?.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background, () =>
                {
                    try
                    {
                        if (vm._disposed) return;
                        vm.PushRealtimeTurbidityPoints();
                    }
                    finally
                    {
                        vm._subChartPending = false;
                    }
                });
            };
            timer.Start();

            InitTurbidityTrendCharts();
            WriteLog($"[ChartPage3] 进入页面托管堆 {GC.GetTotalMemory(false) / 1024} KB");
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            ReleaseChartResources();
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
                    Name = "浊度 (Turbidity)",
                    NameTextSize = 10,
                    TextSize = 10
                }
            };
        }

        private void InitTurbidityTrendCharts()
        {
            try
            {
                var fields = new[] { OutletCfg.Field, OutTurbidityCfg.Field, InTurbidityCfg.Field };
                var data = _aDataService.GetHistoricalData(fields, 24);

                _outletTurbidityPoints.Clear();
                _settledTurbidityPoints.Clear();
                _rawWaterTurbidityPoints.Clear();

                if (data != null)
                {
                    foreach (var p in data)
                    {
                        _outletTurbidityPoints.Add(new LiveChartsCore.Defaults.DateTimePoint(p.Time, p.Values.GetValueOrDefault(OutletCfg.Field)));
                        _settledTurbidityPoints.Add(new LiveChartsCore.Defaults.DateTimePoint(p.Time, p.Values.GetValueOrDefault(OutTurbidityCfg.Field)));
                        _rawWaterTurbidityPoints.Add(new LiveChartsCore.Defaults.DateTimePoint(p.Time, p.Values.GetValueOrDefault(InTurbidityCfg.Field)));
                    }
                }

                if (data == null || data.Count == 0)
                {
                    WriteLog("[ChartPage3] 浊度趋势图初始化：数据库近24小时无历史数据");
                }
                else
                {
                    var outletValid = _outletTurbidityPoints.Count(p => p.Value is double cv && cv != 0);
                    var settledValid = _settledTurbidityPoints.Count(p => p.Value is double sv && sv != 0);
                    var rawValid = _rawWaterTurbidityPoints.Count(p => p.Value is double rv && rv != 0);

                    WriteLog($"[ChartPage3] 浊度趋势图初始化完成：共 {data.Count} 行；出厂水浊度有效 {outletValid} 点、沉后水浊度有效 {settledValid} 点、原水浊度有效 {rawValid} 点");

                    if (outletValid == 0)
                        WriteLog($"[ChartPage3] 浊度趋势图：字段 {OutletCfg.Field} 近24小时无有效数据（值均为0/空）");
                    if (settledValid == 0)
                        WriteLog($"[ChartPage3] 浊度趋势图：字段 {OutTurbidityCfg.Field} 近24小时无有效数据（值均为0/空）");
                    if (rawValid == 0)
                        WriteLog($"[ChartPage3] 浊度趋势图：字段 {InTurbidityCfg.Field} 近24小时无有效数据（值均为0/空）");
                }

                TrimTurbidityPoints();
                InitTurbidityTrendAxes();
                RebuildTurbidityCharts();
            }
            catch (Exception ex)
            {
                WriteLog($"[ChartPage3] 浊度趋势图初始化失败：{ex.Message}");
            }
        }

        private void PushRealtimeTurbidityPoints()
        {
            if (_disposed) return;
            try
            {
                if (CurrentData == null)
                {
                    WriteLog("[ChartPage3] 浊度趋势图推点跳过：CurrentData 为空");
                    return;
                }

                var now = DateTime.Now;
                PushFieldPoint(_outletTurbidityPoints, OutletCfg.Field, now);
                PushFieldPoint(_settledTurbidityPoints, OutTurbidityCfg.Field, now);
                PushFieldPoint(_rawWaterTurbidityPoints, InTurbidityCfg.Field, now);
                TrimTurbidityPoints();
                RebuildTurbidityCharts();
            }
            catch (Exception ex)
            {
                WriteLog($"[ChartPage3] 浊度趋势图刷新失败：{ex.Message}");
            }
        }

        private void PushFieldPoint(List<LiveChartsCore.Defaults.DateTimePoint> points, string field, DateTime now)
        {
            var connect = CurrentData?.GetField(field);
            if (connect == null)
            {
                WriteLog($"[ChartPage3] 浊度趋势图推点跳过：字段 {field} 不存在于 CurrentData");
                return;
            }

            if (connect.Value == null)
            {
                WriteLog($"[ChartPage3] 浊度趋势图推点跳过：字段 {field} 值为空");
                return;
            }

            if (!connect.isvalid)
            {
                WriteLog($"[ChartPage3] 浊度趋势图推点跳过：字段 {field} 数据无效(isvalid=false)");
                return;
            }

            double value;
            try
            {
                value = Convert.ToDouble(connect.Value);
            }
            catch (Exception)
            {
                WriteLog($"[ChartPage3] 浊度趋势图推点失败：字段 {field} 值转换异常");
                return;
            }

            points.Add(new LiveChartsCore.Defaults.DateTimePoint(now, value));
        }

        private void TrimTurbidityPoints()
        {
            TrimList(_outletTurbidityPoints);
            TrimList(_settledTurbidityPoints);
            TrimList(_rawWaterTurbidityPoints);
        }

        private static void TrimList(List<LiveChartsCore.Defaults.DateTimePoint> points)
        {
            while (points.Count > MaxTurbidityTrendPoints)
                points.RemoveAt(0);
        }

        private void InitTurbidityTrendAxes()
        {
            var (outletX, outletY) = CreateTurbidityTrendAxes();
            var (settledX, settledY) = CreateTurbidityTrendAxes();
            var (rawWaterX, rawWaterY) = CreateTurbidityTrendAxes();

            OutletTurbidityXAxes = new[] { outletX };
            OutletTurbidityYAxes = new[] { outletY };
            SettledTurbidityXAxes = new[] { settledX };
            SettledTurbidityYAxes = new[] { settledY };
            RawWaterTurbidityXAxes = new[] { rawWaterX };
            RawWaterTurbidityYAxes = new[] { rawWaterY };
        }

        private static (LiveChartsCore.SkiaSharpView.Axis X, LiveChartsCore.SkiaSharpView.Axis Y) CreateTurbidityTrendAxes()
        {
            return (
                new LiveChartsCore.SkiaSharpView.Axis
                {
                    Labeler = v =>
                    {
                        var ticks = (long)v;
                        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
                            return string.Empty;
                        return new DateTime(ticks).ToString("HH:mm");
                    },
                    MinStep = TimeSpan.FromMinutes(30).Ticks,
                    Name = "时间",
                    NameTextSize = 9,
                    TextSize = 9
                },
                new LiveChartsCore.SkiaSharpView.Axis
                {
                    MinLimit = 0,
                    Name = "Turbidity",
                    NameTextSize = 9,
                    TextSize = 9
                });
        }

        private void RebuildTurbidityCharts()
        {
            var oldOutlet = OutletTurbiditySeries;
            var oldSettled = SettledTurbiditySeries;
            var oldRawWater = RawWaterTurbiditySeries;

            OutletTurbiditySeries = BuildTurbiditySeries(_outletTurbidityPoints, "出厂水浊度", "#00BCD4");
            SettledTurbiditySeries = BuildTurbiditySeries(_settledTurbidityPoints, "沉后水浊度", "#1E90FF");
            RawWaterTurbiditySeries = BuildTurbiditySeries(_rawWaterTurbidityPoints, "原水浊度", "#FF9800");

            DisposeSeriesPaints(oldOutlet);
            DisposeSeriesPaints(oldSettled);
            DisposeSeriesPaints(oldRawWater);
        }

        private static ISeries[] BuildTurbiditySeries(List<LiveChartsCore.Defaults.DateTimePoint> points, string name, string color)
        {
            return new ISeries[]
            {
                new LineSeries<LiveChartsCore.Defaults.DateTimePoint>
                {
                    Values = new ObservableCollection<LiveChartsCore.Defaults.DateTimePoint>(points),
                    Stroke = new SolidColorPaint(SKColor.Parse(color)) { StrokeThickness = 1 },
                    GeometryFill = null,
                    GeometryStroke = null,
                    GeometrySize = 0,
                    Fill = null,
                    LineSmoothness = 0,
                    Name = name
                }
            };
        }

        private void ReleaseTurbidityTrendCharts()
        {
            DisposeSeriesPaints(OutletTurbiditySeries);
            DisposeSeriesPaints(SettledTurbiditySeries);
            DisposeSeriesPaints(RawWaterTurbiditySeries);
            
            foreach (var series in OutletTurbiditySeries ?? Array.Empty<ISeries>())
                if (series is LineSeries<LiveChartsCore.Defaults.DateTimePoint> ls)
                    ls.Values = null;
            foreach (var series in SettledTurbiditySeries ?? Array.Empty<ISeries>())
                if (series is LineSeries<LiveChartsCore.Defaults.DateTimePoint> ls)
                    ls.Values = null;
            foreach (var series in RawWaterTurbiditySeries ?? Array.Empty<ISeries>())
                if (series is LineSeries<LiveChartsCore.Defaults.DateTimePoint> ls)
                    ls.Values = null;
            
            _outletTurbidityPoints.Clear();
            _settledTurbidityPoints.Clear();
            _rawWaterTurbidityPoints.Clear();
            OutletTurbiditySeries = null;
            SettledTurbiditySeries = null;
            RawWaterTurbiditySeries = null;
            OutletTurbidityXAxes = OutletTurbidityYAxes = null;
            SettledTurbidityXAxes = SettledTurbidityYAxes = null;
            RawWaterTurbidityXAxes = RawWaterTurbidityYAxes = null;
        }

        private static void DisposeSeriesPaints(ISeries[] seriesArray)
        {
            if (seriesArray == null) return;
            foreach (var series in seriesArray)
            {
                try
                {
                    if (series is LineSeries<LiveChartsCore.Defaults.DateTimePoint> lineSeries)
                    {
                        (lineSeries.Stroke as IDisposable)?.Dispose();
                        (lineSeries.Fill as IDisposable)?.Dispose();
                        (lineSeries.GeometryStroke as IDisposable)?.Dispose();
                        (lineSeries.GeometryFill as IDisposable)?.Dispose();
                    }
                }
                catch { }
            }
        }

        private void ReleaseChartResources()
        {
            _subChartTimer?.Stop();
            _subChartTimer?.Dispose();
            _subChartTimer = null;
            try
            {
                ReleaseTurbidityTrendCharts();
                
                foreach (var series in TurbidityStackedSeries ?? Array.Empty<ISeries>())
                {
                    if (series is ColumnSeries<double> cs)
                    {
                        cs.Values = null;
                        (cs.Fill as IDisposable)?.Dispose();
                    }
                }
                DisposeSeriesPaints(TurbidityStackedSeries);
                TurbidityStackedSeries = null;
                TurbidityStackedXAxes = null;
                TurbidityStackedYAxes = null;
            }
            catch (Exception ex)
            {
                WriteLog($"[ChartPage3] 图表资源释放异常：{ex.Message}");
            }
            WriteLog("[ChartPage3] 图表资源已释放（子图定时器已停止/释放）");
        }

        private void UpdateFromCurrentData()
        {
            var isAuto = CurrentData?.GetField(IsAutoFieldName)?.Value;
            RingIsAuto = isAuto is int i && i == 3;
        }

        private void ToggleAuto()
        {
            try
            {
                if (CurrentData == null || WriteableService == null) return;

                var field = CurrentData.GetField(IsAutoFieldName);
                if (field == null) return;

                var currentValue = Convert.ToInt32(field.Value);
                var newValue = currentValue == 3 ? 1 : 3;
                WriteableService.WriteValue(field, newValue);
            }
            catch (Exception ex)
            {
                WriteLog($"[ChartPage3] ToggleAuto 异常：{ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            ReleaseChartResources();

            if (_serviceNpc != null && _servicePropertyChangedHandler != null)
                _serviceNpc.PropertyChanged -= _servicePropertyChangedHandler;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            WriteLog($"[ChartPage3] Dispose 完成，托管堆 {GC.GetTotalMemory(true) / 1024} KB");
        }
    }
}
