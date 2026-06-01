using System.Collections.Concurrent;
using System.Reflection;

namespace SimpleFramework.Net;

/// <summary>
/// 多人流程聚合策略。
/// </summary>
public sealed class FlowPolicy
{
    private FlowPolicy(string mode, int count = 0)
    {
        Mode = mode;
        Count = count;
    }

    private FlowPolicy(Func<int, int, bool> customEvaluator)
    {
        Mode = "custom";
        CustomEvaluator = customEvaluator;
    }

    /// <summary>
    /// 策略模式。
    /// </summary>
    public string Mode { get; }

    /// <summary>
    /// 策略阈值。
    /// </summary>
    public int Count { get; }

    internal Func<int, int, bool>? CustomEvaluator { get; }

    /// <summary>
    /// 所有目标都接受才成功。
    /// </summary>
    public static FlowPolicy AllAccepted() => new("all");

    /// <summary>
    /// 任一目标接受即成功。
    /// </summary>
    public static FlowPolicy AnyAccepted() => new("any");

    /// <summary>
    /// 超过半数目标接受即成功。
    /// </summary>
    public static FlowPolicy MajorityAccepted() => new("majority");

    /// <summary>
    /// 达到指定接受数量即成功。
    /// </summary>
    public static FlowPolicy Quorum(int count)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Quorum count must be greater than zero.");

        return new FlowPolicy("quorum", count);
    }

    /// <summary>
    /// 使用接受数量和目标数量自定义是否成功。
    /// </summary>
    public static FlowPolicy Custom(Func<int, int, bool> evaluator)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        return new FlowPolicy(evaluator);
    }
}

/// <summary>
/// 单个对等体的流程响应。
/// </summary>
/// <typeparam name="TResponse">响应类型。</typeparam>
public sealed record FlowPeerResponse<TResponse>(PeerId PeerId, TResponse? Response, bool Accepted, string? Message);

/// <summary>
/// 多人流程聚合结果。
/// </summary>
/// <typeparam name="TResponse">响应类型。</typeparam>
public sealed class FlowResult<TResponse>
{
    /// <summary>
    /// 本次流程的稳定标识；未真正启动的流程为 0。
    /// </summary>
    public long FlowId { get; init; }

    /// <summary>
    /// 流程结束原因。
    /// </summary>
    public required FlowEndReason Reason { get; init; }

    /// <summary>
    /// 流程是否被聚合为接受。
    /// </summary>
    public bool Accepted => Reason == FlowEndReason.Accepted;

    /// <summary>
    /// 已收到的对等体响应。
    /// </summary>
    public required IReadOnlyList<FlowPeerResponse<TResponse>> Responses { get; init; }
}

/// <summary>
/// 服务器拥有的多人流程协调组件。
/// </summary>
public sealed class NetFlow
{
    private readonly NetMessenger _messenger;
    private readonly NetDiagnostics _diagnostics;
    private readonly Func<NetSessionRole> _getRole;
    private readonly Func<PeerId, bool> _canReachPeer;
    private readonly TimeProvider _timeProvider;
    private readonly Func<bool> _isDisposed;
    private readonly ConcurrentDictionary<long, IPendingFlow> _pendingFlows = new();
    private long _nextFlowId;

    internal NetFlow(
        NetMessenger messenger,
        NetDiagnostics diagnostics,
        TimeProvider timeProvider,
        Func<NetSessionRole> getRole,
        Func<PeerId, bool>? canReachPeer = null,
        Func<bool>? isDisposed = null)
    {
        _messenger = messenger;
        _diagnostics = diagnostics;
        _timeProvider = timeProvider;
        _getRole = getRole;
        _canReachPeer = canReachPeer ?? (_ => true);
        _isDisposed = isDisposed ?? (() => false);
    }

    /// <summary>
    /// 当前仍在等待响应的流程标识快照。
    /// </summary>
    public IReadOnlyList<long> PendingFlowIds => _pendingFlows.Keys.OrderBy(id => id).ToArray();

    /// <summary>
    /// 查询指定流程中尚未响应的目标对等体。
    /// </summary>
    public IReadOnlyList<PeerId> GetPendingPeers(long flowId)
    {
        return _pendingFlows.TryGetValue(flowId, out var flow)
            ? flow.GetPendingPeers()
            : Array.Empty<PeerId>();
    }

    /// <summary>
    /// 将指定流程的提案手动重发给仍处于 pending 状态的目标对等体。
    /// </summary>
    public Task<NetSendResult> ResendPendingToAsync(long flowId, PeerId peerId)
    {
        if (_isDisposed())
            return Task.FromResult(new NetSendResult(NetSendStatus.ObjectDisposed));

        return _pendingFlows.TryGetValue(flowId, out var flow)
            ? flow.ResendPendingToAsync(peerId)
            : Task.FromResult(new NetSendResult(NetSendStatus.PeerUnavailable, "Flow is not pending."));
    }

