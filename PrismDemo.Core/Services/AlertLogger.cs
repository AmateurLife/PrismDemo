using System;
using System.IO;
using System.Windows;

namespace PrismDemo.Core.Services
{
    public static class AlertLogger
    {
        private static readonly object _lock = new();

        public static void WriteLog(string message)
        {
            lock (_lock)
            {
                try
                {
                    var now = DateTime.Now;
                    var dir = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory, "Log", now.ToString("yyyyMM"));
                    var filePath = Path.Combine(dir, $"{now:yyyy-MM-dd}.log");
                    Directory.CreateDirectory(dir);
                    File.AppendAllText(filePath, $"[{now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
                }
                catch { }
            }
        }

        public static void ShowMessageBox(string messageBoxText)
        {
            WriteLog(messageBoxText);
            MessageBox.Show(messageBoxText);
        }

        public static MessageBoxResult ShowMessageBox(string messageBoxText, string caption)
        {
            WriteLog(messageBoxText);
            return MessageBox.Show(messageBoxText, caption);
        }

        public static MessageBoxResult ShowMessageBox(string messageBoxText, string caption, MessageBoxButton button)
        {
            WriteLog(messageBoxText);
            return MessageBox.Show(messageBoxText, caption, button);
        }

        public static MessageBoxResult ShowMessageBox(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            WriteLog(messageBoxText);
            return MessageBox.Show(messageBoxText, caption, button, icon);
        }

        public static MessageBoxResult ShowMessageBox(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
        {
            WriteLog(messageBoxText);
            return MessageBox.Show(messageBoxText, caption, button, icon, defaultResult);
        }
    }
}
