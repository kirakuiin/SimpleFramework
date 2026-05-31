using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace SimpleFramework.Net;

/// <summary>
/// 基于 TCP 的传输实现，使用长度前缀帧拆分网络包。
/// </summary>
public sealed class TcpNetTransport : INetTransport
{
    private readonly ConcurrentDictionary<TransportConnectionId, TcpConnection> _connections = new();
    private readonly CancellationTokenSource _disposeCts = new();
    private long _nextConnectionId;
    private TcpListener? _listener;
    private Task? _acceptTask;
    private int _disposed;
    private int _running;

    public TcpNetTransport(int maxFrameSize = 64 * 1024)
    {
        if (maxFrameSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxFrameSize), "Max frame size must be greater than zero.");

        MaxFrameSize = maxFrameSize;
    }

    /// <summary>
    /// 读取 TCP 帧前允许的最大声明长度，应与上层 MaxPacketSize 保持一致。
    /// </summary>
    public int MaxFrameSize { get; set; }

    /// <summary>
    /// 监听成功后的本地终结点；端口为 0 时可从这里读取实际端口。
    /// </summary>
    public IPEndPoint? LocalEndPoint { get; private set; }

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
        if (options.Port < 0 || options.Port > 65535)
            return Task.FromResult(new TransportStartResult(NetTransportStatus.InvalidEndpoint, "Port must be between 0 and 65535."));
        if (Interlocked.Exchange(ref _running, 1) == 1)
            return Task.FromResult(new TransportStartResult(NetTransportStatus.AlreadyRunning));

        try
        {
            var bindAddress = options.BindAddress ?? IPAddress.Any;
            _listener = new TcpListener(bindAddress, options.Port);
            _listener.Start(options.MaxConnections);
            LocalEndPoint = (IPEndPoint)_listener.LocalEndpoint;
            _acceptTask = AcceptLoopAsync(_disposeCts.Token);
            return Task.FromResult(new TransportStartResult(NetTransportStatus.Ok));
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
            Interlocked.Exchange(ref _running, 0);
            DispatchError(TransportConnectionId.None, ex.Message, ex);
            return Task.FromResult(new TransportStartResult(NetTransportStatus.TransportFailed, ex.Message));
        }
    }

    /// <inheritdoc />
    public async Task<TransportConnectResult> ConnectAsync(NetConnectOptions options, CancellationToken token = default)
    {
        if (IsDisposed)
            return new TransportConnectResult(NetTransportStatus.ObjectDisposed, TransportConnectionId.None);
        if (token.IsCancellationRequested)
            return new TransportConnectResult(NetTransportStatus.Cancelled, TransportConnectionId.None);
        if (string.IsNullOrWhiteSpace(options.Host) || options.Port <= 0 || options.Port > 65535)
            return new TransportConnectResult(NetTransportStatus.InvalidEndpoint, TransportConnectionId.None, "Host and port must be valid.");

        using var timeoutCts = new CancellationTokenSource(options.Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token, _disposeCts.Token);
        var tcpClient = new TcpClient();

        try
        {
            await tcpClient.ConnectAsync(options.Host, options.Port, linkedCts.Token).ConfigureAwait(false);
            var connectionId = NextConnectionId();
            AddConnection(connectionId, tcpClient);
            return new TransportConnectResult(NetTransportStatus.Ok, connectionId);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !token.IsCancellationRequested)
        {
            tcpClient.Dispose();
            return new TransportConnectResult(NetTransportStatus.Timeout, TransportConnectionId.None);
        }
        catch (OperationCanceledException)
        {
            tcpClient.Dispose();
            return new TransportConnectResult(NetTransportStatus.Cancelled, TransportConnectionId.None);
        }
        catch (SocketException ex)
        {
            tcpClient.Dispose();
            return new TransportConnectResult(NetTransportStatus.ConnectionRefused, TransportConnectionId.None, ex.Message);
        }
        catch (Exception ex)
        {
            tcpClient.Dispose();
            DispatchError(TransportConnectionId.None, ex.Message, ex);
            return new TransportConnectResult(NetTransportStatus.TransportFailed, TransportConnectionId.None, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<TransportStartResult> StopServerAsync(CancellationToken token = default)
    {
        if (IsDisposed)
            return new TransportStartResult(NetTransportStatus.ObjectDisposed);
        if (token.IsCancellationRequested)
            return new TransportStartResult(NetTransportStatus.Cancelled);

        _listener?.Stop();
        _listener = null;
        LocalEndPoint = null;

        foreach (var connectionId in _connections.Keys.ToArray())
            await DisconnectAsync(connectionId, DisconnectReason.ServerClosed).ConfigureAwait(false);

        if (_acceptTask is not null)
        {
            try
            {
                await _acceptTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        _acceptTask = null;
        Interlocked.Exchange(ref _running, 0);
        return new TransportStartResult(NetTransportStatus.Ok);
    }

    /// <inheritdoc />
    public Task DisconnectAsync(TransportConnectionId connectionId, DisconnectReason reason = DisconnectReason.LocalClosed)
    {
        if (_connections.TryRemove(connectionId, out var connection))
        {
            connection.Dispose();
            DispatchPeerDisconnected(connectionId, reason);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask<NetSendResult> SendAsync(TransportConnectionId connectionId, ReadOnlyMemory<byte> data, NetChannel channel, CancellationToken token = default)
    {
        if (IsDisposed)
            return new NetSendResult(NetSendStatus.ObjectDisposed);
        if (token.IsCancellationRequested)
            return new NetSendResult(NetSendStatus.TransportFailed, "Send was cancelled.");
        if (channel == NetChannel.Unreliable)
            return new NetSendResult(NetSendStatus.ChannelUnsupported);
        if (!_connections.TryGetValue(connectionId, out var connection))
            return new NetSendResult(NetSendStatus.ConnectionUnavailable);

        try
        {
            await connection.WriteLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await WriteFrameAsync(connection.Stream, data, token).ConfigureAwait(false);
            }
            finally
            {
                connection.WriteLock.Release();
            }

            return NetSendResult.Ok();
        }
        catch (OperationCanceledException)
        {
            return new NetSendResult(NetSendStatus.TransportFailed, "Send was cancelled.");
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            DispatchError(connectionId, ex.Message, ex);
            await DisconnectAsync(connectionId, DisconnectReason.TransportFailed).ConfigureAwait(false);
            return new NetSendResult(NetSendStatus.TransportFailed, ex.Message);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        _disposeCts.Cancel();
        _listener?.Stop();

        foreach (var connectionId in _connections.Keys.ToArray())
            await DisconnectAsync(connectionId, DisconnectReason.LocalClosed).ConfigureAwait(false);

        if (_acceptTask is not null)
        {
            try
            {
                await _acceptTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        _disposeCts.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener!.AcceptTcpClientAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception ex)
            {
                DispatchError(TransportConnectionId.None, ex.Message, ex);
                continue;
            }

            var connectionId = NextConnectionId();
            AddConnection(connectionId, client);
            DispatchPeerConnected(connectionId);
        }
    }

    private void AddConnection(TransportConnectionId connectionId, TcpClient client)
    {
        var connection = new TcpConnection(client);
        _connections[connectionId] = connection;
        _ = Task.Run(() => ReadLoopAsync(connectionId, connection, _disposeCts.Token));
    }

    private async Task ReadLoopAsync(TransportConnectionId connectionId, TcpConnection connection, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var packet = await ReadFrameAsync(connection.Stream, MaxFrameSize, token).ConfigureAwait(false);
                if (packet is null)
                    break;

                DispatchPacketReceived(connectionId, packet, NetChannel.Reliable);
            }

            if (_connections.TryRemove(connectionId, out var removed))
            {
                removed.Dispose();
                DispatchPeerDisconnected(connectionId, DisconnectReason.RemoteClosed);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or InvalidDataException)
        {
            if (_connections.TryRemove(connectionId, out var removed))
                removed.Dispose();

            DispatchError(connectionId, ex.Message, ex);
            DispatchPeerDisconnected(connectionId, ex is InvalidDataException ? DisconnectReason.TransportFailed : DisconnectReason.RemoteClosed);
        }
    }

    private static async Task WriteFrameAsync(NetworkStream stream, ReadOnlyMemory<byte> data, CancellationToken token)
    {
        var length = BitConverter.GetBytes(data.Length);
        await stream.WriteAsync(length, token).ConfigureAwait(false);
        await stream.WriteAsync(data, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
    }

    private static async Task<byte[]?> ReadFrameAsync(NetworkStream stream, int maxFrameSize, CancellationToken token)
    {
        var lengthBytes = await ReadExactlyOrNullAsync(stream, 4, token).ConfigureAwait(false);
        if (lengthBytes is null)
            return null;

        var length = BitConverter.ToInt32(lengthBytes, 0);
        if (length < 0)
            throw new InvalidDataException("Frame length cannot be negative.");
        if (length > maxFrameSize)
            throw new InvalidDataException($"Frame length {length} exceeds MaxFrameSize {maxFrameSize}.");

        return await ReadExactlyOrNullAsync(stream, length, token).ConfigureAwait(false);
    }

    private static async Task<byte[]?> ReadExactlyOrNullAsync(NetworkStream stream, int length, CancellationToken token)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), token).ConfigureAwait(false);
            if (read == 0)
                return offset == 0 ? null : throw new EndOfStreamException("TCP frame ended unexpectedly.");

            offset += read;
        }

        return buffer;
    }

    private TransportConnectionId NextConnectionId() => new((ulong)Interlocked.Increment(ref _nextConnectionId));

    private void DispatchPeerConnected(TransportConnectionId connectionId)
    {
        Task.Run(() => PeerConnected?.Invoke(new TransportPeerConnected(connectionId)));
    }

    private void DispatchPeerDisconnected(TransportConnectionId connectionId, DisconnectReason reason)
    {
        Task.Run(() => PeerDisconnected?.Invoke(new TransportPeerDisconnected(connectionId, reason)));
    }

    private void DispatchPacketReceived(TransportConnectionId connectionId, byte[] data, NetChannel channel)
    {
        PacketReceived?.Invoke(new TransportPacketReceived(connectionId, data, channel));
    }

    private void DispatchError(TransportConnectionId connectionId, string message, Exception? exception = null)
    {
        Task.Run(() => Error?.Invoke(new TransportError(connectionId, message, exception)));
    }

    private bool IsDisposed => Volatile.Read(ref _disposed) == 1;

    private sealed class TcpConnection : IDisposable
    {
        private readonly TcpClient _client;

        public TcpConnection(TcpClient client)
        {
            _client = client;
            Stream = client.GetStream();
        }

        public NetworkStream Stream { get; }
        public SemaphoreSlim WriteLock { get; } = new(1, 1);

        public void Dispose()
        {
            Stream.Dispose();
            _client.Dispose();
            WriteLock.Dispose();
        }
    }
}
