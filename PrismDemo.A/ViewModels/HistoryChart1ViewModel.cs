using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using Prism.Regions;
using PrismDemo.Core.Helpers;
using PrismDemo.Core.Models;
using PrismDemo.A.Services;
using System;

namespace PrismDemo.A.ViewModels
{
    public class HistoryChart1ViewModel : HistoryChartBaseViewModel
    {
        private readonly ADataService _aDataService;

        private static readonly (string Field, string Title, string Color, int Axis)[] CurveDefs =
        {
            ("InFlow_2",           "原水流量",     "#1976D2", 0),
            ("SettledTurbidity_2_1_1",         "沉后水浊度",   "#1E90FF", 1),
            ("InTurbidity_2",          "原水浊度",     "#FF9800", 1),
            ("OutletTurbidity_2",     "出厂水浊度",   "#00BCD4", 1),
            ("MainABack_2_1",        "末端加药量",   "#4CAF50", 2),
            ("PredictMainA_2_1",     "智能加药量",   "#8BC34A", 2),
            ("PredictMainAUnitRate_2_1",  "智能投加率",     "#FF5722", 2),
            ("IsAutoMainA_2_1",         "是否智能加药", "#9C27B0", 2),
        };

        private static readonly Axis[] YAxisDefs =
        {
            new Axis { Name = "流量(m³/h)", NameTextSize = 13, TextSize = 12, Position = AxisPosition.Start },
            new Axis { Name = "浊度(Turbidity)", NameTextSize = 13, TextSize = 12, Position = AxisPosition.Start },
            new Axis { Name = "加药量/投加率", NameTextSize = 13, TextSize = 12, Position = AxisPosition.Start }
        };

        protected override (string Field, string Title, string Color, int Axis)[] AllCurves => CurveDefs;
        protected override Axis[] AllYAxisDefs => YAxisDefs;
        protected override string ExportFileNamePrefix => "A";

        private bool _isFlow, _isOutTurbidity, _isInTurbidity, _isOutlet, _isBackA, _isPredA, _isPredUnitRate, _isIsAuto;

        public bool IsFlowChecked      { get => _isFlow;      set => SetCheck(ref _isFlow,      value, 0); }
        public bool IsOutTurbidityChecked    { get => _isOutTurbidity;    set => SetCheck(ref _isOutTurbidity,    value, 1); }
        public bool IsInTurbidityChecked     { get => _isInTurbidity;     set => SetCheck(ref _isInTurbidity,     value, 2); }
        public bool IsOutletChecked  { get => _isOutlet;  set => SetCheck(ref _isOutlet,  value, 3); }
        public bool IsBackAChecked   { get => _isBackA;   set => SetCheck(ref _isBackA,   value, 4); }
        public bool IsPredAChecked   { get => _isPredA;   set => SetCheck(ref _isPredA,   value, 5); }
        public bool IsPredUnitRateChecked { get => _isPredUnitRate; set => SetCheck(ref _isPredUnitRate, value, 6); }
        public bool IsIsAutoChecked    { get => _isIsAuto;    set => SetCheck(ref _isIsAuto,    value, 7); }

        public HistoryChart1ViewModel(ADataService aDataService)
        {
            _aDataService = aDataService;
        }

        protected override bool[] GetCheckedFlags() => new[]
        {
            _isFlow, _isOutTurbidity, _isInTurbidity, _isOutlet,
            _isBackA, _isPredA, _isPredUnitRate, _isIsAuto
        };

        protected override ChartQueryResult QueryHistoricalData(string[] fields, DateTime start, DateTime end)
            => _aDataService.GetHistoryChartData(fields, start, end);

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
