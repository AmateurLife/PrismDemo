using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using PrismDemo.APP.Services;
using PrismDemo.Core.Interfaces;
using PrismDemo.Core.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;
using static PrismDemo.Core.Services.AlertLogger;

namespace PrismDemo.APP.ViewModels
{
    public class HomePageViewModel : BindableBase, INavigationAware, IDisposable
    {
        private readonly IRegionManager _regionManager;
        private readonly IDatabaseService _databaseService;
        private DispatcherTimer _refreshTimer;
        private DispatcherTimer _sampleTimer;
        private DispatcherTimer _midnightTimer;
        private DateTime _complianceChartDate;
        private bool _disposed;

        #region 命令
        public DelegateCommand ChangeHistoricalAlarmCmd { get; }
        public DelegateCommand<AlarmRecord> ShowAlarmDetailCmd { get; }
        #endregion

        public SharedDataModel Data { get; }

        #region 报警列表
        private ObservableCollection<AlarmRecord> _alarmItems;
        public ObservableCollection<AlarmRecord> AlarmItems
        {
            get => _alarmItems;
            set => SetProperty(ref _alarmItems, value);
        }
        #endregion

        #region 数据缓冲
        private readonly TimeSeriesBuffer _inFlowBuffer = new(60);
        private readonly TimeSeriesBuffer _outletFlowBuffer = new(60);
        private readonly TimeSeriesBuffer _bBuffer = new(60);
        private readonly TimeSeriesBuffer _turbidityBuffer = new(60);
        private readonly TimeSeriesBuffer _flow24h = new(1440);
        private readonly TimeSeriesBuffer _b24h = new(1440);
        private readonly TimeSeriesBuffer _turbidity24h = new(1440);
        #endregion

        #region 图表对象复用（避免每秒整体重建 Series/Axes/数据集合造成的持续 GC 压力）
        private ObservableCollection<DateTimePoint> _flowDiffPoints;
        private ObservableCollection<DateTimePoint> _bPoints;
        private ObservableCollection<DateTimePoint> _turbidityPoints;
        private ObservableCollection<DateTimePoint> _rtFlowPoints;
        private ObservableCollection<DateTimePoint> _rtBPoints;
        private ObservableCollection<DateTimePoint> _rtTurbidityPoints;
        private Axis _flowDiffXAxis;
        private Axis _bXAxis;
        private Axis _turbidityXAxis;
        private Axis _realTimeXAxis;

        /// <summary>
        /// 将时间序列快照增量同步到图表数据集合（复用集合，只增删差异点）。
        /// 适用于按时间升序、仅头部淘汰/尾部追加的环形缓冲快照。
        /// </summary>
        private static void SyncPoints(ObservableCollection<DateTimePoint> target, List<DateTimePoint> source)
        {
            if (source.Count == 0)
            {
                target.Clear();
                return;
            }

            // 移除头部已被淘汰的旧点
            var firstTime = source[0].DateTime;
            while (target.Count > 0 && target[0].DateTime < firstTime)
                target.RemoveAt(0);

            // 移除尾部超出快照末点的点（缓冲被清空重建的场景）
            var lastTime = source[source.Count - 1].DateTime;
            for (int i = target.Count - 1; i >= 0 && target[i].DateTime > lastTime; i--)
                target.RemoveAt(i);

            // 尾部追加新点
            int startIndex = 0;
            if (target.Count > 0)
            {
                var existingLast = target[target.Count - 1].DateTime;
                while (startIndex < source.Count && source[startIndex].DateTime <= existingLast)
                    startIndex++;
            }
            for (int i = startIndex; i < source.Count; i++)
                target.Add(source[i]);
        }

