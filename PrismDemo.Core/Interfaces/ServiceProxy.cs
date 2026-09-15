using System;
using System.Diagnostics;
using System.Threading;

namespace PrismDemo.Core.Interfaces
{
    /// <summary>
    /// 服务代理包装器，用于热重载场景
    /// 
    /// 设计意图：
    ///   - 容器中注册的是 Proxy 实例（Singleton，永不改变）
    ///   - Proxy 内部持有对实际服务实例的引用
    ///   - 热重载时，只需替换 Target 引用，无需修改容器注册
    /// 
    /// 使用方式：
    ///   var proxy = new ServiceProxy<IAService>();
    ///   container.RegisterInstance<IAService>(proxy);
    ///   // 热重载时：
    ///   proxy.SetTarget(newServiceInstance);
    /// </summary>
    /// <typeparam name="T">服务接口类型</typeparam>
    public class ServiceProxy<T> : IDisposable where T : class
    {
        private T _target;
        private readonly object _lock = new object();
        private bool _disposed;

        /// <summary>
        /// 当前代理的目标实例
        /// </summary>
        public T Target
        {
            get
            {
                lock (_lock)
                {
                    if (_disposed)
                        throw new ObjectDisposedException(GetType().Name);
                    return _target;
                }
            }
        }

        /// <summary>
        /// 是否有已设置的目标实例
        /// </summary>
        public bool HasTarget
        {
            get
            {
                lock (_lock)
                {
                    return _target != null;
                }
            }
        }

        /// <summary>
        /// 设置新的目标实例（线程安全）
        /// </summary>
        /// <param name="newTarget">新的服务实例</param>
        /// <returns>被替换的旧实例（如果有的话）</returns>
        public T SetTarget(T newTarget)
        {
            if (newTarget == null)
                throw new ArgumentNullException(nameof(newTarget));

            lock (_lock)
            {
                if (_disposed)
                    throw new ObjectDisposedException(GetType().Name);

                var oldTarget = _target;
                _target = newTarget;

                Debug.WriteLine($"[Proxy<{typeof(T).Name}>] Target 已更新: " +
                    $"旧 Hash={oldTarget?.GetHashCode()}, 新 Hash={newTarget.GetHashCode()}");

                return oldTarget;
            }
        }

        /// <summary>
        /// 清除目标实例（用于卸载场景）
        /// </summary>
        /// <returns>被清除的旧实例</returns>
        public T ClearTarget()
        {
            lock (_lock)
            {
                if (_disposed)
                    throw new ObjectDisposedException(GetType().Name);

                var oldTarget = _target;
                _target = null;

                Debug.WriteLine($"[Proxy<{typeof(T).Name}>] Target 已清除: Hash={oldTarget?.GetHashCode()}");

                return oldTarget;
            }
        }

        /// <summary>
        /// 通过反射调用目标实例的方法
        /// </summary>
        public TResult Invoke<TResult>(Func<T, TResult> func)
        {
            var target = Target;
            if (target == null)
                throw new InvalidOperationException($"Proxy<{typeof(T).Name}> 未设置目标实例");

            return func(target);
        }

        /// <summary>
        /// 通过反射调用目标实例的无返回值方法
        /// </summary>
        public void Invoke(Action<T> action)
        {
            var target = Target;
            if (target == null)
                throw new InvalidOperationException($"Proxy<{typeof(T).Name}> 未设置目标实例");

            action(target);
        }

        public void Dispose()
        {
            if (_disposed) return;

            lock (_lock)
            {
                _disposed = true;
                var oldTarget = _target;
                _target = null;

                if (oldTarget is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                        Debug.WriteLine($"[Proxy<{typeof(T).Name}>] 已 Dispose 旧目标: Hash={oldTarget.GetHashCode()}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Proxy<{typeof(T).Name}>] Dispose 旧目标异常: {ex.Message}");
                    }
                }
            }
        }
    }
}
