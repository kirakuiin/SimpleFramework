using System.Collections.Concurrent;

namespace SimpleFramework.Net;

/// <summary>
/// 用于单元测试的内存网络，负责在同一进程内配对传输端。
/// </summary>
public sealed class MemoryNetNetwork
{
    private readonly object _gate = new();
    private readonly Dictionary<(string Host, int Port), MemoryNetTransport> _servers = new();
    private ulong _nextConnectionId;

    /// <summary>
    /// 创建一个命名的内存传输端。
    /// </summary>
    /// <param name="name">传输端名称，客户端连接时作为主机名使用。</param>
    public MemoryNetTransport CreateTransport(string name) => new(this, name);

    internal TransportStartResult RegisterServer(MemoryNetTransport transport, int port)
    {
        lock (_gate)
        {
            var key = (transport.Name, port);
            if (_servers.ContainsKey(key))
                return new TransportStartResult(NetTransportStatus.AlreadyRunning, "A memory transport is already listening on this endpoint.");

            _servers[key] = transport;
            return new TransportStartResult(NetTransportStatus.Ok);
        }
    }

    internal void UnregisterServer(MemoryNetTransport transport)
    {
        lock (_gate)
        {
            foreach (var pair in _servers.Where(pair => ReferenceEquals(pair.Value, transport)).ToArray())
                _servers.Remove(pair.Key);
        }
    }

    internal TransportConnectResult Connect(MemoryNetTransport client, string host, int port)
    {
        MemoryNetTransport server;
        TransportConnectionId clientId;
        TransportConnectionId serverId;
        var connectedPublished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_gate)
        {
            if (!_servers.TryGetValue((host, port), out server!))
                return new TransportConnectResult(NetTransportStatus.ConnectionRefused, TransportConnectionId.None, "No memory server is listening on this endpoint.");
            if (!client.CanCreateConnection || !server.CanAcceptConnections)
                return new TransportConnectResult(NetTransportStatus.ConnectionRefused, TransportConnectionId.None, "The memory transport endpoint is not running.");

            clientId = new TransportConnectionId(++_nextConnectionId);
            serverId = new TransportConnectionId(++_nextConnectionId);
            client.AddConnection(clientId, server, serverId, connectedPublished.Task);
            server.AddConnection(serverId, client, clientId, connectedPublished.Task);
        }

        try
        {
            server.DispatchPeerConnected(serverId);
        }
        finally
        {
            connectedPublished.TrySetResult();
        }

        if (!client.HasConnection(clientId))
        {
            var status = client.CanCreateConnection
                ? NetTransportStatus.ConnectionRefused
                : NetTransportStatus.ObjectDisposed;
            return new TransportConnectResult(status, TransportConnectionId.None, "Connection closed before connect completed.");
        }

        return new TransportConnectResult(NetTransportStatus.Ok, clientId);
    }
}

/// <summary>
/// 确定性的内存传输实现，适合无 Socket 的会话和消息测试。
/// </summary>
public sealed class MemoryNetTransport : INetTransport
{
    [ThreadStatic]
    private static HashSet<Task>? _publishingConnections;

    private readonly MemoryNetNetwork _network;
    private readonly ConcurrentDictionary<TransportConnectionId, MemoryConnection> _connections = new();
    private readonly object _lifecycleGate = new();
    private readonly object _disposeGate = new();
    private readonly SemaphoreSlim _shutdownGate = new(1, 1);
    private int _disposed;
    private int _running;
    private int _maxConnections;
    private Task? _disposeTask;

    internal MemoryNetTransport(MemoryNetNetwork network, string name)
    {
        _network = network;
        Name = name;
    }

    internal string Name { get; }

    /// <inheritdoc />
    public event Action<TransportPeerConnected>? PeerConnected;

    /// <inheritdoc />
    public event Action<TransportPeerDisconnected>? PeerDisconnected;

    /// <inheritdoc />
    public event Action<TransportPacketReceived>? PacketReceived;

    /// <inheritdoc />
    public event Action<TransportError>? Error;

