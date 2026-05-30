namespace SimpleFramework.Net;

/// <summary>
/// 收集 Net 模块运行期间的计数器和结构化错误。
/// </summary>
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

    /// <summary>
    /// 记录结构化错误时触发。
    /// </summary>
    public event Action<NetError>? ErrorRecorded;

    /// <summary>
    /// 增加已发送包计数和字节数。
    /// </summary>
    public void AddPacketSent(long bytes)
    {
        Interlocked.Increment(ref _packetsSent);
        Interlocked.Add(ref _bytesSent, bytes);
    }

    /// <summary>
    /// 增加已接收包计数和字节数。
    /// </summary>
    public void AddPacketReceived(long bytes)
    {
        Interlocked.Increment(ref _packetsReceived);
        Interlocked.Add(ref _bytesReceived, bytes);
    }

    /// <summary>
    /// 增加丢弃包计数。
    /// </summary>
    public void AddDroppedPacket() => Interlocked.Increment(ref _droppedPackets);

    /// <summary>
    /// 增加通用错误计数。
    /// </summary>
    public void AddError() => RecordError(NetError.Unspecified);

    /// <summary>
    /// 记录一个结构化错误。
    /// </summary>
    public void RecordError(NetError error)
    {
        Interlocked.Increment(ref _errorCount);
        ErrorRecorded?.Invoke(error);
    }

    /// <summary>
    /// 获取当前诊断数据的只读快照。
    /// </summary>
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

/// <summary>
/// Net 诊断计数器的只读快照。
/// </summary>
public sealed class NetDiagnosticsSnapshot
{
    /// <summary>
    /// 当前连接对等体数量。
    /// </summary>
    public int ConnectedPeerCount { get; init; }

    /// <summary>
    /// 当前等待响应的请求数量。
    /// </summary>
    public int PendingRequestCount { get; init; }

    /// <summary>
    /// 当前等待完成的流程数量。
    /// </summary>
    public int PendingFlowCount { get; init; }

    /// <summary>
    /// 已发送包数量。
    /// </summary>
    public long PacketsSent { get; init; }

    /// <summary>
    /// 已接收包数量。
    /// </summary>
    public long PacketsReceived { get; init; }

    /// <summary>
    /// 已发送字节数。
    /// </summary>
    public long BytesSent { get; init; }

    /// <summary>
    /// 已接收字节数。
    /// </summary>
    public long BytesReceived { get; init; }

    /// <summary>
    /// 丢弃包数量。
    /// </summary>
    public long DroppedPackets { get; init; }

    /// <summary>
    /// 错误数量。
    /// </summary>
    public long ErrorCount { get; init; }
}

/// <summary>
/// Net 模块产生的结构化错误。
/// </summary>
public sealed record NetError(string Code, string Message, Exception? Exception = null)
{
    /// <summary>
    /// 未分类错误。
    /// </summary>
    public static readonly NetError Unspecified = new("Unspecified", "An unspecified networking error occurred.");
}
