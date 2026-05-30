namespace SimpleFramework.Net;

public readonly record struct PeerId(ulong Value)
{
    public static readonly PeerId None = new(0);
    public static readonly PeerId Server = new(1);
}

public readonly record struct TransportConnectionId(ulong Value)
{
    public static readonly TransportConnectionId None = new(0);
}

public readonly record struct RoomId(string Value)
{
    public static readonly RoomId None = new(string.Empty);
}
