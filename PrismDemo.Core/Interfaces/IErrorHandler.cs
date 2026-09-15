using PrismDemo.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrismDemo.Core.Interfaces
{
    /// <summary>
    /// 错误处理器接口
    /// 定义错误处理的规范
    /// </summary>
    public interface IErrorHandler
    {
        /// <summary>
        /// 处理错误
        /// </summary>
        /// <param name="ex">异常对象</param>
        /// <param name="source">错误来源标识</param>
        void HandleError(Exception ex, string source = "");

        /// <summary>
        /// 获取所有错误列表
        /// </summary>
        /// <returns>错误信息列表</returns>
        List<ErrorInfo> GetErrors();

        /// <summary>
        /// 清空错误列表
        /// </summary>
        void ClearErrors();

        /// <summary>
        /// 错误发生事件
        /// 订阅此事件可自定义处理逻辑
        /// </summary>
        event EventHandler<ErrorInfo> OnErrorOccurred;
    }
}

