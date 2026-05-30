using System.Collections.Concurrent;

namespace SimpleFramework.Net;

/// <summary>
/// 统计探测结果状态。
/// </summary>
public enum NetStatsStatus
{
    Ok,
    ObjectDisposed,
    Timeout,
    SessionClosed,
    PeerUnavailable,
    SendQueueFull,
    RateLimited,
    TransportFailed
}

/// <summary>
/// 单个对等体的应用层统计快照。
/// </summary>
public sealed class NetPeerStats
{
    /// <summary>
    /// 最近一次应用层往返延迟。
    /// </summary>
    public TimeSpan? Rtt { get; init; }

    /// <summary>
    /// 应用层平均往返延迟。
    /// </summary>
    public TimeSpan? AverageRtt { get; init; }

    /// <summary>
    /// 应用层延迟抖动估计。
    /// </summary>
    public TimeSpan? Jitter { get; init; }

    /// <summary>
    /// 应用层探测丢失比例。
    /// </summary>
    public double ProbeLoss { get; init; }

    /// <summary>
    /// 传输层丢包率；TCP 和内存传输无法提供时保持为空。
    /// </summary>
    public double? TransportLoss { get; init; }

    /// <summary>
    /// 最近一次收到该对等体统计响应的时间。
    /// </summary>
    public DateTimeOffset LastSeenAt { get; init; }
}

/// <summary>
/// 应用层统计探测结果。
/// </summary>
public sealed class NetStatsResult
{
    /// <summary>
    /// 探测状态。
    /// </summary>
    public required NetStatsStatus Status { get; init; }

    /// <summary>
    /// 对等体统计快照。
    /// </summary>
    public required NetPeerStats PeerStats { get; init; }

    /// <summary>
    /// 可选错误或诊断信息。
    /// </summary>
    public string? Message { get; init; }
}

/// <summary>
/// 应用层延迟和探测统计组件。
/// </summary>
public sealed class NetStats
{
    private readonly NetMessenger _messenger;
    private readonly TimeProvider _timeProvider;
    private readonly Func<bool> _isDisposed;
    private readonly ConcurrentDictionary<long, PendingProbe> _pending = new();
    private readonly Dictionary<PeerId, MutablePeerStats> _stats = new();
    private readonly object _statsGate = new();
    private long _nextSequence;

    internal NetStats(NetMessenger messenger, TimeProvider timeProvider, Func<bool>? isDisposed = null)
    {
        _messenger = messenger;
        _timeProvider = timeProvider;
        _isDisposed = isDisposed ?? (() => false);
        _messenger.RegisterMessage<NetPing>();
        _messenger.RegisterMessage<NetPong>();
        _messenger.On<NetPing>(HandlePing);
        _messenger.On<NetPong>(HandlePong);
    }

    /// <summary>
    /// 测试用开关；开启后收到 ping 不回复 pong。
    /// </summary>
    public bool DropProbeResponses { get; set; }

