using Prism.Mvvm;
using PrismDemo.Core.Interfaces;
using PrismDemo.Core.Models;
using PrismDemo.Core.Services;
using PrismDemo.Core.Views;
using PrismDemo.B.Interfaces;
using PrismDemo.B.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace PrismDemo.B.Services
{
    /// <summary>
    /// B剂主服务
    ///
    /// 职责分离：
    ///   冷启动路径：Module.OnInitialized → EnsureInitialized() → Start()
    ///   热切换路径：用户切换开关 → SwitchChanged 事件 → Start()/Stop()
    ///   销毁路径：  容器释放 → Dispose()
    ///
    /// 注册要求：必须是 Singleton，否则事件重复订阅导致重复启动
    /// 单例保护：使用静态字段防止多个实例同时运行后台循环
    /// </summary>
    public class BMainService : BindableBase, IBService, IWriteableService, IDisposable
    {
        // ═══════════════════════════════════════════════════════
        //  字段
        // ═══════════════════════════════════════════════════════

        private readonly IModuleSwitch _moduleSwitch;
        private CancellationTokenSource _cts;
        private Task _runningTask;
        private CancellationTokenSource _heartbeatCts;
        private Task _heartbeatTask;
        private bool _disposed = false;
        private int _startFlag = 0;

        private BData? _bData;
        private readonly SharedDataModel _data;
        private readonly IOpcService _opcService;
        private readonly BDataService _bDataService;
        private readonly IAlarmConfigProvider _alarmConfigProvider;

        /// <summary>
        /// 冷启动是否已完成（防止 EnsureInitialized 被多次调用）
        /// </summary>
        private int _initializedFlag = 0;

        private static int _nextId = 0;
        private readonly int _instanceId = Interlocked.Increment(ref _nextId);

        public bool IsRunning { get; private set; }

        public BData? CurrentData => _bData;

        // ═══════════════════════════════════════════════════════
        //  构造函数
        // ═══════════════════════════════════════════════════════


        public BMainService(IModuleSwitch moduleSwitch,
                                SharedDataModel data,
                                BDataService bDataService,
                                IOpcService opcService,
                                IAlarmConfigProvider alarmConfigProvider)
        {
            _moduleSwitch = moduleSwitch ?? throw new ArgumentNullException(nameof(moduleSwitch));
            _data = data;
            _bDataService = bDataService;
            _opcService = opcService;
            _alarmConfigProvider = alarmConfigProvider;
            _moduleSwitch.SwitchChanged += OnSwitchChanged;
            Debug.WriteLine($"[B] Service#{_instanceId} 构造完成，已订阅 SwitchChanged");
        }

        // ═══════════════════════════════════════════════════════
        //  公开方法
        // ═══════════════════════════════════════════════════════

        public void EnsureInitialized()
        {
            Debug.WriteLine($"[B] Service#{_instanceId} EnsureInitialized 被调用");

            if (Interlocked.CompareExchange(ref _initializedFlag, 1, 0) != 0)
            {
                Debug.WriteLine($"[B] Service#{_instanceId} EnsureInitialized 跳过（已初始化过）");
                return;
            }

            if (_disposed)
            {
                Debug.WriteLine($"[B] Service#{_instanceId} EnsureInitialized 跳过（已销毁）");
                return;
            }

            if (_moduleSwitch.IsModuleEnabled("B"))
            {
                Debug.WriteLine($"[B] Service#{_instanceId} 模块已启用 → 启动后台循环");
                Start();
            }
            else
            {
                Debug.WriteLine($"[B] Service#{_instanceId} 模块未启用 → 等待用户开启");
            }
        }

        public Task StartAsync()
        {
            if (_disposed) return Task.CompletedTask;
            Start();
            return Task.CompletedTask;
        }

        // ═══════════════════════════════════════════════════════
        //  事件回调 —— 热切换路径
        // ═══════════════════════════════════════════════════════

        private async void OnSwitchChanged(string moduleName, bool enabled)
        {
            if (moduleName != "B") return;

            if (_disposed)
            {
                Debug.WriteLine($"[B] Service#{_instanceId} OnSwitchChanged 忽略（已销毁）");
                return;
            }

            Debug.WriteLine($"[B] Service#{_instanceId} OnSwitchChanged 热切换 enabled={enabled} IsRunning={IsRunning}");

            try
            {
                if (enabled)
                {
                    Debug.WriteLine($"[B] Service#{_instanceId} 热切换 → 启动");
                    Start();
                }
                else
                {
                    Debug.WriteLine($"[B] Service#{_instanceId} 热切换 → 停止");
                    await Stop();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[B] Service#{_instanceId} OnSwitchChanged 异常: {ex}");
            }
        }

        // ═══════════════════════════════════════════════════════
        //  内部逻辑
        // ═══════════════════════════════════════════════════════

        private void Start()
        {
            Debug.WriteLine($"[B] Service#{_instanceId} Start 进入 _disposed={_disposed} _startFlag={_startFlag}");

            if (_disposed)
            {
                Debug.WriteLine($"[B] Service#{_instanceId} Start 跳过（已销毁）");
                return;
            }

            if (Interlocked.CompareExchange(ref _startFlag, 1, 0) != 0)
            {
                Debug.WriteLine($"[B] Service#{_instanceId} Start 跳过（已在运行）");
                return;
            }

            Debug.WriteLine($"[B] Service#{_instanceId} Start 通过检查，开始启动");

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            IsRunning = true;

            _heartbeatCts = new CancellationTokenSource();
            _heartbeatTask = RunHeartbeatAsync(_heartbeatCts.Token);

            Debug.WriteLine($"[B] Service#{_instanceId} IsRunning=true，派发后台任务");

            _runningTask = Task.Run(async () =>
            {
                int count = 0;
                Debug.WriteLine($"[B] Service#{_instanceId} 后台循环开始");

                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        count++;
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss}] [B] Service#{_instanceId} 第 {count} 轮");

                        try
                        {
                            var loopStart = Stopwatch.GetTimestamp();
                            //保存上一轮数据（深拷贝），用于 UpdateBData 回退 + 反馈算法比较
                            _bDataService.SaveLastBData(_bData);

                            var collectedData = _data.CollectedData;
                            if (collectedData != null && collectedData.Count > 0)
                            {
                                _bData = _bDataService.UpdateBData(collectedData, _bData);
                                RaisePropertyChanged(nameof(CurrentData));
                            }

                            #region 算法逻辑实现
                            Trace.WriteLine($"[B] 时间: {DateTime.Now}, Service#{_instanceId} Hash = {_bData?.GetHashCode()}");

                            #region 赋值初始投加率
                            // 算法实现已移除：本示例不包含具体的加药计算/反馈算法细节
                            #endregion

                            #region 数据预处理（数据检查、过滤）
                            // 算法实现已移除：本示例不包含具体的加药计算/反馈算法细节
                            #endregion

                            #region 反馈更新
                            // 算法实现已移除：本示例不包含具体的加药计算/反馈算法细节
                            #endregion

                            #region 计算加药量
                            // 算法实现已移除：本示例不包含具体的加药计算/反馈算法细节
                            #endregion

                            #region 加药联动控制
                            // 算法实现已移除：本示例不包含具体的加药计算/反馈算法细节
                            #endregion

                            #region 保存数据库
                            _bDataService.SaveBData(_bData);
                            #endregion

                            #endregion

                            // 循环间隔60秒（漂移补偿）
                            var elapsedMs = (Stopwatch.GetTimestamp() - loopStart) * 1000.0 / Stopwatch.Frequency;
                            var delay = (int)Math.Max(0, 60000 - elapsedMs);
                            //delay = (int)Math.Max(0, 10000 - elapsedMs); //测试
                            await Task.Delay(delay, token);
						}
                        catch (OperationCanceledException)
                        {
                            Debug.WriteLine($"[B] Service#{_instanceId} 收到取消信号，退出循环");
                            break;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[B] Service#{_instanceId} 循环异常: {ex.Message}");
                            try { await Task.Delay(5000, token); } catch { Debug.WriteLine($"[B] 延迟异常"); }
                        }
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _startFlag, 0);
                    IsRunning = false;
                    Debug.WriteLine($"[B] Service#{_instanceId} 循环退出，共 {count} 轮，_startFlag=0 IsRunning=false");
                }
            }, token);

            Debug.WriteLine($"[B] Service#{_instanceId} 启动完成");
        }

        // ═══════════════════════════════════════════════════════
        //  OPC 数据写入方法
        // ═══════════════════════════════════════════════════════

        public bool CanWrite(string name) => true;

        public string GetDisplayName(string name) => name;

        public bool WriteValue(ConnectData data, object value)
        {
            try
            {
                if (!_opcService.IsConnected)
                {
                    Debug.WriteLine("[B] OPC未连接，尝试重连...");
                    if (!_opcService.Open(5000))
                    {
                        Debug.WriteLine("[B] 重连失败");
                        new AlarmWindow("OPC未连接，无法写入").Show();
                        return false;
                    }
                    Debug.WriteLine("[B] OPC重连成功");
                }

                var (success, primaryFailed) = _opcService.TryWriteValue(data.OpcAddress, data.MockOpcAddress, value);

                if (success)
                {
                    _bData?.SetField(data.Name, value);
                    if (_data.CollectedData != null && _data.CollectedData.TryGetValue(data.Name, out var sourceData))
                        sourceData.Value = value;
                    Debug.WriteLine(primaryFailed
                        ? $"[B] 写入成功（备用地址）- {data.Name}: {value}"
                        : $"[B] 写入成功 - {data.Name}: {value}");
                }
                else
                {
                    Debug.WriteLine($"[B] 写入失败 - {data.Name}: {value}");
                    new AlarmWindow($"写入失败：无法写入 {data.Name}").Show();
                }

                return success;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[B] WriteValue 异常: {ex}");
                new AlarmWindow($"写入失败：{ex.Message}").Show();
                return false;
            }
        }

        public void ToggleBoolValue(ConnectData data)
        {
            try
            {
                if (!_opcService.IsConnected)
                {
                    Debug.WriteLine("[B] OPC未连接，尝试重连...");
                    if (!_opcService.Open(5000))
                    {
                        Debug.WriteLine("[B] 重连失败");
                        new AlarmWindow("OPC未连接，无法写入").Show();
                        return;
                    }
                    Debug.WriteLine("[B] OPC重连成功");
                }

                if (data.Value is bool b)
                {
                    _opcService.WriteNode(data.OpcAddress, !b);
                }
                else if (bool.TryParse(data.Value?.ToString(), out bool currentValue))
                {
                    _opcService.WriteNode(data.OpcAddress, !currentValue);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[B] ToggleBoolValue 异常: {ex}");
                new AlarmWindow($"写入失败：{ex.Message}").Show();
            }
        }

        // ═══════════════════════════════════════════════════════
        //  心跳保持
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 心跳线程：每秒向 plcTimerZeroB 写入 0-100 循环值，保持与下位通讯
        /// 当 _aData 为 null 时降级从 _data.CollectedData 读取字段配置
        /// </summary>
        private async Task RunHeartbeatAsync(CancellationToken token)
        {
            float heartbeatValue = 0;
            Debug.WriteLine($"[B] Service#{_instanceId} 心跳线程启动");

            try
            {
                while (!token.IsCancellationRequested)
                {
                    ConnectData data = null;
                    if (_bData != null)
                    {
                        data = _bData.GetField("plcTimerZeroB");
                    }

                    // 降级：从 CollectedData 直接读取
                    if (data == null && _data.CollectedData != null)
                    {
                        _data.CollectedData.TryGetValue("plcTimerZeroB", out data);
                    }

                    if (data != null && (!string.IsNullOrEmpty(data.OpcAddress) || !string.IsNullOrEmpty(data.MockOpcAddress)))
                    {
                        var typedValue = data.Type?.ToLowerInvariant() switch
                        {
                            "word" or "uint16" => (object)(ushort)heartbeatValue,
                            "int" or "int32" => (object)(int)heartbeatValue,
                            "short" or "int16" => (object)(short)heartbeatValue,
                            "bool" or "boolean" => (object)(heartbeatValue != 0),
                            _ => (object)heartbeatValue
                        };

                        //var (success, primaryFailed) = _opcService.TryWriteValue(data.OpcAddress, data.MockOpcAddress, typedValue, token);

                        //if (success)
                        //{
                        //    if (primaryFailed)
                        //        Debug.WriteLine($"[heartbeatWork] plcTimerZeroB 经备用地址写入，当前值:{heartbeatValue}");
                        //    else
                        //        Debug.WriteLine($"[heartbeatWork] plcTimerZeroB 正在跳动，当前值:{heartbeatValue}");
                        //}
                        //else
                        //{
                        //    Debug.WriteLine($"[heartbeatWork] plcTimerZeroB 写入失败，当前值:{heartbeatValue}");
                        //}
                    }
                    else
                    {
                        Debug.WriteLine("[heartbeatWork] plcTimerZeroB 不存在于B数据或CollectedData中");
                    }

                    heartbeatValue = (heartbeatValue + 1) % 101;
                    await Task.Delay(1000, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"[B] Service#{_instanceId} 心跳线程收到取消信号");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[B] Service#{_instanceId} 心跳线程异常: {ex.Message}");
            }

            Debug.WriteLine($"[B] Service#{_instanceId} 心跳线程退出");
        }

        // ═══════════════════════════════════════════════════════
        //  内部停止
        // ═══════════════════════════════════════════════════════

        private async Task Stop()
        {
            Debug.WriteLine($"[B] Service#{_instanceId} Stop 进入 _startFlag={_startFlag}");

            if (Interlocked.CompareExchange(ref _startFlag, 0, 1) != 1)
            {
                Debug.WriteLine($"[B] Service#{_instanceId} Stop 跳过（未在运行）");
                return;
            }

            Debug.WriteLine($"[B] Service#{_instanceId} Stop 开始取消");

            _heartbeatCts?.Cancel();
            _cts?.Cancel();
            Debug.WriteLine($"[B] Service#{_instanceId} 已发送取消信号");

            if (_heartbeatTask != null)
            {
                Debug.WriteLine($"[B] Service#{_instanceId} 等待心跳任务结束...");
                try { await _heartbeatTask; }
                catch { Debug.WriteLine($"[B] Service#{_instanceId} 心跳任务异常"); }
                Debug.WriteLine($"[B] Service#{_instanceId} 心跳任务已结束");
            }

            if (_runningTask != null)
            {
                Debug.WriteLine($"[B] Service#{_instanceId} 等待后台任务结束...");
                try { await _runningTask; }
                catch { Debug.WriteLine($"[B] Service#{_instanceId} 后台任务异常"); }
                Debug.WriteLine($"[B] Service#{_instanceId} 后台任务已结束");
            }

            _cts?.Dispose();
            _cts = null;
            _runningTask = null;

            _heartbeatCts?.Dispose();
            _heartbeatCts = null;
            _heartbeatTask = null;

            IsRunning = false;

            Debug.WriteLine($"[B] Service#{_instanceId} 停止完成");
        }

        // ═══════════════════════════════════════════════════════
        //  销毁
        // ═══════════════════════════════════════════════════════

        public void Dispose()
        {
            Debug.WriteLine($"[B] Service#{_instanceId} Dispose 被调用 _disposed={_disposed}");
            if (_disposed) return;

            _disposed = true;
            IsRunning = false;
            Debug.WriteLine($"[B] Service#{_instanceId} _disposed=true");

            _moduleSwitch.SwitchChanged -= OnSwitchChanged;
            Debug.WriteLine($"[B] Service#{_instanceId} 已取消订阅 SwitchChanged");

            var cts = _cts;
            var task = _runningTask;
            _cts = null;
            _runningTask = null;

            var heartbeatCts = _heartbeatCts;
            var heartbeatTask = _heartbeatTask;
            _heartbeatCts = null;
            _heartbeatTask = null;

            if (cts != null)
            {
                Debug.WriteLine($"[B] Service#{_instanceId} 等待循环结束（最多 5 秒）");
                cts.Cancel();

                if (task != null)
                {
                    try
                    {
                        bool completed = task.Wait(5000);
                        Debug.WriteLine($"[B] Service#{_instanceId} 循环等待结果 completed={completed}");
                    }
                    catch (AggregateException) { Debug.WriteLine($"[B] Service#{_instanceId} Dispose等待异常"); }
                }

                cts.Dispose();
                Debug.WriteLine($"[B] Service#{_instanceId} CTS 已释放");
            }

            if (heartbeatCts != null)
            {
                Debug.WriteLine($"[B] Service#{_instanceId} 等待心跳线程结束...");
                heartbeatCts.Cancel();

                if (heartbeatTask != null)
                {
                    try
                    {
                        bool completed = heartbeatTask.Wait(3000);
                        Debug.WriteLine($"[B] Service#{_instanceId} 心跳等待结果 completed={completed}");
                    }
                    catch (AggregateException) { Debug.WriteLine($"[B] Service#{_instanceId} 心跳等待异常"); }
                }

                heartbeatCts.Dispose();
                Debug.WriteLine($"[B] Service#{_instanceId} 心跳 CTS 已释放");
            }

            Debug.WriteLine($"[B] Service#{_instanceId} Dispose 完成");
        }
    }
}