using Microsoft.Data.SqlClient;
using PrismDemo.Core.Configuration;
using PrismDemo.Core.Interfaces;
using PrismDemo.Core.Models;
using PrismDemo.Core.Services;
using PrismDemo.A.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace PrismDemo.A.Services
{
    /// <summary>
    /// A剂数据服务实现类
    /// 负责从ConnectData字典中提取A剂所需数据形成新字典pd并更新数据库AData
    /// </summary>
    public class ADataService : IDisposable
    {
        private Timer _timer;
        private bool _disposed = false;

        /// <summary>
        /// 字段配置列表（从数据库加载）
        /// </summary>
        public List<AFieldConfig> _fieldConfigs = new();

        /// <summary>
        /// 上一次A剂数据
        /// </summary>
        public AData? LastAData { get; set; }

        /// <summary>
        /// 保存本轮A剂数据供下轮使用（深拷贝，与当前数据完全独立）
        /// </summary>
        public void SaveLastAData(AData? aData)
        {
            LastAData = CloneAData(aData);
        }

        private static AData? CloneAData(AData? source)
        {
            if (source == null) return null;
            var clone = new AData();
            foreach (var kvp in source)
                clone[kvp.Key] = DatabaseService.CloneConnectData(kvp.Value);
            return clone;
        }

        /// <summary>
        /// A剂数据更新后触发的事件
        /// </summary>
        public event Action ADataUpdated;

        public ADataService()
        {
            try
            {
                Load_fieldConfigsFromDb();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ADataService] 构造时加载字段配置失败（模块仍可加载，等待后续重试）: {ex.Message}");
            }
        }

        /// <summary>
        /// 从数据库加载字段配置
        /// </summary>
        private void Load_fieldConfigsFromDb()
        {
            try
            {
                using var conn = new SqlConnection(Config.DatabaseConnectionString);
                conn.Open();

                var sql = "SELECT name, connectName, IsStore, IsFilter, FilterLength FROM AConfig";
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    _fieldConfigs.Add(new AFieldConfig
                    {
                        Name = reader.IsDBNull(0) ? "" : reader.GetString(0),
                        ConnectName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        IsStore = reader.IsDBNull(2) ? false : reader.GetBoolean(2),
                        IsFilter = reader.IsDBNull(3) ? false : reader.GetBoolean(3),
                        FilterLength = reader.IsDBNull(4) ? null : reader.GetInt32(4)
                    });
                }

                Debug.WriteLine($"[ADataService] 从数据库加载了 {_fieldConfigs.Count} 个字段配置");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ADataService] 加载字段配置失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 更新A剂数据
        /// 从ConnectData字典中提取数据并更新剂ADataCollection
        /// </summary>
        /// <param name="collectedData">连接配置数据字典</param>
        /// <param name="pd">A剂数据字典</param>
        /// <returns>更新后的pd</returns>
        public AData UpdateAData(Dictionary<string, ConnectData> collectedData, AData? pd)
        {
            if (collectedData == null || collectedData.Count == 0)
            {
                Debug.WriteLine("[ADataService] collectedData字典为空");
                return pd ?? new AData();
            }

            try
            {
                if (pd == null)
                    pd = new AData();

                foreach (var config in _fieldConfigs)
                {
                    if (!string.IsNullOrEmpty(config.ConnectName) &&
                        collectedData.TryGetValue(config.ConnectName, out var connectData))
                    {
                        if (connectData.isvalid)
                        {
                            //采用克隆而不是直接引用，避免后续数据变动影响已保存的A剂数据
                            pd[config.Name] = DatabaseService.CloneConnectData(connectData);
                        }
                        else
                        {
                            if (LastAData != null && LastAData.ContainsKey(config.Name))
                                pd[config.Name] = DatabaseService.CloneConnectData(LastAData[config.Name]);
                            else
                                pd[config.Name] = DatabaseService.CloneConnectData(connectData);
                        }
                    }
                    else if (!pd.ContainsKey(config.Name))
                    {
                        //若connectName为空也可以创建一个同名的空connect类，保证pd有对应字段，方便前端展示和后续数据更新
                        pd[config.Name] = DatabaseService.CreateDefaultConnectData(config.Name);
                    }
                }

                ADataUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ADataService] 更新数据异常: {ex.Message}");
            }

            return pd;
        }

        /// <summary>
        /// 停止自动更新
        /// </summary>
        public void StopAutoUpdate()
        {
            _timer?.Dispose();
            _timer = null;
            Debug.WriteLine("[ADataService] 自动更新已停止");
        }

        /// <summary>
        /// 确保数据库表结构正确（启动时调用一次）
        /// 根据 _fieldConfigs 中 IsStore=true 的字段，自动创建表和缺失的列
        /// </summary>
        public void EnsureTableStructure()
        {
            try
            {
                using var conn = new SqlConnection(Config.DatabaseConnectionString);
                conn.Open();

                var tableName = "AData";
                var storeFields = _fieldConfigs.Where(c => c.IsStore).ToList();

                if (!storeFields.Any())
                {
                    Debug.WriteLine("[EnsureTableStructure] 没有需要保存的字段");
                    return;
                }

                var columnNames = storeFields.Select(c => c.Name).ToList();

                var createTableSql = $@"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tableName}')
                    BEGIN
                        CREATE TABLE {tableName} (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            Datetime DATETIME
                        )
                    END
                    ELSE
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = 'Datetime')
                        BEGIN
                            ALTER TABLE {tableName} ADD [Datetime] DATETIME NULL
                        END
                    END";

                using var createCmd = conn.CreateCommand();
                createCmd.CommandText = createTableSql;
                createCmd.ExecuteNonQuery();

                var existingColumns = GetExistingColumns(conn, tableName);
                var missingColumns = columnNames.Except(existingColumns, StringComparer.Ordinal).ToList();

                var typeMap = LoadConnectTypeMap(conn, storeFields);

                foreach (var col in missingColumns)
                {
                    var config = storeFields.First(c => c.Name == col);
                    var connectType = typeMap.TryGetValue(config.ConnectName, out var t) ? t : "";
                    var sqlType = MapTypeToSqlType(connectType);
                    var alterSql = $"ALTER TABLE {tableName} ADD [{col}] {sqlType} NULL";
                    using var alterCmd = conn.CreateCommand();
                    alterCmd.CommandText = alterSql;
                    alterCmd.ExecuteNonQuery();
                    Debug.WriteLine($"[EnsureTableStructure] 已添加列: {col} (类型: {sqlType})");
                }

                Debug.WriteLine("[EnsureTableStructure] 表结构已确保");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EnsureTableStructure] 失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 保存A剂数据到数据库（循环中调用）
        /// 只保存 IsStore=true 的字段数据
        /// </summary>
        public void SaveAData(AData aData)
        {
            if (aData == null) return;

            try
            {
                using var conn = new SqlConnection(Config.DatabaseConnectionString);
                conn.Open();

                var tableName = "AData";
                var storeFields = _fieldConfigs.Where(c => c.IsStore).ToList();

                if (!storeFields.Any())
                {
                    Debug.WriteLine("[SaveAData] 没有需要保存的字段");
                    return;
                }

                var columnNames = storeFields.Select(c => c.Name).ToList();
                var paramNames = storeFields.Select(c => $"@{c.Name}").ToList();

                var insertSql = $"INSERT INTO {tableName} (Datetime, {string.Join(",", columnNames)}) VALUES (GETDATE(), {string.Join(",", paramNames)})";

                using var insertCmd = conn.CreateCommand();
                insertCmd.CommandText = insertSql;

                foreach (var config in storeFields)
                {
                    var paramName = $"@{config.Name}";
                    var connectData = aData.GetField(config.Name);
                    var value = connectData?.Value;
                    var dataType = connectData?.Type ?? "string";

                    var dbValue = ConvertToDbValue(value, dataType);
                    insertCmd.Parameters.AddWithValue(paramName, dbValue);
                }

                insertCmd.ExecuteNonQuery();
                Trace.WriteLine($"[SaveAData] 数据已保存到数据库");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[SaveAData] 保存失败: {ex.Message}");
                Trace.WriteLine($"异常详情: {ex.StackTrace}");
            }
        }
        public List<HistoryPoint> GetHistoricalData(string[] fields, int hours = 24)
        {
            var result = new List<HistoryPoint>();
            try
            {
                using var conn = new SqlConnection(Config.DatabaseConnectionString);
                conn.Open();

                var columns = string.Join(",", fields.Select(f => $"[{f}]"));
                var sql = $"SELECT Datetime, {columns} FROM AData WHERE Datetime >= DATEADD(HOUR, -@hours, GETDATE()) ORDER BY Datetime";

                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@hours", hours);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var point = new HistoryPoint { Time = reader.GetDateTime(0), Values = new() };
                    for (int i = 0; i < fields.Length; i++)
                        point.Values[fields[i]] = reader.IsDBNull(i + 1) ? 0 : Convert.ToDouble(reader.GetValue(i + 1));
                    result.Add(point);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GetHistoricalData] 查询失败: {ex.Message}");
            }
            return result;
        }

        /// <summary>
        /// 按日期范围查询历史数据（历史曲线页使用）
        /// SQL: SELECT Datetime, {fields} FROM AData WHERE Datetime BETWEEN @start AND @end
        /// </summary>
        /// <param name="fields">要查询的字段名数组（对应 aData 表的列名）</param>
        /// <param name="start">起始时间（含）</param>
        /// <param name="end">结束时间（含）</param>
        public List<HistoryPoint> GetHistoricalDataByRange(string[] fields, DateTime start, DateTime end)
        {
            var result = new List<HistoryPoint>();
            try
            {
                using var conn = new SqlConnection(Config.DatabaseConnectionString);
                conn.Open();

                var columns = string.Join(",", fields.Select(f => $"[{f}]"));
                var sql = $"SELECT Datetime, {columns} FROM AData WHERE Datetime >= @start AND Datetime <= @end ORDER BY Datetime";

                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@start", start);
                cmd.Parameters.AddWithValue("@end", end);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var point = new HistoryPoint { Time = reader.GetDateTime(0), Values = new() };
                    for (int i = 0; i < fields.Length; i++)
                        point.Values[fields[i]] = reader.IsDBNull(i + 1) ? 0 : Convert.ToDouble(reader.GetValue(i + 1));
                    result.Add(point);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GetHistoricalDataByRange] 查询失败: {ex.Message}");
            }
            return result;
        }

        public ChartQueryResult GetHistoryChartData(string[] fields, DateTime start, DateTime end)
        {
            try
            {
                using var conn = new SqlConnection(Config.DatabaseConnectionString);
                conn.Open();

                var columns = string.Join(",", fields.Select(f => $"[{f}]"));
                var sql = $"SELECT Datetime, {columns} FROM AData WHERE Datetime >= @start AND Datetime <= @end ORDER BY Datetime";

                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@start", start);
                cmd.Parameters.AddWithValue("@end", end);

                var timestamps = new List<double>();
                var values = new List<double>[fields.Length];
                for (int i = 0; i < fields.Length; i++)
                    values[i] = new List<double>();

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    timestamps.Add(reader.GetDateTime(0).ToOADate());
                    for (int i = 0; i < fields.Length; i++)
                        values[i].Add(reader.IsDBNull(i + 1) ? 0 : Convert.ToDouble(reader.GetValue(i + 1)));
                }

                var dict = new Dictionary<string, double[]>(fields.Length);
                for (int i = 0; i < fields.Length; i++)
                    dict[fields[i]] = values[i].ToArray();

                return new ChartQueryResult
                {
                    Timestamps = timestamps.ToArray(),
                    Values = dict
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GetHistoryChartData] 查询失败: {ex.Message}");
                return new ChartQueryResult { Timestamps = Array.Empty<double>(), Values = new() };
            }
        }

        private static Dictionary<string, string> LoadConnectTypeMap(
            SqlConnection conn, IEnumerable<AFieldConfig> storeFields)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var names = storeFields.Select(f => f.ConnectName)
                                   .Where(n => !string.IsNullOrEmpty(n))
                                   .Distinct().ToList();
            if (names.Count == 0) return map;

            var placeholders = names.Select((_, i) => $"@n{i}");
            var sql = $"SELECT name, type FROM ConnectConfig WHERE name IN ({string.Join(",", placeholders)})";
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            for (int i = 0; i < names.Count; i++)
                cmd.Parameters.AddWithValue($"@n{i}", names[i]);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                map[reader.GetString(0)] = reader.IsDBNull(1) ? "" : reader.GetString(1);
            return map;
        }

        private static string MapTypeToSqlType(string connectType)
        {
            return connectType?.ToLowerInvariant() switch
            {
                "float" or "double" => "FLOAT",
                "int" or "int32" or "short" or "word" => "INT",
                "bool" or "boolean" => "BIT",
                _ => "NVARCHAR(MAX)"
            };
        }

        /// <summary>
        /// 配合 SaveAData 方法使用，检查表中已有的列，避免重复添加
        /// </summary>
        private HashSet<string> GetExistingColumns(SqlConnection conn, string tableName)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sql = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @tableName";
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@tableName", tableName);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                columns.Add(reader.IsDBNull(0) ? string.Empty : reader.GetString(0));
            }
            return columns;
        }

        /// <summary>
        /// 配合 SaveAData 方法使用，根据 ConnectData 的 Type 将 Value 转换为适合数据库存储的值
        /// </summary>
        private static object ConvertToDbValue(object value, string type)
        {
            if (value == null) return DBNull.Value;

            return type.ToLowerInvariant() switch
            {
                "float" or "double" => Math.Round(Convert.ToDouble(value), 3),
                "int" or "int32" or "short" or "word" => Convert.ToInt32(value),
                "bool" or "boolean" => Convert.ToBoolean(value),
                _ => value == null ? DBNull.Value : value.ToString()
            };
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                StopAutoUpdate();
                _disposed = true;
            }
        }
    }
}