namespace SimpleFramework.Net;

/// <summary>
/// 描述应用级网络兼容性信息。
/// </summary>
public sealed class NetApplicationInfo
{
    /// <summary>
    /// 应用唯一标识，用于发现和直连握手的兼容性校验。
    /// </summary>
    public required Guid ApplicationId { get; init; }

    /// <summary>
    /// 应用协议版本，不同版本的客户端和服务器默认不可互连。
    /// </summary>
    public int ProtocolVersion { get; init; } = 1;
}

/// <summary>
/// 配置 <see cref="GameNet"/> 实例的共享选项。
/// </summary>
public sealed class GameNetOptions
{
    /// <summary>
    /// 调试名称，用于日志或诊断中区分实例。
    /// </summary>
    public string? DebugName { get; init; }

    /// <summary>
    /// 应用兼容性信息。
    /// </summary>
    public required NetApplicationInfo Application { get; init; }

    /// <summary>
    /// 用于超时、重连和统计的时间源。
    /// </summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    /// <summary>
    /// 可选事件调度器；为空时事件在网络任务上下文执行。
    /// </summary>
    public INetEventDispatcher? EventDispatcher { get; init; }

    /// <summary>
    /// 局域网发现和房间浏览配置。
    /// </summary>
    public DiscoveryOptions Discovery { get; init; } = new();

    /// <summary>
    /// 单个框架包允许的最大字节数。
    /// </summary>
    public int MaxPacketSize { get; init; } = 64 * 1024;

    /// <summary>
    /// 单个对等体发送队列允许的最大字节数。
    /// </summary>
    public int MaxSendQueueBytesPerPeer { get; init; } = 1024 * 1024;

    /// <summary>
    /// 单个对等体发送队列允许的最大包数量。
    /// </summary>
    public int MaxSendQueuePacketsPerPeer { get; init; } = 1024;

    /// <summary>
    /// 单个对等体每秒允许发起的最大包数量。
    /// </summary>
    public int MaxSendsPerSecondPerPeer { get; init; } = int.MaxValue;
}
