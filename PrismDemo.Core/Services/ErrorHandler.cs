using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using PrismDemo.Core.Interfaces;
using PrismDemo.Core.Views;

namespace PrismDemo.Core.Services
{
    /// <summary>
    /// 错误信息数据类
    /// 用于存储和传递错误相关的信息
    /// </summary>
    public class ErrorInfo
    {
        /// <summary>
        /// 报错时间
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 堆栈跟踪
        /// </summary>
        public string StackTrace { get; set; }

        /// <summary>
        /// 错误来源（如方法名）
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// 内部异常
        /// </summary>
        public Exception InnerException { get; set; }

        /// <summary>
        /// 无参构造函数
        /// </summary>
        public ErrorInfo() { }

        /// <summary>
        /// 从异常对象构造错误信息
        /// </summary>
        /// <param name="ex">异常对象</param>
        /// <param name="source">错误来源标识</param>
        public ErrorInfo(Exception ex, string source = "")
        {
            Timestamp = DateTime.Now;
            Message = ex.Message;
            StackTrace = ex.StackTrace;
            Source = source;
            InnerException = ex.InnerException;
        }

        /// <summary>
        /// 重写ToString方法，返回格式化的错误信息
        /// </summary>
        /// <returns>格式化后的错误字符串</returns>
        public override string ToString()
        {
            return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Source}] {Message}";
        }
    }

    /// <summary>
    /// 错误处理器实现类
    /// 负责捕获错误、记录日志、弹出报警窗口
    /// </summary>
    public class ErrorHandler : IErrorHandler
    {
        /// <summary>
        /// 错误列表，存储所有捕获的错误
        /// </summary>
        private readonly List<ErrorInfo> _errors = new();

        /// <summary>
        /// 线程锁对象，保证线程安全
        /// </summary>
        private readonly object _lock = new object();

        /// <summary>
        /// 错误发生事件
        /// 订阅此事件可自定义处理逻辑（如自定义弹窗）
        /// </summary>
        public event EventHandler<ErrorInfo> OnErrorOccurred;

        /// <summary>
        /// 处理错误的核心方法
        /// 1. 创建ErrorInfo对象
        /// 2. 添加到错误列表
        /// 3. 输出到Debug日志
        /// 4. 触发事件
        /// 5. 弹出报警窗口
        /// </summary>
        /// <param name="ex">异常对象</param>
        /// <param name="source">错误来源标识</param>
        public void HandleError(Exception ex, string source = "")
        {
            ErrorInfo errorInfo;
            lock (_lock)
            {
                errorInfo = new ErrorInfo(ex, source);
                _errors.Add(errorInfo);
                Debug.WriteLine(errorInfo.ToString());
                OnErrorOccurred?.Invoke(this, errorInfo);
            }

            // UI 操作放到锁外面异步执行，不阻塞调用线程
            Application.Current?.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background, () =>
            {
                var alarmWindow = new AlarmWindow(
                    $"{errorInfo.Timestamp:yyyy-MM-dd HH:mm:ss}\n错误信息: {errorInfo.Message}");
                alarmWindow.ShowDialog();
            });
        }

        /// <summary>
        /// 获取所有已捕获的错误列表
        /// </summary>
        /// <returns>错误信息列表的副本</returns>
        public List<ErrorInfo> GetErrors()
        {
            lock (_lock)
            {
                return new List<ErrorInfo>(_errors);
            }
        }

        /// <summary>
        /// 清空所有错误记录
        /// </summary>
        public void ClearErrors()
        {
            lock (_lock)
            {
                _errors.Clear();
            }
        }
    }
}
