namespace SimpleFramework.Net;

/// <summary>
/// 会话中的通用对等体信息。
/// </summary>
public sealed record PeerInfo(PeerId PeerId, bool IsServer, bool IsLocal, DateTimeOffset JoinedAt, bool IsConnected = true);

/// <summary>
/// 维护当前会话可见的对等体目录。
/// </summary>
public sealed class PeerDirectory
{
    private readonly object _gate = new();
    private readonly Dictionary<PeerId, PeerInfo> _peers = new();

    /// <summary>
    /// 本地对等体标识。
    /// </summary>
    public PeerId LocalPeerId { get; private set; } = PeerId.None;

    /// <summary>
    /// 当前目录快照。
    /// </summary>
    public IReadOnlyCollection<PeerInfo> Peers
    {
        get
        {
            lock (_gate)
                return _peers.Values.ToArray();
        }
    }

    /// <summary>
    /// 设置本地对等体标识。
    /// </summary>
    public void SetLocalPeer(PeerId peerId)
    {
        LocalPeerId = peerId;
    }

    /// <summary>
    /// 添加或更新一个对等体。
    /// </summary>
    public void Upsert(PeerInfo peer)
    {
        lock (_gate)
            _peers[peer.PeerId] = peer;
    }

    /// <summary>
    /// 用给定集合替换整个目录。
    /// </summary>
    public void Replace(IEnumerable<PeerInfo> peers)
    {
        lock (_gate)
        {
            _peers.Clear();
            foreach (var peer in peers)
                _peers[peer.PeerId] = peer;
        }
    }

    /// <summary>
    /// 移除指定对等体。
    /// </summary>
    public bool Remove(PeerId peerId)
    {
        lock (_gate)
            return _peers.Remove(peerId);
    }

    /// <summary>
    /// 获取非本地、非服务器的远端参与者标识。
    /// </summary>
    public IReadOnlyCollection<PeerId> RemoteParticipants()
    {
        lock (_gate)
            return _peers.Values.Where(p => !p.IsLocal && !p.IsServer).Select(p => p.PeerId).ToArray();
    }
}
