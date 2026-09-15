using Opc.Ua;
using Opc.Ua.Client;
using PrismDemo.Core.Interfaces;
using PrismDemo.Core.Models;
using PrismDemo.Core.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PrismDemo.Core.Services
{
    /// <summary>
    /// OPC UA 客户端服务类
    /// 提供OPC UA服务器的连接、读写操作和实时数据监测功能
    /// </summary>
    public class OpcServices : IOpcService, IDisposable
    {
        private Session _session;
        private bool _disposed = false;
        private string _opcAddress;
        private readonly HashSet<string> _badNodeIds = new HashSet<string>();
        private readonly object _openLock = new object();

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="opcAddress">OPC UA 服务器地址，默认为 opc.tcp://127.0.0.1:4840</param>
        public OpcServices(string opcAddress = "opc.tcp://127.0.0.1:4840")
        {
            _opcAddress = opcAddress ?? "opc.tcp://127.0.0.1:4840";
        }

        /// <summary>
        /// 更新OPC UA服务器地址（重新加载配置后调用）。
        /// 会先断开旧会话，确保下次 Open 时使用新端点重新连接。
        /// </summary>
        /// <param name="opcAddress">OPC UA 服务器地址</param>
        public void Reconfigure(string opcAddress)
        {
            if (string.IsNullOrWhiteSpace(opcAddress)) return;

            lock (_openLock)
            {
                Disconnect();
                _opcAddress = opcAddress;
                Debug.WriteLine($"[Core.OpcServices] 已更新OPC端点: {_opcAddress}");
            }
        }

        /// <summary>
        /// 同步连接到OPC UA服务器（2025-03-27 改动：将异步改为同步，添加超时处理）
        /// </summary>
        /// <param name="timeoutMs">连接超时时间（毫秒），默认30000ms</param>
        /// <returns>连接是否成功</returns>
        /// <exception cref="OpcConnectionException">连接失败时抛出</exception>
        public bool Open(int timeoutMs = 30000)
        {
            // 加锁防止多线程并发重连时重复创建 Session 互相覆盖泄漏
            lock (_openLock)
            {
                try
                {
                    if (_session != null && _session.Connected)
                    {
                        Debug.WriteLine("OPC 服务器已连接");
                        return true;
                    }

                    Debug.WriteLine($"[Core.OpcServices] 开始连接OPC服务器，超时时间: {timeoutMs}ms");

                    // 先释放旧的失效会话，避免直接覆盖造成泄漏
                    DisposeSession();

                    var configTask = CreateApplicationConfigurationAsync();
                    if (!configTask.Wait(timeoutMs))
                    {
                        Debug.WriteLine($"[Core.OpcServices] 创建配置超时 ({timeoutMs}ms)，连接失败");
                        return false;
                    }

                    var sessionTask = CreateSessionAsync(configTask.Result, _opcAddress);
                    if (!sessionTask.Wait(timeoutMs))
                    {
                        Debug.WriteLine($"[Core.OpcServices] 创建会话超时 ({timeoutMs}ms)，连接失败");
                        // 超时返回后后台任务仍可能成功创建 Session，必须在其完成后关闭并释放，避免孤儿 Session 泄漏
                        sessionTask.ContinueWith(t =>
                        {
                            try { t.Result?.CloseAsync(CancellationToken.None).Wait(5000); } catch { }
                            try { t.Result?.Dispose(); } catch { }
                        }, TaskContinuationOptions.OnlyOnRanToCompletion);
                        return false;
                    }

                    _session = sessionTask.Result;

                    if (_session.Connected)
                    {
                        Debug.WriteLine("OPC 服务器连接成功");
                        return true;
                    }
                    else
                    {
                        Debug.WriteLine("OPC 服务器连接失败");
                        DisposeSession();
                        return false;
                    }
                }
                catch (AggregateException ae)
                {
                    var ex = ae.InnerException ?? ae;
                    Debug.WriteLine($"OPC 服务器连接异常：{ex.Message}");
                    throw new OpcConnectionException($"OPC 服务器连接失败：{ex.Message}", ex);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OPC 服务器连接异常：{ex.Message}");
                    throw new OpcConnectionException($"OPC 服务器连接失败：{ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// 创建应用程序配置
        /// </summary>
        /// <returns>OPC UA应用程序配置</returns>
        private async Task<ApplicationConfiguration> CreateApplicationConfigurationAsync()
        {
            return await Task.Run(() =>
            {
                var config = new ApplicationConfiguration
                {
                    ApplicationName = "PrismDemo OPC Client",
                    ApplicationType = ApplicationType.Client,
                    ApplicationUri = "urn:PrismDemo:OPCClient",
                    SecurityConfiguration = new SecurityConfiguration
                    {
                        ApplicationCertificate = new CertificateIdentifier
                        {
                            StoreType = CertificateStoreType.Directory,
                            StorePath = "%CommonApplicationData%\\OPC Foundation\\CertificateStores\\MachineDefault",
                            SubjectName = "PrismDemo OPC Client"
                        },
                        TrustedPeerCertificates = new CertificateTrustList
                        {
                            StoreType = CertificateStoreType.Directory,
                            StorePath = "%CommonApplicationData%\\OPC Foundation\\CertificateStores\\UA Applications"
                        },
                        TrustedIssuerCertificates = new CertificateTrustList
                        {
                            StoreType = CertificateStoreType.Directory,
                            StorePath = "%CommonApplicationData%\\OPC Foundation\\CertificateStores\\UA Certificate Authorities"
                        },
                        RejectedCertificateStore = new CertificateTrustList
                        {
                            StoreType = CertificateStoreType.Directory,
                            StorePath = "%CommonApplicationData%\\OPC Foundation\\CertificateStores\\RejectedCertificates"
                        },
                        AutoAcceptUntrustedCertificates = true,
                        AddAppCertToTrustedStore = true,
                        RejectSHA1SignedCertificates = false,
                        MinimumCertificateKeySize = 1024
                    },
                    TransportConfigurations = new TransportConfigurationCollection(),
                    TransportQuotas = new TransportQuotas
                    {
                        OperationTimeout = 15000,
                        MaxStringLength = 1048576,
                        MaxByteStringLength = 1048576,
                        MaxArrayLength = 65535,
                        MaxMessageSize = 4194304,
                        MaxBufferSize = 65535,
                        ChannelLifetime = 300000,
                        SecurityTokenLifetime = 3600000
                    },
                    ClientConfiguration = new ClientConfiguration
                    {
                        DefaultSessionTimeout = 60000,
                        MinSubscriptionLifetime = 10000
                    },
                    TraceConfiguration = new Opc.Ua.TraceConfiguration()
                };

                return config;
            });
        }

        /// <summary>
        /// 创建OPC UA会话
        /// </summary>
        /// <param name="config">应用程序配置</param>
        /// <param name="endpointUrl">服务器端点URL</param>
        /// <returns>OPC UA会话实例</returns>
        private async Task<Session> CreateSessionAsync(ApplicationConfiguration config, string endpointUrl)
        {
            var selectedEndpoint = CoreClientUtils.SelectEndpoint(config, endpointUrl, false);

            Debug.WriteLine($"选择端点：{selectedEndpoint.EndpointUrl}");
            Debug.WriteLine($"安全模式：{selectedEndpoint.SecurityMode}");
            Debug.WriteLine($"安全策略：{selectedEndpoint.SecurityPolicyUri}");

            var session = Session.Create(
                config,
                new ConfiguredEndpoint(null, selectedEndpoint),
                false,
                "PrismDemo Session",
                60000,
                new UserIdentity(new AnonymousIdentityToken()),
                null).Result;

            return session;
        }

        /// <summary>
        /// 断开与OPC UA服务器的连接
        /// </summary>
        public void Disconnect()
        {
            if (_session != null)
            {
                try
                {
                    DisposeSession();
                    Debug.WriteLine("OPC 服务器已断开");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"断开连接异常：{ex.Message}");
                }
            }
        }

        /// <summary>
        /// 关闭并释放当前会话（不抛出异常）
        /// </summary>
        private void DisposeSession()
        {
            if (_session == null) return;
            try { _session.CloseAsync(CancellationToken.None).Wait(5000); } catch { }
            try { _session.Dispose(); } catch { }
            _session = null;
        }

        /// <summary>
        /// 检查当前连接状态
        /// </summary>
        /// <returns>是否已连接</returns>
        public bool IsConnected()
        {
            return _session?.Connected ?? false;
        }

        bool IOpcService.IsConnected => IsConnected();

        public bool IsConnectedProperty => IsConnected();

        /// <summary>
        /// 验证连接状态，确保会话已初始化并连接
        /// </summary>
        /// <exception cref="InvalidOperationException">会话未初始化或未连接时抛出</exception>
        private void ValidateConnection()
        {
            if (_session == null)
            {
                throw new InvalidOperationException("OPC 客户端未初始化，请先调用 Open");
            }

            if (!_session.Connected)
            {
                throw new InvalidOperationException("OPC 服务器未连接，请先调用 Open");
            }
        }

        /// <summary>
        /// 解析节点ID字符串
        /// </summary>
        /// <param name="node">节点地址字符串</param>
        /// <returns>解析后的NodeId</returns>
        private NodeId ParseNodeId(string node)
        {
            if (node.StartsWith("ns=") || node.StartsWith("i=") || node.StartsWith("s="))
            {
                return NodeId.Parse(node);
            }
            return new NodeId(node);
        }

        /// <summary>
        /// 写入Float类型值到指定节点
        /// </summary>
        /// <param name="node">节点地址</param>
        /// <param name="value">要写入的值</param>
        /// <returns>写入是否成功</returns>
        /// <exception cref="OpcWriteException">写入失败时抛出</exception>
        public bool WriteNode(string node, float value, CancellationToken token = default)
        {
            return WriteNode<float>(node, value, token);
        }

        /// <summary>
        /// 泛型方法写入值到指定节点
        /// </summary>
        /// <typeparam name="T">值的数据类型</typeparam>
        /// <param name="node">节点地址</param>
        /// <param name="value">要写入的值</param>
        /// <returns>写入是否成功</returns>
        /// <exception cref="OpcWriteException">写入失败时抛出</exception>
        public bool WriteNode<T>(string node, T value, CancellationToken token = default)
        {
            try
            {
                ValidateConnection();
                var nodeId = ParseNodeId(node);
                var writeValue = new WriteValue
                {
                    NodeId = nodeId,
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(value))
                };
                var writeValues = new WriteValueCollection { writeValue };
                var result = _session.WriteAsync(null, writeValues, token).GetAwaiter().GetResult();

                if (result.Results == null || result.Results.Count == 0 || StatusCode.IsGood(result.Results[0]))
                {
                    return true;
                }
                Debug.WriteLine($"写入节点 {node} 失败：{result.Results[0]}");
                return false;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"写入节点 {node} 被取消");
                return false;
            }
            catch (ServiceResultException ex)
            {
                //if (ex.StatusCode == StatusCodes.BadNodeIdUnknown && _badNodeIds.Add(node))
                //{
                //    var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BadNodes.log");
                //    File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} BadNodeIdUnknown | 写入节点: {node} | {ex.Message}{Environment.NewLine}");
                //}
                Debug.WriteLine($"写入节点 {node} 失败：{ex.Message} (StatusCode={ex.StatusCode})");
                if (IsSessionError(ex.StatusCode))
                    InvalidateSession();
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"写入节点 {node} 失败：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 尝试写入节点值，优先主地址，失败时自动使用备用地址
        /// </summary>
        /// <param name="opcAddress">主OPC地址</param>
        /// <param name="mockOpcAddress">备用OPC地址（模拟地址）</param>
        /// <param name="value">要写入的值</param>
        /// <returns>(success: 是否写入成功, primaryFailed: 主地址是否写入失败)</returns>
        public (bool success, bool primaryFailed) TryWriteValue<T>(string opcAddress, string mockOpcAddress, T value, CancellationToken token = default)
        {
            var primaryFailed = false;

            if (!string.IsNullOrEmpty(opcAddress))
            {
                if (WriteNode(opcAddress, value, token))
                    return (true, false);

                primaryFailed = true;
                Debug.WriteLine($"主地址 {opcAddress} 写入失败，尝试备用地址");
            }

            if (!string.IsNullOrEmpty(mockOpcAddress))
            {
                if (WriteNode(mockOpcAddress, value, token))
                {
                    Debug.WriteLine($"备用地址 {mockOpcAddress} 写入成功");
                    return (true, true);
                }
            }

            return (false, primaryFailed);
        }

        /// <summary>
        /// 释放OPC UA客户端资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Disconnect();
                _disposed = true;
            }
        }

        /// <summary>
        /// 保持OPC UA服务器连接
        /// 如果当前未连接或已断开，则自动尝试重新连接
        /// </summary>
        public void KeepConnected()
        {
            if (_session != null && _session.Connected)
            {
                return;
            }
            Open(); 
        }

        /// <summary>
        /// 作废当前会话（Server重启后Session表面Connected但实际已失效）
        /// </summary>
        private void InvalidateSession()
        {
            if (_session != null)
            {
                DisposeSession();
                Debug.WriteLine("[OpcServices] 会话已作废，将触发重连");
            }
        }

        private static bool IsSessionError(StatusCode statusCode)
        {
            var code = statusCode.Code;
            return code == StatusCodes.BadSessionClosed
                || code == StatusCodes.BadSessionIdInvalid
                || code == StatusCodes.BadSessionNotActivated
                || code == StatusCodes.BadSecureChannelTokenUnknown
                || code == StatusCodes.BadSecureChannelClosed
                || code == StatusCodes.BadConnectionClosed
                || code == StatusCodes.BadDisconnect
                || code == StatusCodes.BadServerHalted
                || code == StatusCodes.BadNoCommunication
                || code == StatusCodes.BadTimeout;
        }

        /// <summary>
        /// 刷新一组连接数据对象的值
        /// 优先读取OpcAddress，如果失败则读取MockOpcAddress
        /// </summary>
        /// <param name="datas">连接数据列表，包含OpcAddress和MockOpcAddress</param>
        public async Task RefreshDataAsync(List<ConnectData> datas, CancellationToken token = default)
        {
            ValidateConnection();

            for (int i = 0; i < datas.Count; i++)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    object newValue = TryReadValue(datas[i].OpcAddress, datas[i].MockOpcAddress, token);

                    if (newValue != null)
                    {
                        datas[i].Value = RoundToTwoDecimals(newValue);
                        datas[i].isvalid = true;
                    }
                    else
                    {
                        datas[i].isvalid = false;
                    }
                }
                catch (ServiceResultException ex)
                {
                    //var addr = datas[i].OpcAddress ?? "(null)";
                    //if (ex.StatusCode == StatusCodes.BadNodeIdUnknown && _badNodeIds.Add(addr))
                    //{
                    //    var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BadNodes.log");
                    //    File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} BadNodeIdUnknown | RefreshDataAsync 节点: {datas[i].Name} ({addr}) | {ex.Message}{Environment.NewLine}");
                    //}
                    Debug.WriteLine($"[RefreshDataAsync] 节点 {datas[i].Name}({datas[i].OpcAddress}) 读取失败: {ex.Message} (StatusCode={ex.StatusCode})");
                    datas[i].isvalid = false;
                }
                catch
                {
                    datas[i].isvalid = false;
                }

                if ((i + 1) % 20 == 0)
                    await Task.Yield();
            }
        }

        /// <summary>
        /// 将数值类型四舍五入保留两位小数（仅作用于 float/double/decimal）
        /// </summary>
        private static object RoundToTwoDecimals(object value)
        {
            if (value is float f) return (float)Math.Round(f, 2);
            if (value is double d) return Math.Round(d, 2);
            if (value is decimal m) return Math.Round(m, 2);
            return value;
        }

        /// <summary>
        /// 尝试读取节点值，优先主地址，失败时使用备用地址。
        /// 主/备地址读取均不走异常流控——遇到 BadNodeIdUnknown 等节点级错误时不抛异常，
        /// 只返回 null 并静默降级到下一个地址尝试，彻底消除 VS 输出窗口的首机会异常刷屏。
        /// </summary>
        private object TryReadValue(string opcAddress, string mockOpcAddress, CancellationToken token)
        {
            if (_session == null) return null;

            // 主地址
            if (!string.IsNullOrEmpty(opcAddress))
            {
                var (value, isSessionError) = TryReadSingleNode(opcAddress, token);
                if (isSessionError)
                {
                    InvalidateSession();
                    throw new ServiceResultException(StatusCodes.BadSessionClosed);
                }
                if (value != null)
                    return value;
            }

            // 备用地址
            if (!string.IsNullOrEmpty(mockOpcAddress))
            {
                var (value, isSessionError) = TryReadSingleNode(mockOpcAddress, token);
                if (isSessionError)
                {
                    InvalidateSession();
                    throw new ServiceResultException(StatusCodes.BadSessionClosed);
                }
                if (value != null)
                    return value;
            }

            return null;
        }

        /// <summary>
        /// 读取单个节点的值。使用 Session.Read（而非 ReadValueAsync），该 API 对不良 StatusCode
        /// 不抛异常，只在返回的 DataValue 中标记状态码。调用方自行区分节点级错误和会话级错误。
        /// </summary>
        /// <returns>(value: 读到的值 / null, isSessionError: 是否需作废会话)</returns>
        private (object value, bool isSessionError) TryReadSingleNode(string nodeAddress, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var nodeId = ParseNodeId(nodeAddress);
            var nodesToRead = new ReadValueIdCollection
            {
                new ReadValueId { NodeId = nodeId, AttributeId = Attributes.Value }
            };

            try
            {
                _session.Read(
                    null,                              // requestHeader
                    0,                                 // maxAge
                    TimestampsToReturn.Neither,
                    nodesToRead,
                    out var results,
                    out var _);

                if (results == null || results.Count == 0)
                    return (null, false);

                var dataValue = results[0];
                if (StatusCode.IsGood(dataValue.StatusCode))
                    return (dataValue.Value, false);

                if (IsSessionError(dataValue.StatusCode))
                    return (null, true);

                return (null, false);
            }
            catch (ServiceResultException ex)
            {
                if (IsSessionError(ex.StatusCode))
                    return (null, true);
                return (null, false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return (null, false);
            }
        }
    }

    /// <summary>
    /// OPC连接异常类
    /// </summary>
    public class OpcConnectionException : Exception
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="message">异常消息</param>
        public OpcConnectionException(string message) : base(message) { }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="message">异常消息</param>
        /// <param name="inner">内部异常</param>
        public OpcConnectionException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// OPC写入异常类
    /// </summary>
    public class OpcWriteException : Exception
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="message">异常消息</param>
        public OpcWriteException(string message) : base(message) { }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="message">异常消息</param>
        /// <param name="inner">内部异常</param>
        public OpcWriteException(string message, Exception inner) : base(message, inner) { }
    }
}