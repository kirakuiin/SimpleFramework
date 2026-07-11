using System.Collections.Concurrent;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace SimpleFramework.Net;

internal interface ITcpFrameWriter
{
    ValueTask WriteAsync(
        NetworkStream stream,
        Memory<byte> lengthBuffer,
        ReadOnlyMemory<byte> data,
        Action writeStarted,
        CancellationToken token);
}

internal sealed class DefaultTcpFrameWriter : ITcpFrameWriter
{
    public static DefaultTcpFrameWriter Instance { get; } = new();

    private DefaultTcpFrameWriter()
    {
    }

    public async ValueTask WriteAsync(
        NetworkStream stream,
        Memory<byte> lengthBuffer,
        ReadOnlyMemory<byte> data,
        Action writeStarted,
        CancellationToken token)
    {
        BinaryPrimitives.WriteInt32LittleEndian(lengthBuffer.Span, data.Length);
        writeStarted();
        await stream.WriteAsync(lengthBuffer, token).ConfigureAwait(false);
        await stream.WriteAsync(data, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
    }
}

/// <summary>
/// 基于 TCP 的传输实现，使用长度前缀帧拆分网络包。
/// </summary>
public sealed class TcpNetTransport : INetTransport
{
    private static readonly AsyncLocal<TcpConnection?> CurrentReadConnection = new();
    private readonly ConcurrentDictionary<TransportConnectionId, TcpConnection> _connections = new();
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly CancellationToken _disposeToken;
    private readonly SemaphoreSlim _serverLifecycleGate = new(1, 1);
    private readonly object _disposeGate = new();
    private readonly ITcpFrameWriter _frameWriter;
    private long _nextConnectionId;
    private TcpListener? _listener;
    private CancellationTokenSource? _acceptCts;
    private Task? _acceptTask;
    private TimeProvider _timeProvider;
    private int _maxFrameSize;
    private int _disposed;
    private int _running;
    private Task? _disposeTask;

    public TcpNetTransport(int maxFrameSize = 64 * 1024, TimeProvider? timeProvider = null)
        : this(maxFrameSize, timeProvider, DefaultTcpFrameWriter.Instance)
    {
    }

    internal TcpNetTransport(int maxFrameSize, TimeProvider? timeProvider, ITcpFrameWriter frameWriter)
    {
        if (maxFrameSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxFrameSize), "Max frame size must be greater than zero.");

        MaxFrameSize = maxFrameSize;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _frameWriter = frameWriter ?? throw new ArgumentNullException(nameof(frameWriter));
        _disposeToken = _disposeCts.Token;
    }

