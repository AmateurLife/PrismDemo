#nullable enable
namespace PrismDemo.Core.Interfaces
{
    /// <summary>
    /// 可热重载模块契约接口。模块实现此接口后，
    /// DynamicModuleManager 可在运行时卸载旧版本并加载新版本，无需重启应用。
    /// </summary>
    public interface IReloadableModule
    {
        string ModuleName { get; }

        bool IsLoaded { get; }

        string? Version { get; }
    }
}