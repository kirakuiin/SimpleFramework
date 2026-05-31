namespace SimpleFramework.Net;

internal sealed record SessionPacket(
    string Kind,
    Guid ApplicationId,
    int ProtocolVersion,
    byte[]? AuthPayload,
    string? ReconnectToken,
    PeerId PeerId,
    PeerInfo[]? Peers,
    string? MessageFingerprint,
    NetSessionStatus Status,
    string? Message,
    DisconnectReason DisconnectReason = DisconnectReason.RemoteClosed)
{
    public const string JoinRequest = "session.join.request";
    public const string JoinAccepted = "session.join.accepted";
    public const string JoinRejected = "session.join.rejected";
    public const string PeerDirectoryUpdated = "session.peer_directory.updated";
    public const string DisconnectNotice = "session.disconnect.notice";
}
