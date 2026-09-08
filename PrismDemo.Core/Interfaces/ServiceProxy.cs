using System;
using System.Diagnostics;

namespace PrismDemo.Core.Interfaces
{
    /// <summary>
    /// 服务代理包装器，用于热重载场景。
    /// 容器中注册的是 Proxy 实例（Singleton，永不改变），
    /// 热重载时只需替换 Target 引用，无需修改容器注册。
    /// </summary>
    /// <typeparam name="T">服务接口类型</typeparam>
    public class ServiceProxy<T> : IDisposable where T : class
    {
        private T _target;
        private readonly object _lock = new();
        private bool _disposed;

        public T Target
        {
            get
            {
                lock (_lock)
                {
                    if (_disposed) throw new ObjectDisposedException(GetType().Name);
                    return _target;
                }
            }
        }

        public bool HasTarget
        {
            get
            {
                lock (_lock) { return _target != null; }
            }
        }

        public T SetTarget(T newTarget)
        {
            if (newTarget == null) throw new ArgumentNullException(nameof(newTarget));

            lock (_lock)
            {
                if (_disposed) throw new ObjectDisposedException(GetType().Name);

                var oldTarget = _target;
                _target = newTarget;
                Debug.WriteLine($"[Proxy<{typeof(T).Name}>] Target 已更新");
                return oldTarget;
            }
        }

        public T ClearTarget()
        {
            lock (_lock)
            {
                if (_disposed) throw new ObjectDisposedException(GetType().Name);

                var oldTarget = _target;
                _target = null;
                Debug.WriteLine($"[Proxy<{typeof(T).Name}>] Target 已清除");
                return oldTarget;
            }
        }

        public TResult Invoke<TResult>(Func<T, TResult> func)
        {
            var target = Target;
            if (target == null) throw new InvalidOperationException($"Proxy<{typeof(T).Name}> 未设置目标实例");
            return func(target);
        }

        public void Invoke(Action<T> action)
        {
            var target = Target;
            if (target == null) throw new InvalidOperationException($"Proxy<{typeof(T).Name}> 未设置目标实例");
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
                    try { disposable.Dispose(); }
                    catch (Exception ex) { Debug.WriteLine($"[Proxy<{typeof(T).Name}>] Dispose 旧目标异常: {ex.Message}"); }
                }
            }
        }
    }
}