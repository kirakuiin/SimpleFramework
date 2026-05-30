namespace SimpleFramework.Net;

/// <summary>
/// 表示发送操作的结果状态。
/// </summary>
public enum NetSendStatus
{
    Ok,
    ObjectDisposed,
    SessionClosed,
    PeerUnavailable,
    ConnectionUnavailable,
    ChannelUnsupported,
    PacketTooLarge,
    SendQueueFull,
    RateLimited,
    PermissionDenied,
    TransportFailed
}

/// <summary>
/// 表示一次发送操作的结果。
/// </summary>
/// <param name="Status">发送状态。</param>
/// <param name="Message">可选错误或诊断信息。</param>
public readonly record struct NetSendResult(NetSendStatus Status, string? Message = null)
{
    /// <summary>
    /// 是否发送成功。
    /// </summary>
    public bool Succeeded => Status == NetSendStatus.Ok;

    /// <summary>
    /// 创建成功结果。
    /// </summary>
    public static NetSendResult Ok() => new(NetSendStatus.Ok);
}

/// <summary>
/// 表示传输层启动或连接操作的结果状态。
/// </summary>
public enum NetTransportStatus
{
    Ok,
    ObjectDisposed,
    InvalidEndpoint,
    AlreadyRunning,
    ConnectionRefused,
    Timeout,
    Cancelled,
    TransportFailed
}

/// <summary>
/// 表示会话生命周期或加入操作的结果状态。
/// </summary>
public enum NetSessionStatus
{
    Ok,
    ObjectDisposed,
    InvalidState,
    TransportFailed,
    Cancelled,
    IncompatibleApplication,
    IncompatibleProtocol,
    AuthenticationFailed,
    CapacityFull
}

/// <summary>
/// 表示一次会话操作的结果。
/// </summary>
/// <param name="Status">会话状态。</param>
/// <param name="Message">可选错误或诊断信息。</param>
public readonly record struct NetSessionResult(NetSessionStatus Status, string? Message = null)
{
    /// <summary>
    /// 是否操作成功。
    /// </summary>
    public bool Succeeded => Status == NetSessionStatus.Ok;

    /// <summary>
    /// 创建成功结果。
    /// </summary>
    public static NetSessionResult Ok() => new(NetSessionStatus.Ok);
}

/// <summary>
/// 表示请求响应操作的结果状态。
/// </summary>
public enum NetRequestStatus
{
    Ok,
    Timeout,
    Cancelled,
    NoHandler,
    HandlerException,
    SessionClosed,
    TransportFailed
}

/// <summary>
/// 表示一次请求响应操作的结果。
/// </summary>
/// <typeparam name="TResponse">响应消息类型。</typeparam>
public sealed class NetRequestResult<TResponse>
{
    /// <summary>
    /// 请求结果状态。
    /// </summary>
    public required NetRequestStatus Status { get; init; }

    /// <summary>
    /// 成功时的响应对象。
    /// </summary>
    public TResponse? Response { get; init; }

    /// <summary>
    /// 可选错误或诊断信息。
    /// </summary>
    public string? Message { get; init; }
}

/// <summary>
/// 表示多人流程结束的原因。
/// </summary>
public enum FlowEndReason
{
    Accepted,
    Rejected,
    Timeout,
    Cancelled,
    NoTargets,
    SessionClosed,
    NotServer
}
