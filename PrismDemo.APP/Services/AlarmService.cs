using PrismDemo.Core.Interfaces;
using PrismDemo.Core.Models;
using PrismDemo.Core.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace PrismDemo.APP.Services
{
    public class AlarmService : IDisposable, IAlarmConfigProvider
    {
        private readonly IDatabaseService _databaseService;
        private List<AlarmConfig> _configs;
        private readonly Dictionary<string, AlarmRuntimeState> _runtimeStates = new();
        private readonly object _lock = new();
        private bool _configsLoaded;
        private bool _disposed;
        private static int _windowOffsetCounter;

        public AlarmConfig GetConfig(string name)
        {
            return _configs?.FirstOrDefault(c => c.Name == name);
        }

        public AlarmService(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        /// <summary>
        /// 从数据库加载启用的报警配置并缓存（double-check lock）
        /// </summary>
        private async Task EnsureConfigsLoadedAsync()
        {
            if (_configsLoaded) return;
            lock (_lock)
            {
                if (_configsLoaded) return;
            }

            try
            {
                var allConfigs = await _databaseService.GetAlarmConfigsAsync();
                _configs = allConfigs.Where(c => c.IsEnabled == 1).ToList();
                _configsLoaded = true;
                Debug.WriteLine($"[AlarmService] 已加载 {_configs.Count} 条启用的报警配置");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[AlarmService] 加载报警配置失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 检查所有启用的报警配置，对每条匹配的 CollectedData 执行上限/下限判断
        /// </summary>
        /// <param name="collectedData">当前采集到的数据字典，key = 监测点名称</param>
        public async Task CheckAlarmsAsync(Dictionary<string, ConnectData> collectedData)
        {
            if (_disposed) return;
            if (collectedData == null || collectedData.Count == 0) return;

            await EnsureConfigsLoadedAsync();
            if (_configs == null || _configs.Count == 0) return;

            foreach (var config in _configs)
            {
                if (_disposed) return;

                if (!collectedData.TryGetValue(config.ConnectName, out var connectData))
                    continue;

                try
                {
                    if (config.UpperLimit.HasValue && config.UpperAllowCount.HasValue && config.UpperAllowCount.Value > 0)
                        await CheckUpperLimitAsync(config, connectData);

                    if (config.LowerLimit.HasValue && config.LowerAllowCount.HasValue && config.LowerAllowCount.Value > 0)
                        await CheckLowerLimitAsync(config, connectData);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AlarmService] 检查 {config.ConnectName} 异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 越上限报警判断。数据无效时直接递增计数，有效时比较值与上限阈值；
        /// 连续计数超过允许次数且未触发时执行报警，数据恢复正常后重置计数和 trigger
        /// </summary>
        private async Task CheckUpperLimitAsync(AlarmConfig config, ConnectData connectData)
        {
            var state = GetOrCreateState(config.ConnectName);

            if (!connectData.isvalid)
            {
                state.UpperCurrentCount++;
                if (state.UpperCurrentCount > (int)config.UpperAllowCount.Value)
                {
                    var wasTriggered = config.UpperLimitTrigger ?? false;
                    if (!wasTriggered)
                    {
                        config.UpperLimitTrigger = true;
                        await _databaseService.UpdateAlarmConfigTriggerAsync(config.Id, "upper_limit_trigger", true);
                        await FireAlarmAsync(config, config.UpperLimitMessage ?? $"越上限报警: {config.ConnectName}", 0);
                    }
                }
                return;
            }

            if (!TryGetValueAsDecimal(connectData.Value, out var value))
                return;

            if (value > config.UpperLimit.Value)
            {
                state.UpperCurrentCount++;

                if (state.UpperCurrentCount > (int)config.UpperAllowCount.Value)
                {
                    var wasTriggered = config.UpperLimitTrigger ?? false;
                    if (!wasTriggered)
                    {
                        config.UpperLimitTrigger = true;
                        await _databaseService.UpdateAlarmConfigTriggerAsync(config.Id, "upper_limit_trigger", true);
                        await FireAlarmAsync(config, config.UpperLimitMessage ?? $"越上限报警: {config.ConnectName}", value);
                    }
                }
            }
            else
            {
                state.UpperCurrentCount = 0;
                if ((config.UpperLimitTrigger ?? false) == true)
                {
                    config.UpperLimitTrigger = false;
                    await _databaseService.UpdateAlarmConfigTriggerAsync(config.Id, "upper_limit_trigger", false);
                }
            }
        }

        /// <summary>
        /// 越下限报警判断。数据无效时直接递增计数，有效时比较值与下限阈值；
        /// 连续计数超过允许次数且未触发时执行报警，数据恢复正常后重置计数和 trigger
        /// </summary>
        private async Task CheckLowerLimitAsync(AlarmConfig config, ConnectData connectData)
        {
            var state = GetOrCreateState(config.ConnectName);

            if (!connectData.isvalid)
            {
                state.LowerCurrentCount++;
                if (state.LowerCurrentCount > (int)config.LowerAllowCount.Value)
                {
                    var wasTriggered = config.LowerLimitTrigger ?? false;
                    if (!wasTriggered)
                    {
                        config.LowerLimitTrigger = true;
                        await _databaseService.UpdateAlarmConfigTriggerAsync(config.Id, "lower_limit_trigger", true);
                        await FireAlarmAsync(config, config.LowerLimitMessage ?? $"越下限报警: {config.ConnectName}", 0);
                    }
                }
                return;
            }

            if (!TryGetValueAsDecimal(connectData.Value, out var value))
                return;

            if (value < config.LowerLimit.Value)
            {
                state.LowerCurrentCount++;

                if (state.LowerCurrentCount > (int)config.LowerAllowCount.Value)
                {
                    var wasTriggered = config.LowerLimitTrigger ?? false;
                    if (!wasTriggered)
                    {
                        config.LowerLimitTrigger = true;
                        await _databaseService.UpdateAlarmConfigTriggerAsync(config.Id, "lower_limit_trigger", true);
                        await FireAlarmAsync(config, config.LowerLimitMessage ?? $"越下限报警: {config.ConnectName}", value);
                    }
                }
            }
            else
            {
                state.LowerCurrentCount = 0;
                if ((config.LowerLimitTrigger ?? false) == true)
                {
                    config.LowerLimitTrigger = false;
                    await _databaseService.UpdateAlarmConfigTriggerAsync(config.Id, "lower_limit_trigger", false);
                }
            }
        }

        /// <summary>
        /// 执行报警动作：写入 AlarmHistory 表，更新 trigger 状态，弹出报警窗口
        /// </summary>
        /// <param name="config">触发的报警配置</param>
        /// <param name="message">报警消息内容</param>
        /// <param name="value">报警时的数值</param>
        private async Task FireAlarmAsync(AlarmConfig config, string message, decimal value)
        {
            var now = DateTime.Now;
            var record = new AlarmRecord
            {
                DeviceName = config.ConnectName,
                AlarmTime = now,
                ConfigId = config.Id,
                Value = value.ToString(),
                AlarmContent = message,
                IsChecked = false,
                IsShowed = true,
                ConfirmTime = null
            };

            int recordId;
            try
            {
                recordId = await _databaseService.InsertAlarmHistoryAsync(record);
                record.Id = recordId;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AlarmService] 插入报警记录失败: {ex.Message}");
                recordId = 0;
            }

            Debug.WriteLine($"[AlarmService] 触发报警: {config.ConnectName}, 值={value}, recordId={recordId}");

            ShowAlarmWindow(message, now, recordId);
        }

        /// <summary>
        /// 展示报警弹窗。使用静态计数器在屏幕中央做偏移，多条报警不重叠；
        /// 用户点击"确定"时异步更新 AlarmHistory 的确认时间
        /// </summary>
        private void ShowAlarmWindow(string message, DateTime alarmTime, int recordId)
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                var fullMessage = $"[{alarmTime:yyyy-MM-dd HH:mm:ss}]\n{message}";

                Action onConfirm = null;
                if (recordId > 0)
                {
                    var db = _databaseService;
                    onConfirm = async () =>
                    {
                        try
                        {
                            await db.UpdateAlarmHistoryConfirmAsync(recordId, DateTime.Now);
                        }
                        catch { }
                    };
                }

                var window = new AlarmWindow(fullMessage, onConfirm);

                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;
                var offsetBase = 30;
                var maxOffset = 10;
                var idx = _windowOffsetCounter++ % maxOffset;

                window.Left = (screenWidth - window.Width) / 2 + idx * offsetBase;
                window.Top = (screenHeight - window.Height) / 2 + idx * offsetBase;
                window.Topmost = true;

                window.Show();
            }));
        }

        /// <summary>
        /// 获取或创建指定监测点的运行时状态（含上下限当前计数）
        /// </summary>
        private AlarmRuntimeState GetOrCreateState(string name)
        {
            lock (_lock)
            {
                if (!_runtimeStates.TryGetValue(name, out var state))
                {
                    state = new AlarmRuntimeState();
                    _runtimeStates[name] = state;
                }
                return state;
            }
        }

        /// <summary>
        /// 安全尝试将 OPC 采集值转为 decimal，转换失败返回 false
        /// </summary>
        private static bool TryGetValueAsDecimal(object value, out decimal result)
        {
            result = 0;
            if (value == null) return false;
            try
            {
                result = Convert.ToDecimal(value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 重置报警配置缓存与运行时状态（系统重新加载时调用），
        /// 下次 CheckAlarmsAsync 会重新从数据库 AlarmConfig 表加载
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _configs = null;
                _configsLoaded = false;
                _runtimeStates.Clear();
            }
            Debug.WriteLine("[AlarmService] 报警配置缓存已重置");
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
