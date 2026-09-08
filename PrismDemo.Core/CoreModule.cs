using Prism.Ioc;
using Prism.Modularity;

namespace PrismDemo.Core
{
    /// <summary>
    /// Core 层模块钩子（框架演示版）。
    /// 真实工程中 Core 的注册主要在 App.RegisterTypes 内联完成，
    /// 此处保留空模块作为"Core 可注册全局依赖"的扩展点。
    /// </summary>
    public class CoreModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
        }
    }
}