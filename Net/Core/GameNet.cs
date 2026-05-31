namespace SimpleFramework.Net;

using System.Security.Cryptography;
using System.Text.Json;

/// <summary>
/// Net 模块的主入口，负责组合传输、会话、消息、诊断等组件。
/// </summary>
public sealed class GameNet : IAsyncDisposable
{
    private readonly GameNetOptions _options;
    private readonly INetTransport? _transport;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _serverJoinGate = new(1, 1);
    private readonly CancellationTokenSource _lifecycleCancellation = new();
    private readonly Dictionary<TransportConnectionId, PeerId> _connectionPeers = new();
    private readonly Dictionary<PeerId, TransportConnectionId> _peerConnections = new();
    private NetLifecycleState _state;
    private int _disposed;
    private ulong _nextPeerId = PeerId.Server.Value;
    private HostOptions? _hostOptions;
    private TaskCompletionSource<JoinResult>? _pendingJoin;
    private TransportConnectionId _serverConnectionId = TransportConnectionId.None;
    private readonly Dictionary<string, ReconnectEntry> _reconnectTokens = new();
    private readonly Dictionary<PeerId, string> _peerReconnectTokens = new();
    private readonly Dictionary<PeerId, CancellationTokenSource> _reconnectExpiry = new();
    private JoinOptions? _lastJoinOptions;
    private string? _lastReconnectToken;
    private int _reconnectLoopRunning;

    /// <summary>
    /// 创建不带传输的 GameNet 实例，适用于只使用注册、验证或离线组件的场景。
    /// </summary>
    /// <param name="options">共享网络配置。</param>
    public GameNet(GameNetOptions options, IDiscoveryBackend? discoveryBackend = null)
    {
        ValidateOptions(options);

        _options = options;
        Diagnostics = new NetDiagnostics(_options.EventDispatcher);
        Session = new NetSession();
        Peers = new PeerDirectory();
        Discovery = new NetDiscovery(_options, discoveryBackend ?? new UdpDiscoveryNetwork(_options.TimeProvider), Diagnostics);
        Messages = new NetMessenger(
            SendToServerPacketAsync,
            SendToPeerPacketAsync,
            GetBroadcastTargets,
            Diagnostics,
            _options.MaxPacketSize,
            _options.MaxSendQueueBytesPerPeer,
            _options.MaxSendQueuePacketsPerPeer,
            _options.MaxSendsPerSecondPerPeer,
            _options.TimeProvider,
            _options.EventDispatcher);
        Stats = new NetStats(Messages, _options.TimeProvider, () => IsDisposed);
        Flow = new NetFlow(Messages, Diagnostics, _options.TimeProvider, () => Session.Role, CanReachPeer, () => IsDisposed);
    }

    /// <summary>
    /// 创建带底层传输的 GameNet 实例。
    /// </summary>
    /// <param name="transport">底层网络传输实现。</param>
    /// <param name="options">共享网络配置。</param>
    public GameNet(INetTransport transport, GameNetOptions options, IDiscoveryBackend? discoveryBackend = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ValidateOptions(options);

        _transport = transport;
        if (_transport is TcpNetTransport tcpTransport)
            tcpTransport.MaxFrameSize = options.MaxPacketSize;
        _options = options;
        Diagnostics = new NetDiagnostics(_options.EventDispatcher);
        Session = new NetSession();
        Peers = new PeerDirectory();
        Discovery = new NetDiscovery(_options, discoveryBackend ?? new UdpDiscoveryNetwork(_options.TimeProvider), Diagnostics);
        Messages = new NetMessenger(
            SendToServerPacketAsync,
            SendToPeerPacketAsync,
            GetBroadcastTargets,
            Diagnostics,
            _options.MaxPacketSize,
            _options.MaxSendQueueBytesPerPeer,
            _options.MaxSendQueuePacketsPerPeer,
            _options.MaxSendsPerSecondPerPeer,
            _options.TimeProvider,
            _options.EventDispatcher);
        Stats = new NetStats(Messages, _options.TimeProvider, () => IsDisposed);
        Flow = new NetFlow(Messages, Diagnostics, _options.TimeProvider, () => Session.Role, CanReachPeer, () => IsDisposed);
        _transport.PacketReceived += OnTransportPacketReceived;
        _transport.PeerDisconnected += OnTransportPeerDisconnected;
        _transport.Error += OnTransportError;
    }

    /// <summary>
    /// 当前实例的共享配置。
    /// </summary>
    public GameNetOptions Options => _options;

    /// <summary>
    /// 网络诊断计数器和错误事件。
    /// </summary>
    public NetDiagnostics Diagnostics { get; }

    /// <summary>
    /// 当前会话状态。
    /// </summary>
    public NetSession Session { get; }

    /// <summary>
    /// 当前会话中的对等体目录。
    /// </summary>
    public PeerDirectory Peers { get; }

    /// <summary>
    /// 局域网房间发现组件。
    /// </summary>
    public NetDiscovery Discovery { get; }

    /// <summary>
    /// 类型化消息收发组件。
    /// </summary>
    public NetMessenger Messages { get; }

    /// <summary>
    /// 应用层延迟和探测统计组件。
    /// </summary>
    public NetStats Stats { get; }

    /// <summary>
    /// 服务器拥有的多人流程协调组件。
    /// </summary>
    public NetFlow Flow { get; }

    /// <summary>
    /// 注册类型化消息处理器。
    /// </summary>
    /// <typeparam name="T">消息类型。</typeparam>
    /// <param name="handler">收到消息时执行的处理器。</param>
    public void On<T>(Action<NetContext, T> handler) => Messages.On(handler);

    /// <summary>
    /// 注册类型化请求处理器；同一请求类型只允许一个处理器。
    /// </summary>
    /// <typeparam name="TRequest">请求消息类型。</typeparam>
    /// <typeparam name="TResponse">响应消息类型。</typeparam>
    /// <param name="handler">收到请求时执行的处理器。</param>
    public void OnRequest<TRequest, TResponse>(Func<NetContext, TRequest, TResponse> handler) =>
        Messages.OnRequest(handler);

