namespace SimpleFramework.Net;

internal sealed record NetPacket
{
    public const string Message = "message";
    public const string Request = "request";
    public const string Response = "response";

    public required string Kind { get; init; }
    public ulong MessageId { get; init; }
    public PeerId SenderId { get; init; }
    public byte[] Payload { get; init; } = Array.Empty<byte>();
    public long CorrelationId { get; init; }
    public ulong ResponseMessageId { get; init; }
    public NetRequestStatus RequestStatus { get; init; } = NetRequestStatus.Ok;
    public string? Error { get; init; }
}

/// <summary>
/// 消息处理器收到消息时的上下文。
/// </summary>
public sealed record NetContext(PeerId SenderId);
