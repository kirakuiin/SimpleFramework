namespace SimpleFramework.Net;

/// <summary>
/// 定义 Net 模块使用的低层传输接口。
/// </summary>
public interface INetTransport : IAsyncDisposable
{
    /// <summary>
    /// 启动服务器监听。
    /// </summary>
    Task<TransportStartResult> StartServerAsync(NetListenOptions options, CancellationToken token = default);

    /// <summary>
    /// 连接到远端服务器。
    /// </summary>
    Task<TransportConnectResult> ConnectAsync(NetConnectOptions options, CancellationToken token = default);

    /// <summary>
    /// 断开指定传输连接。
    /// </summary>
    Task DisconnectAsync(TransportConnectionId connectionId, DisconnectReason reason = DisconnectReason.LocalClosed);

    /// <summary>
    /// 发送原始字节包。
    /// </summary>
    ValueTask<NetSendResult> SendAsync(TransportConnectionId connectionId, ReadOnlyMemory<byte> data, NetChannel channel, CancellationToken token = default);

    /// <summary>
    /// 远端传输连接建立时触发。
    /// </summary>
    event Action<TransportPeerConnected>? PeerConnected;

    /// <summary>
    /// 远端传输连接断开时触发。
    /// </summary>
    event Action<TransportPeerDisconnected>? PeerDisconnected;

    /// <summary>
    /// 收到原始字节包时触发。
    /// </summary>
    event Action<TransportPacketReceived>? PacketReceived;

    /// <summary>
    /// 传输层发生错误时触发。
    /// </summary>
    event Action<TransportError>? Error;
}

/// <summary>
/// 服务器监听选项。
/// </summary>
public sealed class NetListenOptions
{
    /// <summary>
    /// 绑定地址；为空时由传输实现选择默认地址。
    /// </summary>
    public System.Net.IPAddress? BindAddress { get; init; }

    /// <summary>
    /// 监听端口。TCP 传输允许 0 表示系统分配端口。
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// 最大连接数量。
    /// </summary>
    public int MaxConnections { get; init; } = 32;
}

/// <summary>
/// 客户端连接选项。
/// </summary>
public sealed class NetConnectOptions
{
    /// <summary>
    /// 远端主机名或地址。
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// 远端端口。
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// 连接超时时间。
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// 传输监听启动结果。
/// </summary>
public readonly record struct TransportStartResult(NetTransportStatus Status, string? Message = null);

/// <summary>
/// 传输连接结果。
/// </summary>
public readonly record struct TransportConnectResult(NetTransportStatus Status, TransportConnectionId ConnectionId, string? Message = null);

/// <summary>
/// 传输连接建立事件。
/// </summary>
public readonly record struct TransportPeerConnected(TransportConnectionId ConnectionId);

/// <summary>
/// 传输连接断开事件。
/// </summary>
public readonly record struct TransportPeerDisconnected(TransportConnectionId ConnectionId, DisconnectReason Reason);

/// <summary>
/// 传输收到数据包事件。
/// </summary>
public readonly record struct TransportPacketReceived(TransportConnectionId ConnectionId, ReadOnlyMemory<byte> Data, NetChannel Channel);

/// <summary>
/// 传输错误事件。
/// </summary>
public readonly record struct TransportError(TransportConnectionId ConnectionId, string Message, Exception? Exception = null);

/// <summary>
/// 断开连接的原因。
/// </summary>
public enum DisconnectReason
{
    LocalClosed,
    RemoteClosed,
    ServerClosed,
    Kicked,
    TransportFailed,
    RateLimited
}
