using PrismDemo.Core.Configuration;
using System;
using System.IO;
using System.Text;

namespace PrismDemo.Core.Services
{
    public static class Log
    {
        private static readonly string _path;
        private static readonly StreamWriter _writer;
        private static readonly object _lock = new();

        static Log()
        {
            _path = Config.LogPath ??
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.log");
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            _writer = new StreamWriter(_path, true, Encoding.UTF8) { AutoFlush = true };
            AppDomain.CurrentDomain.ProcessExit += (s, e) => { _writer?.Dispose(); };
        }

        public static bool write(string msg)
        {
            try
            {
                lock (_lock)
                {
                    _writer.WriteLine(msg);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
