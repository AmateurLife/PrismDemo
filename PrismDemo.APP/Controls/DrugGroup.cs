using System;
using System.Windows;

namespace PrismDemo.APP.Controls
{
    public class DrugGroup : Freezable
    {
        internal DrugDosageChart Owner { get; set; }

        public static readonly DependencyProperty DrugNameProperty =
            DependencyProperty.Register(nameof(DrugName), typeof(string), typeof(DrugGroup),
                new PropertyMetadata(string.Empty, OnPropertyChanged));

        public static readonly DependencyProperty PumpValueProperty =
            DependencyProperty.Register(nameof(PumpValue), typeof(double), typeof(DrugGroup),
                new PropertyMetadata(0.0, OnPropertyChanged));

        public static readonly DependencyProperty PointValueProperty =
            DependencyProperty.Register(nameof(PointValue), typeof(double), typeof(DrugGroup),
                new PropertyMetadata(0.0, OnPropertyChanged));

        public string DrugName
        {
            get => (string)GetValue(DrugNameProperty);
            set => SetValue(DrugNameProperty, value);
        }

        public double PumpValue
        {
            get => (double)GetValue(PumpValueProperty);
            set => SetValue(PumpValueProperty, value);
        }

        public double PointValue
        {
            get => (double)GetValue(PointValueProperty);
            set => SetValue(PointValueProperty, value);
        }

        protected override Freezable CreateInstanceCore()
        {
            return new DrugGroup();
        }

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DrugGroup)d).Owner?.RebuildChart();
        }
    }
}
