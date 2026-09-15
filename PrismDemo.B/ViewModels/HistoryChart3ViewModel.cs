using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using Prism.Regions;
using PrismDemo.Core.Helpers;
using PrismDemo.Core.Models;
using PrismDemo.B.Services;
using System;

namespace PrismDemo.B.ViewModels
{
    public class HistoryChart3ViewModel : HistoryChartBaseViewModel
    {
        private readonly BDataService _bDataService;

        private static readonly (string Field, string Title, string Color, int Axis)[] CurveDefs =
        {
            ("TankFlow_2_1_1",        "滤后流量",       "#1976D2", 0),
            ("TankB_2_1_N_1",       "调节池前端B剂浓度",  "#1E90FF", 1),
            ("TankB_2_1_N_2",       "调节池后端B剂浓度",  "#FF9800", 1),
            ("MainBManualLL_2_1",   "实际加药量",     "#4CAF50", 2),
            ("PredictMainB_2_1",    "智能加药量",     "#8BC34A", 2),
            ("PredictMainBUnitRate_2_1", "智能投加率",    "#FF5722", 2),
            ("isAutoMainB_2_1",     "是否智能加药",   "#9C27B0", 2),
        };

        private static readonly Axis[] YAxisDefs =
        {
            new Axis { Name = "滤后流量(m³/h)", NameTextSize = 13, TextSize = 12, Position = AxisPosition.Start },
            new Axis { Name = "B剂浓度(mg/L)", NameTextSize = 13, TextSize = 12, Position = AxisPosition.Start },
            new Axis { Name = "加药量/投加率", NameTextSize = 13, TextSize = 12, Position = AxisPosition.Start }
        };

        protected override (string Field, string Title, string Color, int Axis)[] AllCurves => CurveDefs;
        protected override Axis[] AllYAxisDefs => YAxisDefs;
        protected override string ExportFileNamePrefix => "B";

        private bool _isFlow, _isFrontB, _isBackB, _isActualB, _isPredictB, _isPredictUnitRate, _isAuto;

        public bool IsFlowChecked      { get => _isFlow;      set => SetCheck(ref _isFlow,      value, 0); }
        public bool IsFrontBChecked   { get => _isFrontB;   set => SetCheck(ref _isFrontB,   value, 1); }
        public bool IsBackBChecked    { get => _isBackB;    set => SetCheck(ref _isBackB,    value, 2); }
        public bool IsActualBChecked  { get => _isActualB;  set => SetCheck(ref _isActualB,  value, 3); }
        public bool IsPredictBChecked { get => _isPredictB; set => SetCheck(ref _isPredictB, value, 4); }
        public bool IsPredictUnitRateChecked { get => _isPredictUnitRate; set => SetCheck(ref _isPredictUnitRate, value, 5); }
        public bool IsAutoChecked      { get => _isAuto;      set => SetCheck(ref _isAuto,      value, 6); }

        public HistoryChart3ViewModel(BDataService bDataService)
        {
            _bDataService = bDataService;
        }

        protected override bool[] GetCheckedFlags() => new[]
        {
            _isFlow, _isFrontB, _isBackB, _isActualB,
            _isPredictB, _isPredictUnitRate, _isAuto
        };

        protected override ChartQueryResult QueryHistoricalData(string[] fields, DateTime start, DateTime end)
            => _bDataService.GetHistoryChartData(fields, start, end);

        private void SetCheck(ref bool field, bool value, int idx)
        {
            if (!SetProperty(ref field, value)) return;
            if (value)
                _ = QueryAsync();
            else
                RebuildSeries();
        }
    }
}
