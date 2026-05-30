namespace SimpleFramework.Net;

internal sealed record NetPacket(
    string Kind,
    ulong MessageId,
    PeerId SenderId,
    byte[] Payload)
{
    public const string Message = "message";
}

/// <summary>
/// 消息处理器收到消息时的上下文。
/// </summary>
public sealed record NetContext(PeerId SenderId);
