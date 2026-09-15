using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using PrismDemo.Core.Interfaces;
using PrismDemo.Core.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using static PrismDemo.Core.Services.AlertLogger;

namespace PrismDemo.APP.ViewModels
{
    public class AlarmDetailViewModel : BindableBase, INavigationAware, IDisposable
    {
        private readonly IRegionManager _regionManager;
        private readonly IDatabaseService _databaseService;
        private bool _disposed;

        private AlarmRecord _alarmRecord;
        public AlarmRecord AlarmRecord
        {
            get => _alarmRecord;
            set
            {
                if (SetProperty(ref _alarmRecord, value))
                    RaisePropertyChanged(nameof(IsRecordLoaded));
            }
        }

        public bool IsRecordLoaded => AlarmRecord != null;

        private AlarmConfig _alarmConfig;
        public AlarmConfig AlarmConfig
        {
            get => _alarmConfig;
            set => SetProperty(ref _alarmConfig, value);
        }

        private ISeries[] _series;
        public ISeries[] Series
        {
            get => _series;
            set => SetProperty(ref _series, value);
        }

        private Axis[] _xAxes;
        public Axis[] XAxes
        {
            get => _xAxes;
            set => SetProperty(ref _xAxes, value);
        }

        private Axis[] _yAxes;
        public Axis[] YAxes
        {
            get => _yAxes;
            set => SetProperty(ref _yAxes, value);
        }

        public DelegateCommand GoBackCmd { get; set; }

        public AlarmDetailViewModel(IRegionManager regionManager, IDatabaseService databaseService)
        {
            _regionManager = regionManager;
            _databaseService = databaseService;

            GoBackCmd = new DelegateCommand(() =>
                _regionManager.RequestNavigate("ContentRegion", "HistoricalAlarm"));

            YAxes = new Axis[]
            {
                new Axis { Name = "值", NameTextSize = 14 }
            };

            Debug.WriteLine("AlarmDetailViewModel 构造函数被调用");
        }

        public async void OnNavigatedTo(NavigationContext navigationContext)
        {
            Debug.WriteLine("OnNavigatedTo 被调用");

            if (navigationContext.Parameters.ContainsKey("AlarmRecord"))
            {
                var record = navigationContext.Parameters.GetValue<AlarmRecord>("AlarmRecord");
                if (record != null)
                {
                    Debug.WriteLine($"收到报警记录: {record.DeviceName}");

                    AlarmRecord = new AlarmRecord
                    {
                        Id = record.Id,
                        AlarmTime = record.AlarmTime,
                        DeviceName = record.DeviceName,
                        ConfigId = record.ConfigId,
                        ConfigType = record.ConfigType,
                        Value = record.Value,
                        AlarmContent = record.AlarmContent,
                        IsChecked = record.IsChecked,
                        IsShowed = record.IsShowed,
                        ConfirmTime = record.ConfirmTime
                    };

                    LoadAlarmDetailAsync(record);

                    if (!record.IsChecked && record.Id > 0)
                    {
                        try
                        {
                            await _databaseService.ConfirmAlarmIfNeededAsync(record.Id, DateTime.Now);
                            AlarmRecord.IsChecked = true;
                            AlarmRecord.ConfirmTime = DateTime.Now;
                            Debug.WriteLine($"[AlarmDetail] 已确认报警记录 recordId={record.Id}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[AlarmDetail] 确认报警失败: {ex.Message}");
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("收到的报警记录为 null");
                }
            }
            else
            {
                Debug.WriteLine("导航参数中没有找到 AlarmRecord");
            }
        }

        private async void LoadAlarmDetailAsync(AlarmRecord record)
        {
            try
            {
                if (record.ConfigId > 0)
                {
                    AlarmConfig = await _databaseService.GetAlarmConfigByIdAsync(record.ConfigId);
                    Debug.WriteLine($"[AlarmDetail] AlarmConfig 加载完成: {AlarmConfig?.ConnectName}");
                }

                var alarmTime = record.AlarmTime;
                var startTime = alarmTime.AddHours(-1);
                var endTime = alarmTime.AddHours(1);

                var columnName = record.DeviceName;
                if (AlarmConfig != null && !string.IsNullOrEmpty(AlarmConfig.ConnectName))
                    columnName = AlarmConfig.ConnectName;

                var dataPoints = await _databaseService.GetColumnTimeSeriesAsync(columnName, startTime, endTime);

                if (dataPoints != null && dataPoints.Count > 0)
                {
                    BuildChart(dataPoints, alarmTime);
                    Debug.WriteLine($"[AlarmDetail] 折线图加载完成，共 {dataPoints.Count} 个数据点");
                }
                else
                {
                    Debug.WriteLine("[AlarmDetail] 无时间序列数据");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AlarmDetail] 加载失败: {ex.Message}");
            }
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
                }
                catch { }
            }
        }

        private void BuildChart(IEnumerable<DateTimePoint> dataPoints, DateTime alarmTime)
        {
            DisposeSeriesPaints(_series);

            var alarmTicks = alarmTime.Ticks;
            var pointsArray = new ObservableCollection<DateTimePoint>(dataPoints);

            var yValues = dataPoints.Select(p => p.Value).ToList();
            var minY = yValues.Min();
            var maxY = yValues.Max();
            var padding = (maxY - minY) * 0.1;
            var yMinLimit = minY - padding;
            var yMaxLimit = maxY + padding;

            Series = new ISeries[]
            {
                new LineSeries<DateTimePoint>
                {
                    Values = pointsArray,
                    GeometrySize = 0,
                    Stroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 2 },
                    Fill = null,
                    LineSmoothness = 0.3,
                    Name = "数据值",
                    ZIndex = 1
                },
                new LineSeries<DateTimePoint>
                {
                    Values = new ObservableCollection<DateTimePoint>
                    {
                        new DateTimePoint(new DateTime(alarmTicks), yMinLimit),
                        new DateTimePoint(new DateTime(alarmTicks), yMaxLimit)
                    },
                    GeometrySize = 0,
                    Stroke = new SolidColorPaint(SKColors.Red) { StrokeThickness = 3 },
                    Fill = null,
                    LineSmoothness = 0,
                    Name = "报警点",
                    ZIndex = 2
                }
            };

            XAxes = new Axis[]
            {
                new Axis
                {
                    Labeler = value =>
                    {
                        if (double.IsNaN(value) || double.IsInfinity(value))
                            return string.Empty;
                        long ticks = (long)value;
                        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
                            return string.Empty;
                        return new DateTime(ticks).ToString("HH:mm");
                    },
                    UnitWidth = TimeSpan.FromMinutes(1).Ticks,
                    MinStep = TimeSpan.FromMinutes(10).Ticks,
                    MinLimit = alarmTime.AddMinutes(-70).Ticks,
                    MaxLimit = alarmTime.AddMinutes(70).Ticks,
                    Name = "时间",
                    NameTextSize = 14,
                    TextSize = 12
                }
            };

            YAxes = new Axis[]
            {
                new Axis
                {
                    Name = "值",
                    NameTextSize = 14,
                    MinLimit = yMinLimit,
                    MaxLimit = yMaxLimit
                }
            };
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            WriteLog("[AlarmDetail] 离开页面");
            DisposeSeriesPaints(_series);
            _series = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            DisposeSeriesPaints(_series);
            _series = null;

            WriteLog("[AlarmDetail] Dispose 完成");
        }
    }
}
