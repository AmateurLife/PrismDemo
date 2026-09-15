using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PrismDemo.APP.Controls
{
    public enum GaugeLabelPosition
    {
        Bottom,
        Left
    }

    public partial class LiquidLevelGauge : UserControl
    {
        #region Dependency Properties

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(LiquidLevelGauge),
                new PropertyMetadata(0.0, OnPropertyChanged));

        public static readonly DependencyProperty MaxAlarmValueProperty =
            DependencyProperty.Register(nameof(MaxAlarmValue), typeof(double), typeof(LiquidLevelGauge),
                new PropertyMetadata(100.0, OnPropertyChanged));

        public static readonly DependencyProperty MinAlarmValueProperty =
            DependencyProperty.Register(nameof(MinAlarmValue), typeof(double), typeof(LiquidLevelGauge),
                new PropertyMetadata(0.0, OnPropertyChanged));

        public static readonly DependencyProperty MaxValueProperty =
            DependencyProperty.Register(nameof(MaxValue), typeof(double), typeof(LiquidLevelGauge),
                new PropertyMetadata(100.0, OnPropertyChanged));

        public static readonly DependencyProperty MinValueProperty =
            DependencyProperty.Register(nameof(MinValue), typeof(double), typeof(LiquidLevelGauge),
                new PropertyMetadata(0.0, OnPropertyChanged));

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(LiquidLevelGauge),
                new PropertyMetadata(string.Empty, OnLabelChanged));

        public static readonly DependencyProperty LabelPositionProperty =
            DependencyProperty.Register(nameof(LabelPosition), typeof(GaugeLabelPosition), typeof(LiquidLevelGauge),
                new PropertyMetadata(GaugeLabelPosition.Bottom, OnLabelPositionChanged));

        public static readonly DependencyProperty LabelFontSizeProperty =
            DependencyProperty.Register(nameof(LabelFontSize), typeof(double), typeof(LiquidLevelGauge),
                new PropertyMetadata(14.0, OnLabelFontSizeChanged));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public double MaxAlarmValue
        {
            get => (double)GetValue(MaxAlarmValueProperty);
            set => SetValue(MaxAlarmValueProperty, value);
        }

        public double MinAlarmValue
        {
            get => (double)GetValue(MinAlarmValueProperty);
            set => SetValue(MinAlarmValueProperty, value);
        }

        public double MaxValue
        {
            get => (double)GetValue(MaxValueProperty);
            set => SetValue(MaxValueProperty, value);
        }

        public double MinValue
        {
            get => (double)GetValue(MinValueProperty);
            set => SetValue(MinValueProperty, value);
        }

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public GaugeLabelPosition LabelPosition
        {
            get => (GaugeLabelPosition)GetValue(LabelPositionProperty);
            set => SetValue(LabelPositionProperty, value);
        }

        public double LabelFontSize
        {
            get => (double)GetValue(LabelFontSizeProperty);
            set => SetValue(LabelFontSizeProperty, value);
        }

        #endregion

        public LiquidLevelGauge()
        {
            InitializeComponent();
        }

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LiquidLevelGauge)d).AnimateUpdate();
        }

        private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LiquidLevelGauge)d).RefreshLabelText();
        }

        private static void OnLabelPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LiquidLevelGauge)d).ApplyLabelPosition((GaugeLabelPosition)e.NewValue);
        }

        private static void OnLabelFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LiquidLevelGauge)d).LabelText.FontSize = (double)e.NewValue;
        }

        private void ApplyLabelPosition(GaugeLabelPosition position)
        {
            if (position == GaugeLabelPosition.Bottom)
            {
                LayoutRoot.Width = 200;
                LayoutRoot.Height = 240;

                Grid.SetRow(GaugeRoot, 0);
                Grid.SetColumn(GaugeRoot, 0);
                Grid.SetColumnSpan(GaugeRoot, 2);
                Grid.SetRowSpan(GaugeRoot, 1);
                GaugeRoot.Width = 200;
                GaugeRoot.Height = 200;

                Grid.SetRow(LabelText, 1);
                Grid.SetColumn(LabelText, 0);
                Grid.SetColumnSpan(LabelText, 2);
                Grid.SetRowSpan(LabelText, 1);
                LabelText.HorizontalAlignment = HorizontalAlignment.Center;
                LabelText.VerticalAlignment = VerticalAlignment.Center;
                LabelText.TextWrapping = TextWrapping.NoWrap;
            }
            else
            {
                LayoutRoot.Width = 260;
                LayoutRoot.Height = 200;

                Grid.SetRow(GaugeRoot, 0);
                Grid.SetColumn(GaugeRoot, 1);
                Grid.SetColumnSpan(GaugeRoot, 1);
                Grid.SetRowSpan(GaugeRoot, 1);
                GaugeRoot.Width = 200;
                GaugeRoot.Height = 200;

                Grid.SetRow(LabelText, 0);
                Grid.SetColumn(LabelText, 0);
                Grid.SetColumnSpan(LabelText, 1);
                Grid.SetRowSpan(LabelText, 1);
                LabelText.HorizontalAlignment = HorizontalAlignment.Center;
                LabelText.VerticalAlignment = VerticalAlignment.Center;
                LabelText.TextWrapping = TextWrapping.Wrap;
            }
            RefreshLabelText();
        }

        private void RefreshLabelText()
        {
            if (LabelPosition == GaugeLabelPosition.Left && !string.IsNullOrEmpty(Label))
                LabelText.Text = string.Join("\n", Label.ToCharArray());
            else
                LabelText.Text = Label ?? string.Empty;
        }

        private void AnimateUpdate()
        {
            var percentage = CalculatePercentage();
            var isAlarm = Value > MaxAlarmValue || Value < MinAlarmValue;

            var container = (Grid)LiquidRect.Parent;
            var maxHeight = container.ActualHeight;
            if (maxHeight <= 1) maxHeight = 192;
            var targetHeight = maxHeight * percentage / 100;
            if (targetHeight < 0) targetHeight = 0;

            UpdateColors(isAlarm);
            UpdatePercentText(percentage);

            LiquidRect.BeginAnimation(FrameworkElement.HeightProperty, null);

            var animation = new DoubleAnimation
            {
                To = targetHeight,
                Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            animation.Completed += (_, _) =>
            {
                if (LiquidRect != null)
                    LiquidRect.Height = targetHeight;
            };
            LiquidRect.BeginAnimation(FrameworkElement.HeightProperty, animation,
                HandoffBehavior.SnapshotAndReplace);
        }

        private double CalculatePercentage()
        {
            var range = MaxValue - MinValue;
            if (Math.Abs(range) < 0.0001) return 0;
            var pct = (Value - MinValue) / range * 100;
            return Math.Max(0, Math.Min(100, pct));
        }

        private void UpdateColors(bool isAlarm)
        {
            if (isAlarm)
            {
                var redStart = (Color)ColorConverter.ConvertFromString("#EF4444");
                var redEnd = (Color)ColorConverter.ConvertFromString("#DC2626");
                var alarmGlow = (Color)ColorConverter.ConvertFromString("#F97316");

                GradStart.Color = redStart;
                GradEnd.Color = redEnd;
                GlowEffect.Color = alarmGlow;
                GlowEllipse.Stroke = new SolidColorBrush(alarmGlow);
            }
            else
            {
                var greenStart = (Color)ColorConverter.ConvertFromString("#10B981");
                var greenEnd = (Color)ColorConverter.ConvertFromString("#059669");
                var normalGlow = (Color)ColorConverter.ConvertFromString("#06B6D4");

                GradStart.Color = greenStart;
                GradEnd.Color = greenEnd;
                GlowEffect.Color = normalGlow;
                GlowEllipse.Stroke = new SolidColorBrush(normalGlow);
            }
        }

        private void UpdatePercentText(double percentage)
        {
            PercentText.Text = $"{Math.Round(percentage)}%";
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            AnimateUpdate();
        }
    }
}
