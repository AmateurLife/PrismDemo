using Prism.Mvvm;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PrismDemo.APP.ViewModels
{
    /// <summary>
    /// 已部署模块的行模型（对应真实工程的 ModuleInfo）。
    /// 其中 AssemblyVersion 通过 MetadataLoadContext 读取，不加载程序集。
    /// </summary>
    public class ModuleInfo : BindableBase
    {
        private bool _suppressEvent;

        public string Name { get; }
        public string DllPath { get; }
        public string DeployTime { get; }

        public event Action<string, bool> IsEnabledChanged;

        private string _assemblyVersion;
        public string AssemblyVersion
        {
            get => _assemblyVersion;
            set => SetProperty(ref _assemblyVersion, value);
        }

        private bool _isLoaded;
        public bool IsLoaded
        {
            get => _isLoaded;
            set => SetProperty(ref _isLoaded, value);
        }

        private bool? _isEnabled;
        public bool? IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_suppressEvent)
                {
                    _isEnabled = value;
                    return;
                }

                if (SetProperty(ref _isEnabled, value))
                    IsEnabledChanged?.Invoke(Name, value ?? false);
            }
        }

        private string _status;
        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public ModuleInfo(string name, string dllPath, bool isEnabled)
        {
            Name = name;
            DllPath = dllPath;

            _suppressEvent = true;
            _isEnabled = isEnabled;
            _isLoaded = isEnabled;
            _status = isEnabled ? "已加载" : "未加载";
            _suppressEvent = false;

            DeployTime = ExtractDeployTimeFromPath(dllPath);
            AssemblyVersion = ReadAssemblyVersion(dllPath);

            RaisePropertyChanged(nameof(IsEnabled));
            RaisePropertyChanged(nameof(IsLoaded));
            RaisePropertyChanged(nameof(Status));
        }

        private static string ExtractDeployTimeFromPath(string dllPath)
        {
            try
            {
                var parts = Path.GetFileNameWithoutExtension(dllPath).Split('.');
                if (parts.Length >= 3)
                {
                    string lastPart = parts[^1];
                    if (lastPart.Length == 12 && long.TryParse(lastPart, out _))
                    {
                        if (DateTime.TryParseExact(lastPart, "yyyyMMddHHmm", null, DateTimeStyles.None, out var dt))
                            return dt.ToString("yyyy-MM-dd HH:mm");
                    }
                }
            }
            catch { }
            return "Unknown";
        }

        private static string ReadAssemblyVersion(string dllPath)
        {
            try
            {
                var runtimeAssemblies = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
                using var mlc = new MetadataLoadContext(new PathAssemblyResolver(runtimeAssemblies));
                var assembly = mlc.LoadFromAssemblyPath(dllPath);
                return assembly.GetName().Version?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}