        private static string TimeAxisLabel(double value, string format)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return string.Empty;
            var ticks = (long)value;
            if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks) return string.Empty;
            return new DateTime(ticks).ToString(format);
        }

        private static void DisposeSeriesPaints(ISeries[] seriesArray)
        {
            if (seriesArray == null) return;
            foreach (var series in seriesArray)
            {
                try
                {
                    if (series is LineSeries<DateTimePoint> lineSeries)
                    {
                        (lineSeries.Stroke as IDisposable)?.Dispose();
                        (lineSeries.GeometryStroke as IDisposable)?.Dispose();
                        (lineSeries.GeometryFill as IDisposable)?.Dispose();
                    }
                    else if (series is ColumnSeries<double> colSeries)
                    {
                        (colSeries.Fill as IDisposable)?.Dispose();
                    }
                    else if (series is StackedRowSeries<double> rowSeries)
                    {
                        (rowSeries.Fill as IDisposable)?.Dispose();
                    }
                }
                catch { }
            }
        }
        #endregion

        #region 流量卡片属性
        private double _currentInFlow;
        public double CurrentInFlow
        {
            get => _currentInFlow;
            set => SetProperty(ref _currentInFlow, value);
        }

        private double _currentOutletFlow;
        public double CurrentOutletFlow
        {
            get => _currentOutletFlow;
            set => SetProperty(ref _currentOutletFlow, value);
        }

        private double _flowBalanceRatio;
        public double FlowBalanceRatio
        {
            get => _flowBalanceRatio;
            set => SetProperty(ref _flowBalanceRatio, value);
        }

        private string _flowChangeRate = "--";
        public string FlowChangeRate
        {
            get => _flowChangeRate;
            set => SetProperty(ref _flowChangeRate, value);
        }

        private ISeries[] _flowDiffSeries = Array.Empty<ISeries>();
        public ISeries[] FlowDiffSeries
        {
            get => _flowDiffSeries;
            set => SetProperty(ref _flowDiffSeries, value);
        }

        private Axis[] _flowDiffXAxes;
        public Axis[] FlowDiffXAxes
        {
            get => _flowDiffXAxes;
            set => SetProperty(ref _flowDiffXAxes, value);
        }
        #endregion

        #region 相关过程数据
        // 原水流量
        private string _inFlowDisplay = "--";
        public string InFlowDisplay
        {
            get => _inFlowDisplay;
            set => SetProperty(ref _inFlowDisplay, value);
        }
        // 原水浊度
        private string _inTurbidityDisplay = "--";
        public string InTurbidityDisplay
        {
            get => _inTurbidityDisplay;
            set => SetProperty(ref _inTurbidityDisplay, value);
        }
       
        // 沉后水浊度
        private string _outTurbidityDisplay = "--";
        public string OutTurbidityDisplay
        {
            get => _outTurbidityDisplay;
            set => SetProperty(ref _outTurbidityDisplay, value);
        }

        #region 药泵运行状态
        // 1号A剂主泵运行状态
        private bool _isWorkMainA_2_1 = false;
        public bool IsWorkMainA_2_1
        {
            get => _isWorkMainA_2_1;
            set => SetProperty(ref _isWorkMainA_2_1, value);
        }
        // 3号A剂主泵运行状态
        private bool _isWorkMainA_2_3 = false;
        public bool IsWorkMainA_2_3
        {
            get => _isWorkMainA_2_3;
            set => SetProperty(ref _isWorkMainA_2_3, value);
        }
        // 4号A剂主泵运行状态
        private bool _isWorkMainA_2_4 = false;
        public bool IsWorkMainA_2_4
        {
            get => _isWorkMainA_2_4;
            set => SetProperty(ref _isWorkMainA_2_4, value);
        }
        // 6号A剂主泵运行状态
        private bool _isWorkMainA_2_6 = false;
        public bool IsWorkMainA_2_6
        {
            get => _isWorkMainA_2_6;
            set => SetProperty(ref _isWorkMainA_2_6, value);
        }
        // 2号A剂备泵运行状态
        private bool _isWorkStandbyA_2_2 = false;
        public bool IsWorkStandbyA_2_2
        {
            get => _isWorkStandbyA_2_2;
            set => SetProperty(ref _isWorkStandbyA_2_2, value);
        }
        // 5号A剂备泵运行状态
        private bool _isWorkStandbyA_2_5 = false;
        public bool IsWorkStandbyA_2_5
        {
            get => _isWorkStandbyA_2_5;
            set => SetProperty(ref _isWorkStandbyA_2_5, value);
        }
        #endregion
        #region 智能运行状态
        // 1号控制点
        private string _isAutoMainA_2_1 = "--";
        public string IsAutoMainA_2_1
        {
            get => _isAutoMainA_2_1;
            set => SetProperty(ref _isAutoMainA_2_1, value);
        }
        // 2号控制点
        private string _isAutoMainA_2_2 = "--";
        public string IsAutoMainA_2_2
        {
            get => _isAutoMainA_2_2;
            set => SetProperty(ref _isAutoMainA_2_2, value);
        }
        // 3号控制点
        private string _isAutoMainA_2_3 = "--";
        public string IsAutoMainA_2_3
        {
            get => _isAutoMainA_2_3;
            set => SetProperty(ref _isAutoMainA_2_3, value);
        }
        // 4号控制点
        private string _isAutoMainA_2_4 = "--";
        public string IsAutoMainA_2_4
        {
            get => _isAutoMainA_2_4;
            set => SetProperty(ref _isAutoMainA_2_4, value);
        }
        #endregion
        #region 泵后流量
        // 1号A剂泵后流量
        private string _backA_1Display = "--";
        public string BackA_1Display
        {
            get => _backA_1Display;
            set => SetProperty(ref _backA_1Display, value);
        }
        // 3号A剂泵后流量
        private string _backA_3Display = "--";
        public string BackA_3Display
        {
            get => _backA_3Display;
            set => SetProperty(ref _backA_3Display, value);
        }
        // 4号A剂泵后流量
        private string _backA_4Display = "--";
        public string BackA_4Display
        {
            get => _backA_4Display;
            set => SetProperty(ref _backA_4Display, value);
        }
        // 6号A剂泵后流量
        private string _backA_6Display = "--";
        public string BackA_6Display
        {
            get => _backA_6Display;
            set => SetProperty(ref _backA_6Display, value);
        }
        #endregion
        #region 人工投加率
        // 1号A剂泵人工加药投加率
        private string _mainAManualUnitRate_2_1 = "--";
        public string MainAManualUnitRate_2_1
        {
            get => _mainAManualUnitRate_2_1;
            set => SetProperty(ref _mainAManualUnitRate_2_1, value);
        }
        // 3号A剂泵人工加药投加率
        private string _mainAManualUnitRate_2_3 = "--";
        public string MainAManualUnitRate_2_3
        {
            get => _mainAManualUnitRate_2_3;
            set => SetProperty(ref _mainAManualUnitRate_2_3, value);
        }
        // 4号A剂泵人工加药投加率
        private string _mainAManualUnitRate_2_4 = "--";
        public string MainAManualUnitRate_2_4
        {
            get => _mainAManualUnitRate_2_4;
            set => SetProperty(ref _mainAManualUnitRate_2_4, value);
        }
        // 6号A剂泵人工加药投加率
        private string _mainAManualUnitRate_2_6 = "--";
        public string MainAManualUnitRate_2_6
        {
            get => _mainAManualUnitRate_2_6;
            set => SetProperty(ref _mainAManualUnitRate_2_6, value);
        }
        #endregion
        #region 智能投加率
        // 1号控制点智能加药投加率
        private string _predictMainAUnitRate_2_1 = "--";
        public string PredictMainAUnitRate_2_1
        {
            get => _predictMainAUnitRate_2_1;
            set => SetProperty(ref _predictMainAUnitRate_2_1, value);
        }
        // 2号控制点智能加药投加率
        private string _predictMainAUnitRate_2_2 = "--";
        public string PredictMainAUnitRate_2_2
        {
            get => _predictMainAUnitRate_2_2;
            set => SetProperty(ref _predictMainAUnitRate_2_2, value);
        }
        // 3号控制点智能加药投加率
        private string _predictMainAUnitRate_2_3 = "--";
        public string PredictMainAUnitRate_2_3
        {
            get => _predictMainAUnitRate_2_3;
            set => SetProperty(ref _predictMainAUnitRate_2_3, value);
        }
        // 4号控制点智能加药投加率
        private string _predictMainAUnitRate_2_4 = "--";
        public string PredictMainAUnitRate_2_4
        {
            get => _predictMainAUnitRate_2_4;
            set => SetProperty(ref _predictMainAUnitRate_2_4, value);
        }
        #endregion
        #endregion

        #region B剂浓度卡片属性
        private double _currentB;
        public double CurrentB
        {
            get => _currentB;
            set => SetProperty(ref _currentB, value);
        }

        private string _bChangeRate = "--";
        public string BChangeRate
        {
            get => _bChangeRate;
            set => SetProperty(ref _bChangeRate, value);
        }

        private ISeries[] _bTrend = Array.Empty<ISeries>();
        public ISeries[] BTrend
        {
            get => _bTrend;
            set => SetProperty(ref _bTrend, value);
        }

        private Axis[] _bXAxes;
        public Axis[] BXAxes
        {
            get => _bXAxes;
            set => SetProperty(ref _bXAxes, value);
        }
        #endregion

        #region 浊度卡片属性
        private double _currentTurbidity;
        public double CurrentTurbidity
        {
            get => _currentTurbidity;
            set => SetProperty(ref _currentTurbidity, value);
        }

        private string _turbidityChangeRate = "--";
        public string TurbidityChangeRate
        {
            get => _turbidityChangeRate;
            set => SetProperty(ref _turbidityChangeRate, value);
        }

        private ISeries[] _turbidityTrend = Array.Empty<ISeries>();
        public ISeries[] TurbidityTrend
        {
            get => _turbidityTrend;
            set => SetProperty(ref _turbidityTrend, value);
        }

        private Axis[] _turbidityXAxes;
        public Axis[] TurbidityXAxes
        {
            get => _turbidityXAxes;
            set => SetProperty(ref _turbidityXAxes, value);
        }
        #endregion

        #region 实时趋势图
        private ISeries[] _realTimeTrend = Array.Empty<ISeries>();
        public ISeries[] RealTimeTrend
        {
            get => _realTimeTrend;
            set => SetProperty(ref _realTimeTrend, value);
        }

        private Axis[] _realTimeXAxes;
        public Axis[] RealTimeXAxes
        {
            get => _realTimeXAxes;
            set => SetProperty(ref _realTimeXAxes, value);
        }

        private Axis[] _realTimeYAxes;
        public Axis[] RealTimeYAxes
        {
            get => _realTimeYAxes;
            set => SetProperty(ref _realTimeYAxes, value);
        }
        #endregion

        #region 合规率图表属性
        private ISeries[] _complianceBarSeries = Array.Empty<ISeries>();
        public ISeries[] ComplianceBarSeries
        {
            get => _complianceBarSeries;
            set => SetProperty(ref _complianceBarSeries, value);
        }

        private Axis[] _complianceBarXAxes;
        public Axis[] ComplianceBarXAxes
        {
            get => _complianceBarXAxes;
            set => SetProperty(ref _complianceBarXAxes, value);
        }

        private Axis[] _complianceBarYAxes;
        public Axis[] ComplianceBarYAxes
        {
            get => _complianceBarYAxes;
            set => SetProperty(ref _complianceBarYAxes, value);
        }

        private ISeries[] _dailyComplianceSeries = Array.Empty<ISeries>();
        public ISeries[] DailyComplianceSeries
        {
            get => _dailyComplianceSeries;
            set => SetProperty(ref _dailyComplianceSeries, value);
        }

        private Axis[] _dailyComplianceXAxes;
        public Axis[] DailyComplianceXAxes
        {
            get => _dailyComplianceXAxes;
            set => SetProperty(ref _dailyComplianceXAxes, value);
        }

        private Axis[] _dailyComplianceYAxes;
        public Axis[] DailyComplianceYAxes
        {
            get => _dailyComplianceYAxes;
            set => SetProperty(ref _dailyComplianceYAxes, value);
        }
        #endregion

        public HomePageViewModel(IRegionManager regionManager, SharedDataModel data,
            IDatabaseService databaseService)
        {
            _regionManager = regionManager;
            _databaseService = databaseService;
            Data = data;

            ChangeHistoricalAlarmCmd = new DelegateCommand(ChangeHistoricalAlarm);
            ShowAlarmDetailCmd = new DelegateCommand<AlarmRecord>(ShowAlarmDetail);

            AlarmItems = new ObservableCollection<AlarmRecord>();

            // 注意：ConnectDataUpdated 订阅只在 OnNavigatedTo/OnNavigatedFrom 中配对进行，
            // 构造函数中不要重复订阅，否则会造成双重触发且离开页面后永久后台刷新
        }

        #region 数据初始化
        private async void InitializeCardDataAsync()
        {
            try
            {
                WriteLog("[HomePage] 开始加载历史时间序列数据");
                var columns = new[] { "InFlow_2", "OutletFlow_2", "OutletB_2", "OutletTurbidity_2" };
                var dict = await _databaseService.LoadHomePageTimeSeriesAsync(columns, 1);
                if (dict != null)
                {
                    LoadBuffer(dict, "InFlow_2", _inFlowBuffer);
                    LoadBuffer(dict, "OutletFlow_2", _outletFlowBuffer);
                    LoadBuffer(dict, "OutletB_2", _bBuffer);
                    LoadBuffer(dict, "OutletTurbidity_2", _turbidityBuffer);
                    RefreshAllCards();
                }

                var dict24 = await _databaseService.LoadHomePageTimeSeriesAsync(columns, 24);
                if (dict24 != null)
                {
                    LoadBuffer(dict24, "OutletFlow_2", _flow24h);
                    LoadBuffer(dict24, "OutletB_2", _b24h);
                    LoadBuffer(dict24, "OutletTurbidity_2", _turbidity24h);
                    RefreshRealTimeChart();
                }
                WriteLog("[HomePage] 历史时间序列数据加载完成");
            }
            catch (Exception ex)
            {
                WriteLog($"[HomePage] 加载历史时间序列失败: {ex.Message}");
            }
        }

        private static void LoadBuffer(Dictionary<string, System.Collections.Generic.List<DateTimePoint>> dict,
            string key, TimeSeriesBuffer buffer)
        {
            buffer.Clear();
            if (dict.TryGetValue(key, out var list))
            {
                foreach (var p in list)
                    buffer.Push(p.DateTime, p.Value ?? 0);
            }
        }
        #endregion

        #region 采样
        private void SamplingTimer_Tick(object sender, EventArgs e)
        {
            PushAndRefreshAll();
        }

        private void PushAndRefreshAll()
        {
            try
            {
                var now = DateTime.Now;
                var list = Data.ConnectDataList;
                if (list == null) return;

                foreach (var cd in list)
                {
                    if (cd.Value == null) continue;
                    var val = Convert.ToDouble(cd.Value);
                    switch (cd.Name)
                    {
                        case "InFlow_2": _inFlowBuffer.Push(now, val); break;
                        case "OutletFlow_2": _outletFlowBuffer.Push(now, val); _flow24h.Push(now, val); break;
                        case "OutletB_2": _bBuffer.Push(now, val); _b24h.Push(now, val); break;
                        case "OutletTurbidity_2": _turbidityBuffer.Push(now, val); _turbidity24h.Push(now, val); break;
                    }
                }
                RefreshAllCards();
                RefreshRealTimeChart();
            }
            catch (Exception ex)
            {
                WriteLog($"[HomePage] PushAndRefreshAll 异常: {ex.Message}");
            }
        }
        #endregion

        #region 卡片刷新
        private void RefreshAllCards()
        {
            RefreshFlowCard();
            RefreshBCard();
            RefreshTurbidityCard();
            RefreshProcessData();
        }

        private void RefreshFlowCard()
        {
            var inLast = _inFlowBuffer.Last();
            var cfLast = _outletFlowBuffer.Last();
            CurrentInFlow = inLast?.Value ?? 0;
            CurrentOutletFlow = cfLast?.Value ?? 0;

            var diff = CurrentInFlow - CurrentOutletFlow;
            var max = Math.Max(CurrentInFlow, CurrentOutletFlow);
            FlowBalanceRatio = max > 0 ? Math.Clamp(diff / max * 100, -100, 100) : 0;

            FlowChangeRate = $"进水 {CalcRate(_inFlowBuffer)}  出厂 {CalcRate(_outletFlowBuffer)}";

            var diffPoints = new List<DateTimePoint>();
            var inSnap = _inFlowBuffer.Snapshot();
            var cfSnap = _outletFlowBuffer.Snapshot();
            
            var allTimes = new SortedSet<DateTime>();
            foreach (var p in inSnap) allTimes.Add(p.DateTime);
            foreach (var p in cfSnap) allTimes.Add(p.DateTime);
            
            var inDict = inSnap.ToDictionary(p => p.DateTime, p => p.Value);
            var cfDict = cfSnap.ToDictionary(p => p.DateTime, p => p.Value);
            
            foreach (var t in allTimes)
            {
                if (inDict.TryGetValue(t, out var inVal) && cfDict.TryGetValue(t, out var cfVal))
                    diffPoints.Add(new DateTimePoint(t, inVal - cfVal));
            }

            // 首次使用时创建一次 Series/Axes，之后复用（只同步数据点）
            if (_flowDiffPoints == null)
            {
                _flowDiffPoints = new ObservableCollection<DateTimePoint>();
                FlowDiffSeries = new ISeries[]
                {
                    new LineSeries<DateTimePoint>
                    {
                        Values = _flowDiffPoints,
                        GeometrySize = 0,
                        Stroke = new SolidColorPaint(SKColors.SteelBlue) { StrokeThickness = 1.5f },
                        Fill = null, LineSmoothness = 0.5
                    }
                };
                _flowDiffXAxis = new Axis
                {
                    Labeler = value => TimeAxisLabel(value, "HH:mm"),
                    TextSize = 9
                };
                FlowDiffXAxes = new Axis[] { _flowDiffXAxis };
            }
            SyncPoints(_flowDiffPoints, diffPoints);

            long flowStepTicks = TimeSpan.FromMinutes(5).Ticks;
            if (diffPoints.Count >= 2)
            {
                var span = diffPoints[diffPoints.Count - 1].DateTime - diffPoints[0].DateTime;
                if (span.Ticks > 0) flowStepTicks = span.Ticks / 4;
            }
            _flowDiffXAxis.MinStep = flowStepTicks;
        }

        private void RefreshBCard()
        {
            CurrentB = _bBuffer.Last()?.Value ?? 0;
            BChangeRate = CalcRate(_bBuffer);
            var bSnapshot = _bBuffer.Snapshot();

            // 首次使用时创建一次 Series/Axes，之后复用（只同步数据点）
            if (_bPoints == null)
            {
                _bPoints = new ObservableCollection<DateTimePoint>();
                BTrend = new ISeries[]
                {
                    new LineSeries<DateTimePoint>
                    {
                        Values = _bPoints,
                        GeometrySize = 0,
                        Stroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 1.5f },
                        Fill = null, LineSmoothness = 0.5
                    }
                };
                _bXAxis = new Axis
                {
                    Labeler = value => TimeAxisLabel(value, "HH:mm"),
                    TextSize = 9
                };
                BXAxes = new Axis[] { _bXAxis };
            }
            SyncPoints(_bPoints, bSnapshot);

            long bStepTicks = TimeSpan.FromMinutes(5).Ticks;
            if (bSnapshot.Count >= 2)
            {
                var span = bSnapshot[bSnapshot.Count - 1].DateTime - bSnapshot[0].DateTime;
                if (span.Ticks > 0) bStepTicks = span.Ticks / 4;
            }
            _bXAxis.MinStep = bStepTicks;
        }

        private void RefreshTurbidityCard()
        {
            CurrentTurbidity = _turbidityBuffer.Last()?.Value ?? 0;
            TurbidityChangeRate = CalcRate(_turbidityBuffer);
            var turbiditySnapshot = _turbidityBuffer.Snapshot();

            // 首次使用时创建一次 Series/Axes，之后复用（只同步数据点）
            if (_turbidityPoints == null)
            {
                _turbidityPoints = new ObservableCollection<DateTimePoint>();
                TurbidityTrend = new ISeries[]
                {
                    new LineSeries<DateTimePoint>
                    {
                        Values = _turbidityPoints,
                        GeometrySize = 0,
                        Stroke = new SolidColorPaint(SKColors.Orange) { StrokeThickness = 1.5f },
                        Fill = null, LineSmoothness = 0.5
                    }
                };
                _turbidityXAxis = new Axis
                {
                    Labeler = value => TimeAxisLabel(value, "HH:mm"),
                    TextSize = 9
                };
                TurbidityXAxes = new Axis[] { _turbidityXAxis };
            }
            SyncPoints(_turbidityPoints, turbiditySnapshot);

            long turbidityStepTicks = TimeSpan.FromMinutes(5).Ticks;
            if (turbiditySnapshot.Count >= 2)
            {
                var span = turbiditySnapshot[turbiditySnapshot.Count - 1].DateTime - turbiditySnapshot[0].DateTime;
                if (span.Ticks > 0) turbidityStepTicks = span.Ticks / 4;
            }
            _turbidityXAxis.MinStep = turbidityStepTicks;
        }

        private void RefreshProcessData()
        {
            var dict = Data.CollectedData;

            #region 原水流量
             if (dict != null && dict.TryGetValue("InFlow_2", out var inflow) && inflow?.Value != null)
                InFlowDisplay = inflow.Value.ToString();
            else
                InFlowDisplay = "--";
            #endregion
            #region 原水浊度
            if (dict != null && dict.TryGetValue("InTurbidity_2", out var inTurbidity) && inTurbidity?.Value != null)
                InTurbidityDisplay = inTurbidity.Value.ToString();
            else
                InTurbidityDisplay = "--";
            #endregion
            
            #region 沉后水浊度
            if (dict != null && dict.TryGetValue("SettledTurbidity_2_1_1", out var outTurbidity) && outTurbidity?.Value != null)
                OutTurbidityDisplay = outTurbidity.Value.ToString();
            else
                OutTurbidityDisplay = "--";
            #endregion

            #region 药泵运行状态
            if (dict != null && dict.TryGetValue("IsWorkMainA_2_1", out var workMainA_2_1) && workMainA_2_1?.Value != null)
                IsWorkMainA_2_1 = Convert.ToBoolean(workMainA_2_1.Value);
            else
                IsWorkMainA_2_1 = false;

            if (dict != null && dict.TryGetValue("IsWorkMainA_2_3", out var workMainA_2_3) && workMainA_2_3?.Value != null)
                IsWorkMainA_2_3 = Convert.ToBoolean(workMainA_2_3.Value);
            else
                IsWorkMainA_2_3 = false;

            if (dict != null && dict.TryGetValue("IsWorkMainA_2_4", out var workMainA_2_4) && workMainA_2_4?.Value != null)
                IsWorkMainA_2_4 = Convert.ToBoolean(workMainA_2_4.Value);
            else
                IsWorkMainA_2_4 = false;

            if (dict != null && dict.TryGetValue("IsWorkMainA_2_6", out var workMainA_2_6) && workMainA_2_6?.Value != null)
                IsWorkMainA_2_6 = Convert.ToBoolean(workMainA_2_6.Value);
            else
                IsWorkMainA_2_6 = false;
            #endregion

            #region 智能运行状态
            if (dict != null && dict.TryGetValue("IsAutoMainA_2_1", out var autoMainA_2_1) && autoMainA_2_1?.Value != null)
                IsAutoMainA_2_1 = Convert.ToInt32(autoMainA_2_1.Value) == 3 ? "智能" : "人工";
            else
                IsAutoMainA_2_1 = "--";

            if (dict != null && dict.TryGetValue("IsAutoMainA_2_2", out var autoMainA_2_2) && autoMainA_2_2?.Value != null)
                IsAutoMainA_2_2 = Convert.ToInt32(autoMainA_2_2.Value) == 3 ? "智能" : "人工";
            else
                IsAutoMainA_2_2 = "--";

            if (dict != null && dict.TryGetValue("IsAutoMainA_2_3", out var autoMainA_2_3) && autoMainA_2_3?.Value != null)
                IsAutoMainA_2_3 = Convert.ToInt32(autoMainA_2_3.Value) == 3 ? "智能" : "人工";
            else
                IsAutoMainA_2_3 = "--";

            if (dict != null && dict.TryGetValue("IsAutoMainA_2_4", out var autoMainA_2_4) && autoMainA_2_4?.Value != null)
                IsAutoMainA_2_4 = Convert.ToInt32(autoMainA_2_4.Value) == 3 ? "智能" : "人工";
            else
                IsAutoMainA_2_4 = "--";
            #endregion
            #region 泵后流量
            if (dict != null && dict.TryGetValue("MainABack_2_1", out var backA_1) && backA_1?.Value != null)
                BackA_1Display = backA_1.Value.ToString();
            else
                BackA_1Display = "--";

            if (dict != null && dict.TryGetValue("MainABack_2_3", out var backA_3) && backA_3?.Value != null)
                BackA_3Display = backA_3.Value.ToString();
            else
                BackA_3Display = "--";

            if (dict != null && dict.TryGetValue("MainABack_2_4", out var backA_4) && backA_4?.Value != null)
                BackA_4Display = backA_4.Value.ToString();
            else
                BackA_4Display = "--";

            if (dict != null && dict.TryGetValue("MainABack_2_6", out var backA_6) && backA_6?.Value != null)
                BackA_6Display = backA_6.Value.ToString();
            else
                BackA_6Display = "--";
            #endregion
            #region 人工投加率
            if (dict != null && dict.TryGetValue("MainAManualUnitRate_2_1", out var manualUnitRate_2_1) && manualUnitRate_2_1?.Value != null)
                MainAManualUnitRate_2_1 = manualUnitRate_2_1.Value.ToString();
            else
                MainAManualUnitRate_2_1 = "--";

            if (dict != null && dict.TryGetValue("MainAManualUnitRate_2_3", out var manualUnitRate_2_3) && manualUnitRate_2_3?.Value != null)
                MainAManualUnitRate_2_3 = manualUnitRate_2_3.Value.ToString();
            else
                MainAManualUnitRate_2_3 = "--";

            if (dict != null && dict.TryGetValue("MainAManualUnitRate_2_4", out var manualUnitRate_2_4) && manualUnitRate_2_4?.Value != null)
                MainAManualUnitRate_2_4 = manualUnitRate_2_4.Value.ToString();
            else
                MainAManualUnitRate_2_4 = "--";

            if (dict != null && dict.TryGetValue("MainAManualUnitRate_2_6", out var manualUnitRate_2_6) && manualUnitRate_2_6?.Value != null)
                MainAManualUnitRate_2_6 = manualUnitRate_2_6.Value.ToString();
            else
                MainAManualUnitRate_2_6 = "--";
            #endregion
            #region 智能投加率
            if (dict != null && dict.TryGetValue("PredictMainAUnitRate_2_1", out var predictUnitRate_2_1) && predictUnitRate_2_1?.Value != null)
                PredictMainAUnitRate_2_1 = predictUnitRate_2_1.Value.ToString();
            else
                PredictMainAUnitRate_2_1 = "--";

            if (dict != null && dict.TryGetValue("PredictMainAUnitRate_2_2", out var predictUnitRate_2_2) && predictUnitRate_2_2?.Value != null)
                PredictMainAUnitRate_2_2 = predictUnitRate_2_2.Value.ToString();
            else
                PredictMainAUnitRate_2_2 = "--";

            if (dict != null && dict.TryGetValue("PredictMainAUnitRate_2_3", out var predictUnitRate_2_3) && predictUnitRate_2_3?.Value != null)
                PredictMainAUnitRate_2_3 = predictUnitRate_2_3.Value.ToString();
            else
                PredictMainAUnitRate_2_3 = "--";

            if (dict != null && dict.TryGetValue("PredictMainAUnitRate_2_4", out var predictUnitRate_2_4) && predictUnitRate_2_4?.Value != null)
                PredictMainAUnitRate_2_4 = predictUnitRate_2_4.Value.ToString();
            else
                PredictMainAUnitRate_2_4 = "--";

            #endregion
        }

        private void OnConnectDataUpdated()
        {
            try
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new Action(() =>
                    {
                        try
                        {
                            PushAndRefreshAll();
                        }
                        catch (Exception ex)
                        {
                            WriteLog($"[HomePage] OnConnectDataUpdated 回调异常: {ex.Message}");
                        }
                    }));
            }
            catch (Exception ex)
            {
                WriteLog($"[HomePage] OnConnectDataUpdated 异常: {ex.Message}");
            }
        }

        private static string CalcRate(TimeSeriesBuffer buffer)
        {
            var list = buffer.Snapshot();
            if (list.Count < 2) return "--";
            var current = list.Last().Value ?? 0;
            var ago = list.First().Value ?? 0;
            if (Math.Abs(ago) < 0.001) return "--";
            var rate = (current - ago) / Math.Abs(ago) * 100;
            return (rate >= 0 ? "▲" : "▼") + Math.Abs(rate).ToString("F1") + "%";
        }

        private void RefreshRealTimeChart()
        {
            // 首次使用时创建一次 Series/Axes，之后复用（只同步数据点和坐标轴范围）
            if (_rtFlowPoints == null)
            {
                _rtFlowPoints = new ObservableCollection<DateTimePoint>();
                _rtBPoints = new ObservableCollection<DateTimePoint>();
                _rtTurbidityPoints = new ObservableCollection<DateTimePoint>();
                RealTimeTrend = new ISeries[]
                {
                    new LineSeries<DateTimePoint>
                    {
                        Values = _rtFlowPoints,
                        GeometrySize = 0,
                        Stroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 1.5f },
                        Fill = null, LineSmoothness = 0.5,
                        ScalesYAt = 0,
                        Name = "出厂水流量 (m³/h)"
                    },
                    new LineSeries<DateTimePoint>
                    {
                        Values = _rtBPoints,
                        GeometrySize = 0,
                        Stroke = new SolidColorPaint(SKColors.MediumSeaGreen) { StrokeThickness = 1.5f },
                        Fill = null, LineSmoothness = 0.5,
                        ScalesYAt = 1,
                        Name = "出厂水B剂浓度 (mg/L)"
                    },
                    new LineSeries<DateTimePoint>
                    {
                        Values = _rtTurbidityPoints,
                        GeometrySize = 0,
                        Stroke = new SolidColorPaint(SKColors.Orange) { StrokeThickness = 1.5f },
                        Fill = null, LineSmoothness = 0.5,
                        ScalesYAt = 2,
                        Name = "出厂水浊度 (Turbidity)"
                    }
                };

                _realTimeXAxis = new Axis
                {
                    Labeler = value => TimeAxisLabel(value, "MM/dd HH:mm"),
                    MinStep = TimeSpan.FromHours(2).Ticks,
                    Name = "时间",
                    NameTextSize = 12,
                    TextSize = 10
                };
                RealTimeXAxes = new Axis[] { _realTimeXAxis };

                RealTimeYAxes = new Axis[]
                {
                    new Axis
                    {
                        Name = "m³/h",
                        Position = LiveChartsCore.Measure.AxisPosition.Start,
                        NameTextSize = 10,
                        TextSize = 10
                    },
                    new Axis
                    {
                        Name = "mg/L",
                        Position = LiveChartsCore.Measure.AxisPosition.End,
                        NameTextSize = 10,
                        TextSize = 10,
                        ShowSeparatorLines = false
                    },
                    new Axis
                    {
                        Name = "Turbidity",
                        Position = LiveChartsCore.Measure.AxisPosition.End,
                        NameTextSize = 10,
                        TextSize = 10,
                        ShowSeparatorLines = false
                    }
                };
            }

            SyncPoints(_rtFlowPoints, _flow24h.Snapshot());
            SyncPoints(_rtBPoints, _b24h.Snapshot());
            SyncPoints(_rtTurbidityPoints, _turbidity24h.Snapshot());

            _realTimeXAxis.MinLimit = DateTime.Now.AddHours(-24).Ticks;
            _realTimeXAxis.MaxLimit = DateTime.Now.Ticks;
        }
        #endregion

        #region 合规率图表
        private void InitComplianceCharts()
        {
            _complianceChartDate = DateTime.Now.Date;
            RefreshComplianceCharts();
        }

        private void RefreshComplianceCharts()
        {
            try
            {
                _complianceChartDate = DateTime.Now.Date;
                var today = _complianceChartDate.AddDays(-1);
                var start = today.AddDays(-6);
                var rng = new Random((int)today.Ticks);

                double Clamp(double v, double min, double max) => Math.Max(min, Math.Min(max, v));

                var bControlRate = Clamp(82 + rng.NextDouble() * 10, 0, 100);
                var bComplianceRate = Clamp(75 + rng.NextDouble() * 15, 0, 100);
                var aControlRate = Clamp(88 + rng.NextDouble() * 8, 0, 100);
                var aComplianceRate = Clamp(80 + rng.NextDouble() * 12, 0, 100);

                var passed = new double[] { bControlRate, bComplianceRate, aControlRate, aComplianceRate };
                var failed = passed.Select(v => Math.Max(0, 100 - v)).ToArray();

                DisposeSeriesPaints(_complianceBarSeries);
                ComplianceBarSeries = new ISeries[]
                {
                    new StackedRowSeries<double>
                    {
                        Values = new ObservableCollection<double>(passed),
                        Fill = new SolidColorPaint(SKColors.ForestGreen),
                        Stroke = null,
                        MaxBarWidth = 15,
                        Name = "达标"
                    },
                    new StackedRowSeries<double>
                    {
                        Values = new ObservableCollection<double>(failed),
                        Fill = new SolidColorPaint(SKColors.Red),
                        Stroke = null,
                        MaxBarWidth = 15,
                        Name = "未达标"
                    }
                };

                ComplianceBarXAxes = new Axis[]
                {
                    new Axis
                    {
                        MinLimit = 0,
                        MaxLimit = 100,
                        Name = "%",
                        NameTextSize = 10,
                        TextSize = 10
                    }
                };

                ComplianceBarYAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = new string[] { "B剂控制率", "B剂合规率", "A剂控制率", "A剂合规率" },
                        LabelsRotation = 0,
                        TextSize = 9
                    }
                };

                var days = Enumerable.Range(0, 7).Select(i => start.AddDays(i)).ToArray();
                var bDaily = new double[7];
                var aDaily = new double[7];
                for (int i = 0; i < 7; i++)
                {
                    bDaily[i] = Clamp(68 + rng.NextDouble() * 25, 0, 100);
                    aDaily[i] = Clamp(73 + rng.NextDouble() * 22, 0, 100);
                }

                DisposeSeriesPaints(_dailyComplianceSeries);
                DailyComplianceSeries = new ISeries[]
                {
                    new ColumnSeries<double>
                    {
                        Values = new ObservableCollection<double>(bDaily),
                        Fill = new SolidColorPaint(SKColors.DodgerBlue),
                        Stroke = null,
                        MaxBarWidth = 12,
                        Padding = 4,
                        Name = "B剂"
                    },
                    new ColumnSeries<double>
                    {
                        Values = new ObservableCollection<double>(aDaily),
                        Fill = new SolidColorPaint(SKColors.MediumSeaGreen),
                        Stroke = null,
                        MaxBarWidth = 12,
                        Padding = 4,
                        Name = "A"
                    }
                };

                DailyComplianceXAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = days.Select(d => d.ToString("MM/dd")).ToArray(),
                        LabelsRotation = 0,
                        TextSize = 9
                    }
                };

                DailyComplianceYAxes = new Axis[]
                {
                    new Axis
                    {
                        MinLimit = 0,
                        MaxLimit = 100,
                        Name = "合规率(%)",
                        NameTextSize = 10,
                        TextSize = 10
                    }
                };

                WriteLog("[HomePage] 合规率图表刷新完成");
            }
            catch (Exception ex)
            {
                WriteLog($"[HomePage] RefreshComplianceCharts 异常: {ex.Message}");
            }
        }

        private void StartMidnightTimer()
        {
            if (_midnightTimer != null) return;
            _midnightTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _midnightTimer.Tick += (_, _) =>
            {
                if (DateTime.Now.Date != _complianceChartDate)
                {
                    RefreshComplianceCharts();
                }
            };
            _midnightTimer.Start();
        }

        private void StopMidnightTimer()
        {
            _midnightTimer?.Stop();
            _midnightTimer = null;
        }
        #endregion

        #region 定时器管理
        private void StartSamplingTimer()
        {
            if (_sampleTimer != null) return;
            _sampleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            _sampleTimer.Tick += SamplingTimer_Tick;
            _sampleTimer.Start();
        }

        private void StopSamplingTimer()
        {
            _sampleTimer?.Stop();
            _sampleTimer = null;
        }

        private void StartAutoRefresh()
        {
            if (_refreshTimer != null) return;
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _refreshTimer.Tick += (_, _) => LoadAlarmHistoryAsync();
            _refreshTimer.Start();
        }

        private void StopAutoRefresh()
        {
            _refreshTimer?.Stop();
            _refreshTimer = null;
        }
        #endregion

        #region 导航
        private void ChangeHistoricalAlarm() => _regionManager.RequestNavigate("ContentRegion", "HistoricalAlarm");

        private void ShowAlarmDetail(AlarmRecord record)
        {
            if (record == null) return;
            var parameters = new NavigationParameters { { "AlarmRecord", record } };
            _regionManager.RequestNavigate("ContentRegion", "AlarmDetail", parameters);
        }
        #endregion

        private async void LoadAlarmHistoryAsync()
        {
            try
            {
                var items = await _databaseService.GetAlarmHistoryAsync();
                AlarmItems = new ObservableCollection<AlarmRecord>(items);
                WriteLog($"[HomePage] 报警记录加载完成，共 {items?.Count ?? 0} 条");
            }
            catch (Exception ex)
            {
                WriteLog($"[HomePage] 加载报警记录失败: {ex.Message}");
            }
        }

        private void OnAlarmInserted()
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(LoadAlarmHistoryAsync));
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            WriteLog("[HomePage] 进入页面");
            _databaseService.ConnectDataUpdated += OnConnectDataUpdated;
            _databaseService.AlarmInserted += OnAlarmInserted;
            LoadAlarmHistoryAsync();
            InitializeCardDataAsync();
            StartSamplingTimer();
            StartAutoRefresh();
            InitComplianceCharts();
            StartMidnightTimer();
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            WriteLog("[HomePage] 离开页面");
            _databaseService.ConnectDataUpdated -= OnConnectDataUpdated;
            _databaseService.AlarmInserted -= OnAlarmInserted;
            StopSamplingTimer();
            StopAutoRefresh();
            StopMidnightTimer();
            _inFlowBuffer.Clear();
            _outletFlowBuffer.Clear();
            _bBuffer.Clear();
            _turbidityBuffer.Clear();
            _flow24h.Clear();
            _b24h.Clear();
            _turbidity24h.Clear();
            
            DisposeSeriesPaints(_flowDiffSeries);
            DisposeSeriesPaints(_bTrend);
            DisposeSeriesPaints(_turbidityTrend);
            DisposeSeriesPaints(_realTimeTrend);
            DisposeSeriesPaints(_complianceBarSeries);
            DisposeSeriesPaints(_dailyComplianceSeries);
            
            _flowDiffPoints = null;
            _bPoints = null;
            _turbidityPoints = null;
            _rtFlowPoints = null;
            _rtBPoints = null;
            _rtTurbidityPoints = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            StopSamplingTimer();
            StopAutoRefresh();
            StopMidnightTimer();

            DisposeSeriesPaints(_flowDiffSeries);
            DisposeSeriesPaints(_bTrend);
            DisposeSeriesPaints(_turbidityTrend);
            DisposeSeriesPaints(_realTimeTrend);
            DisposeSeriesPaints(_complianceBarSeries);
            DisposeSeriesPaints(_dailyComplianceSeries);

            _flowDiffPoints = null;
            _bPoints = null;
            _turbidityPoints = null;
            _rtFlowPoints = null;
            _rtBPoints = null;
            _rtTurbidityPoints = null;

            WriteLog("[HomePage] Dispose 完成");
        }
    }
}
