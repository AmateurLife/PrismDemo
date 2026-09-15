using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PrismDemo.Core.Controls
{
    /// <summary>
    /// WaterLevel_Arc.xaml 的交互逻辑
    /// 该控件用于显示水位，支持设置当前值、最大值、最小值、报警阈值等属性，并根据当前值动态更新显示的弧线和颜色。
    /// </summary>
    public partial class WaterLevel_Arc : UserControl
    {
        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(double), typeof(WaterLevel_Arc),
                new PropertyMetadata(0.0, new PropertyChangedCallback(OnPropertyChanged)));

        public double MaxLevel
        {
            get { return (double)GetValue(MaxLevelProperty); }
            set { SetValue(MaxLevelProperty, value); }
        }

        public static readonly DependencyProperty MaxLevelProperty =
            DependencyProperty.Register("MaxLevel", typeof(double), typeof(WaterLevel_Arc),
                new PropertyMetadata(100.0, new PropertyChangedCallback(OnPropertyChanged)));

        public double MinLevel
        {
            get { return (double)GetValue(MinLevelProperty); }
            set { SetValue(MinLevelProperty, value); }
        }

        public static readonly DependencyProperty MinLevelProperty =
            DependencyProperty.Register("MinLevel", typeof(double), typeof(WaterLevel_Arc),
                new PropertyMetadata(0.0, new PropertyChangedCallback(OnPropertyChanged)));

        public double AlarmMaxLevel
        {
            get { return (double)GetValue(AlarmMaxLevelProperty); }
            set { SetValue(AlarmMaxLevelProperty, value); }
        }

        public static readonly DependencyProperty AlarmMaxLevelProperty =
            DependencyProperty.Register("AlarmMaxLevel", typeof(double), typeof(WaterLevel_Arc),
                new PropertyMetadata(90.0, new PropertyChangedCallback(OnPropertyChanged)));

        public double AlarmMinLevel
        {
            get { return (double)GetValue(AlarmMinLevelProperty); }
            set { SetValue(AlarmMinLevelProperty, value); }
        }

        public static readonly DependencyProperty AlarmMinLevelProperty =
            DependencyProperty.Register("AlarmMinLevel", typeof(double), typeof(WaterLevel_Arc),
                new PropertyMetadata(10.0, new PropertyChangedCallback(OnPropertyChanged)));

        public Brush BackColor
        {
            get { return (Brush)GetValue(BackColorProperty); }
            set { SetValue(BackColorProperty, value); }
        }

        public static readonly DependencyProperty BackColorProperty =
            DependencyProperty.Register("BackColor", typeof(Brush), typeof(WaterLevel_Arc),
                new PropertyMetadata(new SolidColorBrush(Colors.Gray)));

        public Brush NormalColor
        {
            get { return (Brush)GetValue(NormalColorProperty); }
            set { SetValue(NormalColorProperty, value); }
        }

        public static readonly DependencyProperty NormalColorProperty =
            DependencyProperty.Register("NormalColor", typeof(Brush), typeof(WaterLevel_Arc),
                new PropertyMetadata(new SolidColorBrush(Colors.LightGreen)));

        public Brush AlarmColor
        {
            get { return (Brush)GetValue(AlarmColorProperty); }
            set { SetValue(AlarmColorProperty, value); }
        }

        public static readonly DependencyProperty AlarmColorProperty =
            DependencyProperty.Register("AlarmColor", typeof(Brush), typeof(WaterLevel_Arc),
                new PropertyMetadata(new SolidColorBrush(Colors.Red)));

        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(string), typeof(WaterLevel_Arc),
                new PropertyMetadata(default(string)));

        public static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var obj = d as WaterLevel_Arc;
            obj.UpdateValue();
        }

        private void UpdateValue()
        {
            double size = Math.Min(this.RenderSize.Width, this.RenderSize.Height);
            if (size <= 0) return;

            this.layout.Width = size;
            this.layout.Height = size;

            double radius = size / 2;
            double strokeWidth = Math.Max(2, size * 0.1);
            double arcRadius = radius - strokeWidth / 2 - 1;

            // 百分比
            double percentage = 0;
            if (MaxLevel > MinLevel)
            {
                percentage = Math.Max(0, Math.Min(1, (Value - MinLevel) / (MaxLevel - MinLevel)));
            }

            // 设置粗细
            path.StrokeThickness = strokeWidth;
            backEllipse.StrokeThickness = strokeWidth;

            // 设置颜色
            path.Stroke = (Value > AlarmMaxLevel || Value < AlarmMinLevel) ? AlarmColor : NormalColor;

            // 情况1：无液位
            if (percentage <= 0)
            {
                path.Data = null;
                return;
            }

            // 情况2：满液位
            if (percentage >= 1.0)
            {
                path.Data = new EllipseGeometry(new Point(radius, radius), arcRadius, arcRadius);
                return;
            }

            // 起点：顶部 (-90°)
            Point startPoint = new Point(
                radius + arcRadius * Math.Cos(-90 * Math.PI / 180),
                radius + arcRadius * Math.Sin(-90 * Math.PI / 180)
            );

            // 终点：根据百分比计算角度
            double angle = percentage * 360 - 90;
            Point endPoint = new Point(
                radius + arcRadius * Math.Cos(angle * Math.PI / 180),
                radius + arcRadius * Math.Sin(angle * Math.PI / 180)
            );

            // 创建弧线段
            var arcSegment = new ArcSegment(endPoint, new Size(arcRadius, arcRadius), 0,
                percentage > 0.5, SweepDirection.Clockwise, true);

            // 创建路径图
            var figure = new PathFigure(startPoint, new[] { arcSegment }, false);
            var pathGeometry = new PathGeometry();
            pathGeometry.Figures.Add(figure);

            path.Data = pathGeometry;

        }

        public WaterLevel_Arc()
        {
            InitializeComponent();
            this.SizeChanged += CircularProgressBar_SizeChanged;
        }

        private void CircularProgressBar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.UpdateValue();
        }
    }
}
