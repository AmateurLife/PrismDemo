namespace PrismDemo.Core.Interfaces
{
    /// <summary>
    /// 滤波配置接口
    /// 由 AFieldConfig / BFieldConfig 实现，对应模块配置表中的 IsFilter / FilterLength 列。
    /// 本示例只保留配置契约，不含具体的滤波实现。
    /// </summary>
    public interface IFilterConfig
    {
        /// <summary>
        /// 字段名
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 是否需要滤波
        /// </summary>
        bool IsFilter { get; }

        /// <summary>
        /// 滤波窗口长度（null 表示不滤波）
        /// </summary>
        int? FilterLength { get; }
    }
}
