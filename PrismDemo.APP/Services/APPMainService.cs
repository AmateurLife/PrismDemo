using Opc.Ua;
using PrismDemo.Core.Interfaces;
using PrismDemo.Core.Models;
using PrismDemo.Core.Services;
using PrismDemo.Core.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PrismDemo.APP.Services
{
    public class APPMainService : IDisposable
    {
        private readonly IDatabaseService _databaseService;
        private readonly AlarmService _alarmService;
        private OpcServices _opcService;
        private readonly SharedDataModel _data;
        private List<ConnectData> _connectDataList;
        private Timer _saveTimer;
        private bool _disposed = false;
        private int _startFlag = 0;
        private readonly object _dbLock = new object();

        private Thread _dataLoopThread;
        private CancellationTokenSource _loopCts;
        private AlarmWindow _reconnectAlarmWindow;
        private AlarmWindow _dbErrorAlarmWindow;
        private bool _dbErrorShown;

        public OpcServices OpcService => _opcService;

        public APPMainService(IDatabaseService databaseService, SharedDataModel data, OpcServices opcService, AlarmService alarmService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _opcService = opcService ?? throw new ArgumentNullException(nameof(opcService));
            _alarmService = alarmService ?? throw new ArgumentNullException(nameof(alarmService));

            _databaseService.ConnectConfigUpdated += OnConnectConfigUpdated;
            _databaseService.DatabaseErrorOccurred += OnDatabaseError;
            _databaseService.DatabaseRecovered += OnDatabaseRecovered;

            Debug.WriteLine("[APPMainService] 构造完成");
        }

        private void OnConnectConfigUpdated()
        {
            Debug.WriteLine("[APPMainService] 收到配置更新事件，开始重新加载...");

            Task.Run(async () =>
            {
                try
                {
                    List<ConnectData> newList;
                    lock (_dbLock)
                    {
                        newList = _databaseService.GetConnectData().GetAwaiter().GetResult();
                    }

                    Debug.WriteLine("[APPMainService] 正在停止旧监控...");
                    await _databaseService.StopMonitoringAsync();

                    _connectDataList = newList;
                    _data.ConnectDataList = newList;

                    _databaseService.EnsureConnectDataTable(newList);

                    Debug.WriteLine("[APPMainService] 正在启动新监控...");
                    await _databaseService.StartMonitoringAsync(_connectDataList, _opcService, 1000);

                    Debug.WriteLine("[APPMainService] 配置已重新加载并重启监控，共 " + newList.Count + " 条");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[APPMainService] 配置重新加载失败: " + ex.Message);
                }
            });
        }

        public void Start()
        {
            Debug.WriteLine("[APPMainService] Start 开始");

            if (_disposed)
            {
                Debug.WriteLine("[APPMainService] Start 跳过（已销毁）");
                return;
            }

            if (Interlocked.CompareExchange(ref _startFlag, 1, 0) != 0)
            {
                Debug.WriteLine("[APPMainService] Start 跳过（已在运行）");
                return;
            }

            _saveTimer = new Timer(SaveConnectData, null, 2000, 60000);
            Debug.WriteLine("[APPMainService] 保存Timer已启动（60秒间隔）");

            try
            {
                Debug.WriteLine("[APPMainService] 正在加载ConnectData配置...");
                _connectDataList = Task.Run(async () => await _databaseService.GetConnectData())
                    .GetAwaiter().GetResult();
                Debug.WriteLine($"[APPMainService] ConnectData加载完成，共 {_connectDataList.Count} 条");

                _data.ConnectDataList = _connectDataList;
                _databaseService.EnsureConnectDataTable(_connectDataList);
                Debug.WriteLine("[APPMainService] connectData 表结构已确保");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APPMainService] 加载配置/建表失败: {ex.Message}");
            }

            _loopCts = new CancellationTokenSource();
            _dataLoopThread = new Thread(() => RunDataLoop(_loopCts.Token))
            {
                IsBackground = true,
                Name = "OPCDataLoop"
            };
            _dataLoopThread.Start();

            Debug.WriteLine("[APPMainService] 数据循环线程已启动");
        }

        private void RunDataLoop(CancellationToken token)
        {
            Debug.WriteLine("[APPMainService] RunDataLoop 开始");

            int disconnectCount = 0;
            bool alarmShown = false;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_disposed || _opcService == null || _connectDataList == null)
                    {
                        if (token.WaitHandle.WaitOne(1000)) break;
                        continue;
                    }

                    if (!_opcService.IsConnected())
                    {
                        disconnectCount++;

                        if (disconnectCount >= 5 && !alarmShown)
                        {
                            alarmShown = true;
                            ShowReconnectAlarm();
                        }

                        try
                        {
                            Debug.WriteLine("[APPMainService] 尝试重连OPC...");
                            _opcService.Open(15000);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[APPMainService] 重连异常: {ex.Message}");
                        }

                        if (_opcService.IsConnected())
                        {
                            disconnectCount = 0;
                            alarmShown = false;
                            CloseReconnectAlarm();
                            Debug.WriteLine("[APPMainService] OPC重连成功");
                        }
                        else
                        {
                            if (token.WaitHandle.WaitOne(1000)) break;
                            continue;
                        }
                    }
                    else
                    {
                        if (disconnectCount > 0)
                        {
                            disconnectCount = 0;
                            alarmShown = false;
                            CloseReconnectAlarm();
                        }
                    }

                    try
                    {
                        _opcService.RefreshDataAsync(_connectDataList, token).GetAwaiter().GetResult();
                        var collected = _databaseService.GetConnectDataValue(_connectDataList, _opcService);
                        _data.CollectedData = collected;

                        Task.Run(() => _alarmService.CheckAlarmsAsync(collected), CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[APPMainService] 数据采集异常: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[APPMainService] RunDataLoop 异常: {ex.Message}");
                }

                try
                {
                    if (token.WaitHandle.WaitOne(1000)) break;
                }
                catch (ObjectDisposedException)
                {
                    break; // CTS 已被释放，线程直接退出
                }
            }

            CloseReconnectAlarm();
            Debug.WriteLine("[APPMainService] RunDataLoop 结束");
        }

        private void SaveConnectData(object state)
        {
            if (_disposed || _connectDataList == null) return;

            lock (_dbLock)
            {
                try
                {
                    _databaseService.EnsureConnectDataTable(_connectDataList);
                    _databaseService.SaveConnectData(_connectDataList);
                    Debug.WriteLine("[APPMainService] ConnectData已保存到数据库");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[APPMainService] SaveConnectData 异常: {ex.Message}");
                }
            }
        }

        public void Stop()
        {
            Debug.WriteLine("[APPMainService] Stop 开始");

            var cts = _loopCts;
            cts?.Cancel();

            if (_dataLoopThread != null && _dataLoopThread.IsAlive)
            {
                _dataLoopThread.Join(3000);
            }
            _dataLoopThread = null;

            // 线程退出后再释放 CTS，避免线程仍在访问 token.WaitHandle 时被释放而抛出 ObjectDisposedException
            cts?.Dispose();
            _loopCts = null;

            CloseReconnectAlarm();
            CloseDbErrorWindow();
            _dbErrorShown = false;

            _saveTimer?.Dispose();
            _saveTimer = null;

            Interlocked.Exchange(ref _startFlag, 0);

            _opcService?.Disconnect();

            Debug.WriteLine("[APPMainService] Stop 完成");
        }

        private void ShowReconnectAlarm()
        {
            Application.Current?.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background, () =>
            {
                if (_reconnectAlarmWindow != null) return;
                _reconnectAlarmWindow = new AlarmWindow("系统与PLC失去连接，正在重连...");
                _reconnectAlarmWindow.Show();
            });
        }

        private void CloseReconnectAlarm()
        {
            Application.Current?.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background, () =>
            {
                if (_reconnectAlarmWindow != null)
                {
                    _reconnectAlarmWindow.Close();
                    _reconnectAlarmWindow = null;
                }
            });
        }

        private void OnDatabaseError(string message)
        {
            if (_dbErrorShown) return;
            _dbErrorShown = true;
            if (Application.Current?.Dispatcher.CheckAccess() == true)
                ShowDbErrorWindow(message);
            else
                Application.Current?.Dispatcher.BeginInvoke(() => ShowDbErrorWindow(message));
        }

        private void ShowDbErrorWindow(string message)
        {
            if (_dbErrorAlarmWindow != null) return;
            _dbErrorAlarmWindow = new AlarmWindow(message);
            _dbErrorAlarmWindow.Closed += (s, e) => _dbErrorAlarmWindow = null;
            _dbErrorAlarmWindow.Owner = Application.Current?.MainWindow;
            _dbErrorAlarmWindow.Show();
            _dbErrorAlarmWindow.Activate();
        }

        private void OnDatabaseRecovered()
        {
            _dbErrorShown = false;
            if (Application.Current?.Dispatcher.CheckAccess() == true)
                CloseDbErrorWindow();
            else
                Application.Current?.Dispatcher.BeginInvoke(CloseDbErrorWindow);
        }

        private void CloseDbErrorWindow()
        {
            if (_dbErrorAlarmWindow != null)
            {
                _dbErrorAlarmWindow.Close();
                _dbErrorAlarmWindow = null;
            }
        }

        public void Dispose()
        {
            Debug.WriteLine("[APPMainService] Dispose 被调用");

            if (_disposed) return;
            _disposed = true;

            Stop();

            Debug.WriteLine("[APPMainService] Dispose 完成");
        }
    }
}
