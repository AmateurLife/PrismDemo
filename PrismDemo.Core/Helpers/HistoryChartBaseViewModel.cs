using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Win32;
using Prism.Commands;
using Prism.Mvvm;
using PrismDemo.Core.Models;
using PrismDemo.Core.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using static PrismDemo.Core.Services.AlertLogger;

namespace PrismDemo.Core.Helpers
{
    public abstract class HistoryChartBaseViewModel : BindableBase, IDisposable
    {
        protected abstract (string Field, string Title, string Color, int Axis)[] AllCurves { get; }
        protected abstract Axis[] AllYAxisDefs { get; }
        protected abstract string ExportFileNamePrefix { get; }
        protected abstract bool[] GetCheckedFlags();
        protected abstract ChartQueryResult QueryHistoricalData(string[] fields, DateTime start, DateTime end);

        private List<ChartQueryResult> _segments = new();
        private readonly Dictionary<int, int> _axisIndexMap = new();
        private CancellationTokenSource _cts = new();
        private bool _disposed;

        private ISeries[] _series = Array.Empty<ISeries>();
        public ISeries[] Series { get => _series; set => SetProperty(ref _series, value); }

        private Axis[] _xAxes;
        public Axis[] XAxes { get => _xAxes; set => SetProperty(ref _xAxes, value); }

        private Axis[] _yAxes;
        public Axis[] YAxes { get => _yAxes; set => SetProperty(ref _yAxes, value); }

        private DateTime _startDate = DateTime.Today.AddDays(-7);
        public DateTime StartDate { get => _startDate; set => SetProperty(ref _startDate, value); }

        private DateTime _endDate = DateTime.Today.AddDays(1).AddSeconds(-1);
        public DateTime EndDate { get => _endDate; set => SetProperty(ref _endDate, value); }

        public DelegateCommand QueryCommand { get; }
        public DelegateCommand ExportCommand { get; }
        public DelegateCommand BackCommand { get; }
        public DelegateCommand ResetZoomCommand { get; }

        protected HistoryChartBaseViewModel()
        {
            InitAxes();
            UpdateYAxes();

            QueryCommand = new DelegateCommand(async () => await QueryAsync());
            ExportCommand = new DelegateCommand(async () => await ExportCsvAsync());
            BackCommand = new DelegateCommand(CloseWindow);
            ResetZoomCommand = new DelegateCommand(ResetZoom);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { _cts?.Cancel(); } catch { }
            _cts?.Dispose();
            _cts = null;

            _segments?.Clear();
            _segments = null;

            DisposeSeriesArray(_series);
            _series = null;

            DisposeAxisArray(_xAxes);
            _xAxes = null;

            DisposeAxisArray(_yAxes);
            _yAxes = null;

            GC.SuppressFinalize(this);
        }

        private static void DisposeSeriesArray(ISeries[] seriesArray)
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

        private static void DisposeAxisArray(Axis[] axisArray)
        {
            if (axisArray == null) return;
            foreach (var axis in axisArray)
            {
                try { (axis as IDisposable)?.Dispose(); } catch { }
            }
        }

        private void CloseWindow()
        {
            var window = Application.Current.Windows.OfType<Window>()
                .FirstOrDefault(w => w.DataContext == this);
            window?.Close();
        }

        private void InitAxes()
        {
            XAxes = new Axis[]
            {
                new Axis
                {
                    Labeler = value =>
                    {
                        var ticks = (long)value;
                        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
                            return "";
                        return new DateTime(ticks).ToString("MM-dd HH:mm");
                    },
                    MinStep = TimeSpan.FromMinutes(30).Ticks,
                    Name = "时间",
                    NameTextSize = 12,
                    TextSize = 10
                }
            };
        }

        private void ResetZoom()
        {
            var oldAxes = _xAxes;
            InitAxes();
            DisposeAxisArray(oldAxes);
        }

        protected void UpdateYAxes()
        {
            _axisIndexMap.Clear();
            var flags = GetCheckedFlags();
            var usedAxes = AllCurves
                .Select((c, i) => (c.Axis, Checked: i < flags.Length && flags[i]))
                .Where(x => x.Checked)
                .Select(x => x.Axis)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var axes = new List<Axis>();
            var targetAxes = usedAxes.Count > 0 ? usedAxes : Enumerable.Range(0, AllYAxisDefs.Length).ToList();
            foreach (var idx in targetAxes)
            {
                _axisIndexMap[idx] = axes.Count;
                var def = AllYAxisDefs[idx];
                axes.Add(new Axis
                {
                    Name = def.Name,
                    NameTextSize = def.NameTextSize,
                    TextSize = def.TextSize,
                    Position = def.Position,
                    ShowSeparatorLines = axes.Count == 0
                });
            }

            var oldAxes = _yAxes;
            YAxes = axes.ToArray();
            DisposeAxisArray(oldAxes);
        }

        private static double ComputeGapThreshold(double[] timestamps)
        {
            if (timestamps.Length < 10) return double.MaxValue;
            var intervals = new double[timestamps.Length - 1];
            for (int i = 1; i < timestamps.Length; i++)
                intervals[i - 1] = timestamps[i] - timestamps[i - 1];
            Array.Sort(intervals);
            var median = intervals[intervals.Length / 2];
            return Math.Max(median * 5, 30.0 / 1440.0);
        }

