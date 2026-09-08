using System;
using System.Diagnostics;
using System.IO;

namespace PrismDemo.Core.Services
{
    /// <summary>
    /// 极简日志服务（框架演示版）。
    /// 真实工程使用 AlertLogger 同时写 Debug 与日志文件，
    /// Demo 保留同等能力便于框架流程的可观测与排查。
    /// </summary>
    public static class Log
    {
        private static readonly object _lock = new();
        private static string _directory;

        public static void Initialize()
        {
            _directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log");
            Directory.CreateDirectory(_directory);
        }

        public static void Write(string message)
        {
            Debug.WriteLine(message);

            lock (_lock)
            {
                try
                {
                    var file = Path.Combine(_directory, $"app_{DateTime.Now:yyyyMM}.log");
                    File.AppendAllText(file, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
                }
                catch { }
            }
        }
    }
}