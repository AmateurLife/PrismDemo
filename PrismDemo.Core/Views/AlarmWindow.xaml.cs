using System;
using System.Windows;
using System.Windows.Input;
using PrismDemo.Core.Services;

namespace PrismDemo.Core.Views
{
    public partial class AlarmWindow : Window
    {
        private readonly Action _onConfirm;

        public AlarmWindow(string message, Action onConfirm = null)
        {
            InitializeComponent();
            AlarmText.Text = message;
            _onConfirm = onConfirm;
            AlertLogger.WriteLog(message);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            _onConfirm?.Invoke();
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }
    }
}