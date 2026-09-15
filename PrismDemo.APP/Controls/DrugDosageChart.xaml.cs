using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace PrismDemo.APP.Controls
{
    [ContentProperty(nameof(Groups))]
    public partial class DrugDosageChart : UserControl
    {
        public static readonly DependencyProperty GroupsProperty =
            DependencyProperty.Register(nameof(Groups), typeof(FreezableCollection<DrugGroup>), typeof(DrugDosageChart),
                new PropertyMetadata(null, OnGroupsChanged));

        public DrugDosageChart()
        {
            InitializeComponent();
            Groups = new FreezableCollection<DrugGroup>();
        }

        public FreezableCollection<DrugGroup> Groups
        {
            get => (FreezableCollection<DrugGroup>)GetValue(GroupsProperty);
            set => SetValue(GroupsProperty, value);
        }

        private static void OnGroupsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var chart = (DrugDosageChart)d;
            if (e.OldValue is FreezableCollection<DrugGroup> oldCol)
            {
                oldCol.Changed -= chart.OnCollectionChanged;
                foreach (var g in oldCol) g.Owner = null;
            }
            if (e.NewValue is FreezableCollection<DrugGroup> newCol)
            {
                newCol.Changed += chart.OnCollectionChanged;
                foreach (var g in newCol) g.Owner = chart;
                chart.RebuildChart();
            }
        }

        private void OnCollectionChanged(object sender, EventArgs e)
        {
            if (Groups != null)
                foreach (var g in Groups) g.Owner = this;
            RebuildChart();
        }

        internal void RebuildChart()
        {
            var groups = Groups?.ToArray() ?? Array.Empty<DrugGroup>();
            if (groups.Length == 0)
            {
                Chart.Series = Array.Empty<ISeries>();
                Chart.XAxes = Array.Empty<Axis>();
                Chart.YAxes = Array.Empty<Axis>();
                return;
            }

            var drugNames = groups.Select(g => g.DrugName ?? "").ToArray();
            var pumpValues = groups.Select(g => g.PumpValue).ToArray();
            var pointValues = groups.Select(g => g.PointValue).ToArray();

            Chart.Series = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = new ObservableCollection<double>(pumpValues),
                    Fill = new SolidColorPaint(SKColors.ForestGreen),
                    Stroke = null,
                    MaxBarWidth = 20,
                    Padding = 4,
                    Name = "泵后加药量"
                },
                new ColumnSeries<double>
                {
                    Values = new ObservableCollection<double>(pointValues),
                    Fill = new SolidColorPaint(new SKColor(255, 152, 0)),
                    Stroke = null,
                    MaxBarWidth = 20,
                    Padding = 4,
                    Name = "控制点加药量"
                }
            };

            Chart.XAxes = new Axis[]
            {
                new Axis
                {
                    Labels = drugNames,
                    LabelsRotation = 0,
                    TextSize = 10
                }
            };

            Chart.YAxes = new Axis[]
            {
                new Axis
                {
                    MinLimit = 0,
                    TextSize = 10
                }
            };
        }
    }
}