    /// <summary>
    /// 注册本地客户端收到流程提案时的处理器。
    /// </summary>
    public void OnProposal<TProposal, TResponse>(Func<NetContext, TProposal, TResponse> handler)
    {
        if (_isDisposed())
            throw new ObjectDisposedException(nameof(NetFlow));

        _messenger.OnRequest(handler);
    }

    /// <summary>
    /// 注册本地客户端收到流程提案时的异步处理器。
    /// </summary>
    public void OnProposal<TProposal, TResponse>(Func<NetContext, TProposal, Task<TResponse>> handler)
    {
        if (_isDisposed())
            throw new ObjectDisposedException(nameof(NetFlow));

        _messenger.OnRequest(handler);
    }

    /// <summary>
    /// 由服务器向一组目标发起流程提案。
    /// </summary>
    public async Task<FlowResult<TResponse>> ProposeAsync<TProposal, TResponse>(
        IEnumerable<PeerId> targets,
        TProposal proposal,
        FlowPolicy policy,
        TimeSpan timeout,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(policy);

        if (_isDisposed())
            throw new ObjectDisposedException(nameof(NetFlow));

        if (_getRole() is not (NetSessionRole.Host or NetSessionRole.DedicatedServer))
            return End<TResponse>(FlowEndReason.NotServer);

        var targetList = targets.Where(peer => peer != PeerId.None && peer != PeerId.Server).Distinct().ToArray();
        if (targetList.Length == 0)
            return End<TResponse>(FlowEndReason.NoTargets);

        if (token.IsCancellationRequested)
            return End<TResponse>(FlowEndReason.Cancelled);

        var flowId = Interlocked.Increment(ref _nextFlowId);
        var flow = new PendingFlow<TProposal, TResponse>(this, flowId, targetList, proposal, policy, timeout, token);
        _pendingFlows[flowId] = flow;
        _diagnostics.SetPendingFlowCount(_pendingFlows.Count);

        flow.Start();
        return await flow.Completion.Task.ConfigureAwait(false);
    }

    internal void CancelPendingFlows(FlowEndReason reason)
    {
        foreach (var flow in _pendingFlows.Values.ToArray())
            flow.Complete(reason);
    }

    private async Task<FlowPeerResponse<TResponse>> RequestPeerAsync<TProposal, TResponse>(
        PeerId peerId,
        TProposal proposal,
        TimeSpan timeout,
        CancellationToken token)
    {
        var result = await _messenger.RequestAsync<TProposal, TResponse>(peerId, proposal, timeout, token).ConfigureAwait(false);
        if (result.Status != NetRequestStatus.Ok)
            return new FlowPeerResponse<TResponse>(peerId, default, false, result.Status.ToString());

        var accepted = IsAccepted(result.Response);
        return new FlowPeerResponse<TResponse>(peerId, result.Response, accepted, null);
    }

    private void RemovePendingFlow(long flowId)
    {
        _pendingFlows.TryRemove(flowId, out _);
        _diagnostics.SetPendingFlowCount(_pendingFlows.Count);
    }

    private static bool EvaluatePolicy(FlowPolicy policy, int acceptedCount, int targetCount)
    {
        return policy.Mode switch
        {
            "all" => acceptedCount == targetCount,
            "any" => acceptedCount > 0,
            "majority" => acceptedCount > targetCount / 2,
            "quorum" => acceptedCount >= policy.Count,
            "custom" => policy.CustomEvaluator?.Invoke(acceptedCount, targetCount) == true,
            _ => false
        };
    }

    private static bool IsAccepted<TResponse>(TResponse? response)
    {
        if (response is null)
            return false;

        var property = typeof(TResponse).GetProperty("Accepted", BindingFlags.Instance | BindingFlags.Public);
        return property?.PropertyType == typeof(bool) && (bool)(property.GetValue(response) ?? false);
    }

    private static FlowResult<TResponse> End<TResponse>(FlowEndReason reason)
    {
        return new FlowResult<TResponse>
        {
            FlowId = 0,
            Reason = reason,
            Responses = Array.Empty<FlowPeerResponse<TResponse>>()
        };
    }

    private static FlowResult<TResponse> End<TResponse>(FlowEndReason reason, IReadOnlyList<FlowPeerResponse<TResponse>> responses)
    {
        return new FlowResult<TResponse>
        {
            FlowId = 0,
            Reason = reason,
            Responses = responses
        };
    }

    private interface IPendingFlow
    {
        IReadOnlyList<PeerId> GetPendingPeers();
        Task<NetSendResult> ResendPendingToAsync(PeerId peerId);
        void Complete(FlowEndReason reason);
    }

