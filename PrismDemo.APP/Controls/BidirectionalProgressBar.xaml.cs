using System;
using System.Windows;
using System.Windows.Controls;

namespace PrismDemo.APP.Controls
{
    public partial class BidirectionalProgressBar : UserControl
    {
        public static readonly DependencyProperty RatioProperty =
            DependencyProperty.Register(nameof(Ratio), typeof(double), typeof(BidirectionalProgressBar),
                new PropertyMetadata(0.0, OnRatioChanged));

        public double Ratio
        {
            get => (double)GetValue(RatioProperty);
            set => SetValue(RatioProperty, value);
        }

        public BidirectionalProgressBar()
        {
            InitializeComponent();
        }

        private static void OnRatioChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var bar = (BidirectionalProgressBar)d;
            bar.UpdateBars();
        }

        private void UpdateBars()
        {
            var value = Ratio;
            var halfWidth = ActualWidth / 2;
            if (halfWidth <= 0) halfWidth = 100;

            if (value >= 0)
            {
                LeftBar.Width = 0;
                LeftBar.Margin = new Thickness(0);
                var barWidth = Math.Min(value / 100.0 * halfWidth, halfWidth);
                RightBar.Width = barWidth;
                RightBar.Margin = new Thickness(halfWidth, 0, 0, 0);
                LabelText.Text = $"+{value:F1}";
                LabelText.Foreground = System.Windows.Media.Brushes.Green;
            }
            else
            {
                RightBar.Width = 0;
                RightBar.Margin = new Thickness(0);
                var barWidth = Math.Min(Math.Abs(value) / 100.0 * halfWidth, halfWidth);
                LeftBar.Width = barWidth;
                LeftBar.Margin = new Thickness(halfWidth - barWidth, 0, 0, 0);
                LabelText.Text = $"{value:F1}";
                LabelText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            UpdateBars();
        }
    }
}
