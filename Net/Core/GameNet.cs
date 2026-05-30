namespace SimpleFramework.Net;

using System.Text.Json;

public sealed class GameNet : IAsyncDisposable
{
    private readonly GameNetOptions _options;
    private readonly INetTransport? _transport;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly Dictionary<TransportConnectionId, PeerId> _connectionPeers = new();
    private readonly Dictionary<PeerId, TransportConnectionId> _peerConnections = new();
    private NetLifecycleState _state;
    private int _disposed;
    private ulong _nextPeerId = PeerId.Server.Value;
    private HostOptions? _hostOptions;
    private TaskCompletionSource<JoinResult>? _pendingJoin;
    private TransportConnectionId _serverConnectionId = TransportConnectionId.None;

    public GameNet(GameNetOptions options)
    {
        ValidateOptions(options);

        _options = options;
        Diagnostics = new NetDiagnostics();
        Session = new NetSession();
        Peers = new PeerDirectory();
        Messages = new NetMessenger(SendToServerPacketAsync, SendToPeerPacketAsync, GetBroadcastTargets, Diagnostics, _options.MaxPacketSize);
    }

    public GameNet(INetTransport transport, GameNetOptions options)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ValidateOptions(options);

        _transport = transport;
        _options = options;
        Diagnostics = new NetDiagnostics();
        Session = new NetSession();
        Peers = new PeerDirectory();
        Messages = new NetMessenger(SendToServerPacketAsync, SendToPeerPacketAsync, GetBroadcastTargets, Diagnostics, _options.MaxPacketSize);
        _transport.PacketReceived += OnTransportPacketReceived;
    }

    public GameNetOptions Options => _options;
    public NetDiagnostics Diagnostics { get; }
    public NetSession Session { get; }
    public PeerDirectory Peers { get; }
    public NetMessenger Messages { get; }

    public void On<T>(Action<NetContext, T> handler) => Messages.On(handler);

    public ValueTask<NetSendResult> SendToServerAsync<T>(T message) => Messages.SendToServerAsync(message);

    public ValueTask<NetSendResult> SendAsync<T>(PeerId peerId, T message) => Messages.SendAsync(peerId, message);

    public ValueTask<NetSendResult> BroadcastAsync<T>(T message) => Messages.BroadcastAsync(message);

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

            var pendingJoin = new TaskCompletionSource<JoinResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingJoin = pendingJoin;
            var request = new SessionPacket(
                SessionPacket.JoinRequest,
                _options.Application.ApplicationId,
                _options.Application.ProtocolVersion,
                options.AuthPayload,
                PeerId.None,
                null,
                NetSessionStatus.Ok,
                null);
            var send = await _transport.SendAsync(connect.ConnectionId, SerializePacket(request), NetChannel.System, token).ConfigureAwait(false);
            if (!send.Succeeded)
            {
                _pendingJoin = null;
                return new JoinResult(NetSessionStatus.TransportFailed, PeerId.None, send.Message);
            }

            JoinResult join;
            try
            {
                join = await pendingJoin.Task.WaitAsync(options.Timeout, _options.TimeProvider, token).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                _pendingJoin = null;
                return new JoinResult(NetSessionStatus.TransportFailed, PeerId.None, "Join timed out.");
            }

            if (join.Succeeded)
            {
                _serverConnectionId = connect.ConnectionId;
                lock (_connectionPeers)
                    _connectionPeers[connect.ConnectionId] = PeerId.Server;
                _state = NetLifecycleState.Client;
                Session.SetState(NetSessionRole.Client);
            }

            return join;
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

            _hostOptions = options;
            _state = startedState;
            Session.SetState(startedState == NetLifecycleState.Hosting ? NetSessionRole.Host : NetSessionRole.DedicatedServer);
            Peers.SetLocalPeer(PeerId.Server);
            Peers.Upsert(new PeerInfo(
                PeerId.Server,
                IsServer: true,
                IsLocal: startedState == NetLifecycleState.Hosting,
                JoinedAt: _options.TimeProvider.GetUtcNow()));
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

    private void OnTransportPacketReceived(TransportPacketReceived packet)
    {
        _ = Task.Run(() => HandleTransportPacketAsync(packet));
    }

    private async Task HandleTransportPacketAsync(TransportPacketReceived packet)
    {
        SessionPacket? sessionPacket;
        try
        {
            sessionPacket = JsonSerializer.Deserialize<SessionPacket>(packet.Data.Span);
        }
        catch (Exception ex)
        {
            Diagnostics.RecordError(new NetError("SessionPacketDecodeFailed", ex.Message, ex));
            return;
        }

        if (sessionPacket is null)
            return;

        switch (sessionPacket.Kind)
        {
            case SessionPacket.JoinRequest:
                await HandleJoinRequestAsync(packet.ConnectionId, sessionPacket).ConfigureAwait(false);
                return;
            case SessionPacket.JoinAccepted:
                HandleJoinAccepted(sessionPacket);
                return;
            case SessionPacket.JoinRejected:
                HandleJoinRejected(sessionPacket);
                return;
        }

        var senderId = PeerId.None;
        lock (_connectionPeers)
            _connectionPeers.TryGetValue(packet.ConnectionId, out senderId);
        Messages.TryHandlePacket(packet.Data, senderId);
    }

    private async Task HandleJoinRequestAsync(TransportConnectionId connectionId, SessionPacket packet)
    {
        if (_transport is null || _hostOptions is null || _state is not (NetLifecycleState.Hosting or NetLifecycleState.DedicatedServer))
            return;

        if (packet.ApplicationId != _options.Application.ApplicationId)
        {
            await SendJoinRejectedAsync(connectionId, NetSessionStatus.IncompatibleApplication, "ApplicationId is incompatible.").ConfigureAwait(false);
            return;
        }

        if (packet.ProtocolVersion != _options.Application.ProtocolVersion)
        {
            await SendJoinRejectedAsync(connectionId, NetSessionStatus.IncompatibleProtocol, "ProtocolVersion is incompatible.").ConfigureAwait(false);
            return;
        }

        if (Peers.Peers.Count(p => !p.IsServer) >= _hostOptions.MaxPeers)
        {
            await SendJoinRejectedAsync(connectionId, NetSessionStatus.CapacityFull, "Server is full.").ConfigureAwait(false);
            return;
        }

        if (_hostOptions.Authenticator is not null)
        {
            var auth = await _hostOptions.Authenticator(new AuthContext
            {
                ConnectionId = connectionId,
                AuthPayload = packet.AuthPayload
            }).ConfigureAwait(false);

            if (!auth.Succeeded)
            {
                await SendJoinRejectedAsync(connectionId, NetSessionStatus.AuthenticationFailed, auth.Message ?? "Authentication failed.").ConfigureAwait(false);
                return;
            }
        }

        var peerId = new PeerId(++_nextPeerId);
        var peer = new PeerInfo(peerId, IsServer: false, IsLocal: false, JoinedAt: _options.TimeProvider.GetUtcNow());
        lock (_connectionPeers)
        {
            _connectionPeers[connectionId] = peerId;
            _peerConnections[peerId] = connectionId;
        }
        Peers.Upsert(peer);
        Diagnostics.SetConnectedPeerCount(Peers.Peers.Count);

        var accepted = new SessionPacket(
            SessionPacket.JoinAccepted,
            _options.Application.ApplicationId,
            _options.Application.ProtocolVersion,
            null,
            peerId,
            Peers.Peers.ToArray(),
            NetSessionStatus.Ok,
            null);
        await _transport.SendAsync(connectionId, SerializePacket(accepted), NetChannel.System).ConfigureAwait(false);
    }

    private async Task SendJoinRejectedAsync(TransportConnectionId connectionId, NetSessionStatus status, string message)
    {
        if (_transport is null)
            return;

        var rejected = new SessionPacket(
            SessionPacket.JoinRejected,
            _options.Application.ApplicationId,
            _options.Application.ProtocolVersion,
            null,
            PeerId.None,
            null,
            status,
            message);
        await _transport.SendAsync(connectionId, SerializePacket(rejected), NetChannel.System).ConfigureAwait(false);
    }

    private void HandleJoinAccepted(SessionPacket packet)
    {
        var peers = packet.Peers ?? Array.Empty<PeerInfo>();
        Peers.SetLocalPeer(packet.PeerId);
        Peers.Replace(peers.Select(p => p.PeerId == packet.PeerId ? p with { IsLocal = true } : p));
        Diagnostics.SetConnectedPeerCount(Peers.Peers.Count);
        _pendingJoin?.TrySetResult(new JoinResult(NetSessionStatus.Ok, packet.PeerId, packet.Message));
        _pendingJoin = null;
    }

    private void HandleJoinRejected(SessionPacket packet)
    {
        _pendingJoin?.TrySetResult(new JoinResult(packet.Status, PeerId.None, packet.Message));
        _pendingJoin = null;
    }

    private static byte[] SerializePacket(SessionPacket packet) => JsonSerializer.SerializeToUtf8Bytes(packet);

    private ValueTask<NetSendResult> SendToServerPacketAsync(byte[] packet)
    {
        if (IsDisposed)
            return ValueTask.FromResult(new NetSendResult(NetSendStatus.ObjectDisposed));
        if (_transport is null || _serverConnectionId == TransportConnectionId.None)
            return ValueTask.FromResult(new NetSendResult(NetSendStatus.SessionClosed));

        return _transport.SendAsync(_serverConnectionId, packet, NetChannel.Reliable);
    }

    private ValueTask<NetSendResult> SendToPeerPacketAsync(PeerId peerId, byte[] packet)
    {
        if (IsDisposed)
            return ValueTask.FromResult(new NetSendResult(NetSendStatus.ObjectDisposed));
        if (_transport is null)
            return ValueTask.FromResult(new NetSendResult(NetSendStatus.SessionClosed));

        TransportConnectionId connectionId;
        lock (_connectionPeers)
        {
            if (!_peerConnections.TryGetValue(peerId, out connectionId))
                return ValueTask.FromResult(new NetSendResult(NetSendStatus.PeerUnavailable));
        }

        return _transport.SendAsync(connectionId, packet, NetChannel.Reliable);
    }

    private IReadOnlyCollection<PeerId> GetBroadcastTargets()
    {
        lock (_connectionPeers)
            return _peerConnections.Keys.ToArray();
    }

    private enum NetLifecycleState
    {
        Stopped,
        Hosting,
        DedicatedServer,
        Client
    }
}
