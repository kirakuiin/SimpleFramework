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

        lock (_gate)
        {
            if (!_servers.TryGetValue((host, port), out server!))
                return new TransportConnectResult(NetTransportStatus.ConnectionRefused, TransportConnectionId.None, "No memory server is listening on this endpoint.");

            clientId = new TransportConnectionId(++_nextConnectionId);
            serverId = new TransportConnectionId(++_nextConnectionId);
        }

        client.AddConnection(clientId, server, serverId);
        server.AddConnection(serverId, client, clientId);
        server.DispatchPeerConnected(serverId);

        return new TransportConnectResult(NetTransportStatus.Ok, clientId);
    }
}

/// <summary>
/// 确定性的内存传输实现，适合无 Socket 的会话和消息测试。
/// </summary>
public sealed class MemoryNetTransport : INetTransport
{
    private readonly MemoryNetNetwork _network;
    private readonly ConcurrentDictionary<TransportConnectionId, MemoryConnection> _connections = new();
    private int _disposed;
    private int _running;

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
        if (IsDisposed)
            return Task.FromResult(new TransportStartResult(NetTransportStatus.ObjectDisposed));
        if (token.IsCancellationRequested)
            return Task.FromResult(new TransportStartResult(NetTransportStatus.Cancelled));
        if (options.Port <= 0)
            return Task.FromResult(new TransportStartResult(NetTransportStatus.InvalidEndpoint, "Port must be greater than zero."));
        if (Interlocked.Exchange(ref _running, 1) == 1)
            return Task.FromResult(new TransportStartResult(NetTransportStatus.AlreadyRunning));

        var result = _network.RegisterServer(this, options.Port);
        if (result.Status != NetTransportStatus.Ok)
            Interlocked.Exchange(ref _running, 0);

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<TransportConnectResult> ConnectAsync(NetConnectOptions options, CancellationToken token = default)
    {
        if (IsDisposed)
            return Task.FromResult(new TransportConnectResult(NetTransportStatus.ObjectDisposed, TransportConnectionId.None));
        if (token.IsCancellationRequested)
            return Task.FromResult(new TransportConnectResult(NetTransportStatus.Cancelled, TransportConnectionId.None));
        if (string.IsNullOrWhiteSpace(options.Host) || options.Port <= 0)
        {
            DispatchError(TransportConnectionId.None, "Host and port must be valid.");
            return Task.FromResult(new TransportConnectResult(NetTransportStatus.InvalidEndpoint, TransportConnectionId.None, "Host and port must be valid."));
        }

        return Task.FromResult(_network.Connect(this, options.Host, options.Port));
    }

    /// <inheritdoc />
    public Task DisconnectAsync(TransportConnectionId connectionId, DisconnectReason reason = DisconnectReason.LocalClosed)
    {
        if (_connections.TryRemove(connectionId, out var connection))
            connection.Remote.RemoveRemoteConnection(connection.RemoteConnectionId, ToRemoteReason(reason));

        return Task.CompletedTask;
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
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        _network.UnregisterServer(this);

        foreach (var connectionId in _connections.Keys.ToArray())
            await DisconnectAsync(connectionId).ConfigureAwait(false);
    }

    internal void AddConnection(TransportConnectionId localId, MemoryNetTransport remote, TransportConnectionId remoteId)
    {
        _connections[localId] = new MemoryConnection(remote, remoteId);
    }

    internal void RemoveRemoteConnection(TransportConnectionId localId, DisconnectReason reason)
    {
        if (_connections.TryRemove(localId, out _))
            DispatchPeerDisconnected(localId, reason);
    }

    internal void DispatchPeerConnected(TransportConnectionId connectionId)
    {
        Task.Run(() => PeerConnected?.Invoke(new TransportPeerConnected(connectionId)));
    }

    private void DispatchPeerDisconnected(TransportConnectionId connectionId, DisconnectReason reason)
    {
        Task.Run(() => PeerDisconnected?.Invoke(new TransportPeerDisconnected(connectionId, reason)));
    }

    private void DispatchPacketReceived(TransportConnectionId connectionId, byte[] data, NetChannel channel)
    {
        Task.Run(() => PacketReceived?.Invoke(new TransportPacketReceived(connectionId, data, channel)));
    }

    private void DispatchError(TransportConnectionId connectionId, string message, Exception? exception = null)
    {
        Task.Run(() => Error?.Invoke(new TransportError(connectionId, message, exception)));
    }

    private static DisconnectReason ToRemoteReason(DisconnectReason reason) => reason switch
    {
        DisconnectReason.LocalClosed => DisconnectReason.RemoteClosed,
        _ => reason
    };

    private bool IsDisposed => Volatile.Read(ref _disposed) == 1;

    private readonly record struct MemoryConnection(MemoryNetTransport Remote, TransportConnectionId RemoteConnectionId);
}