    /// <summary>
    /// 读取 TCP 帧前允许的最大声明长度，应与上层 MaxPacketSize 保持一致。
    /// </summary>
    public int MaxFrameSize
    {
        get => Volatile.Read(ref _maxFrameSize);
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Max frame size must be greater than zero.");
            Volatile.Write(ref _maxFrameSize, value);
        }
    }

    /// <summary>
    /// 用于连接超时的时间源。
    /// </summary>
    public TimeProvider TimeProvider
    {
        get => _timeProvider;
        set => _timeProvider = value ?? TimeProvider.System;
    }

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
    public async Task<TransportStartResult> StartServerAsync(NetListenOptions options, CancellationToken token = default)
    {
        if (IsDisposed)
            return new TransportStartResult(NetTransportStatus.ObjectDisposed);
        if (token.IsCancellationRequested)
            return new TransportStartResult(NetTransportStatus.Cancelled);
        if (options.Port < 0 || options.Port > 65535)
            return new TransportStartResult(NetTransportStatus.InvalidEndpoint, "Port must be between 0 and 65535.");
        if (options.MaxConnections <= 0)
            return new TransportStartResult(NetTransportStatus.InvalidEndpoint, "MaxConnections must be greater than zero.");

        try
        {
            await _serverLifecycleGate.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new TransportStartResult(NetTransportStatus.Cancelled);
        }

        try
        {
            if (IsDisposed)
                return new TransportStartResult(NetTransportStatus.ObjectDisposed);
            if (Volatile.Read(ref _running) == 1)
                return new TransportStartResult(NetTransportStatus.AlreadyRunning);

            var bindAddress = options.BindAddress ?? IPAddress.Any;
            var listener = new TcpListener(bindAddress, options.Port);
            listener.Start(options.MaxConnections);
            _listener = listener;
            LocalEndPoint = (IPEndPoint)listener.LocalEndpoint;
            _acceptCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeToken);
            _acceptTask = AcceptLoopAsync(listener, options.MaxConnections, _acceptCts.Token);
            Volatile.Write(ref _running, 1);
            return new TransportStartResult(NetTransportStatus.Ok);
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
            Volatile.Write(ref _running, 0);
            DispatchError(TransportConnectionId.None, ex.Message, ex);
            return new TransportStartResult(NetTransportStatus.TransportFailed, ex.Message);
        }
        finally
        {
            _serverLifecycleGate.Release();
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
        if (options.Timeout <= TimeSpan.Zero)
            return new TransportConnectResult(NetTransportStatus.InvalidEndpoint, TransportConnectionId.None, "Timeout must be greater than zero.");

        using var timeoutCts = new CancellationTokenSource(options.Timeout, _timeProvider);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token, _disposeToken);
        var tcpClient = new TcpClient();

        try
        {
            await tcpClient.ConnectAsync(options.Host, options.Port, linkedCts.Token).ConfigureAwait(false);
            var connectionId = NextConnectionId();
            if (!TryAddConnection(connectionId, tcpClient, out var connection))
                return new TransportConnectResult(NetTransportStatus.ObjectDisposed, TransportConnectionId.None);
            StartReadLoop(connectionId, connection);
            if (IsDisposed)
            {
                await DisconnectAsync(connectionId, DisconnectReason.LocalClosed).ConfigureAwait(false);
                return new TransportConnectResult(NetTransportStatus.ObjectDisposed, TransportConnectionId.None);
            }
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

        try
        {
            await _serverLifecycleGate.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new TransportStartResult(NetTransportStatus.Cancelled);
        }

        try
        {
            if (IsDisposed)
                return new TransportStartResult(NetTransportStatus.ObjectDisposed);

            await StopServerCoreAsync(DisconnectReason.ServerClosed).ConfigureAwait(false);
            return new TransportStartResult(NetTransportStatus.Ok);
        }
        finally
        {
            _serverLifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    public Task DisconnectAsync(TransportConnectionId connectionId, DisconnectReason reason = DisconnectReason.LocalClosed)
    {
        if (_connections.TryRemove(connectionId, out var connection))
        {
            connection.Dispose();
            if (!ReferenceEquals(CurrentReadConnection.Value, connection))
                return DispatchDisconnectedAfterReadLoopAsync(connectionId, connection, reason);

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
        if (data.Length > MaxFrameSize)
            return new NetSendResult(NetSendStatus.PacketTooLarge, $"Packet length {data.Length} exceeds MaxFrameSize {MaxFrameSize}.");
        if (!_connections.TryGetValue(connectionId, out var connection))
            return new NetSendResult(NetSendStatus.ConnectionUnavailable);
        if (!connection.TryEnterSend())
            return new NetSendResult(NetSendStatus.ConnectionUnavailable);

        var lockTaken = false;
        var writeStarted = false;
        try
        {
            await connection.WriteLock.WaitAsync(token).ConfigureAwait(false);
            lockTaken = true;
            if (!_connections.TryGetValue(connectionId, out var current) || !ReferenceEquals(current, connection))
                return new NetSendResult(NetSendStatus.ConnectionUnavailable);

            await _frameWriter.WriteAsync(
                connection.Stream,
                connection.WriteLengthBuffer,
                data,
                () => writeStarted = true,
                token).ConfigureAwait(false);

            return NetSendResult.Ok();
        }
        catch (OperationCanceledException)
        {
            if (writeStarted)
                await DisconnectAsync(connectionId, DisconnectReason.TransportFailed).ConfigureAwait(false);
            return new NetSendResult(NetSendStatus.TransportFailed, "Send was cancelled.");
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            DispatchError(connectionId, ex.Message, ex);
            if (writeStarted)
                await DisconnectAsync(connectionId, DisconnectReason.TransportFailed).ConfigureAwait(false);
            return new NetSendResult(NetSendStatus.TransportFailed, ex.Message);
        }
        finally
        {
            if (lockTaken)
                connection.WriteLock.Release();
            connection.ExitSend();
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_disposeGate)
        {
            if (_disposeTask is null)
            {
                Volatile.Write(ref _disposed, 1);
                _disposeTask = Task.Run(DisposeCoreAsync);
            }

            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        _disposeCts.Cancel();
        try
        {
            await _serverLifecycleGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                await StopServerCoreAsync(DisconnectReason.LocalClosed).ConfigureAwait(false);
            }
            finally
            {
                _serverLifecycleGate.Release();
            }
        }
        finally
        {
            _disposeCts.Dispose();
        }
    }

    private async Task StopServerCoreAsync(DisconnectReason reason)
    {
        var acceptCancellation = Interlocked.Exchange(ref _acceptCts, null);
        var listener = Interlocked.Exchange(ref _listener, null);
        var acceptTask = Interlocked.Exchange(ref _acceptTask, null);
        acceptCancellation?.Cancel();
        listener?.Stop();

        if (acceptTask is not null)
        {
            try
            {
                await acceptTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        foreach (var connectionId in _connections.Keys.ToArray())
            await DisconnectAsync(connectionId, reason).ConfigureAwait(false);

        acceptCancellation?.Dispose();
        LocalEndPoint = null;
        Volatile.Write(ref _running, 0);
    }

    private async Task AcceptLoopAsync(TcpListener listener, int maxConnections, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
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
                if (token.IsCancellationRequested)
                    return;

                DispatchError(TransportConnectionId.None, ex.Message, ex);
                continue;
            }

            if (token.IsCancellationRequested)
            {
                client.Dispose();
                return;
            }

            if (_connections.Count >= maxConnections)
            {
                client.Dispose();
                continue;
            }

            var connectionId = NextConnectionId();
            if (!TryAddConnection(connectionId, client, out var connection))
                return;
            StartReadLoop(connectionId, connection);
            DispatchPeerConnected(connectionId);
        }
    }

    private bool TryAddConnection(TransportConnectionId connectionId, TcpClient client, out TcpConnection connection)
    {
        lock (_disposeGate)
        {
            if (IsDisposed)
            {
                client.Dispose();
                connection = null!;
                return false;
            }

            connection = new TcpConnection(client);
            _connections[connectionId] = connection;
            return true;
        }
    }

    private void StartReadLoop(TransportConnectionId connectionId, TcpConnection connection)
    {
        var readTask = Task.Run(async () =>
        {
            CurrentReadConnection.Value = connection;
            try
            {
                await ReadLoopAsync(connectionId, connection, _disposeToken).ConfigureAwait(false);
            }
            finally
            {
                CurrentReadConnection.Value = null;
            }
        });
        connection.SetReadTask(readTask);
    }

    private async Task DispatchDisconnectedAfterReadLoopAsync(TransportConnectionId connectionId, TcpConnection connection, DisconnectReason reason)
    {
        try
        {
            await connection.ReadTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        DispatchPeerDisconnected(connectionId, reason);
    }

    private async Task ReadLoopAsync(TransportConnectionId connectionId, TcpConnection connection, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var packet = await ReadFrameAsync(connection, MaxFrameSize, token).ConfigureAwait(false);
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
            {
                removed.Dispose();
                DispatchError(connectionId, ex.Message, ex);
                DispatchPeerDisconnected(connectionId, ex is InvalidDataException ? DisconnectReason.TransportFailed : DisconnectReason.RemoteClosed);
            }
        }
    }

    private static async Task<byte[]?> ReadFrameAsync(TcpConnection connection, int maxFrameSize, CancellationToken token)
    {
        if (!await ReadExactlyOrNullAsync(connection.Stream, connection.ReadLengthBuffer, token).ConfigureAwait(false))
            return null;

        var length = BinaryPrimitives.ReadInt32LittleEndian(connection.ReadLengthBuffer);
        if (length < 0)
            throw new InvalidDataException("Frame length cannot be negative.");
        if (length > maxFrameSize)
            throw new InvalidDataException($"Frame length {length} exceeds MaxFrameSize {maxFrameSize}.");

        var payload = new byte[length];
        return await ReadExactlyOrNullAsync(connection.Stream, payload, token).ConfigureAwait(false)
            ? payload
            : null;
    }

    private static async Task<bool> ReadExactlyOrNullAsync(NetworkStream stream, Memory<byte> buffer, CancellationToken token)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[offset..], token).ConfigureAwait(false);
            if (read == 0)
                return offset == 0 ? false : throw new EndOfStreamException("TCP frame ended unexpectedly.");

            offset += read;
        }

        return true;
    }

    private TransportConnectionId NextConnectionId() => new((ulong)Interlocked.Increment(ref _nextConnectionId));

    private void DispatchPeerConnected(TransportConnectionId connectionId)
    {
        var callbacks = PeerConnected?.GetInvocationList();
        if (callbacks is null)
            return;

        var message = new TransportPeerConnected(connectionId);
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

    private bool IsDisposed => Volatile.Read(ref _disposed) == 1;

    private sealed class TcpConnection : IDisposable
    {
        private readonly TcpClient _client;
        private readonly TaskCompletionSource<Task> _readTaskReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _sendGate = new();
        private int _activeSends;
        private bool _disposed;

        public TcpConnection(TcpClient client)
        {
            _client = client;
            Stream = client.GetStream();
        }

        public NetworkStream Stream { get; }
        public byte[] ReadLengthBuffer { get; } = new byte[sizeof(int)];
        public byte[] WriteLengthBuffer { get; } = new byte[sizeof(int)];
        public SemaphoreSlim WriteLock { get; } = new(1, 1);
        public Task ReadTask => _readTaskReady.Task.Unwrap();

        public void SetReadTask(Task readTask)
        {
            _readTaskReady.TrySetResult(readTask);
        }

        public bool TryEnterSend()
        {
            lock (_sendGate)
            {
                if (_disposed)
                    return false;

                _activeSends++;
                return true;
            }
        }

        public void ExitSend()
        {
            var disposeWriteLock = false;
            lock (_sendGate)
            {
                _activeSends--;
                disposeWriteLock = _disposed && _activeSends == 0;
            }

            if (disposeWriteLock)
                WriteLock.Dispose();
        }

        public void Dispose()
        {
            var disposeWriteLock = false;
            lock (_sendGate)
            {
                if (_disposed)
                    return;

                _disposed = true;
                disposeWriteLock = _activeSends == 0;
            }

            Stream.Dispose();
            _client.Dispose();
            if (disposeWriteLock)
                WriteLock.Dispose();
        }
    }
}
