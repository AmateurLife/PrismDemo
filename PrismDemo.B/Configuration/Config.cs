using PrismDemo.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace PrismDemo.B.Configuration
{
    /// <summary>
    /// 模块级配置（框架演示版，无业务含义）。
    /// 查找顺序：模块 DLL 所在目录 → BaseDirectory\modules → BaseDirectory\..\Modules（仓储）。
    /// </summary>
    public static class Config
    {
        public static double Target { get; set; } = 50.0;
        public static int TickMs { get; set; } = 1000;

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
                    configDir = FindConfigFallbackDirectory();

                var section = ConfigLoader.LoadSection(configDir, "b.config.json", "B");

                Target = TryGetDouble(section, "Target", Target);
                TickMs = TryGetInt(section, "TickMs", TickMs);

                Debug.WriteLine($"[B.Config] 已加载: Target={Target}, TickMs={TickMs}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[B.Config] 加载失败（使用默认值）: {ex.Message}");
            }
        }

        private static string FindConfigFallbackDirectory()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;

            var runtimeModules = Path.Combine(baseDir, "modules");
            if (File.Exists(Path.Combine(runtimeModules, "b.config.json")))
                return runtimeModules;

            var repoModules = Path.GetFullPath(Path.Combine(baseDir, "..", "Modules"));
            if (File.Exists(Path.Combine(repoModules, "b.config.json")))
                return repoModules;

            return baseDir;
        }

        private static double TryGetDouble(Dictionary<string, string> section, string key, double fallback)
            => double.TryParse(section.GetValueOrDefault(key), out var val) ? val : fallback;

        private static int TryGetInt(Dictionary<string, string> section, string key, int fallback)
            => int.TryParse(section.GetValueOrDefault(key), out var val) ? val : fallback;
    }
}