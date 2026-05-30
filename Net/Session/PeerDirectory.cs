namespace SimpleFramework.Net;

public sealed record PeerInfo(PeerId PeerId, bool IsServer, bool IsLocal, DateTimeOffset JoinedAt);

public sealed class PeerDirectory
{
    private readonly object _gate = new();
    private readonly Dictionary<PeerId, PeerInfo> _peers = new();

    public PeerId LocalPeerId { get; private set; } = PeerId.None;

    public IReadOnlyCollection<PeerInfo> Peers
    {
        get
        {
            lock (_gate)
                return _peers.Values.ToArray();
        }
    }

    public void SetLocalPeer(PeerId peerId)
    {
        LocalPeerId = peerId;
    }

    public void Upsert(PeerInfo peer)
    {
        lock (_gate)
            _peers[peer.PeerId] = peer;
    }

    public void Replace(IEnumerable<PeerInfo> peers)
    {
        lock (_gate)
        {
            _peers.Clear();
            foreach (var peer in peers)
                _peers[peer.PeerId] = peer;
        }
    }

    public bool Remove(PeerId peerId)
    {
        lock (_gate)
            return _peers.Remove(peerId);
    }

    public IReadOnlyCollection<PeerId> RemoteParticipants()
    {
        lock (_gate)
            return _peers.Values.Where(p => !p.IsLocal && !p.IsServer).Select(p => p.PeerId).ToArray();
    }
}
