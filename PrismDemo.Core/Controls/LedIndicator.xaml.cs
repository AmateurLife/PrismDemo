using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PrismDemo.Core.Controls
{
    public partial class LedIndicator : UserControl
    {
        public bool IsAuto
        {
            get => (bool)GetValue(IsAutoProperty);
            set => SetValue(IsAutoProperty, value);
        }

        public static readonly DependencyProperty IsAutoProperty =
            DependencyProperty.Register("IsAuto", typeof(bool), typeof(LedIndicator),
                new PropertyMetadata(false, OnIsAutoChanged));

        public ICommand Command
        {
            get => (ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(LedIndicator), new PropertyMetadata(null));

        private static void OnIsAutoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as LedIndicator)?.UpdateLed();
        }

        public LedIndicator()
        {
            InitializeComponent();
            Loaded += (s, e) => UpdateLed();
            MouseLeftButtonDown += (s, e) =>
            {
                if (Command != null && Command.CanExecute(null))
                    Command.Execute(null);
            };
        }

        private void UpdateLed()
        {
            bool isAuto = IsAuto;

            Color ledCenter = isAuto
                ? (Color)ColorConverter.ConvertFromString("#81C784")
                : (Color)ColorConverter.ConvertFromString("#EF5350");

            Color ledMid = isAuto
                ? (Color)ColorConverter.ConvertFromString("#4CAF50")
                : (Color)ColorConverter.ConvertFromString("#E53935");

            Color ledEdge = isAuto
                ? (Color)ColorConverter.ConvertFromString("#2E7D32")
                : (Color)ColorConverter.ConvertFromString("#B71C1C");

            Color glowColor = isAuto
                ? (Color)ColorConverter.ConvertFromString("#4CAF50")
                : (Color)ColorConverter.ConvertFromString("#F44336");

            var ledBrush = new RadialGradientBrush();
            ledBrush.GradientOrigin = new Point(0.35, 0.35);
            ledBrush.Center = new Point(0.35, 0.35);
            ledBrush.RadiusX = 0.5;
            ledBrush.RadiusY = 0.5;
            ledBrush.GradientStops.Add(new GradientStop(ledCenter, 0));
            ledBrush.GradientStops.Add(new GradientStop(ledMid, 0.5));
            ledBrush.GradientStops.Add(new GradientStop(ledEdge, 1));
            ledBody.Fill = ledBrush;

            var glowBrush = new RadialGradientBrush();
            glowBrush.GradientOrigin = new Point(0.5, 0.5);
            glowBrush.Center = new Point(0.5, 0.5);
            glowBrush.RadiusX = 0.5;
            glowBrush.RadiusY = 0.5;
            glowBrush.GradientStops.Add(new GradientStop(
                Color.FromArgb(0x4D, glowColor.R, glowColor.G, glowColor.B), 0));
            glowBrush.GradientStops.Add(new GradientStop(
                Color.FromArgb(0x1A, glowColor.R, glowColor.G, glowColor.B), 0.4));
            glowBrush.GradientStops.Add(new GradientStop(
                Color.FromArgb(0x00, glowColor.R, glowColor.G, glowColor.B), 1));
            glowEllipse.Fill = glowBrush;

            highlight.Fill = new SolidColorBrush(Color.FromArgb(0x39, 255, 255, 255));
        }
    }
}
