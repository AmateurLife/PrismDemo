﻿// PrismDemo.APP/Services/ModuleLoadContext.cs
// 此类用于创建一个独立的程序集加载上下文，以便动态加载模块程序集并管理其依赖关系。
// 通过重写 Load 方法，可以控制如何解析和加载模块的依赖项，确保共享库（如 PrismDemo.Core）不会被重复加载，从而避免类型冲突和内存泄漏问题。
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace PrismDemo.APP.Services;

public class ModuleLoadContext : AssemblyLoadContext
{
    private readonly string _modulePath;
    private readonly AssemblyDependencyResolver _resolver;

    public ModuleLoadContext(string modulePath) : base(isCollectible: true)
    {
        _modulePath = modulePath;
        _resolver = new AssemblyDependencyResolver(modulePath);
    }

    /// <summary>
    /// 用于加载模块程序集及其依赖项的核心方法。
    /// 它首先检查是否应该共享程序集（如 PrismDemo.Core），
    /// 如果是，则从主上下文（Default）中查找已加载的程序集并返回。
    /// 否则，它使用 AssemblyDependencyResolver 来解析模块私有依赖项的路径，并从该路径加载程序集。
    /// </summary>
    /// <param name="assemblyName"></param>
    /// <returns></returns>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // 🔑 关键：将所有共享库重定向到主上下文（Default）
        if (ShouldShareAssembly(assemblyName.Name))
        {
            // 从主上下文查找已加载的程序集
            var mainAssembly = Default.Assemblies.FirstOrDefault(a =>
                string.Equals(a.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase));

            if (mainAssembly != null)
            {
                System.Diagnostics.Debug.WriteLine($"[ALC] Redirected to main context: {assemblyName.Name}");
                return mainAssembly;
            }
        }

        // 加载模块私有依赖
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            System.Diagnostics.Debug.WriteLine($"[ALC] Loading from path: {assemblyPath}");
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }

    /// <summary>
    /// 用于确定哪些程序集应该在主上下文中共享的辅助方法。
    /// </summary>
    /// <param name="assemblyName"></param>
    /// <returns></returns>
    private static bool ShouldShareAssembly(string? assemblyName)
    {
        if (string.IsNullOrEmpty(assemblyName))
            return false;

        // 🚨 必须包含以下前缀（根据你的实际引用调整）
        return assemblyName.StartsWith("Prism", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("DryIoc", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("Microsoft.Extensions", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.Equals("netstandard", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.Equals("PrismDemo.Core", StringComparison.OrdinalIgnoreCase); // 👈 共享核心库
    }
}
