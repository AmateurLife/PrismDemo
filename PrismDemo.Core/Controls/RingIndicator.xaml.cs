using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PrismDemo.Core.Controls
{
    public partial class RingIndicator : UserControl
    {
        public bool IsAuto
        {
            get { return (bool)GetValue(IsAutoProperty); }
            set { SetValue(IsAutoProperty, value); }
        }

        public static readonly DependencyProperty IsAutoProperty =
            DependencyProperty.Register("IsAuto", typeof(bool), typeof(RingIndicator),
                new PropertyMetadata(false, new PropertyChangedCallback(OnPropertyChanged)));

        public double UsePercentage
        {
            get { return (double)GetValue(UsePercentageProperty); }
            set { SetValue(UsePercentageProperty, value); }
        }

        public static readonly DependencyProperty UsePercentageProperty =
            DependencyProperty.Register("UsePercentage", typeof(double), typeof(RingIndicator),
                new PropertyMetadata(60.0, new PropertyChangedCallback(OnPropertyChanged)));

        private static readonly SolidColorBrush GreenBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
        private static readonly SolidColorBrush RedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
        private static readonly SolidColorBrush GreenLightBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#81C784"));
        private static readonly SolidColorBrush RedLightBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF9A9A"));
        private static readonly SolidColorBrush BackBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0"));

        public static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var obj = d as RingIndicator;
            obj.UpdateArc();
        }

        private void UpdateArc()
        {
            double size = Math.Min(this.RenderSize.Width, this.RenderSize.Height);
            if (size <= 0) return;

            this.layout.Width = size;
            this.layout.Height = size;

            double radius = size / 2;
            double strokeWidth = Math.Max(2, size * 0.1);
            double arcRadius = radius - strokeWidth / 2 - 1;

            bool isAuto = IsAuto;
            double percentage = Math.Max(0, Math.Min(100, UsePercentage)) / 100.0;

            path.StrokeThickness = strokeWidth;
            backEllipse.StrokeThickness = strokeWidth;

            var baseColor = isAuto ? GreenBrush : RedBrush;
            var lightColor = isAuto ? GreenLightBrush : RedLightBrush;

            path.Stroke = baseColor;
            backEllipse.Stroke = BackBrush;
            txtPercentage.Foreground = lightColor;
            txtPercentage.Text = $"{UsePercentage:F0}%";

            if (percentage >= 1.0)
            {
                path.Data = new EllipseGeometry(new Point(radius, radius), arcRadius, arcRadius);
                return;
            }

            Point startPoint = new Point(
                radius + arcRadius * Math.Cos(-90 * Math.PI / 180),
                radius + arcRadius * Math.Sin(-90 * Math.PI / 180)
            );

            if (percentage <= 0)
            {
                double endAngle = (-90 + 1) * Math.PI / 180;
                Point endPoint = new Point(
                    radius + arcRadius * Math.Cos(endAngle),
                    radius + arcRadius * Math.Sin(endAngle)
                );

                var arcSegment = new ArcSegment(endPoint, new Size(arcRadius, arcRadius), 0,
                    false, SweepDirection.Clockwise, true);

                var figure = new PathFigure(startPoint, new[] { arcSegment }, false);
                var pathGeometry = new PathGeometry();
                pathGeometry.Figures.Add(figure);
                path.Data = pathGeometry;
                return;
            }

            double angle = percentage * 360 - 90;
            Point endPt = new Point(
                radius + arcRadius * Math.Cos(angle * Math.PI / 180),
                radius + arcRadius * Math.Sin(angle * Math.PI / 180)
            );

            var arcSeg = new ArcSegment(endPt, new Size(arcRadius, arcRadius), 0,
                percentage > 0.5, SweepDirection.Clockwise, true);

            var fig = new PathFigure(startPoint, new[] { arcSeg }, false);
            var geometry = new PathGeometry();
            geometry.Figures.Add(fig);

            path.Data = geometry;
        }

        public RingIndicator()
        {
            InitializeComponent();
            this.Loaded += (s, e) => UpdateArc();
            this.SizeChanged += (s, e) => UpdateArc();
        }
    }
}