    /// <summary>
    /// 从客户端向服务器发送类型化消息。
    /// </summary>
    /// <typeparam name="T">消息类型。</typeparam>
    /// <param name="message">要发送的消息。</param>
    public ValueTask<NetSendResult> SendToServerAsync<T>(T message)
    {
        return IsDisposed
            ? ValueTask.FromResult(new NetSendResult(NetSendStatus.ObjectDisposed))
            : Messages.SendToServerAsync(message);
    }

    /// <summary>
    /// 从服务器向指定对等体发送类型化消息。
    /// </summary>
    /// <typeparam name="T">消息类型。</typeparam>
    /// <param name="peerId">目标对等体。</param>
    /// <param name="message">要发送的消息。</param>
    public ValueTask<NetSendResult> SendAsync<T>(PeerId peerId, T message)
    {
        return IsDisposed
            ? ValueTask.FromResult(new NetSendResult(NetSendStatus.ObjectDisposed))
            : Messages.SendAsync(peerId, message);
    }

    /// <summary>
    /// 从服务器向所有远端对等体广播类型化消息。
    /// </summary>
    /// <typeparam name="T">消息类型。</typeparam>
    /// <param name="message">要广播的消息。</param>
    public ValueTask<NetSendResult> BroadcastAsync<T>(T message)
    {
        return IsDisposed
            ? ValueTask.FromResult(new NetSendResult(NetSendStatus.ObjectDisposed))
            : Messages.BroadcastAsync(message);
    }

    /// <summary>
    /// 向指定对等体发送请求并等待类型化响应。
    /// </summary>
    /// <typeparam name="TRequest">请求消息类型。</typeparam>
    /// <typeparam name="TResponse">响应消息类型。</typeparam>
    /// <param name="peerId">目标对等体。</param>
    /// <param name="request">请求消息。</param>
    /// <param name="timeout">请求超时时间。</param>
    /// <param name="token">取消标记。</param>
    public Task<NetRequestResult<TResponse>> RequestAsync<TRequest, TResponse>(
        PeerId peerId,
        TRequest request,
        TimeSpan timeout,
        CancellationToken token = default)
    {
        return IsDisposed
            ? Task.FromResult(new NetRequestResult<TResponse> { Status = NetRequestStatus.ObjectDisposed })
            : Messages.RequestAsync<TRequest, TResponse>(peerId, request, timeout, token);
    }

    /// <summary>
    /// 通过服务器向指定对等体中继类型化消息。
    /// </summary>
    /// <typeparam name="T">消息类型。</typeparam>
    /// <param name="targetPeerId">目标对等体。</param>
    /// <param name="message">要中继的消息。</param>
    /// <param name="timeout">等待服务器校验结果的超时时间。</param>
    /// <param name="token">取消标记。</param>
    public Task<NetSendResult> RelayAsync<T>(
        PeerId targetPeerId,
        T message,
        TimeSpan timeout,
        CancellationToken token = default)
    {
        return IsDisposed
            ? Task.FromResult(new NetSendResult(NetSendStatus.ObjectDisposed))
            : Messages.RelayAsync(targetPeerId, message, timeout, token);
    }

    /// <summary>
    /// 以主机模式启动会话，主机同时是权威服务器和本地参与者。
    /// </summary>
    /// <param name="options">主机启动选项。</param>
    /// <param name="token">取消标记。</param>
    public Task<NetSessionResult> HostAsync(HostOptions options, CancellationToken token = default)
    {
        return StartServerCoreAsync(options, NetLifecycleState.Hosting, token);
    }

    /// <summary>
    /// 以专用服务器模式启动会话，不创建本地玩家参与者。
    /// </summary>
    /// <param name="options">服务器启动选项。</param>
    /// <param name="token">取消标记。</param>
    public Task<NetSessionResult> StartServerAsync(HostOptions options, CancellationToken token = default)
    {
        return StartServerCoreAsync(options, NetLifecycleState.DedicatedServer, token);
    }

