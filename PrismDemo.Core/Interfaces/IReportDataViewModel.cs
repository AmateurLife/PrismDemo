using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrismDemo.Core.Interfaces
{
    /// <summary>
    /// 所有动态报表 ViewModel 必须实现此接口
    /// </summary>
    public interface IReportDataViewModel
    {
        /// <summary>
        /// 用于唯一标识，如 "A"
        /// </summary>
        string ReportKey { get; } 
        /// <summary>
        /// 显示名称，如 "A剂报表"
        /// </summary>
        string DisplayName { get; } 
    }
}
