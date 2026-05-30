namespace SimpleFramework.Net;

public sealed class NetDiagnostics
{
    private long _packetsSent;
    private long _packetsReceived;
    private long _bytesSent;
    private long _bytesReceived;
    private long _droppedPackets;
    private long _errorCount;
    private int _connectedPeerCount;
    private int _pendingRequestCount;
    private int _pendingFlowCount;

    public event Action<NetError>? ErrorRecorded;

    public void AddPacketSent(long bytes)
    {
        Interlocked.Increment(ref _packetsSent);
        Interlocked.Add(ref _bytesSent, bytes);
    }

    public void AddPacketReceived(long bytes)
    {
        Interlocked.Increment(ref _packetsReceived);
        Interlocked.Add(ref _bytesReceived, bytes);
    }

    public void AddDroppedPacket() => Interlocked.Increment(ref _droppedPackets);

    public void AddError() => RecordError(NetError.Unspecified);

    public void RecordError(NetError error)
    {
        Interlocked.Increment(ref _errorCount);
        ErrorRecorded?.Invoke(error);
    }

    public NetDiagnosticsSnapshot GetSnapshot() => new()
    {
        ConnectedPeerCount = Volatile.Read(ref _connectedPeerCount),
        PendingRequestCount = Volatile.Read(ref _pendingRequestCount),
        PendingFlowCount = Volatile.Read(ref _pendingFlowCount),
        PacketsSent = Interlocked.Read(ref _packetsSent),
        PacketsReceived = Interlocked.Read(ref _packetsReceived),
        BytesSent = Interlocked.Read(ref _bytesSent),
        BytesReceived = Interlocked.Read(ref _bytesReceived),
        DroppedPackets = Interlocked.Read(ref _droppedPackets),
        ErrorCount = Interlocked.Read(ref _errorCount)
    };

    internal void SetConnectedPeerCount(int count) => Volatile.Write(ref _connectedPeerCount, count);
    internal void SetPendingRequestCount(int count) => Volatile.Write(ref _pendingRequestCount, count);
    internal void SetPendingFlowCount(int count) => Volatile.Write(ref _pendingFlowCount, count);
}

public sealed class NetDiagnosticsSnapshot
{
    public int ConnectedPeerCount { get; init; }
    public int PendingRequestCount { get; init; }
    public int PendingFlowCount { get; init; }
    public long PacketsSent { get; init; }
    public long PacketsReceived { get; init; }
    public long BytesSent { get; init; }
    public long BytesReceived { get; init; }
    public long DroppedPackets { get; init; }
    public long ErrorCount { get; init; }
}

public sealed record NetError(string Code, string Message, Exception? Exception = null)
{
    public static readonly NetError Unspecified = new("Unspecified", "An unspecified networking error occurred.");
}
