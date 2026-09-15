using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace PrismDemo.A.Configuration
{
    public static class Config
    {
        public static double AMax { get; set; } = 300.0;
        public static double AMin { get; set; } = 0.0;
        public static double UnitRateMax { get; set; } = 300.0;
        public static double UnitRateMin { get; set; } = 0.0;
        public static string FlocExePath { get; set; } = @"..\floc\floc-observer-2.exe";

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
                {
                    var fallbackDir = Path.GetFullPath(
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Modules"));
                    configFile = Path.Combine(fallbackDir, "a.config.json");
                    if (File.Exists(configFile))
                        configDir = fallbackDir;
                }

                if (!File.Exists(configFile))
                {
                    Debug.WriteLine("[A.Config] 未找到 a.config.json，使用默认值");
                    return;
                }

                var jsonConfig = PrismDemo.Core.Configuration.Config.LoadModuleConfig(configDir, "a.config.json");
                var section = jsonConfig.GetSection("A");

                AMax = TryGetDouble(section, "AMax", AMax);
                AMin = TryGetDouble(section, "AMin", AMin);
                UnitRateMax = TryGetDouble(section, "UnitRateMax", UnitRateMax);
                UnitRateMin = TryGetDouble(section, "UnitRateMin", UnitRateMin);

                var flocSection = jsonConfig.GetSection("Floc");
                FlocExePath = flocSection["ExePath"] ?? FlocExePath;

                Debug.WriteLine($"[A.Config] 已加载: FlocExePath={FlocExePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[A.Config] 加载失败（使用默认值）: {ex.Message}");
            }
        }

        private static double TryGetDouble(Microsoft.Extensions.Configuration.IConfigurationSection section, string key, double fallback)
        {
            return double.TryParse(section[key], out var val) ? val : fallback;
        }
    }
}
