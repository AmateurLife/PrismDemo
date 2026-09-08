using System.Threading.Tasks;

namespace PrismDemo.Core.Interfaces
{
    public interface IModuleLifecycle
    {
        bool IsModuleLoaded(string moduleName);
        Task LoadModuleAsync(string moduleName, string sourceDllPath);
        Task UpdateModuleAsync(string moduleName, string sourceDllPath);
        Task UnloadModuleAsync(string moduleName);

        void InitializeModule(string moduleName, Prism.Ioc.IContainerProvider rootContainer);
    }
}