    private sealed class PendingFlow<TProposal, TResponse> : IPendingFlow
    {
        private readonly NetFlow _owner;
        private readonly Dictionary<PeerId, FlowPeerResponse<TResponse>> _responses = new();
        private readonly HashSet<PeerId> _pendingPeers;
        private readonly TProposal _proposal;
        private readonly FlowPolicy _policy;
        private readonly TimeSpan _timeout;
        private readonly CancellationToken _token;
        private readonly object _gate = new();
        private bool _completed;

        public PendingFlow(
            NetFlow owner,
            long flowId,
            IReadOnlyCollection<PeerId> targets,
            TProposal proposal,
            FlowPolicy policy,
            TimeSpan timeout,
            CancellationToken token)
        {
            _owner = owner;
            FlowId = flowId;
            _pendingPeers = targets.ToHashSet();
            TargetCount = _pendingPeers.Count;
            _proposal = proposal;
            _policy = policy;
            _timeout = timeout;
            _token = token;
        }

        public long FlowId { get; }
        public int TargetCount { get; }
        public TaskCompletionSource<FlowResult<TResponse>> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<PeerId> GetPendingPeers()
        {
            lock (_gate)
                return _completed ? Array.Empty<PeerId>() : _pendingPeers.OrderBy(peer => peer.Value).ToArray();
        }

        public Task<NetSendResult> ResendPendingToAsync(PeerId peerId)
        {
            lock (_gate)
            {
                if (_completed)
                    return Task.FromResult(new NetSendResult(NetSendStatus.SessionClosed, "Flow is already completed."));
                if (!_pendingPeers.Contains(peerId))
                    return Task.FromResult(new NetSendResult(NetSendStatus.PeerUnavailable, "Peer is not pending in this flow."));
            }

            if (!_owner._canReachPeer(peerId))
                return Task.FromResult(new NetSendResult(NetSendStatus.PeerUnavailable, "Peer is not connected."));

            LaunchRequest(peerId);
            return Task.FromResult(NetSendResult.Ok());
        }

        public void Start()
        {
            foreach (var peerId in _pendingPeers.ToArray())
                LaunchRequest(peerId);

            _ = CompleteAfterTimeoutAsync();
        }

        public void Complete(FlowEndReason reason)
        {
            FlowResult<TResponse>? result = null;
            lock (_gate)
            {
                if (_completed)
                    return;

                _completed = true;
                _pendingPeers.Clear();
                result = new FlowResult<TResponse>
                {
                    FlowId = FlowId,
                    Reason = reason,
                    Responses = _responses.Values.OrderBy(response => response.PeerId.Value).ToArray()
                };
            }

            _owner.RemovePendingFlow(FlowId);
            Completion.TrySetResult(result);
        }

        private void LaunchRequest(PeerId peerId)
        {
            _ = AwaitPeerAsync(peerId);
        }

        private async Task AwaitPeerAsync(PeerId peerId)
        {
            var response = await _owner.RequestPeerAsync<TProposal, TResponse>(peerId, _proposal, _timeout, _token).ConfigureAwait(false);
            ApplyResponse(response);
        }

        private void ApplyResponse(FlowPeerResponse<TResponse> response)
        {
            lock (_gate)
            {
                if (_completed || !_pendingPeers.Contains(response.PeerId))
                    return;

                if (response.Message == NetRequestStatus.SessionClosed.ToString() ||
                    response.Message == NetRequestStatus.TransportFailed.ToString())
                {
                    return;
                }

                if (response.Message == NetRequestStatus.Cancelled.ToString())
                {
                    Complete(FlowEndReason.Cancelled);
                    return;
                }

                if (response.Message == NetRequestStatus.Timeout.ToString())
                {
                    Complete(FlowEndReason.Timeout);
                    return;
                }

                _pendingPeers.Remove(response.PeerId);
                _responses[response.PeerId] = response;

                var acceptedCount = _responses.Values.Count(peerResponse => peerResponse.Accepted);
                if (_policy.Mode == "all" && !response.Accepted)
                {
                    Complete(FlowEndReason.Rejected);
                    return;
                }

                if (EvaluatePolicy(_policy, acceptedCount, TargetCount))
                {
                    Complete(FlowEndReason.Accepted);
                    return;
                }

                if (_pendingPeers.Count == 0)
                    Complete(FlowEndReason.Rejected);
            }
        }

        private async Task CompleteAfterTimeoutAsync()
        {
            try
            {
                await Task.Delay(_timeout, _owner._timeProvider, _token).ConfigureAwait(false);
                Complete(FlowEndReason.Timeout);
            }
            catch (OperationCanceledException) when (_token.IsCancellationRequested)
            {
                Complete(FlowEndReason.Cancelled);
            }
        }
    }
}
