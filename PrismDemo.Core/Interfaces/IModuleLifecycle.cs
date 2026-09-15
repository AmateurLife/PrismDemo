using Prism.Ioc;
using System.Threading.Tasks;

namespace PrismDemo.Core.Interfaces
{
    public interface IModuleLifecycle
    {
        bool IsModuleLoaded(string moduleName);
        Task LoadModuleAsync(string moduleName, string sourceDllPath);
        Task UpdateModuleAsync(string moduleName, string sourceDllPath);
        Task UnloadModuleAsync(string moduleName);

        /// <summary>
        /// 初始化模块
        /// rootContainer: 根容器，模块可在其中注册全局单例
        /// </summary>
        void InitializeModule(string moduleName, IContainerProvider rootContainer);
    }
}