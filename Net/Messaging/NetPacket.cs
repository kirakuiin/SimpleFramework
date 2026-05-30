namespace SimpleFramework.Net;

internal sealed record NetPacket(
    string Kind,
    ulong MessageId,
    PeerId SenderId,
    byte[] Payload)
{
    public const string Message = "message";
}

public sealed record NetContext(PeerId SenderId);
