using PrismDemo.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace PrismDemo.A.Configuration
{
    /// <summary>
    /// 模块级配置（框架演示版，无业务含义）。
    /// 查找顺序：模块 DLL 所在目录 → BaseDirectory\modules。
    /// </summary>
    public static class Config
    {
        public static double SampleUpper { get; set; } = 80.0;
        public static double SampleLower { get; set; } = 20.0;
        public static int RefreshSeconds { get; set; } = 2;

        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                var configDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var configFile = Path.Combine(configDir ?? "", "a.config.json");

                if (!File.Exists(configFile))
                    configDir = AppDomain.CurrentDomain.BaseDirectory;

                var section = ConfigLoader.LoadSection(configDir, "a.config.json", "A");

                SampleUpper = TryGetDouble(section, "SampleUpper", SampleUpper);
                SampleLower = TryGetDouble(section, "SampleLower", SampleLower);
                RefreshSeconds = TryGetInt(section, "RefreshSeconds", RefreshSeconds);

                Debug.WriteLine($"[A.Config] 已加载: SampleUpper={SampleUpper}, SampleLower={SampleLower}, RefreshSeconds={RefreshSeconds}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[A.Config] 加载失败（使用默认值）: {ex.Message}");
            }
        }

        private static double TryGetDouble(Dictionary<string, string> section, string key, double fallback)
            => double.TryParse(section.GetValueOrDefault(key), out var val) ? val : fallback;

        private static int TryGetInt(Dictionary<string, string> section, string key, int fallback)
            => int.TryParse(section.GetValueOrDefault(key), out var val) ? val : fallback;
    }
}