    /// <inheritdoc />
    public Task<TransportStartResult> StartServerAsync(NetListenOptions options, CancellationToken token = default)
    {
        if (options.Port <= 0 || options.Port > 65535)
            return Task.FromResult(new TransportStartResult(NetTransportStatus.InvalidEndpoint, "Port must be between 1 and 65535."));
        if (options.MaxConnections <= 0)
            return Task.FromResult(new TransportStartResult(NetTransportStatus.InvalidEndpoint, "MaxConnections must be greater than zero."));

        lock (_lifecycleGate)
        {
            if (IsDisposed)
                return Task.FromResult(new TransportStartResult(NetTransportStatus.ObjectDisposed));
            if (token.IsCancellationRequested)
                return Task.FromResult(new TransportStartResult(NetTransportStatus.Cancelled));
            if (Volatile.Read(ref _running) == 1)
                return Task.FromResult(new TransportStartResult(NetTransportStatus.AlreadyRunning));

            var result = _network.RegisterServer(this, options.Port);
            if (result.Status == NetTransportStatus.Ok)
            {
                Volatile.Write(ref _maxConnections, options.MaxConnections);
                Volatile.Write(ref _running, 1);
            }
            return Task.FromResult(result);
        }
    }

    /// <inheritdoc />
    public Task<TransportConnectResult> ConnectAsync(NetConnectOptions options, CancellationToken token = default)
    {
        if (IsDisposed)
            return Task.FromResult(new TransportConnectResult(NetTransportStatus.ObjectDisposed, TransportConnectionId.None));
        if (token.IsCancellationRequested)
            return Task.FromResult(new TransportConnectResult(NetTransportStatus.Cancelled, TransportConnectionId.None));
        if (string.IsNullOrWhiteSpace(options.Host) || options.Port <= 0 || options.Port > 65535)
        {
            DispatchError(TransportConnectionId.None, "Host and port must be valid.");
            return Task.FromResult(new TransportConnectResult(NetTransportStatus.InvalidEndpoint, TransportConnectionId.None, "Host and port must be valid."));
        }
        if (options.Timeout <= TimeSpan.Zero)
            return Task.FromResult(new TransportConnectResult(NetTransportStatus.InvalidEndpoint, TransportConnectionId.None, "Timeout must be greater than zero."));

        return Task.FromResult(_network.Connect(this, options.Host, options.Port));
    }

