namespace SimpleFramework.Net;

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
    TransportFailed
}

public readonly record struct NetSendResult(NetSendStatus Status, string? Message = null)
{
    public bool Succeeded => Status == NetSendStatus.Ok;
    public static NetSendResult Ok() => new(NetSendStatus.Ok);
}

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

public readonly record struct NetSessionResult(NetSessionStatus Status, string? Message = null)
{
    public bool Succeeded => Status == NetSessionStatus.Ok;
    public static NetSessionResult Ok() => new(NetSessionStatus.Ok);
}

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

public sealed class NetRequestResult<TResponse>
{
    public required NetRequestStatus Status { get; init; }
    public TResponse? Response { get; init; }
    public string? Message { get; init; }
}

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