        private static List<(int start, int end)> SplitRanges(double[] timestamps, double threshold)
        {
            var ranges = new List<(int start, int end)>();
            int segStart = 0;
            for (int i = 1; i < timestamps.Length; i++)
            {
                if (timestamps[i] - timestamps[i - 1] > threshold)
                {
                    ranges.Add((segStart, i - 1));
                    segStart = i;
                }
            }
            ranges.Add((segStart, timestamps.Length - 1));
            return ranges;
        }

        private static ChartQueryResult ExtractSegment(ChartQueryResult source, int startIdx, int endIdx)
        {
            int len = endIdx - startIdx + 1;
            var timestamps = new double[len];
            Array.Copy(source.Timestamps, startIdx, timestamps, 0, len);
            var values = new Dictionary<string, double[]>(source.Values.Count);
            foreach (var kvp in source.Values)
            {
                var arr = new double[len];
                Array.Copy(kvp.Value, startIdx, arr, 0, len);
                values[kvp.Key] = arr;
            }
            return new ChartQueryResult { Timestamps = timestamps, Values = values };
        }

        protected void RebuildSeries()
        {
            if (_segments == null || _segments.Count == 0) return;
            UpdateYAxes();

            var flags = GetCheckedFlags();
            var newSeries = new List<ISeries>();
            for (int i = 0; i < AllCurves.Length; i++)
            {
                if (i >= flags.Length || !flags[i]) continue;
                var c = AllCurves[i];
                int mappedAxis = _axisIndexMap.GetValueOrDefault(c.Axis, 0);

                var points = new List<DateTimePoint>();
                foreach (var seg in _segments)
                {
                    if (!seg.Values.TryGetValue(c.Field, out var yData) || yData.Length == 0) continue;

                    if (points.Count > 0)
                        points.Add(new DateTimePoint(DateTime.FromOADate(seg.Timestamps[0]), null));

                    for (int j = 0; j < seg.Timestamps.Length; j++)
                        points.Add(new DateTimePoint(DateTime.FromOADate(seg.Timestamps[j]), yData[j]));
                }

                if (points.Count > 0)
                {
                    newSeries.Add(new LineSeries<DateTimePoint>
                    {
                        Name = c.Title,
                        Values = points,
                        ScalesYAt = mappedAxis,
                        Stroke = new SolidColorPaint(SKColor.Parse(c.Color)) { StrokeThickness = 1.5f },
                        Fill = null,
                        GeometrySize = 0,
                        LineSmoothness = 0,

                    });
                }
            }

            var oldSeries = _series;
            Series = newSeries.ToArray();
            DisposeSeriesArray(oldSeries);
        }

        protected async Task QueryAsync()
        {
            if (_disposed) return;

            var oldSeries = _series;
            Series = Array.Empty<ISeries>();
            DisposeSeriesArray(oldSeries);

            _segments.Clear();

            try { _cts?.Cancel(); } catch { }
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var selected = AllCurves
                .Select((c, i) => (Curve: c, Index: i))
                .Where(x => x.Index < GetCheckedFlags().Length && GetCheckedFlags()[x.Index])
                .Select(x => x.Curve)
                .ToList();

            if (selected.Count == 0) return;

            var end = EndDate.Date.AddDays(1).AddSeconds(-1);
            var rawData = await Task.Run(() =>
                QueryHistoricalData(selected.Select(c => c.Field).ToArray(), StartDate, end), token);

            if (token.IsCancellationRequested || _disposed) return;
            if (rawData == null || rawData.Timestamps.Length == 0) return;

            var threshold = ComputeGapThreshold(rawData.Timestamps);
            var ranges = SplitRanges(rawData.Timestamps, threshold);

            foreach (var (segStart, segEnd) in ranges)
            {
                var seg = ExtractSegment(rawData, segStart, segEnd);
                if (seg.Timestamps.Length > 3000)
                    seg = DataDownsampler.Downsample(seg, 2000);
                _segments.Add(seg);
            }

            if (!token.IsCancellationRequested && !_disposed)
                RebuildSeries();
        }

        private async Task ExportCsvAsync()
        {
            try
            {
                var flags = GetCheckedFlags();
                var selected = AllCurves.Where((_, i) => i < flags.Length && flags[i]).ToList();
                if (selected.Count == 0) return;

                var dlg = new SaveFileDialog
                {
                    Filter = "CSV文件 (*.csv)|*.csv",
                    FileName = $"{ExportFileNamePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };
                if (dlg.ShowDialog() != true) return;

                var end = EndDate.Date.AddDays(1).AddSeconds(-1);
                var fullData = await Task.Run(() =>
                    QueryHistoricalData(selected.Select(c => c.Field).ToArray(), StartDate, end));

                if (fullData == null || fullData.Timestamps.Length == 0)
                {
                    ShowMessageBox("没有数据可导出", "导出", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                using var sw = new StreamWriter(dlg.FileName, false, Encoding.UTF8);
                sw.WriteLine("时间," + string.Join(",", selected.Select(c => c.Title)));

                for (int r = 0; r < fullData.Timestamps.Length; r++)
                {
                    var time = DateTime.FromOADate(fullData.Timestamps[r]).ToString("yyyy-MM-dd HH:mm:ss");
                    var line = new List<string> { time };
                    foreach (var c in selected)
                    {
                        if (fullData.Values.TryGetValue(c.Field, out var yData) && r < yData.Length)
                            line.Add(yData[r].ToString("F3"));
                        else
                            line.Add("0");
                    }
                    sw.WriteLine(string.Join(",", line));
                }

                ShowMessageBox("导出完成", "历史数据", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExportCsv] 导出失败: {ex.Message}");
                ShowMessageBox($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
