#nullable enable
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace PrismDemo.APP.Services
{
    /// <summary>
    /// 独立的可回收程序集加载上下文。
    /// 将共享库（Prism/DryIoc/PrismDemo.Core）重定向到主上下文，
    /// 避免类型冲突和程序集重复加载导致的泄漏。
    /// </summary>
    public class ModuleLoadContext : AssemblyLoadContext
    {
        private readonly string _modulePath;
        private readonly AssemblyDependencyResolver _resolver;

        public ModuleLoadContext(string modulePath) : base(isCollectible: true)
        {
            _modulePath = modulePath;
            _resolver = new AssemblyDependencyResolver(modulePath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (ShouldShareAssembly(assemblyName.Name))
            {
                var mainAssembly = Default.Assemblies.FirstOrDefault(a =>
                    string.Equals(a.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase));

                if (mainAssembly != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ALC] Redirected to main context: {assemblyName.Name}");
                    return mainAssembly;
                }
            }

            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                System.Diagnostics.Debug.WriteLine($"[ALC] Loading from path: {assemblyPath}");
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        private static bool ShouldShareAssembly(string? assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
                return false;

            return assemblyName.StartsWith("Prism", StringComparison.OrdinalIgnoreCase) ||
                   assemblyName.StartsWith("DryIoc", StringComparison.OrdinalIgnoreCase) ||
                   assemblyName.StartsWith("Microsoft.Extensions", StringComparison.OrdinalIgnoreCase) ||
                   assemblyName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
                   assemblyName.Equals("netstandard", StringComparison.OrdinalIgnoreCase) ||
                   assemblyName.Equals("PrismDemo.Core", StringComparison.OrdinalIgnoreCase);
        }
    }
}