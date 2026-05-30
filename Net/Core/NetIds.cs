namespace SimpleFramework.Net;

/// <summary>
/// 表示会话层稳定的对等体标识。
/// </summary>
public readonly record struct PeerId(ulong Value)
{
    /// <summary>
    /// 表示没有有效对等体。
    /// </summary>
    public static readonly PeerId None = new(0);

    /// <summary>
    /// 表示权威服务器端点。
    /// </summary>
    public static readonly PeerId Server = new(1);
}

/// <summary>
/// 表示传输层连接标识，仅在具体传输内部稳定。
/// </summary>
public readonly record struct TransportConnectionId(ulong Value)
{
    /// <summary>
    /// 表示没有有效传输连接。
    /// </summary>
    public static readonly TransportConnectionId None = new(0);
}

/// <summary>
/// 表示房间或大厅的逻辑标识。
/// </summary>
public readonly record struct RoomId(string Value)
{
    /// <summary>
    /// 表示没有有效房间。
    /// </summary>
    public static readonly RoomId None = new(string.Empty);
}
