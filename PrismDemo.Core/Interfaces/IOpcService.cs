using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PrismDemo.Core.Models;

namespace PrismDemo.Core.Interfaces
{
    public interface IOpcService : IDisposable
    {
        bool Open(int timeoutMs = 15000);
        void Disconnect();
        bool IsConnected { get; }
        bool WriteNode<T>(string node, T value, CancellationToken token = default);
        (bool success, bool primaryFailed) TryWriteValue<T>(string opcAddress, string mockOpcAddress, T value, CancellationToken token = default);
        Task RefreshDataAsync(List<ConnectData> datas, CancellationToken token = default);
    }
}