    /// <summary>
    /// 加入远端服务器并等待握手结果。
    /// </summary>
    /// <param name="options">加入选项。</param>
    /// <param name="token">取消标记。</param>
    public async Task<JoinResult> JoinAsync(JoinOptions options, CancellationToken token = default)
    {
        if (IsDisposed)
            return new JoinResult(NetSessionStatus.ObjectDisposed, PeerId.None);
        if (_transport is null)
            return new JoinResult(NetSessionStatus.InvalidState, PeerId.None, "No transport was configured.");
        if (token.IsCancellationRequested)
            return new JoinResult(NetSessionStatus.Cancelled, PeerId.None);

        try
        {
            await _lifecycleGate.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return new JoinResult(NetSessionStatus.Cancelled, PeerId.None);
        }

        try
        {
            if (IsDisposed)
                return new JoinResult(NetSessionStatus.ObjectDisposed, PeerId.None);
            if (_state != NetLifecycleState.Stopped)
                return new JoinResult(NetSessionStatus.InvalidState, PeerId.None);

            _state = NetLifecycleState.Connecting;
        }
        finally
        {
            _lifecycleGate.Release();
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(token, _lifecycleCancellation.Token);
        var operationToken = linkedCancellation.Token;
        TransportConnectionId connectionId = TransportConnectionId.None;
        try
        {
            var connect = await _transport.ConnectAsync(new NetConnectOptions
            {
                Host = options.Host,
                Port = options.Port,
                Timeout = options.Timeout
            }, operationToken).ConfigureAwait(false);

            if (connect.Status != NetTransportStatus.Ok)
            {
                await ResetConnectingStateAsync().ConfigureAwait(false);
                return new JoinResult(MapTransportStatus(connect.Status), PeerId.None, connect.Message);
            }

            connectionId = connect.ConnectionId;
            Messages.FreezeProtocolManifest();
            var pendingJoin = new TaskCompletionSource<JoinResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingJoin = pendingJoin;
            var request = new SessionPacket(
                SessionPacket.JoinRequest,
                _options.Application.ApplicationId,
                _options.Application.ProtocolVersion,
                options.AuthPayload,
                options.ReconnectToken,
                PeerId.None,
                null,
                Messages.Registry.GetFingerprint(),
                NetSessionStatus.Ok,
                null);
            var send = await _transport.SendAsync(connectionId, SerializePacket(request), NetChannel.System, operationToken).ConfigureAwait(false);
            if (!send.Succeeded)
            {
                _pendingJoin = null;
                await _transport.DisconnectAsync(connectionId, DisconnectReason.LocalClosed).ConfigureAwait(false);
                await ResetConnectingStateAsync().ConfigureAwait(false);
                return new JoinResult(NetSessionStatus.TransportFailed, PeerId.None, send.Message);
            }

            JoinResult join;
            try
            {
                join = await pendingJoin.Task.WaitAsync(options.Timeout, _options.TimeProvider, operationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                _pendingJoin = null;
                await _transport.DisconnectAsync(connectionId, DisconnectReason.LocalClosed).ConfigureAwait(false);
                await ResetConnectingStateAsync().ConfigureAwait(false);
                return new JoinResult(NetSessionStatus.TransportFailed, PeerId.None, "Join timed out.");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                _pendingJoin = null;
                await _transport.DisconnectAsync(connectionId, DisconnectReason.LocalClosed).ConfigureAwait(false);
                await ResetConnectingStateAsync().ConfigureAwait(false);
                return new JoinResult(NetSessionStatus.Cancelled, PeerId.None);
            }
            catch (OperationCanceledException) when (IsDisposed)
            {
                _pendingJoin = null;
                return new JoinResult(NetSessionStatus.ObjectDisposed, PeerId.None);
            }

            if (IsDisposed)
                return new JoinResult(NetSessionStatus.ObjectDisposed, PeerId.None);

            await _lifecycleGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (IsDisposed)
                {
                    await _transport.DisconnectAsync(connectionId, DisconnectReason.LocalClosed).ConfigureAwait(false);
                    return new JoinResult(NetSessionStatus.ObjectDisposed, PeerId.None);
                }

                if (join.Succeeded)
                {
                    _serverConnectionId = connectionId;
                    lock (_connectionPeers)
                        _connectionPeers[connectionId] = PeerId.Server;
                    _lastJoinOptions = options;
                    _lastReconnectToken = join.ReconnectToken;
                    _state = NetLifecycleState.Client;
                    SetSessionRole(NetSessionRole.Client);
                }
                else
                {
                    await _transport.DisconnectAsync(connectionId, DisconnectReason.LocalClosed).ConfigureAwait(false);
                    _state = NetLifecycleState.Stopped;
                }
            }
            finally
            {
                _lifecycleGate.Release();
            }

            return join;
        }
        catch (OperationCanceledException) when (IsDisposed)
        {
            return new JoinResult(NetSessionStatus.ObjectDisposed, PeerId.None);
        }
        finally
        {
            if (!IsDisposed && _state == NetLifecycleState.Connecting)
                await ResetConnectingStateAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 离开当前会话。
    /// </summary>
    /// <param name="token">取消标记。</param>
    public async Task<NetSessionResult> LeaveAsync(CancellationToken token = default)
    {
        return await StopAsync(token).ConfigureAwait(false);
    }

    /// <summary>
    /// 由服务器断开指定客户端连接。
    /// </summary>
    /// <param name="peerId">要断开的远端对等体。</param>
    /// <param name="reason">断开原因，默认表示踢出。</param>
    public async Task<NetSessionResult> KickAsync(PeerId peerId, DisconnectReason reason = DisconnectReason.Kicked)
    {
        if (IsDisposed)
            return new NetSessionResult(NetSessionStatus.ObjectDisposed);
        if (_transport is null)
            return new NetSessionResult(NetSessionStatus.InvalidState, "No transport was configured.");
        if (_state is not (NetLifecycleState.Hosting or NetLifecycleState.DedicatedServer))
            return new NetSessionResult(NetSessionStatus.InvalidState, "Only a server can kick peers.");
        if (peerId == PeerId.None || peerId == PeerId.Server)
            return new NetSessionResult(NetSessionStatus.InvalidState, "PeerId must be a remote participant.");

        TransportConnectionId connectionId;
        lock (_connectionPeers)
        {
            if (!_peerConnections.TryGetValue(peerId, out connectionId))
                return new NetSessionResult(NetSessionStatus.InvalidState, "Peer is not connected.");
        }

        await SendDisconnectNoticeAsync(connectionId, reason).ConfigureAwait(false);
        await _transport.DisconnectAsync(connectionId, reason).ConfigureAwait(false);
        RemoveRemotePeer(peerId, reason);
        return NetSessionResult.Ok();
    }

    /// <summary>
    /// 停止当前会话。该操作可重复调用。
    /// </summary>
    /// <param name="token">取消标记。</param>
    public async Task<NetSessionResult> StopAsync(CancellationToken token = default)
    {
        if (IsDisposed)
            return new NetSessionResult(NetSessionStatus.ObjectDisposed);
        if (token.IsCancellationRequested)
            return new NetSessionResult(NetSessionStatus.Cancelled);

        try
        {
            await _lifecycleGate.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return new NetSessionResult(NetSessionStatus.Cancelled);
        }

        try
        {
            if (IsDisposed)
                return new NetSessionResult(NetSessionStatus.ObjectDisposed);

            await Discovery.StopAdvertiseAsync().ConfigureAwait(false);
            Flow.CancelPendingFlows(FlowEndReason.SessionClosed);
            Messages.CancelPendingRequests(NetRequestStatus.SessionClosed, "Session stopped.");
            Messages.CancelPendingRelays(NetSendStatus.SessionClosed, "Session stopped.");
            Stats.CancelPendingProbes(NetStatsStatus.SessionClosed, "Session stopped.");
            _pendingJoin?.TrySetResult(new JoinResult(NetSessionStatus.Cancelled, PeerId.None, "Session stopped."));
            _pendingJoin = null;
            if (_transport is not null && _state == NetLifecycleState.Client)
            {
                if (_serverConnectionId != TransportConnectionId.None)
                    await _transport.DisconnectAsync(_serverConnectionId, DisconnectReason.LocalClosed).ConfigureAwait(false);

                ClearLocalSession(DisconnectReason.LocalClosed);
                _lastJoinOptions = null;
                _lastReconnectToken = null;
                return NetSessionResult.Ok();
            }

            if (_transport is not null)
            {
                await SendDisconnectNoticesToAllPeersAsync(DisconnectReason.ServerClosed).ConfigureAwait(false);
                var stop = await _transport.StopServerAsync(token).ConfigureAwait(false);
                if (stop.Status is NetTransportStatus.ObjectDisposed)
                    return new NetSessionResult(NetSessionStatus.ObjectDisposed, stop.Message);
                if (stop.Status is NetTransportStatus.Cancelled)
                    return new NetSessionResult(NetSessionStatus.Cancelled, stop.Message);
            }

            _state = NetLifecycleState.Stopped;
            SetSessionRole(NetSessionRole.None);
            Peers.Replace(Array.Empty<PeerInfo>());
            Peers.SetLocalPeer(PeerId.None);
            lock (_connectionPeers)
            {
                _connectionPeers.Clear();
                _peerConnections.Clear();
            }
            ClearReconnectState();
            _serverConnectionId = TransportConnectionId.None;
            _lastJoinOptions = null;
            _lastReconnectToken = null;
            return NetSessionResult.Ok();
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>
    /// 异步释放 GameNet 和其拥有的传输资源。该操作可重复调用。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        _lifecycleCancellation.Cancel();
        _pendingJoin?.TrySetResult(new JoinResult(NetSessionStatus.ObjectDisposed, PeerId.None));
        _pendingJoin = null;

        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            _state = NetLifecycleState.Stopped;
            SetSessionRole(NetSessionRole.None);
            await Discovery.DisposeAsync().ConfigureAwait(false);
            Flow.CancelPendingFlows(FlowEndReason.SessionClosed);
            Messages.CancelPendingRequests(NetRequestStatus.SessionClosed, "GameNet was disposed.");
            Messages.CancelPendingRelays(NetSendStatus.SessionClosed, "GameNet was disposed.");
            Stats.CancelPendingProbes(NetStatsStatus.ObjectDisposed, "GameNet was disposed.");
            ClearReconnectState();
            _lastJoinOptions = null;
            _lastReconnectToken = null;
        }
        finally
        {
            _lifecycleGate.Release();
        }

        if (_transport is not null)
        {
            _transport.PacketReceived -= OnTransportPacketReceived;
            _transport.PeerDisconnected -= OnTransportPeerDisconnected;
            _transport.Error -= OnTransportError;
            await _transport.DisposeAsync().ConfigureAwait(false);
        }

        _lifecycleCancellation.Dispose();
    }

    /// <summary>
    /// 校验 GameNet 配置是否满足启动要求。
    /// </summary>
    /// <param name="options">待校验配置。</param>
    /// <exception cref="ArgumentException">配置值无效时抛出。</exception>
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
        if (options.MaxSendsPerSecondPerPeer <= 0)
            throw new ArgumentException("MaxSendsPerSecondPerPeer must be > 0.", nameof(options));
        if (options.Discovery.Port is <= 0 or > 65535)
            throw new ArgumentException("Discovery.Port must be between 1 and 65535.", nameof(options));
        if (options.Discovery.AdvertiseInterval <= TimeSpan.Zero)
            throw new ArgumentException("Discovery.AdvertiseInterval must be > 0.", nameof(options));
        if (options.Discovery.MaxMetadataPayloadSize <= 0)
            throw new ArgumentException("MaxMetadataPayloadSize must be > 0.", nameof(options));
        if (options.Discovery.RoomTimeout <= TimeSpan.Zero)
            throw new ArgumentException("RoomTimeout must be > 0.", nameof(options));
        if (options.Discovery.RoomTimeout <= options.Discovery.AdvertiseInterval)
            throw new ArgumentException("Discovery.RoomTimeout must be greater than Discovery.AdvertiseInterval.", nameof(options));
        if (options.Discovery.DefaultScanTimeout <= TimeSpan.Zero)
            throw new ArgumentException("Discovery.DefaultScanTimeout must be > 0.", nameof(options));
    }

    private async Task<NetSessionResult> StartServerCoreAsync(HostOptions options, NetLifecycleState startedState, CancellationToken token)
    {
        if (IsDisposed)
            return new NetSessionResult(NetSessionStatus.ObjectDisposed);
        if (_transport is null)
            return new NetSessionResult(NetSessionStatus.InvalidState, "No transport was configured.");
        if (token.IsCancellationRequested)
            return new NetSessionResult(NetSessionStatus.Cancelled);

        try
        {
            await _lifecycleGate.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return new NetSessionResult(NetSessionStatus.Cancelled);
        }

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

            Messages.FreezeProtocolManifest();
            _hostOptions = options;
            _state = startedState;
            SetSessionRole(startedState == NetLifecycleState.Hosting ? NetSessionRole.Host : NetSessionRole.DedicatedServer);
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

    private async Task ResetConnectingStateAsync()
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_state == NetLifecycleState.Connecting)
                _state = NetLifecycleState.Stopped;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private bool IsDisposed => Volatile.Read(ref _disposed) == 1;

    private void OnTransportPacketReceived(TransportPacketReceived packet)
    {
        if (IsSessionControlPacket(packet.Data.Span))
        {
            _ = HandleTransportPacketAsync(packet);
            return;
        }

        _ = Task.Run(() => HandleTransportPacketAsync(packet));
    }

    private static bool IsSessionControlPacket(ReadOnlySpan<byte> data)
    {
        try
        {
            var packet = JsonSerializer.Deserialize<SessionPacket>(data);
            return packet?.Kind is SessionPacket.JoinRequest
                or SessionPacket.JoinAccepted
                or SessionPacket.JoinRejected
                or SessionPacket.PeerDirectoryUpdated
                or SessionPacket.DisconnectNotice;
        }
        catch
        {
            return false;
        }
    }

    private void OnTransportPeerDisconnected(TransportPeerDisconnected disconnected)
    {
        _pendingJoin?.TrySetResult(new JoinResult(NetSessionStatus.TransportFailed, PeerId.None, "Transport disconnected."));
        _pendingJoin = null;

        if (disconnected.ConnectionId == _serverConnectionId)
        {
            Messages.CancelPendingRequests(NetRequestStatus.SessionClosed, "Server connection was closed.");
            Messages.CancelPendingRelays(NetSendStatus.SessionClosed, "Server connection was closed.");
            var reconnectOptions = ShouldAutoReconnect(disconnected.Reason) ? _lastJoinOptions : null;
            var reconnectToken = _lastReconnectToken;
            var previousPeerId = Peers.LocalPeerId;
            ClearLocalSession(disconnected.Reason);
            if (reconnectOptions is not null && !string.IsNullOrWhiteSpace(reconnectToken))
                StartReconnectLoop(reconnectOptions, reconnectToken, previousPeerId);
            return;
        }

        PeerId peerId;
        lock (_connectionPeers)
        {
            if (!_connectionPeers.TryGetValue(disconnected.ConnectionId, out peerId))
                return;

            _connectionPeers.Remove(disconnected.ConnectionId);
            _peerConnections.Remove(peerId);
        }

        RemoveRemotePeer(peerId, disconnected.Reason);
    }

    private void OnTransportError(TransportError error)
    {
        Diagnostics.RecordError(new NetError("TransportError", error.Message, error.Exception));
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
            case SessionPacket.PeerDirectoryUpdated:
                HandlePeerDirectoryUpdated(sessionPacket);
                return;
            case SessionPacket.DisconnectNotice:
                HandleDisconnectNotice(sessionPacket);
                return;
        }

        var senderId = PeerId.None;
        lock (_connectionPeers)
            _connectionPeers.TryGetValue(packet.ConnectionId, out senderId);
        await Messages.TryHandlePacket(packet.Data, senderId).ConfigureAwait(false);
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

        var fingerprintCheck = CheckRemoteFingerprint(packet.MessageFingerprint, "client");
        if (fingerprintCheck.Status == NetFingerprintStatus.Rejected)
        {
            await SendJoinRejectedAsync(connectionId, NetSessionStatus.IncompatibleProtocol, fingerprintCheck.Message ?? "Message fingerprint mismatch.").ConfigureAwait(false);
            return;
        }

        if (!string.IsNullOrWhiteSpace(packet.ReconnectToken))
        {
            await HandleReconnectRequestAsync(connectionId, packet.ReconnectToken).ConfigureAwait(false);
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

        PeerId peerId;
        string? reconnectToken;
        PeerInfo peer;
        await _serverJoinGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Peers.Peers.Count(p => !p.IsServer) >= _hostOptions.MaxPeers)
            {
                await SendJoinRejectedAsync(connectionId, NetSessionStatus.CapacityFull, "Server is full.").ConfigureAwait(false);
                return;
            }

            peerId = new PeerId(++_nextPeerId);
            reconnectToken = _hostOptions.ReconnectPolicy.IsEnabled ? CreateReconnectToken(peerId) : null;
            peer = new PeerInfo(peerId, IsServer: false, IsLocal: false, JoinedAt: _options.TimeProvider.GetUtcNow());
            lock (_connectionPeers)
            {
                _connectionPeers[connectionId] = peerId;
                _peerConnections[peerId] = connectionId;
            }

            Peers.Upsert(peer);
            Diagnostics.SetConnectedPeerCount(CountConnectedPeers());
        }
        finally
        {
            _serverJoinGate.Release();
        }

        DispatchFrameworkEvent(() => Session.RaisePeerJoined(new NetPeerJoined(peer)));

        var accepted = new SessionPacket(
            SessionPacket.JoinAccepted,
            _options.Application.ApplicationId,
            _options.Application.ProtocolVersion,
            null,
            reconnectToken,
            peerId,
            Peers.Peers.ToArray(),
            Messages.Registry.GetFingerprint(),
            NetSessionStatus.Ok,
            null);
        await _transport.SendAsync(connectionId, SerializePacket(accepted), NetChannel.System).ConfigureAwait(false);
        await BroadcastPeerDirectoryUpdateAsync(connectionId).ConfigureAwait(false);
    }

