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
    private readonly Func<NetSessionRole> _getRole;

    internal NetFlow(NetMessenger messenger, Func<NetSessionRole> getRole)
    {
        _messenger = messenger;
        _getRole = getRole;
    }

    /// <summary>
    /// 注册本地客户端收到流程提案时的处理器。
    /// </summary>
    public void OnProposal<TProposal, TResponse>(Func<NetContext, TProposal, TResponse> handler)
    {
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

        if (_getRole() is not (NetSessionRole.Host or NetSessionRole.DedicatedServer))
            return End<TResponse>(FlowEndReason.NotServer);

        var targetList = targets.Where(peer => peer != PeerId.None && peer != PeerId.Server).Distinct().ToArray();
        if (targetList.Length == 0)
            return End<TResponse>(FlowEndReason.NoTargets);

        if (token.IsCancellationRequested)
            return End<TResponse>(FlowEndReason.Cancelled);

        var tasks = targetList
            .Select(peer => RequestPeerAsync<TProposal, TResponse>(peer, proposal, timeout, token))
            .ToArray();

        FlowPeerResponse<TResponse>[] responses;
        try
        {
            responses = await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return End<TResponse>(FlowEndReason.Cancelled);
        }

        if (responses.Any(response => response.Message == NetRequestStatus.SessionClosed.ToString()))
            return End(FlowEndReason.SessionClosed, responses);
        if (responses.Any(response => response.Message == NetRequestStatus.Timeout.ToString()))
            return End(FlowEndReason.Timeout, responses);

        var acceptedCount = responses.Count(response => response.Accepted);
        var accepted = policy.Mode switch
        {
            "all" => acceptedCount == targetList.Length,
            "any" => acceptedCount > 0,
            "majority" => acceptedCount > targetList.Length / 2,
            "quorum" => acceptedCount >= policy.Count,
            "custom" => policy.CustomEvaluator?.Invoke(acceptedCount, targetList.Length) == true,
            _ => false
        };

        return End(accepted ? FlowEndReason.Accepted : FlowEndReason.Rejected, responses);
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
            Reason = reason,
            Responses = Array.Empty<FlowPeerResponse<TResponse>>()
        };
    }

    private static FlowResult<TResponse> End<TResponse>(FlowEndReason reason, IReadOnlyList<FlowPeerResponse<TResponse>> responses)
    {
        return new FlowResult<TResponse>
        {
            Reason = reason,
            Responses = responses
        };
    }
}
