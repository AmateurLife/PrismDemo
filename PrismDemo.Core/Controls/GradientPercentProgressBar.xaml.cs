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
    /// GradientPercentProgressBar.xaml 的交互逻辑
    /// 该控件是一个带有渐变色的百分比进度条，支持显示当前进度百分比，并且可以设置为不确定状态（Indeterminate）。
    /// 用户可以通过绑定 Value 属性来控制进度条的进度，通过绑定 IsIndeterminate 属性来控制是否显示不确定状态。
    /// 还可以通过绑定 HeightValue、FontSizeValue 和 PaddingValue 来调整控件的高度、字体大小和内边距，以适应不同的使用场景。
    /// </summary>
    public partial class GradientPercentProgressBar : UserControl
    {
        // 暴露 Value
        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(double), typeof(GradientPercentProgressBar), new PropertyMetadata(0.0));

        // 暴露 IsIndeterminate
        public bool IsIndeterminate
        {
            get { return (bool)GetValue(IsIndeterminateProperty); }
            set { SetValue(IsIndeterminateProperty, value); }
        }
        public static readonly DependencyProperty IsIndeterminateProperty =
            DependencyProperty.Register("IsIndeterminate", typeof(bool), typeof(GradientPercentProgressBar), new PropertyMetadata(false));

        // 暴露 Height
        public double HeightValue
        {
            get { return (double)GetValue(HeightValueProperty); }
            set { SetValue(HeightValueProperty, value); }
        }
        public static readonly DependencyProperty HeightValueProperty =
            DependencyProperty.Register("HeightValue", typeof(double), typeof(GradientPercentProgressBar), new PropertyMetadata(15.0));

        // 暴露 FontSize
        public double FontSizeValue
        {
            get { return (double)GetValue(FontSizeValueProperty); }
            set { SetValue(FontSizeValueProperty, value); }
        }
        public static readonly DependencyProperty FontSizeValueProperty =
            DependencyProperty.Register("FontSizeValue", typeof(double), typeof(GradientPercentProgressBar), new PropertyMetadata(10.0));

        // 暴露 Padding
        public Thickness PaddingValue
        {
            get { return (Thickness)GetValue(PaddingValueProperty); }
            set { SetValue(PaddingValueProperty, value); }
        }
        public static readonly DependencyProperty PaddingValueProperty =
            DependencyProperty.Register("PaddingValue", typeof(Thickness), typeof(GradientPercentProgressBar), new PropertyMetadata(new Thickness(5, 0, 5, 0)));

        public GradientPercentProgressBar()
        {
            InitializeComponent();
        }
    }
}
