namespace SimpleFramework.Net;

internal sealed record SessionPacket(
    string Kind,
    Guid ApplicationId,
    int ProtocolVersion,
    byte[]? AuthPayload,
    string? ReconnectToken,
    PeerId PeerId,
    PeerInfo[]? Peers,
    NetSessionStatus Status,
    string? Message)
{
    public const string JoinRequest = "session.join.request";
    public const string JoinAccepted = "session.join.accepted";
    public const string JoinRejected = "session.join.rejected";
}
