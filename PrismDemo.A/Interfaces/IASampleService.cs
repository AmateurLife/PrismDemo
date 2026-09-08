using System.Collections.ObjectModel;
using System.ComponentModel;

namespace PrismDemo.A.Interfaces
{
    /// <summary>
    /// 模块 A 对外服务契约（框架演示版）。
    /// 真实工程在此定义业务接口（如 NaClO 的 IClService），
    /// Demo 仅演示"模块服务通过 ServiceProxy 注册、可热更新目标"这一机制。
    /// </summary>
    public interface IASampleService : INotifyPropertyChanged
    {
        string ModuleName { get; }

        ObservableCollection<Models.SampleItem> Items { get; }

        double LatestValue { get; set; }

        void Start();

        void Stop();
    }
}