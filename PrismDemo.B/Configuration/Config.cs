using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace PrismDemo.B.Configuration
{
    public static class Config
    {
        public static double OutletBUpper { get; set; } = 0.8;
        public static double OutletBLower { get; set; } = 0.6;
        public static double BMax { get; set; } = 300.0;
        public static double BMin { get; set; } = 0.0;
        public static double UnitRateMax { get; set; } = 300.0;
        public static double UnitRateMin { get; set; } = 0.0;

        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                var configDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var configFile = Path.Combine(configDir ?? "", "b.config.json");

                if (!File.Exists(configFile))
                {
                    var fallbackDir = Path.GetFullPath(
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Modules"));
                    configFile = Path.Combine(fallbackDir, "b.config.json");
                    if (File.Exists(configFile))
                        configDir = fallbackDir;
                }

                if (!File.Exists(configFile))
                {
                    Debug.WriteLine("[B.Config] 未找到 b.config.json，使用默认值");
                    return;
                }

                var jsonConfig = PrismDemo.Core.Configuration.Config.LoadModuleConfig(configDir, "b.config.json");
                var section = jsonConfig.GetSection("B");

                OutletBUpper = TryGetDouble(section, "OutletBUpper", OutletBUpper);
                OutletBLower = TryGetDouble(section, "OutletBLower", OutletBLower);
                BMax = TryGetDouble(section, "BMax", BMax);
                BMin = TryGetDouble(section, "BMin", BMin);
                UnitRateMax = TryGetDouble(section, "UnitRateMax", UnitRateMax);
                UnitRateMin = TryGetDouble(section, "UnitRateMin", UnitRateMin);

                Debug.WriteLine($"[B.Config] 已加载: OutletBUpper={OutletBUpper}, OutletBLower={OutletBLower}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[B.Config] 加载失败（使用默认值）: {ex.Message}");
            }
        }

        private static double TryGetDouble(Microsoft.Extensions.Configuration.IConfigurationSection section, string key, double fallback)
        {
            return double.TryParse(section[key], out var val) ? val : fallback;
        }
    }
}
