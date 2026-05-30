namespace SimpleFramework.Net;

public sealed class GameNet : IAsyncDisposable
{
    private readonly GameNetOptions _options;
    private readonly INetTransport? _transport;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private NetLifecycleState _state;
    private int _disposed;

    public GameNet(GameNetOptions options)
    {
        ValidateOptions(options);

        _options = options;
        Diagnostics = new NetDiagnostics();
        Session = new NetSession();
    }

    public GameNet(INetTransport transport, GameNetOptions options)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ValidateOptions(options);

        _transport = transport;
        _options = options;
        Diagnostics = new NetDiagnostics();
        Session = new NetSession();
    }

    public GameNetOptions Options => _options;
    public NetDiagnostics Diagnostics { get; }
    public NetSession Session { get; }

    public Task<NetSessionResult> HostAsync(HostOptions options, CancellationToken token = default)
    {
        return StartServerCoreAsync(options, NetLifecycleState.Hosting, token);
    }

    public Task<NetSessionResult> StartServerAsync(HostOptions options, CancellationToken token = default)
    {
        return StartServerCoreAsync(options, NetLifecycleState.DedicatedServer, token);
    }

    public async Task<JoinResult> JoinAsync(JoinOptions options, CancellationToken token = default)
    {
        if (IsDisposed)
            return new JoinResult(NetSessionStatus.ObjectDisposed, PeerId.None);
        if (_transport is null)
            return new JoinResult(NetSessionStatus.InvalidState, PeerId.None, "No transport was configured.");

        await _lifecycleGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (IsDisposed)
                return new JoinResult(NetSessionStatus.ObjectDisposed, PeerId.None);
            if (_state != NetLifecycleState.Stopped)
                return new JoinResult(NetSessionStatus.InvalidState, PeerId.None);

            var connect = await _transport.ConnectAsync(new NetConnectOptions
            {
                Host = options.Host,
                Port = options.Port,
                Timeout = options.Timeout
            }, token).ConfigureAwait(false);

            if (connect.Status != NetTransportStatus.Ok)
                return new JoinResult(MapTransportStatus(connect.Status), PeerId.None, connect.Message);

            _state = NetLifecycleState.Client;
            Session.SetState(NetSessionRole.Client);
            return new JoinResult(NetSessionStatus.Ok, PeerId.None);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task<NetSessionResult> LeaveAsync(CancellationToken token = default)
    {
        return await StopAsync(token).ConfigureAwait(false);
    }

    public async Task<NetSessionResult> StopAsync(CancellationToken token = default)
    {
        if (IsDisposed)
            return new NetSessionResult(NetSessionStatus.ObjectDisposed);

        await _lifecycleGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (IsDisposed)
                return new NetSessionResult(NetSessionStatus.ObjectDisposed);

            _state = NetLifecycleState.Stopped;
            Session.SetState(NetSessionRole.None);
            return NetSessionResult.Ok();
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            _state = NetLifecycleState.Stopped;
            Session.SetState(NetSessionRole.None);
        }
        finally
        {
            _lifecycleGate.Release();
        }

        if (_transport is not null)
            await _transport.DisposeAsync().ConfigureAwait(false);

        _lifecycleGate.Dispose();
    }

    public static void ValidateOptions(GameNetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Application);

        if (options.Application.ApplicationId == Guid.Empty)
            throw new ArgumentException("ApplicationId cannot be empty.", nameof(options));
        if (options.Application.ProtocolVersion < 1)
            throw new ArgumentException("ProtocolVersion must be >= 1.", nameof(options));
        if (options.MaxPacketSize <= 0)
            throw new ArgumentException("MaxPacketSize must be > 0.", nameof(options));
        if (options.MaxSendQueueBytesPerPeer <= 0)
            throw new ArgumentException("MaxSendQueueBytesPerPeer must be > 0.", nameof(options));
        if (options.MaxSendQueuePacketsPerPeer <= 0)
            throw new ArgumentException("MaxSendQueuePacketsPerPeer must be > 0.", nameof(options));
    }

    private async Task<NetSessionResult> StartServerCoreAsync(HostOptions options, NetLifecycleState startedState, CancellationToken token)
    {
        if (IsDisposed)
            return new NetSessionResult(NetSessionStatus.ObjectDisposed);
        if (_transport is null)
            return new NetSessionResult(NetSessionStatus.InvalidState, "No transport was configured.");

        await _lifecycleGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (IsDisposed)
                return new NetSessionResult(NetSessionStatus.ObjectDisposed);
            if (_state != NetLifecycleState.Stopped)
                return new NetSessionResult(NetSessionStatus.InvalidState);

            var start = await _transport.StartServerAsync(new NetListenOptions
            {
                BindAddress = options.BindAddress,
                Port = options.Port,
                MaxConnections = options.MaxPeers
            }, token).ConfigureAwait(false);

            if (start.Status != NetTransportStatus.Ok)
                return new NetSessionResult(MapTransportStatus(start.Status), start.Message);

            _state = startedState;
            Session.SetState(startedState == NetLifecycleState.Hosting ? NetSessionRole.Host : NetSessionRole.DedicatedServer);
            return NetSessionResult.Ok();
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private static NetSessionStatus MapTransportStatus(NetTransportStatus status) => status switch
    {
        NetTransportStatus.ObjectDisposed => NetSessionStatus.ObjectDisposed,
        NetTransportStatus.Cancelled => NetSessionStatus.Cancelled,
        _ => NetSessionStatus.TransportFailed
    };

    private bool IsDisposed => Volatile.Read(ref _disposed) == 1;

    private enum NetLifecycleState
    {
        Stopped,
        Hosting,
        DedicatedServer,
        Client
    }
}