    private async Task HandleReconnectRequestAsync(TransportConnectionId connectionId, string reconnectToken)
    {
        if (_transport is null || _hostOptions is null || !_hostOptions.ReconnectPolicy.IsEnabled)
        {
            await SendJoinRejectedAsync(connectionId, NetSessionStatus.AuthenticationFailed, "Reconnect is not enabled.").ConfigureAwait(false);
            return;
        }

        ReconnectEntry entry;
        PeerInfo peer;
        await _serverJoinGate.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_connectionPeers)
            {
                if (!_reconnectTokens.TryGetValue(reconnectToken, out entry))
                {
                    entry = default;
                }
            }

            if (entry.PeerId == PeerId.None || entry.ExpiresAt <= _options.TimeProvider.GetUtcNow())
            {
                await SendJoinRejectedAsync(connectionId, NetSessionStatus.AuthenticationFailed, "Reconnect token is invalid or expired.").ConfigureAwait(false);
                return;
            }

            var existing = Peers.Peers.FirstOrDefault(p => p.PeerId == entry.PeerId);
            if (existing is not null && existing.IsConnected)
            {
                await SendJoinRejectedAsync(connectionId, NetSessionStatus.AuthenticationFailed, "Reconnect token has already been used.").ConfigureAwait(false);
                return;
            }

