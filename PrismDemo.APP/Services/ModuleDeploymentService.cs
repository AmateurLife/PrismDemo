#nullable enable
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
    /// <summary>
    /// 模块部署服务：注册/发现模块 DLL。
    /// 约定：模块文件名 PrismDemo.{ModuleName}.{yyyyMMddHHmm}.dll，
    /// 时间戳后缀用于多版本并存与取最新版本。
    /// </summary>
    public class ModuleDeploymentService : IModuleDeploymentService
    {
        private readonly string _modulesWorkingDirectory;

        public ModuleDeploymentService()
        {
            _modulesWorkingDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modules");
            Directory.CreateDirectory(_modulesWorkingDirectory);
        }

        public Task<List<AvailableModuleInfo>> GetAvailableModulesAsync(string sourcePath)
        {
            var availableModules = new List<AvailableModuleInfo>();
            if (!Directory.Exists(sourcePath))
                return Task.FromResult(availableModules);

            foreach (var dllFile in Directory.GetFiles(sourcePath, "PrismDemo.*.dll"))
            {
                MetadataLoadContext? mlc = null;
                try
                {
                    string fileName = Path.GetFileNameWithoutExtension(dllFile);
                    if (!fileName.StartsWith("PrismDemo.", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string moduleName = fileName.Substring("PrismDemo.".Length);

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

            return Task.FromResult(availableModules);
        }

        public List<DiscoveredModuleInfo> GetLatestDeployedModules()
        {
            var modules = new Dictionary<string, (DateTime Timestamp, string FilePath)>();

            if (!Directory.Exists(_modulesWorkingDirectory))
                return new List<DiscoveredModuleInfo>();

            foreach (var dll in Directory.GetFiles(_modulesWorkingDirectory, "PrismDemo.*.dll"))
            {
                string fileName = Path.GetFileNameWithoutExtension(dll);
                var parts = fileName.Split('.');
                if (parts.Length < 3) continue;

                string baseName = string.Join(".", parts.Take(parts.Length - 1));
                string moduleName = baseName.Substring("PrismDemo.".Length);

                if (DateTime.TryParseExact(parts[^1], "yyyyMMddHHmm", null, DateTimeStyles.None, out var ts))
                {
                    if (!modules.TryGetValue(moduleName, out var existing) || ts > existing.Timestamp)
                    {
                        modules[moduleName] = (ts, dll);
                    }
                }
            }

            return modules.Select(kvp => new DiscoveredModuleInfo
            {
                ModuleName = kvp.Key,
                FilePath = kvp.Value.FilePath,
                Version = kvp.Value.Timestamp.ToString("yyyy-MM-dd HH:mm")
            }).ToList();
        }

        public Task DeployModuleAsync(string sourceDllPath, string targetModuleName)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmm");
            string originalName = Path.GetFileNameWithoutExtension(sourceDllPath);
            string extension = Path.GetExtension(sourceDllPath);

            string newFileName = $"{originalName}.{timestamp}{extension}";
            string targetDllPath = Path.Combine(_modulesWorkingDirectory, newFileName);

            File.Copy(sourceDllPath, targetDllPath, overwrite: false);
            Debug.WriteLine($"[ModuleDeployment] 部署成功: {targetDllPath}");
            return Task.CompletedTask;
        }
    }
}