    /// <inheritdoc />
    public async Task<TransportStartResult> StopServerAsync(CancellationToken token = default)
    {
        try
        {
            await _shutdownGate.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new TransportStartResult(NetTransportStatus.Cancelled);
        }

        try
        {
            lock (_lifecycleGate)
            {
                if (IsDisposed)
                    return new TransportStartResult(NetTransportStatus.ObjectDisposed);

                _network.UnregisterServer(this);
                Volatile.Write(ref _running, 0);
                Volatile.Write(ref _maxConnections, 0);
            }

            foreach (var connectionId in _connections.Keys.ToArray())
                await DisconnectAsync(connectionId, DisconnectReason.ServerClosed).ConfigureAwait(false);

            return new TransportStartResult(NetTransportStatus.Ok);
        }
        finally
        {
            _shutdownGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(TransportConnectionId connectionId, DisconnectReason reason = DisconnectReason.LocalClosed)
    {
        if (_connections.TryRemove(connectionId, out var connection))
        {
            if (_publishingConnections?.Contains(connection.ConnectedPublished) == true)
            {
                var remoteRemoved = connection.Remote._connections.TryRemove(connection.RemoteConnectionId, out _);
                _ = DispatchDeferredDisconnectAsync(connectionId, connection, reason, remoteRemoved);
                return;
            }

            await connection.ConnectedPublished.ConfigureAwait(false);
            await connection.Remote.RemoveRemoteConnectionAsync(connection.RemoteConnectionId, ToRemoteReason(reason)).ConfigureAwait(false);
            DispatchPeerDisconnected(connectionId, reason);
        }
    }

    /// <inheritdoc />
    public ValueTask<NetSendResult> SendAsync(TransportConnectionId connectionId, ReadOnlyMemory<byte> data, NetChannel channel, CancellationToken token = default)
    {
        if (IsDisposed)
            return ValueTask.FromResult(new NetSendResult(NetSendStatus.ObjectDisposed));
        if (token.IsCancellationRequested)
            return ValueTask.FromResult(new NetSendResult(NetSendStatus.TransportFailed, "Send was cancelled."));
        if (channel == NetChannel.Unreliable)
            return ValueTask.FromResult(new NetSendResult(NetSendStatus.ChannelUnsupported));
        if (!_connections.TryGetValue(connectionId, out var connection))
            return ValueTask.FromResult(new NetSendResult(NetSendStatus.ConnectionUnavailable));

        var payload = data.ToArray();
        connection.Remote.DispatchPacketReceived(connection.RemoteConnectionId, payload, channel);
        return ValueTask.FromResult(NetSendResult.Ok());
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_disposeGate)
        {
            if (_disposeTask is null)
            {
                Volatile.Write(ref _disposed, 1);
                _disposeTask = DisposeCoreAsync();
            }

            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        await _shutdownGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            lock (_lifecycleGate)
            {
                _network.UnregisterServer(this);
                Volatile.Write(ref _running, 0);
                Volatile.Write(ref _maxConnections, 0);
            }

            foreach (var connectionId in _connections.Keys.ToArray())
                await DisconnectAsync(connectionId).ConfigureAwait(false);
        }
        finally
        {
            _shutdownGate.Release();
        }
    }

    internal void AddConnection(
        TransportConnectionId localId,
        MemoryNetTransport remote,
        TransportConnectionId remoteId,
        Task connectedPublished)
    {
        _connections[localId] = new MemoryConnection(remote, remoteId, connectedPublished);
    }

    internal async Task RemoveRemoteConnectionAsync(TransportConnectionId localId, DisconnectReason reason)
    {
        if (_connections.TryRemove(localId, out var connection))
        {
            await connection.ConnectedPublished.ConfigureAwait(false);
            DispatchPeerDisconnected(localId, reason);
        }
    }

    internal void RemoveRemoteConnection(TransportConnectionId localId, DisconnectReason reason) =>
        RemoveRemoteConnectionAsync(localId, reason).GetAwaiter().GetResult();

    internal void DispatchPeerConnected(TransportConnectionId connectionId)
    {
        var callbacks = PeerConnected?.GetInvocationList();
        if (callbacks is null)
            return;

        var message = new TransportPeerConnected(connectionId);
        var connection = _connections.GetValueOrDefault(connectionId);
        var publishing = _publishingConnections ??= new HashSet<Task>();
        publishing.Add(connection.ConnectedPublished);
        try
        {
            foreach (Action<TransportPeerConnected> callback in callbacks)
            {
                try
                {
                    callback(message);
                }
                catch (Exception ex)
                {
                    DispatchError(connectionId, "PeerConnected handler failed.", ex);
                }
            }
        }
        finally
        {
            publishing.Remove(connection.ConnectedPublished);
        }
    }

    private async Task DispatchDeferredDisconnectAsync(
        TransportConnectionId connectionId,
        MemoryConnection connection,
        DisconnectReason reason,
        bool remoteRemoved)
    {
        await connection.ConnectedPublished.ConfigureAwait(false);
        if (remoteRemoved)
            connection.Remote.DispatchPeerDisconnected(connection.RemoteConnectionId, ToRemoteReason(reason));
        DispatchPeerDisconnected(connectionId, reason);
    }

    private void DispatchPeerDisconnected(TransportConnectionId connectionId, DisconnectReason reason)
    {
        var callbacks = PeerDisconnected?.GetInvocationList();
        if (callbacks is null)
            return;

        var message = new TransportPeerDisconnected(connectionId, reason);
        foreach (Action<TransportPeerDisconnected> callback in callbacks)
        {
            try
            {
                callback(message);
            }
            catch (Exception ex)
            {
                DispatchError(connectionId, "PeerDisconnected handler failed.", ex);
            }
        }
    }

    private void DispatchPacketReceived(TransportConnectionId connectionId, byte[] data, NetChannel channel)
    {
        var callbacks = PacketReceived?.GetInvocationList();
        if (callbacks is null)
            return;

        var message = new TransportPacketReceived(connectionId, data, channel);
        foreach (Action<TransportPacketReceived> callback in callbacks)
        {
            try
            {
                callback(message);
            }
            catch (Exception ex)
            {
                DispatchError(connectionId, "PacketReceived handler failed.", ex);
            }
        }
    }

    private void DispatchError(TransportConnectionId connectionId, string message, Exception? exception = null)
    {
        var callbacks = Error?.GetInvocationList();
        if (callbacks is null)
            return;

        var error = new TransportError(connectionId, message, exception);
        _ = Task.Run(() =>
        {
            foreach (Action<TransportError> callback in callbacks)
            {
                try
                {
                    callback(error);
                }
                catch
                {
                }
            }
        });
    }

    private static DisconnectReason ToRemoteReason(DisconnectReason reason) => reason switch
    {
        DisconnectReason.LocalClosed => DisconnectReason.RemoteClosed,
        _ => reason
    };

    private bool IsDisposed => Volatile.Read(ref _disposed) == 1;

    internal bool CanCreateConnection => !IsDisposed;
    internal bool HasConnection(TransportConnectionId connectionId) => _connections.ContainsKey(connectionId);
    internal bool CanAcceptConnections =>
        !IsDisposed &&
        Volatile.Read(ref _running) == 1 &&
        _connections.Count < Volatile.Read(ref _maxConnections);

    private readonly record struct MemoryConnection(
        MemoryNetTransport Remote,
        TransportConnectionId RemoteConnectionId,
        Task ConnectedPublished);
}
