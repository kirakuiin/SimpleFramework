namespace SimpleFramework.Net;

internal sealed record NetPacket
{
    public const string Message = "message";
    public const string Request = "request";
    public const string Response = "response";
    public const string Relay = "relay";
    public const string RelayResult = "relay.result";

    public required string Kind { get; init; }
    public ulong MessageId { get; init; }
    public PeerId SenderId { get; init; }
    public PeerId TargetPeerId { get; init; }
    public byte[] Payload { get; init; } = Array.Empty<byte>();
    public long CorrelationId { get; init; }
    public ulong ResponseMessageId { get; init; }
    public NetRequestStatus RequestStatus { get; init; } = NetRequestStatus.Ok;
    public NetSendStatus SendStatus { get; init; } = NetSendStatus.Ok;
    public string? Error { get; init; }
}

/// <summary>
/// 消息处理器收到消息时的上下文。
/// </summary>
public sealed record NetContext(PeerId SenderId);

/// <summary>
/// 服务器校验中继消息时使用的上下文。
/// </summary>
public sealed record NetRelayContext(PeerId SenderId, PeerId TargetPeerId);
