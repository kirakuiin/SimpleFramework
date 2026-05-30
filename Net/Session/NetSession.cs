namespace SimpleFramework.Net;

/// <summary>
/// 表示当前网络会话的运行状态。
/// </summary>
public sealed class NetSession
{
    /// <summary>
    /// 会话角色变化时触发。
    /// </summary>
    public event Action<NetSessionStateChanged>? StateChanged;

    /// <summary>
    /// 新对等体加入当前会话时触发。
    /// </summary>
    public event Action<NetPeerJoined>? PeerJoined;

    /// <summary>
    /// 对等体传输连接断开时触发。
    /// </summary>
    public event Action<NetPeerDisconnected>? PeerDisconnected;

    /// <summary>
    /// 对等体从会话目录最终移除时触发。
    /// </summary>
    public event Action<NetPeerLeft>? PeerLeft;

    /// <summary>
    /// 对等体通过重连恢复身份时触发。
    /// </summary>
    public event Action<NetPeerReconnected>? PeerReconnected;

    /// <summary>
    /// 服务器关闭导致本地客户端会话结束时触发。
    /// </summary>
    public event Action<NetServerClosed>? ServerClosed;

    /// <summary>
    /// 当前会话角色。
    /// </summary>
    public NetSessionRole Role { get; private set; }

    /// <summary>
    /// 会话是否正在运行。
    /// </summary>
    public bool IsRunning => Role != NetSessionRole.None;

    internal NetSessionStateChanged? SetState(NetSessionRole role)
    {
        if (Role == role)
            return null;

        var changed = new NetSessionStateChanged(Role, role);
        Role = role;
        return changed;
    }

    internal void RaiseStateChanged(NetSessionStateChanged changed) => StateChanged?.Invoke(changed);
    internal void RaisePeerJoined(NetPeerJoined joined) => PeerJoined?.Invoke(joined);
    internal void RaisePeerDisconnected(NetPeerDisconnected disconnected) => PeerDisconnected?.Invoke(disconnected);
    internal void RaisePeerLeft(NetPeerLeft left) => PeerLeft?.Invoke(left);
    internal void RaisePeerReconnected(NetPeerReconnected reconnected) => PeerReconnected?.Invoke(reconnected);
    internal void RaiseServerClosed(NetServerClosed closed) => ServerClosed?.Invoke(closed);
}

/// <summary>
/// 会话角色变化事件。
/// </summary>
public sealed record NetSessionStateChanged(NetSessionRole OldRole, NetSessionRole NewRole);

/// <summary>
/// 对等体加入事件。
/// </summary>
public sealed record NetPeerJoined(PeerInfo Peer);

/// <summary>
/// 对等体传输连接断开事件。
/// </summary>
public sealed record NetPeerDisconnected(PeerId PeerId, DisconnectReason Reason);

/// <summary>
/// 对等体离开并从目录移除事件。
/// </summary>
public sealed record NetPeerLeft(PeerId PeerId, DisconnectReason Reason);

/// <summary>
/// 对等体重连事件。
/// </summary>
public sealed record NetPeerReconnected(PeerId PeerId);

/// <summary>
/// 服务器关闭事件。
/// </summary>
public sealed record NetServerClosed(DisconnectReason Reason);

/// <summary>
/// 会话角色。
/// </summary>
public enum NetSessionRole
{
    None,
    Host,
    DedicatedServer,
    Client
}
