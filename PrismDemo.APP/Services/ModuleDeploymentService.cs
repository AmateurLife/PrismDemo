using Prism.Modularity;
using PrismDemo.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace PrismDemo.APP.Services
{
    public class ModuleDeploymentService : IModuleDeploymentService
    {
        private readonly string _modulesWorkingDirectory;

        public ModuleDeploymentService()
        {
            _modulesWorkingDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modules");
            Directory.CreateDirectory(_modulesWorkingDirectory);
        }

        public async Task<List<AvailableModuleInfo>> GetAvailableModulesAsync(string sourcePath)
        {
            if (!Directory.Exists(sourcePath))
                return new List<AvailableModuleInfo>();

            var availableModules = new List<AvailableModuleInfo>();

            // ❌ 删除这行：var existingModuleNames = GetDeployedModuleNames();

            foreach (var dllFile in Directory.GetFiles(sourcePath, "PrismDemo.*.dll"))
            {
                MetadataLoadContext? mlc = null;
                try
                {
                    string fileName = Path.GetFileNameWithoutExtension(dllFile);
                    if (!fileName.StartsWith("PrismDemo.", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string moduleName = fileName.Substring("PrismDemo.".Length);

                    // ✅ 不再检查是否已部署！直接验证是否为有效模块
                    var runtimeAssemblies = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")
                        .Concat(Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dll"))
                        .ToArray();

                    mlc = new MetadataLoadContext(new PathAssemblyResolver(runtimeAssemblies));
                    Assembly assembly = mlc.LoadFromAssemblyPath(dllFile);

                    bool hasIModule = assembly.GetTypes()
                        .Any(t => t.IsClass &&
                                 !t.IsAbstract &&
                                 t.GetInterfaces().Any(i =>
                                     (i.Namespace == "Prism.Ioc" || i.Namespace == "Prism.Modularity") &&
                                     i.Name == "IModule"));

                    if (hasIModule)
                    {
                        availableModules.Add(new AvailableModuleInfo
                        {
                            SourcePath = dllFile,
                            ModuleName = moduleName,
                            Version = assembly.GetName().Version?.ToString() ?? "Unknown"
                        });
                        Debug.WriteLine($"[Found] 可部署模块: {moduleName} @ {dllFile}");
                    }
                }
                catch (BadImageFormatException)
                {
                    Debug.WriteLine($"[Skip] 非托管 DLL: {dllFile}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Error] 分析失败 {dllFile}: {ex.Message}");
                }
                finally
                {
                    mlc?.Dispose();
                }
            }

            return availableModules;
        }

        public List<DiscoveredModuleInfo> GetLatestDeployedModules()
        {
            var modules = new Dictionary<string, (DateTime Timestamp, string FilePath)>();

            if (!Directory.Exists(_modulesWorkingDirectory))
                return new List<DiscoveredModuleInfo>();

            foreach (var dll in Directory.GetFiles(_modulesWorkingDirectory, "PrismDemo.*.dll"))
            {
                string fileName = Path.GetFileNameWithoutExtension(dll);
                // 文件名格式：PrismDemo.ModuleName.YYYYMMDDHHmm
                var parts = fileName.Split('.');
                if (parts.Length < 3) continue; // 至少 PrismDemo + Name + Timestamp

                // 提取模块基础名（去掉时间戳）
                string baseName = string.Join(".", parts.Take(parts.Length - 1)); // "PrismDemo.A"
                string moduleName = baseName.Substring("PrismDemo.".Length);       // "A"

                // 尝试解析时间戳
                if (DateTime.TryParseExact(parts[^1], "yyyyMMddHHmm", null, DateTimeStyles.None, out var ts))
                {
                    // 保留最新版本
                    if (!modules.TryGetValue(moduleName, out var existing) || ts > existing.Timestamp)
                    {
                        modules[moduleName] = (ts, dll);
                    }
                }
            }

            // 返回最新版本列表
            return modules.Select(kvp => new DiscoveredModuleInfo
            {
                ModuleName = kvp.Key,
                FilePath = kvp.Value.FilePath,
                Version = kvp.Value.Timestamp.ToString("yyyy-MM-dd HH:mm")
            }).ToList();
        }

        public async Task DeployModuleAsync(string sourceDllPath, string targetModuleName)
        {
            // 获取当前时间戳（格式：yyyyMMddHHmm）
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmm");

            // 原始文件名：PrismDemo.A.dll
            string originalName = Path.GetFileNameWithoutExtension(sourceDllPath); // "PrismDemo.A"
            string extension = Path.GetExtension(sourceDllPath); // ".dll"

            // 新文件名：PrismDemo.A.202604031714.dll
            string newFileName = $"{originalName}.{timestamp}{extension}";
            string targetDllPath = Path.Combine(_modulesWorkingDirectory, newFileName);

            File.Copy(sourceDllPath, targetDllPath, overwrite: false); // 不会冲突！

            Debug.WriteLine($"[ModuleDeployment] 部署成功: {targetDllPath}");
        }

        private HashSet<string> GetDeployedModuleNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(_modulesWorkingDirectory)) return names;

            foreach (var dll in Directory.GetFiles(_modulesWorkingDirectory, "*.dll"))
            {
                var name = Path.GetFileNameWithoutExtension(dll)
                    .Replace("PrismDemo.", "", StringComparison.OrdinalIgnoreCase);
                names.Add(name);
            }
            return names;
        }
    }
    
}