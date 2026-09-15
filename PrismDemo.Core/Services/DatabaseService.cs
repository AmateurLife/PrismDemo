using LiveChartsCore.Defaults;
using Microsoft.Data.SqlClient;
using Opc.Ua;
using PrismDemo.Core.Configuration;
using PrismDemo.Core.Interfaces;
using PrismDemo.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PrismDemo.Core.Services
{
    /// <summary>
    /// 数据库服务实现：管理 ConnectData 配置持久化、OPC 监测、报警配置与历史记录的 SQL Server 访问。
    /// </summary>
    public class DatabaseService : IDatabaseService
    {
        private Timer _timer;

        /// <inheritdoc/>
        public event Action ConnectDataUpdated;

        /// <inheritdoc/>
        public event Action ConnectConfigUpdated;

        /// <inheritdoc/>
        public event Action<string> DatabaseErrorOccurred;

        /// <inheritdoc/>
        public event Action DatabaseRecovered;

        /// <inheritdoc/>
        public event Action AlarmInserted;

        private String DatabaseConfig
        {
            get
            {
                var connStr = Config.DatabaseConnectionString;
                if (connStr.IndexOf("Connect Timeout", StringComparison.OrdinalIgnoreCase) < 0
                    && connStr.IndexOf("Connection Timeout", StringComparison.OrdinalIgnoreCase) < 0)
                    connStr += ";Connect Timeout=3";
                return connStr;
            }
        }

        /// <summary>初始化 DatabaseService 实例</summary>
        public DatabaseService()
        {

        }

        /// <summary>检查异常是否为数据库登录失败，若是则触发事件</summary>
        private void ReportDbError(Exception ex)
        {
            if (ex is SqlException sqlEx && sqlEx.Number == 18456)
            {
                DatabaseErrorOccurred?.Invoke("数据库登录失败，请检查连接配置");
            }
        }

        /// <summary>从字典安全取值并通过委托设置到目标属性</summary>
        public static void SetPropertyValue<T>(Dictionary<string, object> data, string key, Action<T> setter)
        {
            if (data.TryGetValue(key, out var value) && value != null)
            {
                try
                {
                    setter((T)Convert.ChangeType(value, typeof(T)));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("转换键 " + key + " 失败: " + ex.Message);
                }
            }
        }

        public static ConnectData CloneConnectData(ConnectData source)
        {
            return new ConnectData
            {
                Name = source.Name,
                Type = source.Type,
                Value = source.Value,
                OpcAddress = source.OpcAddress,
                MockOpcAddress = source.MockOpcAddress,
                isvalid = source.isvalid
            };
        }

        public static ConnectData CreateDefaultConnectData(string name)
        {
            return new ConnectData { Name = name , Value = 0 };
        }

        /// <summary>从 connect 表读取所有 ConnectData 配置</summary>
        public async Task<List<ConnectData>> GetConnectData()
        {
            if (string.IsNullOrWhiteSpace(DatabaseConfig))
            {
                throw new ArgumentNullException(nameof(DatabaseConfig), "数据库连接字符串不能为空");
            }

            Debug.WriteLine("正在从数据库读取 connect 表的数据...");
            var result = new List<ConnectData>();

            try
            {
                await using var conn = new SqlConnection(DatabaseConfig);
                await conn.OpenAsync();
                Debug.WriteLine("数据库连接成功");

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"SELECT
                                    name, type, opcAddress, mock_opcAddress, description
                                    FROM ConnectConfig";

                await using var reader = await cmd.ExecuteReaderAsync();

                var dbName = reader.GetOrdinal("name");
                var dbType = reader.GetOrdinal("type");
                var dbOpcAddress = reader.GetOrdinal("opcAddress");
                var dbMockOpcAddress = reader.GetOrdinal("mock_opcAddress");
                var dbDescription = reader.GetOrdinal("description");

                int rowIndex = 0;
                while (await reader.ReadAsync())
                {
                    var item = new ConnectData
                    {
                        Name = reader.IsDBNull(dbName)
                            ? string.Empty
                            : reader.GetString(dbName),
                        Type = reader.IsDBNull(dbType)
                            ? string.Empty
                            : reader.GetString(dbType),
                        OpcAddress = reader.IsDBNull(dbOpcAddress)
                            ? string.Empty
                            : "ns=2;s=" + reader.GetString(dbOpcAddress),
                        MockOpcAddress = reader.IsDBNull(dbMockOpcAddress)
                            ? string.Empty
                            : "ns=2;s=" + reader.GetString(dbMockOpcAddress),
                        Description = reader.IsDBNull(dbDescription)
                            ? string.Empty
                            : reader.GetString(dbDescription),
                    };

                    result.Add(item);
                    rowIndex++;
                }
            }
            catch (SqlException ex)
            {
                Debug.WriteLine("数据库操作异常：" + ex.Message + "，错误码：" + ex.Number);
                ReportDbError(ex);
                throw;
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine("数据库操作状态异常：" + ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("未知异常：" + ex.Message);
                ReportDbError(ex);
                throw;
            }

            DatabaseRecovered?.Invoke();
            return result;
        }

        public Dictionary<string, ConnectData> GetConnectDataValue(List<ConnectData> datas, OpcServices opcServices)
        {
            var result = new Dictionary<string, ConnectData>();
            foreach (var item in datas)
            {
                result[item.Name] = item;
                //Debug.WriteLine("[DatabaseService] " + item.Name + " 的值: " + item.Value);
            }
            return result;
        }

        /// <summary>启动 OPC 实时监测，按指定间隔刷新 ConnectData 值</summary>
        public Task StartMonitoringAsync(List<ConnectData> datas, OpcServices opcServices, int intervalMs = 1000)
        {
            _timer?.Dispose();

            opcServices.KeepConnected();

            _timer = new Timer(async _ =>
            {
                try
                {
                    await opcServices.RefreshDataAsync(datas);
                    ConnectDataUpdated?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("监测刷新异常：" + ex.Message);
                }
            }, null, 0, intervalMs);

            Debug.WriteLine("[Core.DatabaseService] 监测已启动，间隔 " + intervalMs + "ms，OpcServices由外部传入");
            return Task.CompletedTask;
        }

        /// <summary>停止 OPC 实时监测</summary>
        public Task StopMonitoringAsync()
        {
            _timer?.Dispose();
            _timer = null;
            Debug.WriteLine("[Core.DatabaseService] 监测已停止");
            return Task.CompletedTask;
        }

        /// <summary>查询 INFORMATION_SCHEMA.COLUMNS 获取指定表的实际字段名称集合（不区分大小写）</summary>
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
                columns.Add(reader.GetString(0));
            }
            return columns;
        }

        private static string GetSqlType(string type)
        {
            return type.ToLowerInvariant() switch
            {
                "float" or "double" => "FLOAT",
                "int" or "int32" or "short" or "word" => "INT",
                "bool" or "boolean" => "BIT",
                "string" => "NVARCHAR(MAX)",
                _ => "NVARCHAR(MAX)"
            };
        }

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

        /// <summary>根据 ConnectData 配置自动创建 connectData 表并补齐缺失列（启动时/配置变更时/循环中调用）</summary>
        public void EnsureConnectDataTable(List<ConnectData> datas)
        {
            if (datas == null || !datas.Any()) return;

            try
            {
                using var conn = new SqlConnection(DatabaseConfig);
                conn.Open();

                var tableName = "ConnectData";

                var createTableSql = @"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '" + tableName + @"')
                    BEGIN
                        CREATE TABLE " + tableName + @" (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            Datetime DATETIME
                        )
                    END
                    ELSE
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '" + tableName + @"' AND COLUMN_NAME = 'Datetime')
                        BEGIN
                            ALTER TABLE " + tableName + " ADD [Datetime] DATETIME NULL\n                        END\n                    END";

                using var createCmd = conn.CreateCommand();
                createCmd.CommandText = createTableSql;
                createCmd.ExecuteNonQuery();

                EnsureSystemMonitorColumns(conn, tableName);

                var columnNames = datas.Select(d => d.Name).ToList();
                var existingColumns = GetExistingColumns(conn, tableName);
                var missingColumns = columnNames.Except(existingColumns, StringComparer.OrdinalIgnoreCase).ToList();

                foreach (var col in missingColumns)
                {
                    var dataItem = datas.First(d => d.Name == col);
                    var sqlType = GetSqlType(dataItem.Type);
                    var alterSql = "ALTER TABLE " + tableName + " ADD [" + col + "] " + sqlType + " NULL";
                    using var alterCmd = conn.CreateCommand();
                    alterCmd.CommandText = alterSql;
                    alterCmd.ExecuteNonQuery();
                    Debug.WriteLine("[EnsureConnectDataTable] 已添加列: " + col + ", 类型: " + sqlType);
                }

                Debug.WriteLine("[Core.EnsureConnectDataTable] 表结构已确保");
                DatabaseRecovered?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Core.EnsureConnectDataTable] 失败: " + ex.Message);
                ReportDbError(ex);
                throw;
            }
        }

        /// <summary>确保 ConnectData 表中存在系统监控列（MemoryMB, DiskFreeGB, RunningMemoryMB）</summary>
        private void EnsureSystemMonitorColumns(SqlConnection conn, string tableName)
        {
            var systemColumns = new Dictionary<string, string>
            {
                { "MemoryMB", "FLOAT" },
                { "DiskFreeGB", "FLOAT" },
                { "RunningMemoryMB", "FLOAT" }
            };

            var existingColumns = GetExistingColumns(conn, tableName);

            foreach (var kvp in systemColumns)
            {
                if (!existingColumns.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
                {
                    var alterSql = $"ALTER TABLE [{tableName}] ADD [{kvp.Key}] {kvp.Value} NULL";
                    using var alterCmd = conn.CreateCommand();
                    alterCmd.CommandText = alterSql;
                    alterCmd.ExecuteNonQuery();
                    Debug.WriteLine($"[EnsureSystemMonitorColumns] 已添加列: {kvp.Key}, 类型: {kvp.Value}");
                }
            }
        }

        /// <summary>保存 ConnectData 值快照到 connectData 表（仅插入数据，不再负责建表扩列）</summary>
        public void SaveConnectData(List<ConnectData> datas)
        {
            if (datas == null || !datas.Any()) return;

            try
            {
                using var conn = new SqlConnection(DatabaseConfig);
                conn.Open();

                var tableName = "ConnectData";
                var columnNames = datas.Select(d => d.Name).ToList();
                var paramNames = datas.Select(d => "@" + d.Name).ToList();

                var memoryMB = Process.GetCurrentProcess().WorkingSet64 / 1024.0 / 1024.0;
                var runningMemoryMB = Process.GetCurrentProcess().PrivateMemorySize64 / 1024.0 / 1024.0;
                var appRoot = Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory);
                var driveInfo = new DriveInfo(appRoot);
                var diskFreeGB = driveInfo.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;

                var insertSql = "INSERT INTO " + tableName + " (Datetime, " + string.Join(",", columnNames) + ", MemoryMB, DiskFreeGB, RunningMemoryMB) VALUES (GETDATE(), " + string.Join(",", paramNames) + ", @MemoryMB, @DiskFreeGB, @RunningMemoryMB)";

                using var insertCmd = conn.CreateCommand();
                insertCmd.CommandText = insertSql;

                foreach (var item in datas)
                {
                    var dbValue = ConvertToDbValue(item.Value, item.Type);
                    insertCmd.Parameters.AddWithValue("@" + item.Name, dbValue);
                }

                insertCmd.Parameters.AddWithValue("@MemoryMB", Math.Round(memoryMB, 2));
                insertCmd.Parameters.AddWithValue("@DiskFreeGB", Math.Round(diskFreeGB, 2));
                insertCmd.Parameters.AddWithValue("@RunningMemoryMB", Math.Round(runningMemoryMB, 2));

                insertCmd.ExecuteNonQuery();
                Debug.WriteLine($"[Core.SaveConnectData] 数据已保存到数据库 (MemoryMB={Math.Round(memoryMB, 2)}, DiskFreeGB={Math.Round(diskFreeGB, 2)}, RunningMemoryMB={Math.Round(runningMemoryMB, 2)})");
                DatabaseRecovered?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Core.SaveConnectData] 保存失败: " + ex.Message);
                ReportDbError(ex);
            }
        }

        /// <summary>
        /// 通用查询：按表名、列名、时间范围获取数据
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <param name="columnNames">要查询的列名集合，null 或空则自动获取所有列（不含 Id）</param>
        /// <param name="startTime">开始时间，null 不限</param>
        /// <param name="endTime">结束时间，null 不限</param>
        /// <returns>数据行列表，每行为 Dictionary{列名, 值}</returns>
        public async Task<List<Dictionary<string, object>>> QueryDataAsync(
            string tableName,
            IEnumerable<string> columnNames = null,
            DateTime? startTime = null,
            DateTime? endTime = null)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("表名不能为空", nameof(tableName));

            if (string.IsNullOrWhiteSpace(DatabaseConfig))
                throw new ArgumentNullException(nameof(DatabaseConfig), "数据库连接字符串不能为空");

            var columns = columnNames?.ToList();
            var result = new List<Dictionary<string, object>>();

            try
            {
                await using var conn = new SqlConnection(DatabaseConfig);
                await conn.OpenAsync();

                if (columns == null || columns.Count == 0)
                {
                    columns = GetExistingColumns(conn, tableName)
                        .Except(new[] { "Id" }, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }

                if (columns.Count == 0)
                {
                    Debug.WriteLine("[QueryDataAsync] 没有可用列");
                    return result;
                }

                bool hasDatetime = columns.Any(c => c.Equals("Datetime", StringComparison.OrdinalIgnoreCase));
                var dataColumns = columns.Where(c => !c.Equals("Datetime", StringComparison.OrdinalIgnoreCase)).ToList();
                var safeColumns = dataColumns.Select(c => "[" + c + "]");
                var selectClause = hasDatetime
                    ? "[Datetime], " + string.Join(", ", safeColumns)
                    : string.Join(", ", safeColumns);

                var whereClauses = new List<string>();
                var parameters = new List<(string name, object value)>();

                if (hasDatetime)
                {
                    if (startTime.HasValue)
                    {
                        whereClauses.Add("[Datetime] >= @startTime");
                        parameters.Add(("@startTime", startTime.Value));
                    }

                    if (endTime.HasValue)
                    {
                        whereClauses.Add("[Datetime] <= @endTime");
                        parameters.Add(("@endTime", endTime.Value));
                    }
                }

                var whereClause = whereClauses.Count > 0
                    ? " WHERE " + string.Join(" AND ", whereClauses)
                    : string.Empty;

                var orderClause = hasDatetime ? " ORDER BY [Datetime] ASC" : "";
                var sql = "SELECT " + selectClause + " FROM [" + tableName + "]" + whereClause + orderClause;

                Debug.WriteLine("[QueryDataAsync] SQL: " + sql);

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;

                foreach (var (name, value) in parameters)
                    cmd.Parameters.AddWithValue(name, value);

                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    int colOffset = 0;

                    if (hasDatetime)
                    {
                        row["Datetime"] = reader.IsDBNull(0) ? DBNull.Value : reader.GetDateTime(0);
                        colOffset = 1;
                    }

                    for (int i = 0; i < dataColumns.Count; i++)
                    {
                        var colName = dataColumns[i];
                        var colIndex = i + colOffset;
                        row[colName] = reader.IsDBNull(colIndex) ? DBNull.Value : reader.GetValue(colIndex);
                    }

                    result.Add(row);
                }

                Debug.WriteLine("[QueryDataAsync] 查询完成，返回 " + result.Count + " 行");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[QueryDataAsync] 查询失败: " + ex.Message);
                ReportDbError(ex);
                throw;
            }

            return result;
        }

        public async Task UpdateConnectDataAsync(ConnectData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.Name))
                throw new ArgumentException("数据或名称不能为空");

            Debug.WriteLine("[Core.UpdateConnectDataAsync] 开始更新: " + data.Name);
            Debug.WriteLine("  Type: " + data.Type);
            Debug.WriteLine("  OpcAddress: " + data.OpcAddress);
            Debug.WriteLine("  MockOpcAddress: " + data.MockOpcAddress);
            Debug.WriteLine("  Description: " + data.Description);

            try
            {
                await using var conn = new SqlConnection(DatabaseConfig);
                await conn.OpenAsync();
                Debug.WriteLine("[Core.UpdateConnectDataAsync] 数据库连接成功");

                var sql = @"UPDATE ConnectConfig 
                            SET type = @type, 
                                opcAddress = @opcAddress, 
                                mock_opcAddress = @mockOpcAddress, 
                                description = @description 
                            WHERE name = @name";

                Debug.WriteLine("[UpdateConnectDataAsync] SQL: " + sql);

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@name", data.Name);
                Debug.WriteLine("  @name = " + data.Name);
                cmd.Parameters.AddWithValue("@type", data.Type ?? (object)DBNull.Value);
                Debug.WriteLine("  @type = " + (data.Type ?? "NULL"));

                var opcAddress = data.OpcAddress ?? string.Empty;
                if (opcAddress.StartsWith("ns=2;s="))
                    opcAddress = opcAddress.Substring(8);
                cmd.Parameters.AddWithValue("@opcAddress", opcAddress);
                Debug.WriteLine("  @opcAddress = " + opcAddress);

                var mockOpcAddress = data.MockOpcAddress ?? string.Empty;
                if (mockOpcAddress.StartsWith("ns=2;s="))
                    mockOpcAddress = mockOpcAddress.Substring(8);
                cmd.Parameters.AddWithValue("@mockOpcAddress", mockOpcAddress);
                Debug.WriteLine("  @mockOpcAddress = " + mockOpcAddress);

                cmd.Parameters.AddWithValue("@description", data.Description ?? (object)DBNull.Value);
                Debug.WriteLine("  @description = " + (data.Description ?? "NULL"));

                var rows = await cmd.ExecuteNonQueryAsync();
                Debug.WriteLine("[UpdateConnectDataAsync] 更新 " + data.Name + ", 影响行数: " + rows);

                if (rows == 0)
                {
                    Debug.WriteLine("[Core.UpdateConnectDataAsync] 警告: 没有行被更新，可能name不存在");
                }
                else
                {
                    Debug.WriteLine("[Core.UpdateConnectDataAsync] 触发配置更新事件");
                    ConnectConfigUpdated?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Core.UpdateConnectDataAsync] 更新失败: " + ex.Message);
                Debug.WriteLine("[Core.UpdateConnectDataAsync] 堆栈: " + ex.StackTrace);
                ReportDbError(ex);
                throw;
            }
        }

        public async Task InsertConnectDataAsync(ConnectData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            Debug.WriteLine("[Core.InsertConnectDataAsync] 开始新增: " + (data.Name ?? "null"));

            try
            {
                await using var conn = new SqlConnection(DatabaseConfig);
                await conn.OpenAsync();

                var sql = @"INSERT INTO ConnectConfig (name, type, opcAddress, mock_opcAddress, description)
                            VALUES (@name, @type, @opcAddress, @mockOpcAddress, @description)";

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@name", data.Name ?? (object)DBNull.Value);

                cmd.Parameters.AddWithValue("@type", data.Type ?? (object)DBNull.Value);

                var opcAddress = data.OpcAddress ?? string.Empty;
                if (opcAddress.StartsWith("ns=2;s="))
                    opcAddress = opcAddress.Substring(8);
                cmd.Parameters.AddWithValue("@opcAddress", opcAddress);

                var mockOpcAddress = data.MockOpcAddress ?? string.Empty;
                if (mockOpcAddress.StartsWith("ns=2;s="))
                    mockOpcAddress = mockOpcAddress.Substring(8);
                cmd.Parameters.AddWithValue("@mockOpcAddress", mockOpcAddress);

                cmd.Parameters.AddWithValue("@description", data.Description ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
                Debug.WriteLine("[Core.InsertConnectDataAsync] 新增成功: " + data.Name);

                ConnectConfigUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Core.InsertConnectDataAsync] 新增失败: " + ex.Message);
                Debug.WriteLine("[Core.InsertConnectDataAsync] 堆栈: " + ex.StackTrace);
                ReportDbError(ex);
                throw;
            }
        }

        public async Task DeleteConnectDataAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("名称不能为空");

            Debug.WriteLine("[Core.DeleteConnectDataAsync] 开始删除: " + name);

            try
            {
                await using var conn = new SqlConnection(DatabaseConfig);
                await conn.OpenAsync();

                var sql = @"DELETE FROM ConnectConfig WHERE name = @name";

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@name", name);

                var rows = await cmd.ExecuteNonQueryAsync();
                Debug.WriteLine("[Core.DeleteConnectDataAsync] 删除 " + name + ", 影响行数: " + rows);

                ConnectConfigUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Core.DeleteConnectDataAsync] 删除失败: " + ex.Message);
                Debug.WriteLine("[Core.DeleteConnectDataAsync] 堆栈: " + ex.StackTrace);
                ReportDbError(ex);
                throw;
            }
        }

        private static readonly HashSet<string> ValidFilterTables = new() { "BConfig", "AConfig" };

        private static void ValidateFilterTable(string tableName)
        {
            if (!ValidFilterTables.Contains(tableName))
                throw new ArgumentException($"不支持的表名: {tableName}");
        }

        public async Task<List<FilterConfigItem>> GetFilterConfigsAsync(string tableName)
        {
            ValidateFilterTable(tableName);

            Debug.WriteLine($"[Core.GetFilterConfigsAsync] 从 {tableName} 读取配置...");
            var result = new List<FilterConfigItem>();

            try
            {
                await using var conn = new SqlConnection(DatabaseConfig);
                await conn.OpenAsync();

                var sql = $"SELECT name, connectName, IsStore, IsFilter, FilterLength FROM {tableName}";
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.Add(new FilterConfigItem
                    {
                        Name = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                        ConnectName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        IsStore = reader.IsDBNull(2) ? false : reader.GetBoolean(2),
                        IsFilter = reader.IsDBNull(3) ? false : reader.GetBoolean(3),
                        FilterLength = reader.IsDBNull(4) ? null : reader.GetInt32(4)
                    });
                }

                Debug.WriteLine($"[Core.GetFilterConfigsAsync] 读取了 {result.Count} 条");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Core.GetFilterConfigsAsync] 读取失败: {ex.Message}");
                ReportDbError(ex);
                throw;
            }

            return result;
        }

        public async Task InsertFilterConfigAsync(string tableName, FilterConfigItem item)
        {
            ValidateFilterTable(tableName);
            if (item == null) throw new ArgumentNullException(nameof(item));

            Debug.WriteLine($"[Core.InsertFilterConfigAsync] 新增到 {tableName}: {item.Name}");

            try
            {
                await using var conn = new SqlConnection(DatabaseConfig);
                await conn.OpenAsync();

                var sql = $"INSERT INTO {tableName} (name, connectName, IsStore, IsFilter, FilterLength) VALUES (@name, @connectName, @isStore, @isFilter, @filterLength)";
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@name", item.Name ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@connectName", item.ConnectName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@isStore", item.IsStore);
                cmd.Parameters.AddWithValue("@isFilter", item.IsFilter);
                cmd.Parameters.AddWithValue("@filterLength", item.FilterLength ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
                Debug.WriteLine($"[Core.InsertFilterConfigAsync] 新增成功: {item.Name}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Core.InsertFilterConfigAsync] 新增失败: {ex.Message}");
                ReportDbError(ex);
                throw;
            }
        }

        public async Task UpdateFilterConfigAsync(string tableName, FilterConfigItem item)
        {
            ValidateFilterTable(tableName);
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (string.IsNullOrWhiteSpace(item.Name)) throw new ArgumentException("名称不能为空");

            Debug.WriteLine($"[Core.UpdateFilterConfigAsync] 更新 {tableName}: {item.Name}");

            try
            {
                await using var conn = new SqlConnection(DatabaseConfig);
                await conn.OpenAsync();

                var sql = $"UPDATE {tableName} SET connectName = @connectName, IsStore = @isStore, IsFilter = @isFilter, FilterLength = @filterLength WHERE name = @name";
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@name", item.Name);
                cmd.Parameters.AddWithValue("@connectName", item.ConnectName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@isStore", item.IsStore);
                cmd.Parameters.AddWithValue("@isFilter", item.IsFilter);
                cmd.Parameters.AddWithValue("@filterLength", item.FilterLength ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
                Debug.WriteLine($"[Core.UpdateFilterConfigAsync] 更新成功: {item.Name}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Core.UpdateFilterConfigAsync] 更新失败: {ex.Message}");
                ReportDbError(ex);
                throw;
            }
        }

        public async Task DeleteFilterConfigAsync(string tableName, string name)
        {
            ValidateFilterTable(tableName);
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("名称不能为空");

            Debug.WriteLine($"[Core.DeleteFilterConfigAsync] 从 {tableName} 删除: {name}");

            try
            {
                await using var conn = new SqlConnection(DatabaseConfig);
                await conn.OpenAsync();

                var sql = $"DELETE FROM {tableName} WHERE name = @name";
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@name", name);

                await cmd.ExecuteNonQueryAsync();
                Debug.WriteLine($"[Core.DeleteFilterConfigAsync] 删除成功: {name}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Core.DeleteFilterConfigAsync] 删除失败: {ex.Message}");
                ReportDbError(ex);
                throw;
            }
        }

        /// <summary>
        /// 获取历史报警记录，优先取未确认的最新30条，不足30条由已确认补齐
        /// </summary>
        public async Task<List<AlarmRecord>> GetAlarmHistoryAsync()
        {
            if (string.IsNullOrWhiteSpace(DatabaseConfig))
                throw new ArgumentNullException(nameof(DatabaseConfig), "数据库连接字符串不能为空");

            var result = new List<AlarmRecord>();

            try
            {
                await using var conn = new SqlConnection(DatabaseConfig);
                await conn.OpenAsync();

                var columns = GetExistingColumns(conn, "AlarmHistory");

                string SafeCol(string name) =>
                    columns.Contains(name) ? $"[{name}]" : $"NULL AS [{name}]";

                string isCheckedWhere0 = columns.Contains("isChecked") ? "[isChecked] = 0" : "1=0";
                string isCheckedWhere1 = columns.Contains("isChecked") ? "[isChecked] = 1" : "1=0";
                string orderCol = columns.Contains("alarmTime") ? "[alarmTime]" : "(SELECT 1)";

                var sql = $@"SELECT {SafeCol("id")}, {SafeCol("name")}, {SafeCol("alarmTime")}, {SafeCol("config_id")}, {SafeCol("value")}, {SafeCol("message")}, 
                                   {SafeCol("isChecked")}, {SafeCol("isShowed")}, {SafeCol("confirmTime")}, {SafeCol("config_type")}, 0 AS sortGroup 
                            FROM (SELECT TOP 30 * FROM [AlarmHistory] WHERE {isCheckedWhere0} 
                                  ORDER BY {orderCol} DESC) t0
                            UNION ALL
                            SELECT {SafeCol("id")}, {SafeCol("name")}, {SafeCol("alarmTime")}, {SafeCol("config_id")}, {SafeCol("value")}, {SafeCol("message")}, 
                                   {SafeCol("isChecked")}, {SafeCol("isShowed")}, {SafeCol("confirmTime")}, {SafeCol("config_type")}, 1 AS sortGroup 
                            FROM (SELECT TOP 30 * FROM [AlarmHistory] WHERE {isCheckedWhere1} 
                                  ORDER BY {orderCol} DESC) t1
                            ORDER BY sortGroup ASC, {orderCol} DESC";

                Debug.WriteLine("[GetAlarmHistoryAsync] SQL: " + sql);

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                await using var reader = await cmd.ExecuteReaderAsync();

                string GetString(string col) { var v = reader[col]; return v == DBNull.Value ? string.Empty : v?.ToString() ?? string.Empty; }
                DateTime GetDateTime(string col) { var v = reader[col]; return v == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(v); }
                int GetInt(string col) { var v = reader[col]; return v == DBNull.Value ? 0 : Convert.ToInt32(v); }
                bool GetBool(string col) { var v = reader[col]; return v != DBNull.Value && Convert.ToBoolean(v); }
                DateTime? GetNullableDateTime(string col) { var v = reader[col]; return v == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(v); }

                var allRecords = new List<AlarmRecord>();
                while (await reader.ReadAsync())
                {
                    allRecords.Add(new AlarmRecord
                    {
                        Id = GetInt("id"),
                        DeviceName = GetString("name"),
                        AlarmTime = GetDateTime("alarmTime"),
                        ConfigId = GetInt("config_id"),
                        AlarmContent = GetString("message"),
                        IsChecked = GetBool("isChecked"),
                        IsShowed = GetBool("isShowed"),
                        ConfirmTime = GetNullableDateTime("confirmTime"),
                        ConfigType = GetString("config_type"),
                    });
                }

                var uncheckedList = allRecords.Where(r => !r.IsChecked).Take(30).ToList();
                if (uncheckedList.Count < 30)
                {
                    var fillCount = 30 - uncheckedList.Count;
                    var checkedList = allRecords.Where(r => r.IsChecked).Take(fillCount).ToList();
                    uncheckedList.AddRange(checkedList);
                }

                result = uncheckedList.OrderByDescending(r => r.AlarmTime).ToList();

                Debug.WriteLine($"[GetAlarmHistoryAsync] 返回 {result.Count} 条，未确认 {result.Count(r => !r.IsChecked)} 条，已确认 {result.Count(r => r.IsChecked)} 条");
                DatabaseRecovered?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[GetAlarmHistoryAsync] 查询失败: " + ex.Message);
                ReportDbError(ex);
            }

            return result;
        }

        /// <summary>插入一条报警历史记录并返回自增 id</summary>
        public async Task<int> InsertAlarmHistoryAsync(AlarmRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));

            try
            {
                await using var conn = new SqlConnection(DatabaseConfig);
                await conn.OpenAsync();

                var columns = GetExistingColumns(conn, "AlarmHistory");

                var fieldMappings = new (string column, object value)[]
                {
                    ("name",        record.DeviceName ?? (object)DBNull.Value),
                    ("alarmTime",   (object)record.AlarmTime),
                    ("config_id",   (object)record.ConfigId),
                    ("value",       record.Value ?? (object)DBNull.Value),
                    ("message",     record.AlarmContent ?? (object)DBNull.Value),
                    ("isChecked",   (object)record.IsChecked),
                    ("isShowed",    (object)record.IsShowed),
                    ("confirmTime", record.ConfirmTime ?? (object)DBNull.Value),
                };

                var existing = fieldMappings.Where(f => columns.Contains(f.column)).ToList();

                if (existing.Count == 0)
                    return 0;

                var colNames = existing.Select(f => $"[{f.column}]");
                var paramNames = existing.Select(f => $"@{f.column}");

                var sql = $"INSERT INTO [AlarmHistory] ({string.Join(",", colNames)}) VALUES ({string.Join(",", paramNames)}); SELECT CAST(SCOPE_IDENTITY() AS INT);";

                Debug.WriteLine("[Core.InsertAlarmHistoryAsync] SQL: " + sql);

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;

                foreach (var (column, value) in existing)
                    cmd.Parameters.AddWithValue($"@{column}", value);

                var newId = (int)await cmd.ExecuteScalarAsync();
                Debug.WriteLine("[Core.InsertAlarmHistoryAsync] 新增报警记录 id=" + newId);
                AlarmInserted?.Invoke();
                return newId;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Core.InsertAlarmHistoryAsync] 插入失败: " + ex.Message);
                ReportDbError(ex);
                throw;
            }
        }

        /// <summary>更新报警配置的 trigger 字段</summary>
        public async Task UpdateAlarmConfigTriggerAsync(int configId, string triggerColumn, bool value)
        {
            try
            {
                await using var conn = new SqlConnection(DatabaseConfig);
                await conn.OpenAsync();

                var sql = $"UPDATE [AlarmConfig] SET [{triggerColumn}]=@val WHERE [id]=@id";
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@val", value);
                cmd.Parameters.AddWithValue("@id", configId);

                await cmd.ExecuteNonQueryAsync();
                Debug.WriteLine($"[Core.UpdateAlarmConfigTriggerAsync] configId={configId}, {triggerColumn}={value}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Core.UpdateAlarmConfigTriggerAsync] 更新失败: " + ex.Message);
                ReportDbError(ex);
            }
        }

        /// <summary>更新报警历史记录的确认时间和状态</summary>
        public async Task UpdateAlarmHistoryConfirmAsync(int recordId, DateTime confirmTime)
        {
            try
            {
                await using var conn = new SqlConnection(DatabaseConfig);
                await conn.OpenAsync();

                var sql = "UPDATE [AlarmHistory] SET [confirmTime]=@time, [isChecked]=1 WHERE [id]=@id";
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@time", confirmTime);
                cmd.Parameters.AddWithValue("@id", recordId);

                await cmd.ExecuteNonQueryAsync();
                Debug.WriteLine($"[Core.UpdateAlarmHistoryConfirmAsync] recordId={recordId}, confirmTime={confirmTime:yyyy-MM-dd HH:mm:ss}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Core.UpdateAlarmHistoryConfirmAsync] 更新失败: " + ex.Message);
                ReportDbError(ex);
            }
        }

        /// <summary>如果报警记录未确认，则更新确认时间和状态</summary>
        public async Task ConfirmAlarmIfNeededAsync(int recordId, DateTime confirmTime)
        {
            try
            {
                await using var conn = new SqlConnection(DatabaseConfig);
                await conn.OpenAsync();

                var sql = "UPDATE [AlarmHistory] SET [isChecked]=1, [confirmTime]=COALESCE([confirmTime], @time) WHERE [id]=@id AND [isChecked]=0";
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@time", confirmTime);
                cmd.Parameters.AddWithValue("@id", recordId);

                var affected = await cmd.ExecuteNonQueryAsync();
                if (affected > 0)
                    Debug.WriteLine($"[Core.ConfirmAlarmIfNeededAsync] 已确认 recordId={recordId}, confirmTime={confirmTime:yyyy-MM-dd HH:mm:ss}");
                else
                    Debug.WriteLine($"[Core.ConfirmAlarmIfNeededAsync] recordId={recordId} 已存在确认时间，跳过更新");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Core.ConfirmAlarmIfNeededAsync] 更新失败: " + ex.Message);
                ReportDbError(ex);
            }
        }

        /// <summary>获取所有报警配置</summary>
        public async Task<List<AlarmConfig>> GetAlarmConfigsAsync()
        {
            if (string.IsNullOrWhiteSpace(DatabaseConfig))
                throw new ArgumentNullException(nameof(DatabaseConfig), "数据库连接字符串不能为空");

            var result = new List<AlarmConfig>();

            try
            {
                await using var conn = new SqlConnection(DatabaseConfig);
                await conn.OpenAsync();

                var columns = GetExistingColumns(conn, "AlarmConfig");

                string SafeCol(string name) =>
                    columns.Contains(name) ? $"[{name}]" : $"NULL AS [{name}]";

                string orderCol = columns.Contains("id") ? "[id]" : "(SELECT 1)";

                var sql = $@"SELECT {SafeCol("id")},{SafeCol("connectname")},{SafeCol("name")},{SafeCol("unit")},
                                   {SafeCol("upper_limit")},{SafeCol("upper_limit_message")},{SafeCol("upper_allowCount")},{SafeCol("upper_limit_trigger")},
                                   {SafeCol("lower_limit")},{SafeCol("lower_limit_message")},{SafeCol("lower_allowCount")},{SafeCol("lower_limit_trigger")},
                                   {SafeCol("mutation_threshold")},{SafeCol("mutation_threshold_message")},{SafeCol("mutation_allowCount")},{SafeCol("mutation_threshold_trigger")},
                                   {SafeCol("unchanged_threshold_message")},{SafeCol("unchanged_allowCount")},{SafeCol("unchanged_threshold_trigger")},
                                   {SafeCol("alarm_level")},{SafeCol("is_enabled")},{SafeCol("update_time")}
                            FROM [AlarmConfig] ORDER BY {orderCol}";

                Debug.WriteLine("[Core.GetAlarmConfigsAsync] SQL: " + sql);

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.Add(ReadAlarmConfig(reader, columns));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Core.GetAlarmConfigsAsync] 查询失败: " + ex.Message);
                ReportDbError(ex);
                throw;
            }

            return result;
        }

        /// <summary>新增报警配置并返回自增 id</summary>
        public async Task<int> InsertAlarmConfigAsync(AlarmConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            try
            {
                await using var conn = new SqlConnection(DatabaseConfig);
                await conn.OpenAsync();

                var columns = GetExistingColumns(conn, "AlarmConfig");

                var expectedCols = new[]
                {
                    "connectname","name","unit",
                    "upper_limit","upper_limit_message","upper_allowCount","upper_limit_trigger",
                    "lower_limit","lower_limit_message","lower_allowCount","lower_limit_trigger",
                    "mutation_threshold","mutation_threshold_message","mutation_allowCount","mutation_threshold_trigger",
                    "unchanged_threshold_message","unchanged_allowCount","unchanged_threshold_trigger",
                    "alarm_level","is_enabled","update_time"
                };

                var existing = expectedCols.Where(c => columns.Contains(c)).ToList();

                if (existing.Count == 0)
                    return 0;

                var colNames = existing.Select(c => $"[{c}]");
                var paramNames = existing.Select(c => $"@{c}");

                var sql = $"INSERT INTO [AlarmConfig] ({string.Join(",", colNames)}) VALUES ({string.Join(",", paramNames)}); SELECT CAST(SCOPE_IDENTITY() AS INT);";

                Debug.WriteLine("[Core.InsertAlarmConfigAsync] SQL: " + sql);

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                AddAlarmConfigParams(cmd, config, columns);
                var newId = (int)await cmd.ExecuteScalarAsync();

                Debug.WriteLine("[Core.InsertAlarmConfigAsync] 新增配置 id=" + newId);
                return newId;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Core.InsertAlarmConfigAsync] 新增失败: " + ex.Message);
                ReportDbError(ex);
                throw;
            }
        }

        /// <summary>更新单条报警配置</summary>
        public async Task UpdateAlarmConfigAsync(AlarmConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            try
            {
                await using var conn = new SqlConnection(DatabaseConfig);
                await conn.OpenAsync();

                var columns = GetExistingColumns(conn, "AlarmConfig");

                var expectedCols = new[]
                {
                    "connectname","name","unit",
                    "upper_limit","upper_limit_message","upper_allowCount","upper_limit_trigger",
                    "lower_limit","lower_limit_message","lower_allowCount","lower_limit_trigger",
                    "mutation_threshold","mutation_threshold_message","mutation_allowCount","mutation_threshold_trigger",
                    "unchanged_threshold_message","unchanged_allowCount","unchanged_threshold_trigger",
                    "alarm_level","is_enabled","update_time"
                };

                var existing = expectedCols.Where(c => columns.Contains(c)).ToList();

                var setClauses = existing.Select(c => $"[{c}]=@{c}");
                var sql = existing.Count > 0
                    ? $"UPDATE [AlarmConfig] SET {string.Join(",", setClauses)} WHERE [id]=@id"
                    : "SELECT 1 WHERE 1=0";

                Debug.WriteLine("[Core.UpdateAlarmConfigAsync] SQL: " + sql);

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@id", config.Id);
                AddAlarmConfigParams(cmd, config, columns);

                var rows = await cmd.ExecuteNonQueryAsync();
                Debug.WriteLine("[Core.UpdateAlarmConfigAsync] 更新 id=" + config.Id + ", 影响行数: " + rows);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Core.UpdateAlarmConfigAsync] 更新失败: " + ex.Message);
                ReportDbError(ex);
                throw;
            }
        }

        /// <summary>按 ID 删除单条报警配置</summary>
        public async Task DeleteAlarmConfigAsync(int id)
        {
            try
            {
                await using var conn = new SqlConnection(DatabaseConfig);
                await conn.OpenAsync();

                var sql = "DELETE FROM [AlarmConfig] WHERE [id]=@id";
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@id", id);

                var rows = await cmd.ExecuteNonQueryAsync();
                Debug.WriteLine("[Core.DeleteAlarmConfigAsync] 删除 id=" + id + ", 影响行数: " + rows);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Core.DeleteAlarmConfigAsync] 删除失败: " + ex.Message);
                ReportDbError(ex);
                throw;
            }
        }

        private static AlarmConfig ReadAlarmConfig(SqlDataReader reader, HashSet<string> columns)
        {
            string ColStr(string n) { var v = reader[n]; return v == DBNull.Value ? null : v.ToString(); }
            int ColInt(string n, int def = 0) { var v = reader[n]; return v == DBNull.Value ? def : Convert.ToInt32(v); }
            decimal? ColDec(string n) { var v = reader[n]; return v == DBNull.Value ? null : (decimal?)reader.GetDecimal(reader.GetOrdinal(n)); }
            bool? ColBool(string n) { var v = reader[n]; return v == DBNull.Value ? null : (bool?)reader.GetBoolean(reader.GetOrdinal(n)); }

            return new AlarmConfig
            {
                Id = ColInt("id"),
                ConnectName = ColStr("connectname") ?? string.Empty,
                Name = ColStr("name"),
                Unit = ColStr("unit"),
                UpperLimit = ColDec("upper_limit"),
                UpperLimitMessage = ColStr("upper_limit_message"),
                UpperAllowCount = ColDec("upper_allowCount"),
                UpperLimitTrigger = ColBool("upper_limit_trigger"),
                LowerLimit = ColDec("lower_limit"),
                LowerLimitMessage = ColStr("lower_limit_message"),
                LowerAllowCount = ColDec("lower_allowCount"),
                LowerLimitTrigger = ColBool("lower_limit_trigger"),
                MutationThreshold = ColDec("mutation_threshold"),
                MutationThresholdMessage = ColStr("mutation_threshold_message"),
                MutationAllowCount = ColDec("mutation_allowCount"),
                MutationThresholdTrigger = ColBool("mutation_threshold_trigger"),
                UnchangedThresholdMessage = ColStr("unchanged_threshold_message"),
                UnchangedAllowCount = ColDec("unchanged_allowCount"),
                UnchangedThresholdTrigger = ColBool("unchanged_threshold_trigger"),
                AlarmLevel = ColInt("alarm_level"),
                IsEnabled = ColInt("is_enabled"),
                UpdateTime = reader["update_time"] == DBNull.Value ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("update_time")),
            };
        }

        /// <summary>根据表中实际存在的列绑定报警配置参数（缺失列自动跳过）</summary>
        private static void AddAlarmConfigParams(SqlCommand cmd, AlarmConfig config, HashSet<string> columns)
        {
            if (columns.Contains("connectname"))
                cmd.Parameters.AddWithValue("@connectname", config.ConnectName ?? (object)DBNull.Value);
            if (columns.Contains("name"))
                cmd.Parameters.AddWithValue("@name", config.Name ?? (object)DBNull.Value);
            if (columns.Contains("unit"))
                cmd.Parameters.AddWithValue("@unit", config.Unit ?? (object)DBNull.Value);
            if (columns.Contains("upper_limit"))
                cmd.Parameters.AddWithValue("@upper_limit", config.UpperLimit ?? (object)DBNull.Value);
            if (columns.Contains("upper_limit_message"))
                cmd.Parameters.AddWithValue("@upper_limit_message", config.UpperLimitMessage ?? (object)DBNull.Value);
            if (columns.Contains("upper_allowCount"))
                cmd.Parameters.AddWithValue("@upper_allowCount", config.UpperAllowCount ?? (object)DBNull.Value);
            if (columns.Contains("upper_limit_trigger"))
                cmd.Parameters.AddWithValue("@upper_limit_trigger", config.UpperLimitTrigger ?? (object)DBNull.Value);
            if (columns.Contains("lower_limit"))
                cmd.Parameters.AddWithValue("@lower_limit", config.LowerLimit ?? (object)DBNull.Value);
            if (columns.Contains("lower_limit_message"))
                cmd.Parameters.AddWithValue("@lower_limit_message", config.LowerLimitMessage ?? (object)DBNull.Value);
            if (columns.Contains("lower_allowCount"))
                cmd.Parameters.AddWithValue("@lower_allowCount", config.LowerAllowCount ?? (object)DBNull.Value);
            if (columns.Contains("lower_limit_trigger"))
                cmd.Parameters.AddWithValue("@lower_limit_trigger", config.LowerLimitTrigger ?? (object)DBNull.Value);
            if (columns.Contains("mutation_threshold"))
                cmd.Parameters.AddWithValue("@mutation_threshold", config.MutationThreshold ?? (object)DBNull.Value);
            if (columns.Contains("mutation_threshold_message"))
                cmd.Parameters.AddWithValue("@mutation_threshold_message", config.MutationThresholdMessage ?? (object)DBNull.Value);
            if (columns.Contains("mutation_allowCount"))
                cmd.Parameters.AddWithValue("@mutation_allowCount", config.MutationAllowCount ?? (object)DBNull.Value);
            if (columns.Contains("mutation_threshold_trigger"))
                cmd.Parameters.AddWithValue("@mutation_threshold_trigger", config.MutationThresholdTrigger ?? (object)DBNull.Value);
            if (columns.Contains("unchanged_threshold_message"))
                cmd.Parameters.AddWithValue("@unchanged_threshold_message", config.UnchangedThresholdMessage ?? (object)DBNull.Value);
            if (columns.Contains("unchanged_allowCount"))
                cmd.Parameters.AddWithValue("@unchanged_allowCount", config.UnchangedAllowCount ?? (object)DBNull.Value);
            if (columns.Contains("unchanged_threshold_trigger"))
                cmd.Parameters.AddWithValue("@unchanged_threshold_trigger", config.UnchangedThresholdTrigger ?? (object)DBNull.Value);
            if (columns.Contains("alarm_level"))
                cmd.Parameters.AddWithValue("@alarm_level", config.AlarmLevel);
            if (columns.Contains("is_enabled"))
                cmd.Parameters.AddWithValue("@is_enabled", config.IsEnabled);
            if (columns.Contains("update_time"))
                cmd.Parameters.AddWithValue("@update_time", config.UpdateTime);
        }

        /// <summary>按 ID 查询单条报警配置</summary>
        public async Task<AlarmConfig?> GetAlarmConfigByIdAsync(int id)
        {
            try
            {
                await using var conn = new SqlConnection(DatabaseConfig);
                await conn.OpenAsync();

                var columns = GetExistingColumns(conn, "AlarmConfig");

                string SafeCol(string name) =>
                    columns.Contains(name) ? $"[{name}]" : $"NULL AS [{name}]";

                var sql = $@"SELECT {SafeCol("id")},{SafeCol("connectname")},{SafeCol("name")},{SafeCol("unit")},
                                   {SafeCol("upper_limit")},{SafeCol("upper_limit_message")},{SafeCol("upper_allowCount")},{SafeCol("upper_limit_trigger")},
                                   {SafeCol("lower_limit")},{SafeCol("lower_limit_message")},{SafeCol("lower_allowCount")},{SafeCol("lower_limit_trigger")},
                                   {SafeCol("mutation_threshold")},{SafeCol("mutation_threshold_message")},{SafeCol("mutation_allowCount")},{SafeCol("mutation_threshold_trigger")},
                                   {SafeCol("unchanged_threshold_message")},{SafeCol("unchanged_allowCount")},{SafeCol("unchanged_threshold_trigger")},
                                   {SafeCol("alarm_level")},{SafeCol("is_enabled")},{SafeCol("update_time")}
                            FROM [AlarmConfig] WHERE [id]=@id";

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@id", id);
                await using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                    return ReadAlarmConfig(reader, columns);

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Core.GetAlarmConfigByIdAsync] 查询失败: " + ex.Message);
                ReportDbError(ex);
                return null;
            }
        }

        /// <summary>从 connectData 表查询某字段的时间序列数据</summary>
        public async Task<List<DateTimePoint>> GetColumnTimeSeriesAsync(
            string columnName, DateTime startTime, DateTime endTime)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                throw new ArgumentException("列名不能为空", nameof(columnName));

            var result = new List<DateTimePoint>();

            try
            {
                await using var conn = new SqlConnection(DatabaseConfig);
                await conn.OpenAsync();

                var sql = $"SELECT [Datetime], [{columnName}] FROM [ConnectData] WHERE [Datetime] BETWEEN @start AND @end ORDER BY [Datetime] ASC";

                Debug.WriteLine("[GetColumnTimeSeriesAsync] SQL: " + sql);

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@start", startTime);
                cmd.Parameters.AddWithValue("@end", endTime);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var dt = reader.IsDBNull(0) ? endTime : reader.GetDateTime(0);
                    double val = 0;
                    if (!reader.IsDBNull(1))
                    {
                        var raw = reader.GetValue(1);
                        val = Convert.ToDouble(raw);
                    }
                    result.Add(new DateTimePoint(dt, val));
                }

                Debug.WriteLine($"[Core.GetColumnTimeSeriesAsync] 返回 {result.Count} 个数据点");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Core.GetColumnTimeSeriesAsync] 查询失败: " + ex.Message);
                ReportDbError(ex);
            }

            return result;
        }

        /// <summary>分页查询报警历史（可选时间范围与类型筛选）</summary>
        public async Task<(List<AlarmRecord> Records, int TotalCount)> QueryAlarmHistoryAsync(
            DateTime? startTime, DateTime? endTime, string configType, int pageIndex, int pageSize)
        {
            var records = new List<AlarmRecord>();
            int totalCount = 0;

            try
            {
                await using var conn = new SqlConnection(DatabaseConfig);
                await conn.OpenAsync();

                var columns = GetExistingColumns(conn, "AlarmHistory");

                var whereClauses = new List<string>();
                var parameters = new List<(string, object)>();

                if (startTime.HasValue && columns.Contains("alarmTime"))
                {
                    whereClauses.Add("[alarmTime] >= @startTime");
                    parameters.Add(("@startTime", startTime.Value));
                }
                if (endTime.HasValue && columns.Contains("alarmTime"))
                {
                    whereClauses.Add("[alarmTime] <= @endTime");
                    parameters.Add(("@endTime", endTime.Value));
                }
                if (!string.IsNullOrEmpty(configType) && columns.Contains("config_type"))
                {
                    whereClauses.Add("[config_type] = @configType");
                    parameters.Add(("@configType", configType));
                }

                var where = whereClauses.Count > 0 ? " WHERE " + string.Join(" AND ", whereClauses) : "";

                var countSql = "SELECT COUNT(*) FROM [AlarmHistory]" + where;
                await using var countCmd = conn.CreateCommand();
                countCmd.CommandText = countSql;
                foreach (var (n, v) in parameters)
                    countCmd.Parameters.AddWithValue(n, v);
                totalCount = (int)await countCmd.ExecuteScalarAsync();

                string SafeCol(string n) =>
                    columns.Contains(n) ? $"[{n}]" : $"NULL AS [{n}]";

                string orderCol = columns.Contains("alarmTime") ? "[alarmTime]" : "(SELECT 1)";

                var offset = (pageIndex - 1) * pageSize;
                var dataSql = $@"SELECT {SafeCol("id")},{SafeCol("name")},{SafeCol("alarmTime")},{SafeCol("config_id")},{SafeCol("value")},{SafeCol("message")},
                                       {SafeCol("isChecked")},{SafeCol("isShowed")},{SafeCol("confirmTime")},{SafeCol("config_type")}
                                FROM [AlarmHistory]{where}
                                ORDER BY {orderCol} DESC
                                OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

                Debug.WriteLine("[QueryAlarmHistoryAsync] SQL: " + dataSql);

                await using var dataCmd = conn.CreateCommand();
                dataCmd.CommandText = dataSql;
                dataCmd.Parameters.AddWithValue("@offset", offset);
                dataCmd.Parameters.AddWithValue("@pageSize", pageSize);
                foreach (var (n, v) in parameters)
                    dataCmd.Parameters.AddWithValue(n, v);

                await using var reader = await dataCmd.ExecuteReaderAsync();

                string GetString(string col) { var v = reader[col]; return v == DBNull.Value ? string.Empty : v?.ToString() ?? string.Empty; }
                DateTime GetDateTime(string col) { var v = reader[col]; return v == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(v); }
                int GetInt(string col) { var v = reader[col]; return v == DBNull.Value ? 0 : Convert.ToInt32(v); }
                bool GetBool(string col) { var v = reader[col]; return v != DBNull.Value && Convert.ToBoolean(v); }
                DateTime? GetNullableDateTime(string col) { var v = reader[col]; return v == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(v); }

                while (await reader.ReadAsync())
                {
                    records.Add(new AlarmRecord
                    {
                        DeviceName = GetString("name"),
                        AlarmTime = GetDateTime("alarmTime"),
                        ConfigId = GetInt("config_id"),
                        AlarmContent = GetString("message"),
                        IsChecked = GetBool("isChecked"),
                        IsShowed = GetBool("isShowed"),
                        ConfirmTime = GetNullableDateTime("confirmTime"),
                        ConfigType = GetString("config_type"),
                    });
                }

                Debug.WriteLine($"[Core.QueryAlarmHistoryAsync] 第{pageIndex}页, 共{totalCount}条, 返回{records.Count}条");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Core.QueryAlarmHistoryAsync] 查询失败: " + ex.Message);
                ReportDbError(ex);
            }

            return (records, totalCount);
        }

        /// <summary>一次查询过去 N 小时指定列的时间序列</summary>
        public async Task<Dictionary<string, List<DateTimePoint>>> LoadHomePageTimeSeriesAsync(string[] columns, int hours = 1)
        {
            var result = new Dictionary<string, List<DateTimePoint>>();
            foreach (var col in columns)
                result[col] = new List<DateTimePoint>();

            try
            {
                await using var conn = new SqlConnection(DatabaseConfig);
                await conn.OpenAsync();

                var endTime = DateTime.Now;
                var startTime = endTime.AddHours(-hours);

                var columnList = string.Join(",", columns.Select(c => $"[{c}]"));
                var sql = $"SELECT [Datetime],{columnList} FROM [ConnectData] WHERE [Datetime] BETWEEN @start AND @end ORDER BY [Datetime] ASC";

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@start", startTime);
                cmd.Parameters.AddWithValue("@end", endTime);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var dt = reader.IsDBNull(0) ? endTime : reader.GetDateTime(0);
                    for (int i = 0; i < columns.Length; i++)
                    {
                        double val = 0;
                        if (!reader.IsDBNull(i + 1))
                            val = Convert.ToDouble(reader.GetValue(i + 1));
                        result[columns[i]].Add(new DateTimePoint(dt, val));
                    }
                }

                Debug.WriteLine($"[Core.LoadHomePageTimeSeriesAsync] 查询完成，各字段点数: {string.Join(", ", columns.Select(c => $"{c}={result[c].Count}"))}");
                DatabaseRecovered?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Core.LoadHomePageTimeSeriesAsync] 查询失败: " + ex.Message);
                ReportDbError(ex);
            }

            return result;
        }

        /// <summary>
        /// 获取所有用户数据表名（排除 alarmConfig、AlarmHistory 及系统表）
        /// </summary>
        public async Task<List<string>> GetTableNamesAsync()
        {
            var result = new List<string>();
            try
            {
                await using var conn = new SqlConnection(DatabaseConfig);
                await conn.OpenAsync();

                var sql = @"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES 
                            WHERE TABLE_TYPE = 'BASE TABLE' 
                              AND TABLE_NAME NOT LIKE 'sys%'
                              AND TABLE_NAME NOT IN ('alarmConfig','AlarmHistory')
                            ORDER BY TABLE_NAME";

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                    result.Add(reader.GetString(0));

                Debug.WriteLine($"[Core.GetTableNamesAsync] 返回 {result.Count} 个表");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Core.GetTableNamesAsync] 查询失败: " + ex.Message);
                ReportDbError(ex);
            }
            return result;
        }

        #region 数据备份

        public async Task<(bool Success, string Message, string FilePath)> BackupDatabaseAsync(
            string backupPath,
            bool enableCompression,
            bool enableVerification,
            int retentionDays,
            string filenameFormat)
        {
            try
            {
                string connectionString = DatabaseConfig;

                var builder = new SqlConnectionStringBuilder(connectionString);
                string databaseName = builder.InitialCatalog;

                if (string.IsNullOrEmpty(databaseName))
                {
                    if (connectionString.Contains("Database="))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(connectionString, @"Database=([^;]+)");
                        if (match.Success)
                            databaseName = match.Groups[1].Value;
                    }

                    if (string.IsNullOrEmpty(databaseName))
                        return (false, "无法从连接字符串中获取数据库名称", "");
                }

                string backupDirectory = ResolveSpecialPath(backupPath);

                if (!Directory.Exists(backupDirectory))
                {
                    try
                    {
                        Directory.CreateDirectory(backupDirectory);
                    }
                    catch (Exception ex)
                    {
                        return (false, $"无法创建备份目录: {backupDirectory}\n错误: {ex.Message}", "");
                    }
                }

                if (string.IsNullOrEmpty(filenameFormat))
                    filenameFormat = "{数据库名称}_{时间戳}.bak";

                string backupFileName = GenerateBackupFileName(filenameFormat, databaseName);
                string backupFullPath = Path.Combine(backupDirectory, backupFileName);

                if (File.Exists(backupFullPath))
                {
                    try
                    {
                        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        string oldBackupPath = backupFullPath.Replace(".bak", $"_冲突_{timestamp}.bak");
                        File.Move(backupFullPath, oldBackupPath);
                    }
                    catch
                    {
                        return (false, $"备份文件已存在且无法重命名: {backupFileName}\n请手动处理后再试。", "");
                    }
                }

                if (!CheckDiskSpace(backupDirectory))
                    return (false, "磁盘空间不足，无法备份", "");

                using var db = new SqlConnection(connectionString);
                await db.OpenAsync();

                string compressionOption = enableCompression ? ", COMPRESSION" : "";
                string backupSql = $@"
                    BACKUP DATABASE [{databaseName}] 
                    TO DISK = '{backupFullPath}' 
                    WITH FORMAT, 
                         MEDIANAME = 'SQLServerBackups', 
                         NAME = 'Full Backup of {databaseName}'{compressionOption};
                ";

                using (var cmd = new SqlCommand(backupSql, db))
                {
                    cmd.CommandTimeout = 0;
                    await cmd.ExecuteNonQueryAsync();
                }

                if (File.Exists(backupFullPath))
                {
                    var fileInfo = new FileInfo(backupFullPath);

                    if (enableVerification)
                    {
                        string verifySql = $"RESTORE VERIFYONLY FROM DISK = '{backupFullPath}';";
                        try
                        {
                            using var verifyCmd = new SqlCommand(verifySql, db);
                            verifyCmd.CommandTimeout = 300;
                            await verifyCmd.ExecuteNonQueryAsync();
                        }
                        catch (Exception ex)
                        {
                            File.Delete(backupFullPath);
                            return (false, $"备份验证失败: {ex.Message}", "");
                        }
                    }

                    CleanupOldBackups(backupDirectory, databaseName, retentionDays);

                    return (true, $"备份成功！\n数据库: {databaseName}\n文件: {backupFileName}\n大小: {FormatFileSize(fileInfo.Length)}\n路径: {backupDirectory}", backupFullPath);
                }
                else
                {
                    return (false, "备份文件未创建成功", "");
                }
            }
            catch (SqlException sqlEx)
            {
                return HandleSqlException(sqlEx);
            }
            catch (Exception ex)
            {
                return (false, $"备份失败: {ex.Message}", "");
            }
        }

        private static string ResolveSpecialPath(string path)
        {
            if (path.Contains("%"))
            {
                path = path.Replace("%Desktop%", Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
                path = path.Replace("%Documents%", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
                path = path.Replace("%AppData%", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
                path = path.Replace("%Temp%", Path.GetTempPath());
                path = path.Replace("%ProgramData%", Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
            }
            return path;
        }

        private static string GenerateBackupFileName(string format, string databaseName)
        {
            string fileName = format;
            fileName = fileName.Replace("{数据库名称}", databaseName);
            fileName = fileName.Replace("{日期}", DateTime.Now.ToString("yyyyMMdd"));
            fileName = fileName.Replace("{时间}", DateTime.Now.ToString("HHmmss"));
            fileName = fileName.Replace("{时间戳}", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            fileName = fileName.Replace("{年}", DateTime.Now.ToString("yyyy"));
            fileName = fileName.Replace("{月}", DateTime.Now.ToString("MM"));
            fileName = fileName.Replace("{日}", DateTime.Now.ToString("dd"));

            if (!fileName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                fileName += ".bak";

            return fileName;
        }

        private static bool CheckDiskSpace(string backupDirectory)
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(backupDirectory));
                return drive.AvailableFreeSpace >= 1024L * 1024L * 100L;
            }
            catch
            {
                return true;
            }
        }

        private static void CleanupOldBackups(string backupDirectory, string databaseName, int retentionDays)
        {
            try
            {
                if (retentionDays <= 0) return;

                string pattern = databaseName + "_*.bak";
                var backupFiles = Directory.GetFiles(backupDirectory, pattern);
                var cutoffDate = DateTime.Now.AddDays(-retentionDays);

                foreach (string file in backupFiles)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }
            catch { }
        }

        private static (bool Success, string Message, string FilePath) HandleSqlException(SqlException sqlEx)
        {
            string errorMessage = sqlEx.Number switch
            {
                18456 => "数据库登录失败，请检查用户名和密码",
                4060 => "无法连接到数据库，请检查数据库是否存在或服务是否启动",
                3201 => "无法创建备份文件，请检查磁盘空间和权限",
                3013 => "备份过程中发生错误，可能是磁盘空间不足或权限问题",
                4208 => "数据库正在使用中，请稍后再试或联系管理员",
                _ => $"数据库错误({sqlEx.Number}): {sqlEx.Message}"
            };
            return (false, $"数据库备份失败: {errorMessage}", "");
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        #endregion
    }
}
