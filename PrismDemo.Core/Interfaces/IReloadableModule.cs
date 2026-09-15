using System.Threading.Tasks;

namespace PrismDemo.Core.Interfaces
{
    /// <summary>
    /// 可热重载模块契约接口
    /// 
    /// 模块实现此接口后，DynamicModuleManager 可在运行时
    /// 卸载旧版本并加载新版本，无需重启应用程序。
    /// </summary>
    public interface IReloadableModule
    {
        /// <summary>
        /// 模块名称（用于标识和匹配）
        /// </summary>
        string ModuleName { get; }

        /// <summary>
        /// 模块是否已加载
        /// </summary>
        bool IsLoaded { get; }

        /// <summary>
        /// 获取模块当前版本信息（可选，用于日志和UI显示）
        /// </summary>
        string? Version { get; }
    }
}
