// 文件：Services/ModuleContainerProvider.cs
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Regions;
using System;
using PrismDemo.Core.Interfaces;

namespace PrismDemo.APP.Services;

internal class ModuleContainerProvider : IContainerProvider, IModuleContainerProvider
{
    private readonly IContainerProvider _parentProvider;
    private readonly IRegionManager _regionManager;

    public ModuleContainerProvider(IContainerProvider parentProvider, IRegionManager regionManager)
    {
        _parentProvider = parentProvider;
        _regionManager = regionManager;
    }

    public IContainerExtension GetContainer()
    {
        var container = _parentProvider.GetContainer();

        // 如果已经是 IContainerExtension，直接返回
        if (container is IContainerExtension ext)
            return ext;

        // 如果是 DryIoc.IContainer，包装成 DryIocContainerExtension
        if (container is DryIoc.IContainer dryIocContainer)
            return new DryIocContainerExtension(dryIocContainer);

        throw new InvalidOperationException($"不支持的容器类型: {container?.GetType()}");
    }
    public object Resolve(Type type) => _parentProvider.Resolve(type);
    public object Resolve(Type type, string name) => _parentProvider.Resolve(type, name);
    public T Resolve<T>() => _parentProvider.Resolve<T>();
    public T Resolve<T>(string name) => _parentProvider.Resolve<T>(name);
    public object Resolve(Type type, params (Type Type, object Instance)[] parameters)
        => _parentProvider.Resolve(type, parameters);
    public object Resolve(Type type, string name, params (Type Type, object Instance)[] parameters)
        => _parentProvider.Resolve(type, name, parameters);
    public IScopedProvider CreateScope() => _parentProvider.CreateScope();
    public IScopedProvider CurrentScope => _parentProvider.CurrentScope;
}