namespace PrismDemo.Core.Interfaces
{
    public interface IModuleContainerProvider
    {
        Prism.Ioc.IContainerExtension GetContainer();
    }
}