            CancelReconnectExpiry(entry.PeerId);
            lock (_connectionPeers)
            {
                _reconnectTokens.Remove(reconnectToken);
                _connectionPeers[connectionId] = entry.PeerId;
                _peerConnections[entry.PeerId] = connectionId;
            }

            peer = existing is null
                ? new PeerInfo(entry.PeerId, IsServer: false, IsLocal: false, JoinedAt: _options.TimeProvider.GetUtcNow())
                : existing with { IsConnected = true };
            Peers.Upsert(peer);
            Diagnostics.SetConnectedPeerCount(CountConnectedPeers());
        }
        finally
        {
            _serverJoinGate.Release();
        }

        DispatchFrameworkEvent(() => Session.RaisePeerReconnected(new NetPeerReconnected(entry.PeerId)));

        var accepted = new SessionPacket(
            SessionPacket.JoinAccepted,
            _options.Application.ApplicationId,
            _options.Application.ProtocolVersion,
            null,
            reconnectToken,
            entry.PeerId,
            Peers.Peers.ToArray(),
            Messages.Registry.GetFingerprint(),
            NetSessionStatus.Ok,
            null);
        await _transport.SendAsync(connectionId, SerializePacket(accepted), NetChannel.System).ConfigureAwait(false);
        await BroadcastPeerDirectoryUpdateAsync(connectionId).ConfigureAwait(false);
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
            null,
            PeerId.None,
            null,
            Messages.Registry.GetFingerprint(),
            status,
            message);
        await _transport.SendAsync(connectionId, SerializePacket(rejected), NetChannel.System).ConfigureAwait(false);
        await _transport.DisconnectAsync(connectionId, DisconnectReason.LocalClosed).ConfigureAwait(false);
    }

    private async Task SendDisconnectNoticeAsync(TransportConnectionId connectionId, DisconnectReason reason)
    {
        if (_transport is null)
            return;

        var notice = new SessionPacket(
            SessionPacket.DisconnectNotice,
            _options.Application.ApplicationId,
            _options.Application.ProtocolVersion,
            null,
            null,
            PeerId.None,
            null,
            Messages.Registry.GetFingerprint(),
            NetSessionStatus.Ok,
            null,
            reason);
        await _transport.SendAsync(connectionId, SerializePacket(notice), NetChannel.System).ConfigureAwait(false);
    }

    private async Task SendDisconnectNoticesToAllPeersAsync(DisconnectReason reason)
    {
        KeyValuePair<PeerId, TransportConnectionId>[] targets;
        lock (_connectionPeers)
            targets = _peerConnections.ToArray();

        foreach (var target in targets)
            await SendDisconnectNoticeAsync(target.Value, reason).ConfigureAwait(false);
    }

    private void HandleJoinAccepted(SessionPacket packet)
    {
        var fingerprintCheck = CheckRemoteFingerprint(packet.MessageFingerprint, "server");
        if (fingerprintCheck.Status == NetFingerprintStatus.Rejected)
        {
            _pendingJoin?.TrySetResult(new JoinResult(NetSessionStatus.IncompatibleProtocol, PeerId.None, fingerprintCheck.Message));
            _pendingJoin = null;
            return;
        }

        var peers = packet.Peers ?? Array.Empty<PeerInfo>();
        Peers.SetLocalPeer(packet.PeerId);
        Peers.Replace(peers.Select(p => p.PeerId == packet.PeerId ? p with { IsLocal = true } : p));
        Diagnostics.SetConnectedPeerCount(Peers.Peers.Count);
        _pendingJoin?.TrySetResult(new JoinResult(NetSessionStatus.Ok, packet.PeerId, packet.Message, packet.ReconnectToken));
        _pendingJoin = null;
    }

    private void HandleJoinRejected(SessionPacket packet)
    {
        _pendingJoin?.TrySetResult(new JoinResult(packet.Status, PeerId.None, packet.Message));
        _pendingJoin = null;
    }

    private async Task BroadcastPeerDirectoryUpdateAsync(TransportConnectionId exceptConnectionId = default)
    {
        if (_transport is null)
            return;

        KeyValuePair<PeerId, TransportConnectionId>[] targets;
        lock (_connectionPeers)
            targets = _peerConnections.ToArray();

        if (targets.Length == 0)
            return;

        var update = new SessionPacket(
            SessionPacket.PeerDirectoryUpdated,
            _options.Application.ApplicationId,
            _options.Application.ProtocolVersion,
            null,
            null,
            PeerId.None,
            Peers.Peers.ToArray(),
            Messages.Registry.GetFingerprint(),
            NetSessionStatus.Ok,
            null);
        var payload = SerializePacket(update);
        foreach (var target in targets)
        {
            if (target.Value == exceptConnectionId)
                continue;

            await _transport.SendAsync(target.Value, payload, NetChannel.System).ConfigureAwait(false);
        }
    }

    private void HandlePeerDirectoryUpdated(SessionPacket packet)
    {
        if (Session.Role != NetSessionRole.Client)
            return;

        var localPeerId = Peers.LocalPeerId;
        var oldPeers = Peers.Peers.ToDictionary(peer => peer.PeerId);
        var newPeers = (packet.Peers ?? Array.Empty<PeerInfo>())
            .Select(peer => peer.PeerId == localPeerId ? peer with { IsLocal = true } : peer)
            .ToArray();
        var newMap = newPeers.ToDictionary(peer => peer.PeerId);
        var events = new List<Action>();

        foreach (var peer in newPeers)
        {
            if (peer.PeerId == localPeerId || peer.IsServer)
                continue;

            if (!oldPeers.TryGetValue(peer.PeerId, out var oldPeer))
            {
                events.Add(() => Session.RaisePeerJoined(new NetPeerJoined(peer)));
                continue;
            }

            if (oldPeer.IsConnected && !peer.IsConnected)
                events.Add(() => Session.RaisePeerDisconnected(new NetPeerDisconnected(peer.PeerId, DisconnectReason.RemoteClosed)));
            else if (!oldPeer.IsConnected && peer.IsConnected)
                events.Add(() => Session.RaisePeerReconnected(new NetPeerReconnected(peer.PeerId)));
        }

        foreach (var oldPeer in oldPeers.Values)
        {
            if (oldPeer.PeerId == localPeerId || oldPeer.IsServer || newMap.ContainsKey(oldPeer.PeerId))
                continue;

            if (oldPeer.IsConnected)
                events.Add(() => Session.RaisePeerDisconnected(new NetPeerDisconnected(oldPeer.PeerId, DisconnectReason.RemoteClosed)));
            events.Add(() => Session.RaisePeerLeft(new NetPeerLeft(oldPeer.PeerId, DisconnectReason.RemoteClosed)));
        }

        Peers.Replace(newPeers);
        Diagnostics.SetConnectedPeerCount(Peers.Peers.Count);
        foreach (var raise in events)
            DispatchFrameworkEvent(raise);
    }

    private void HandleDisconnectNotice(SessionPacket packet)
    {
        if (Session.Role != NetSessionRole.Client)
            return;

        Messages.CancelPendingRequests(NetRequestStatus.SessionClosed, "Server connection was closed.");
        Messages.CancelPendingRelays(NetSendStatus.SessionClosed, "Server connection was closed.");
        Stats.CancelPendingProbes(NetStatsStatus.SessionClosed, "Server connection was closed.");
        ClearLocalSession(packet.DisconnectReason);
    }

    private NetFingerprintCheckResult CheckRemoteFingerprint(string? remoteFingerprint, string remoteName)
    {
        var result = Messages.Registry.CheckFingerprint(remoteFingerprint ?? string.Empty, _options.MessageFingerprintPolicy);
        if (result.Status == NetFingerprintStatus.Match)
            return result;

        var code = result.Status switch
        {
            NetFingerprintStatus.Rejected => "MessageFingerprintMismatchRejected",
            NetFingerprintStatus.Warning => "MessageFingerprintMismatchWarning",
            _ => "MessageFingerprintMismatchIgnored"
        };
        Diagnostics.RecordError(new NetError(code, $"{remoteName} message fingerprint mismatch."));
        return result;
    }

    private void ClearLocalSession(DisconnectReason reason)
    {
        Stats.CancelPendingProbes(NetStatsStatus.SessionClosed, "Local session was closed.");
        _state = NetLifecycleState.Stopped;
        SetSessionRole(NetSessionRole.None);
        Peers.Replace(Array.Empty<PeerInfo>());
        Peers.SetLocalPeer(PeerId.None);
        lock (_connectionPeers)
        {
            _connectionPeers.Clear();
            _peerConnections.Clear();
        }

        _serverConnectionId = TransportConnectionId.None;
        Diagnostics.SetConnectedPeerCount(0);
        DispatchFrameworkEvent(() => Session.RaisePeerDisconnected(new NetPeerDisconnected(PeerId.Server, reason)));
        if (reason == DisconnectReason.ServerClosed)
            DispatchFrameworkEvent(() => Session.RaiseServerClosed(new NetServerClosed(reason)));
    }

    private bool ShouldAutoReconnect(DisconnectReason reason)
    {
        if (_lastJoinOptions?.Reconnect.IsEnabled != true || _lastJoinOptions.Reconnect.Attempts <= 0)
            return false;

        return reason is not (DisconnectReason.LocalClosed or DisconnectReason.ServerClosed or DisconnectReason.Kicked or DisconnectReason.RateLimited);
    }

    private void StartReconnectLoop(JoinOptions options, string reconnectToken, PeerId previousPeerId)
    {
        if (Interlocked.Exchange(ref _reconnectLoopRunning, 1) == 1)
            return;

        _ = ReconnectLoopAsync(options, reconnectToken, previousPeerId);
    }

    private async Task ReconnectLoopAsync(JoinOptions options, string reconnectToken, PeerId previousPeerId)
    {
        try
        {
            for (var attempt = 0; attempt < options.Reconnect.Attempts && !IsDisposed; attempt++)
            {
                try
                {
                    await Task.Delay(options.Reconnect.Interval, _options.TimeProvider, _lifecycleCancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                var join = await JoinAsync(new JoinOptions
                {
                    Host = options.Host,
                    Port = options.Port,
                    Timeout = options.Timeout,
                    AuthPayload = options.AuthPayload,
                    ReconnectToken = reconnectToken,
                    Reconnect = options.Reconnect
                }).ConfigureAwait(false);

                if (!join.Succeeded)
                    continue;

                _lastReconnectToken = join.ReconnectToken ?? reconnectToken;
                DispatchFrameworkEvent(() => Session.RaisePeerReconnected(new NetPeerReconnected(previousPeerId)));
                return;
            }
        }
        finally
        {
            Interlocked.Exchange(ref _reconnectLoopRunning, 0);
        }
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

    private bool CanReachPeer(PeerId peerId)
    {
        if (IsDisposed || _transport is null)
            return false;
        if (peerId == PeerId.Server)
            return _serverConnectionId != TransportConnectionId.None;

        lock (_connectionPeers)
            return _peerConnections.ContainsKey(peerId);
    }

    private void RemoveRemotePeer(PeerId peerId, DisconnectReason reason)
    {
        if (TryMarkPeerTemporarilyDisconnected(peerId, reason))
            return;

        RemoveRemotePeerFinal(peerId, reason, raiseDisconnected: true);
    }

    private bool TryMarkPeerTemporarilyDisconnected(PeerId peerId, DisconnectReason reason)
    {
        if (_hostOptions?.ReconnectPolicy.IsEnabled != true)
            return false;
        if (reason is DisconnectReason.Kicked or DisconnectReason.ServerClosed or DisconnectReason.RateLimited)
            return false;

        var existing = Peers.Peers.FirstOrDefault(p => p.PeerId == peerId);
        if (existing is null)
            return false;

        var token = GetOrCreateReconnectToken(peerId);
        var expiresAt = _options.TimeProvider.GetUtcNow() + _hostOptions.ReconnectPolicy.GraceWindow;
        lock (_connectionPeers)
            _reconnectTokens[token] = new ReconnectEntry(peerId, expiresAt);

        Peers.Upsert(existing with { IsConnected = false });
        Diagnostics.SetConnectedPeerCount(CountConnectedPeers());
        Messages.CancelPendingRequestsForPeer(peerId, NetRequestStatus.SessionClosed, "Peer connection was temporarily disconnected.");
        DispatchFrameworkEvent(() => Session.RaisePeerDisconnected(new NetPeerDisconnected(peerId, reason)));
        _ = BroadcastPeerDirectoryUpdateAsync();
        ScheduleReconnectExpiry(peerId, _hostOptions.ReconnectPolicy.GraceWindow, reason);
        return true;
    }

    private void RemoveRemotePeerFinal(PeerId peerId, DisconnectReason reason, bool raiseDisconnected)
    {
        CancelReconnectExpiry(peerId);
        string? token = null;
        lock (_connectionPeers)
        {
            if (_peerReconnectTokens.Remove(peerId, out var existingToken))
                token = existingToken;
            _peerConnections.Remove(peerId);
            foreach (var pair in _connectionPeers.Where(pair => pair.Value == peerId).ToArray())
                _connectionPeers.Remove(pair.Key);
            if (token is not null)
                _reconnectTokens.Remove(token);
        }

        Peers.Remove(peerId);
        Diagnostics.SetConnectedPeerCount(CountConnectedPeers());
        Messages.CancelPendingRequestsForPeer(peerId, NetRequestStatus.SessionClosed, "Peer connection was closed.");
        if (raiseDisconnected)
            DispatchFrameworkEvent(() => Session.RaisePeerDisconnected(new NetPeerDisconnected(peerId, reason)));
        DispatchFrameworkEvent(() => Session.RaisePeerLeft(new NetPeerLeft(peerId, reason)));
        _ = BroadcastPeerDirectoryUpdateAsync();
    }

    private void SetSessionRole(NetSessionRole role)
    {
        var changed = Session.SetState(role);
        if (changed is not null)
            DispatchFrameworkEvent(() => Session.RaiseStateChanged(changed));
    }

    private void DispatchFrameworkEvent(Action action)
    {
        if (_options.EventDispatcher is null)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Diagnostics.RecordError(new NetError("EventCallbackError", ex.Message, ex));
            }

            return;
        }

        try
        {
            _options.EventDispatcher.Post(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Diagnostics.RecordError(new NetError("EventCallbackError", ex.Message, ex));
                }
            });
        }
        catch (Exception ex)
        {
            Diagnostics.RecordError(new NetError("EventDispatchError", ex.Message, ex));
        }
    }

    private string CreateReconnectToken(PeerId peerId)
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes);
        lock (_connectionPeers)
        {
            _reconnectTokens[token] = new ReconnectEntry(peerId, DateTimeOffset.MaxValue);
            _peerReconnectTokens[peerId] = token;
        }

        return token;
    }

    private string GetOrCreateReconnectToken(PeerId peerId)
    {
        lock (_connectionPeers)
        {
            if (_peerReconnectTokens.TryGetValue(peerId, out var token))
                return token;
        }

        return CreateReconnectToken(peerId);
    }

    private void ScheduleReconnectExpiry(PeerId peerId, TimeSpan graceWindow, DisconnectReason reason)
    {
        CancelReconnectExpiry(peerId);
        var cancellation = new CancellationTokenSource();
        lock (_connectionPeers)
            _reconnectExpiry[peerId] = cancellation;

        _ = ExpireReconnectAsync(peerId, graceWindow, reason, cancellation.Token);
    }

    private async Task ExpireReconnectAsync(PeerId peerId, TimeSpan graceWindow, DisconnectReason reason, CancellationToken token)
    {
        try
        {
            await Task.Delay(graceWindow, _options.TimeProvider, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested)
            return;

        RemoveRemotePeerFinal(peerId, reason, raiseDisconnected: false);
    }

    private void CancelReconnectExpiry(PeerId peerId)
    {
        CancellationTokenSource? cancellation = null;
        lock (_connectionPeers)
        {
            if (_reconnectExpiry.Remove(peerId, out var existing))
                cancellation = existing;
        }

        if (cancellation is null)
            return;

        cancellation.Cancel();
        cancellation.Dispose();
    }

    private void ClearReconnectState()
    {
        CancellationTokenSource[] expirations;
        lock (_connectionPeers)
        {
            expirations = _reconnectExpiry.Values.ToArray();
            _reconnectExpiry.Clear();
            _reconnectTokens.Clear();
            _peerReconnectTokens.Clear();
        }

        foreach (var expiration in expirations)
        {
            expiration.Cancel();
            expiration.Dispose();
        }
    }

    private int CountConnectedPeers()
    {
        return Peers.Peers.Count(peer => peer.IsConnected);
    }

    private readonly record struct ReconnectEntry(PeerId PeerId, DateTimeOffset ExpiresAt);

    private enum NetLifecycleState
    {
        Stopped,
        Connecting,
        Hosting,
        DedicatedServer,
        Client
    }
}
