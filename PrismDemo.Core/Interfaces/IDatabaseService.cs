using LiveChartsCore.Defaults;
using PrismDemo.Core.Models;
using PrismDemo.Core.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PrismDemo.Core.Interfaces
{
    /// <summary>
    /// 数据库服务接口：管理 ConnectData 配置、OPC 监测、报警配置与历史记录的持久化。
    /// </summary>
    public interface IDatabaseService
    {
        /// <summary>ConnectData 监测值更新事件</summary>
        event Action ConnectDataUpdated;

        /// <summary>ConnectData 配置更新事件（SettingConnect 保存后触发）</summary>
        event Action ConnectConfigUpdated;

        /// <summary>数据库错误事件（如登录失败），参数为错误信息</summary>
        event Action<string> DatabaseErrorOccurred;

        /// <summary>数据库连接恢复事件</summary>
        event Action DatabaseRecovered;

        /// <summary>新报警记录插入事件</summary>
        event Action AlarmInserted;

        /// <summary>从 connect 表读取所有 ConnectData 配置</summary>
        Task<List<ConnectData>> GetConnectData();

        /// <summary>获取 ConnectData 当前值的字典（key=名称, value=ConnectData 对象）</summary>
        Dictionary<string, ConnectData> GetConnectDataValue(List<ConnectData> datas, OpcServices opcServices);

        /// <summary>启动 OPC 实时监测，按指定间隔刷新 ConnectData 值</summary>
        Task StartMonitoringAsync(List<ConnectData> datas, OpcServices opcServices, int intervalMs = 1000);

        /// <summary>停止 OPC 实时监测</summary>
        Task StopMonitoringAsync();

        /// <summary>根据 ConnectData 配置自动创建 connectData 表并补齐缺失列（启动时/配置变更时/循环中调用）</summary>
        void EnsureConnectDataTable(List<ConnectData> datas);

        /// <summary>保存 ConnectData 值快照到 connectData 表（仅插入数据，不再负责建表扩列）</summary>
        void SaveConnectData(List<ConnectData> datas);

        /// <summary>更新 connect 表中单条 ConnectData 的配置信息</summary>
        Task UpdateConnectDataAsync(ConnectData data);

        /// <summary>新增一条 ConnectData 配置到 ConnectConfig 表</summary>
        Task InsertConnectDataAsync(ConnectData data);

        /// <summary>删除 ConnectConfig 表中指定名称的配置</summary>
        Task DeleteConnectDataAsync(string name);

        /// <summary>从 BConfig/AConfig 表读取所有 Filter 配置</summary>
        Task<List<FilterConfigItem>> GetFilterConfigsAsync(string tableName);

        /// <summary>新增一条 Filter 配置</summary>
        Task InsertFilterConfigAsync(string tableName, FilterConfigItem item);

        /// <summary>更新一条 Filter 配置</summary>
        Task UpdateFilterConfigAsync(string tableName, FilterConfigItem item);

        /// <summary>删除一条 Filter 配置</summary>
        Task DeleteFilterConfigAsync(string tableName, string name);

        /// <summary>
        /// 获取历史报警记录，优先取未确认的最新30条，不足30条由已确认补齐
        /// </summary>
        Task<List<AlarmRecord>> GetAlarmHistoryAsync();

        /// <summary>
        /// 插入一条报警历史记录，返回自增 id
        /// </summary>
        Task<int> InsertAlarmHistoryAsync(AlarmRecord record);

        /// <summary>
        /// 更新报警配置的 trigger 字段（upper_limit_trigger / lower_limit_trigger）
        /// </summary>
        Task UpdateAlarmConfigTriggerAsync(int configId, string triggerColumn, bool value);

        /// <summary>
        /// 更新报警历史记录的确认时间
        /// </summary>
        Task UpdateAlarmHistoryConfirmAsync(int recordId, DateTime confirmTime);

        /// <summary>
        /// 如果报警记录未确认（confirmTime 为 null），则更新确认时间和状态
        /// </summary>
        Task ConfirmAlarmIfNeededAsync(int recordId, DateTime confirmTime);

        /// <summary>
        /// 获取所有报警配置
        /// </summary>
        Task<List<AlarmConfig>> GetAlarmConfigsAsync();

        /// <summary>
        /// 新增报警配置
        /// </summary>
        Task<int> InsertAlarmConfigAsync(AlarmConfig config);

        /// <summary>
        /// 更新报警配置
        /// </summary>
        Task UpdateAlarmConfigAsync(AlarmConfig config);

        /// <summary>
        /// 删除报警配置
        /// </summary>
        Task DeleteAlarmConfigAsync(int id);

        /// <summary>
        /// 按 ID 查询单条报警配置
        /// </summary>
        Task<AlarmConfig?> GetAlarmConfigByIdAsync(int id);

        /// <summary>
        /// 从 connectData 表查询某字段的时间序列数据
        /// </summary>
        Task<List<DateTimePoint>> GetColumnTimeSeriesAsync(
            string columnName, DateTime startTime, DateTime endTime);

        /// <summary>
        /// 分页查询报警历史（含筛选）
        /// </summary>
        Task<(List<AlarmRecord> Records, int TotalCount)> QueryAlarmHistoryAsync(
            DateTime? startTime, DateTime? endTime,
            string configType, int pageIndex, int pageSize);

        /// <summary>
        /// 查询 HomePage 所需的出厂水核心指标时间序列
        /// </summary>
        /// <returns>key=字段名, value=时间序列</returns>
        Task<Dictionary<string, List<DateTimePoint>>> LoadHomePageTimeSeriesAsync(string[] columns, int hours = 1);

        /// <summary>
        /// 通用查询：按表名、列名、时间范围获取数据
        /// </summary>
        Task<List<Dictionary<string, object>>> QueryDataAsync(
            string tableName,
            IEnumerable<string> columnNames = null,
            DateTime? startTime = null,
            DateTime? endTime = null);

        /// <summary>
        /// 获取所有用户数据表名（排除 alarmConfig、AlarmHistory 及系统表）
        /// </summary>
        Task<List<string>> GetTableNamesAsync();

        /// <summary>
        /// 备份数据库
        /// </summary>
        /// <param name="backupPath">备份目录路径</param>
        /// <param name="enableCompression">是否启用压缩</param>
        /// <param name="enableVerification">是否启用备份验证</param>
        /// <param name="retentionDays">备份保留天数（0=不清理）</param>
        /// <param name="filenameFormat">文件名格式模板</param>
        Task<(bool Success, string Message, string FilePath)> BackupDatabaseAsync(
            string backupPath,
            bool enableCompression,
            bool enableVerification,
            int retentionDays,
            string filenameFormat);
    }
}
