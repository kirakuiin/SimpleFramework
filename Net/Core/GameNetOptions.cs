namespace SimpleFramework.Net;

public sealed class NetApplicationInfo
{
    public required Guid ApplicationId { get; init; }
    public int ProtocolVersion { get; init; } = 1;
}

public sealed class GameNetOptions
{
    public string? DebugName { get; init; }
    public required NetApplicationInfo Application { get; init; }
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
    public INetEventDispatcher? EventDispatcher { get; init; }
    public int MaxPacketSize { get; init; } = 64 * 1024;
    public int MaxSendQueueBytesPerPeer { get; init; } = 1024 * 1024;
    public int MaxSendQueuePacketsPerPeer { get; init; } = 1024;
}