    /// <summary>
    /// 获取指定对等体的应用层延迟。
    /// </summary>
    public Task<NetStatsResult> GetLatencyAsync(PeerId peerId)
    {
        return GetLatencyAsync(peerId, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// 获取指定对等体的应用层延迟。
    /// </summary>
    public async Task<NetStatsResult> GetLatencyAsync(PeerId peerId, TimeSpan timeout, CancellationToken token = default)
    {
        if (_isDisposed())
            return new NetStatsResult { Status = NetStatsStatus.ObjectDisposed, PeerStats = GetPeerStats(peerId) };

        var sequence = Interlocked.Increment(ref _nextSequence);
        var sentAt = _timeProvider.GetUtcNow();
        var pending = new PendingProbe(peerId, sentAt);
        _pending[sequence] = pending;
        RecordProbeAttempt(peerId);

        var send = await _messenger.SendAsync(peerId, new NetPing(sequence, sentAt)).ConfigureAwait(false);
        if (!send.Succeeded)
        {
            _pending.TryRemove(sequence, out _);
            RecordProbeFailure(peerId);
            return new NetStatsResult
            {
                Status = MapSendStatus(send.Status),
                PeerStats = GetPeerStats(peerId),
                Message = send.Message
            };
        }

        try
        {
            var delay = Task.Delay(timeout, _timeProvider, token);
            var completed = await Task.WhenAny(pending.Completion.Task, delay).ConfigureAwait(false);
            if (completed == pending.Completion.Task)
            {
                var pong = await pending.Completion.Task.ConfigureAwait(false);
                var now = _timeProvider.GetUtcNow();
                var rtt = now - pong.SentAt;
                var stats = RecordProbeSuccess(peerId, rtt, now);
                return new NetStatsResult { Status = NetStatsStatus.Ok, PeerStats = stats };
            }

            await delay.ConfigureAwait(false);
            _pending.TryRemove(sequence, out _);
            RecordProbeFailure(peerId);
            return new NetStatsResult { Status = NetStatsStatus.Timeout, PeerStats = GetPeerStats(peerId) };
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            _pending.TryRemove(sequence, out _);
            RecordProbeFailure(peerId);
            return new NetStatsResult
            {
                Status = NetStatsStatus.TransportFailed,
                PeerStats = GetPeerStats(peerId),
                Message = "Stats probe was cancelled."
            };
        }
    }

    /// <summary>
    /// 获取指定对等体当前统计快照。
    /// </summary>
    public NetPeerStats GetPeerStats(PeerId peerId)
    {
        lock (_statsGate)
            return GetMutableStats(peerId).ToSnapshot();
    }

    private void HandlePing(NetContext context, NetPing ping)
    {
        if (DropProbeResponses)
            return;

        var receivedAt = _timeProvider.GetUtcNow();
        _ = _messenger.SendAsync(context.SenderId, new NetPong(ping.Sequence, ping.SentAt, receivedAt)).AsTask();
    }

    private void HandlePong(NetContext context, NetPong pong)
    {
        if (_pending.TryRemove(pong.Sequence, out var pending))
            pending.Completion.TrySetResult(pong);
    }

    private void RecordProbeAttempt(PeerId peerId)
    {
        lock (_statsGate)
            GetMutableStats(peerId).ProbeCount++;
    }

    private void RecordProbeFailure(PeerId peerId)
    {
        lock (_statsGate)
            GetMutableStats(peerId).ProbeFailures++;
    }

    private NetPeerStats RecordProbeSuccess(PeerId peerId, TimeSpan rtt, DateTimeOffset now)
    {
        lock (_statsGate)
        {
            var stats = GetMutableStats(peerId);
            stats.SuccessCount++;
            stats.Jitter = stats.Rtt is null ? TimeSpan.Zero : (rtt - stats.Rtt.Value).Duration();
            stats.Rtt = rtt;
            stats.AverageRtt = stats.AverageRtt is null
                ? rtt
                : TimeSpan.FromTicks((stats.AverageRtt.Value.Ticks * (stats.SuccessCount - 1) + rtt.Ticks) / stats.SuccessCount);
            stats.LastSeenAt = now;
            return stats.ToSnapshot();
        }
    }

    private MutablePeerStats GetMutableStats(PeerId peerId)
    {
        if (_stats.TryGetValue(peerId, out var stats))
            return stats;

        stats = new MutablePeerStats();
        _stats[peerId] = stats;
        return stats;
    }

    private static NetStatsStatus MapSendStatus(NetSendStatus status)
    {
        return status switch
        {
            NetSendStatus.ObjectDisposed => NetStatsStatus.ObjectDisposed,
            NetSendStatus.SessionClosed => NetStatsStatus.SessionClosed,
            NetSendStatus.PeerUnavailable => NetStatsStatus.PeerUnavailable,
            NetSendStatus.SendQueueFull => NetStatsStatus.SendQueueFull,
            NetSendStatus.RateLimited => NetStatsStatus.RateLimited,
            _ => NetStatsStatus.TransportFailed
        };
    }

    private sealed class PendingProbe
    {
        public PendingProbe(PeerId peerId, DateTimeOffset sentAt)
        {
            PeerId = peerId;
            SentAt = sentAt;
        }

        public PeerId PeerId { get; }
        public DateTimeOffset SentAt { get; }
        public TaskCompletionSource<NetPong> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class MutablePeerStats
    {
        public TimeSpan? Rtt { get; set; }
        public TimeSpan? AverageRtt { get; set; }
        public TimeSpan? Jitter { get; set; }
        public long ProbeCount { get; set; }
        public long ProbeFailures { get; set; }
        public int SuccessCount { get; set; }
        public DateTimeOffset LastSeenAt { get; set; }

        public NetPeerStats ToSnapshot()
        {
            return new NetPeerStats
            {
                Rtt = Rtt,
                AverageRtt = AverageRtt,
                Jitter = Jitter,
                ProbeLoss = ProbeCount == 0 ? 0 : (double)ProbeFailures / ProbeCount,
                TransportLoss = null,
                LastSeenAt = LastSeenAt
            };
        }
    }
}

[NetMessage("net.stats.ping")]
internal sealed record NetPing(long Sequence, DateTimeOffset SentAt);

[NetMessage("net.stats.pong")]
internal sealed record NetPong(long Sequence, DateTimeOffset SentAt, DateTimeOffset ReceivedAt);
