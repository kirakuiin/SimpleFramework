namespace SimpleFramework.Net;

public interface INetTransport : IAsyncDisposable
{
    Task<TransportStartResult> StartServerAsync(NetListenOptions options, CancellationToken token = default);
    Task<TransportConnectResult> ConnectAsync(NetConnectOptions options, CancellationToken token = default);
    Task DisconnectAsync(TransportConnectionId connectionId, DisconnectReason reason = DisconnectReason.LocalClosed);
    ValueTask<NetSendResult> SendAsync(TransportConnectionId connectionId, ReadOnlyMemory<byte> data, NetChannel channel, CancellationToken token = default);

    event Action<TransportPeerConnected>? PeerConnected;
    event Action<TransportPeerDisconnected>? PeerDisconnected;
    event Action<TransportPacketReceived>? PacketReceived;
    event Action<TransportError>? Error;
}

public sealed class NetListenOptions
{
    public System.Net.IPAddress? BindAddress { get; init; }
    public required int Port { get; init; }
    public int MaxConnections { get; init; } = 32;
}

public sealed class NetConnectOptions
{
    public required string Host { get; init; }
    public required int Port { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
}

public readonly record struct TransportStartResult(NetTransportStatus Status, string? Message = null);
public readonly record struct TransportConnectResult(NetTransportStatus Status, TransportConnectionId ConnectionId, string? Message = null);
public readonly record struct TransportPeerConnected(TransportConnectionId ConnectionId);
public readonly record struct TransportPeerDisconnected(TransportConnectionId ConnectionId, DisconnectReason Reason);
public readonly record struct TransportPacketReceived(TransportConnectionId ConnectionId, ReadOnlyMemory<byte> Data, NetChannel Channel);
public readonly record struct TransportError(TransportConnectionId ConnectionId, string Message, Exception? Exception = null);

public enum DisconnectReason
{
    LocalClosed,
    RemoteClosed,
    ServerClosed,
    Kicked,
    TransportFailed,
    RateLimited
}
