using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PrismDemo.Core.Configuration
{
    /// <summary>
    /// 轻量配置加载器（框架演示版）。
    /// 用内置 System.Text.Json 替代 Microsoft.Extensions.Configuration，
    /// 接口语义保持一致：支持 core.config.json 全局配置与模块级 section 读取。
    /// </summary>
    public static class ConfigLoader
    {
        private static readonly Dictionary<string, string> _root = new(StringComparer.OrdinalIgnoreCase);
        private static bool _initialized;

        public static void Initialize()
        {
            _root.Clear();
            LoadInto(AppDomain.CurrentDomain.BaseDirectory, "core.config.json", _root);
            _initialized = true;
        }

        public static void Initialize(string basePath)
        {
            _root.Clear();
            LoadInto(basePath, "core.config.json", _root);
            _initialized = true;
        }

        public static string Get(string path, string fallback = null)
        {
            if (!_initialized) return fallback;
            return _root.TryGetValue(path, out var value) && !string.IsNullOrEmpty(value) ? value : fallback;
        }

        public static Dictionary<string, string> LoadSection(string directory, string fileName, string sectionKey)
        {
            var section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var flattened = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                LoadInto(directory, fileName, flattened);

                var prefix = (sectionKey ?? string.Empty).Trim().TrimEnd(':') + ":";
                foreach (var kvp in flattened)
                {
                    if (kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        section[kvp.Key.Substring(prefix.Length)] = kvp.Value;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigLoader] 加载 {fileName} 的 {sectionKey} 失败（使用默认值）: {ex.Message}");
            }
            return section;
        }

        private static void LoadInto(string directory, string fileName, Dictionary<string, string> target)
        {
            var path = System.IO.Path.Combine(directory, fileName);
            if (!System.IO.File.Exists(path)) return;

            using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(path));
            Flatten(doc.RootElement, "", target);
        }

        private static void Flatten(JsonElement element, string prefix, Dictionary<string, string> target)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var prop in element.EnumerateObject())
                    {
                        var key = prefix.Length == 0 ? prop.Name : $"{prefix}:{prop.Name}";
                        Flatten(prop.Value, key, target);
                    }
                    break;
                case JsonValueKind.Array:
                    var i = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        Flatten(item, $"{prefix}:{i}", target);
                        i++;
                    }
                    break;
                case JsonValueKind.String:
                    target[prefix] = element.GetString();
                    break;
                default:
                    target[prefix] = element.ToString();
                    break;
            }
        }
    }
}