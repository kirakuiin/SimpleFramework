namespace SimpleFramework.Net;

/// <summary>
/// 主机或专用服务器启动选项。
/// </summary>
public sealed class HostOptions
{
    /// <summary>
    /// 服务器绑定地址；为空时由传输实现选择默认地址。
    /// </summary>
    public System.Net.IPAddress? BindAddress { get; init; }

    /// <summary>
    /// 监听端口。
    /// </summary>
    public int Port { get; init; }

    /// <summary>
    /// 最大远端对等体数量。
    /// </summary>
    public int MaxPeers { get; init; } = 8;

    /// <summary>
    /// 可选认证回调，用于校验加入请求携带的认证载荷。
    /// </summary>
    public Func<AuthContext, Task<AuthResult>>? Authenticator { get; init; }

    /// <summary>
    /// 重连策略；默认禁用。
    /// </summary>
    public ReconnectPolicy ReconnectPolicy { get; init; } = ReconnectPolicy.Disabled;
}

/// <summary>
/// 客户端加入服务器的选项。
/// </summary>
public sealed class JoinOptions
{
    /// <summary>
    /// 目标主机名或地址。
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// 目标端口。
    /// </summary>
    public int Port { get; init; }

    /// <summary>
    /// 加入握手超时时间。
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 传递给服务器认证器的认证载荷。
    /// </summary>
    public byte[]? AuthPayload { get; init; }

    /// <summary>
    /// 可选重连令牌。
    /// </summary>
    public string? ReconnectToken { get; init; }

    /// <summary>
    /// 客户端断线后的重连策略；默认禁用。
    /// </summary>
    public ReconnectPolicy Reconnect { get; init; } = ReconnectPolicy.Disabled;
}

/// <summary>
/// 描述断线重连策略。
/// </summary>
public sealed class ReconnectPolicy
{
    /// <summary>
    /// 禁用重连。
    /// </summary>
    public static ReconnectPolicy Disabled { get; } = new(false, TimeSpan.Zero);

    /// <summary>
    /// 启用重连并指定宽限时间。
    /// </summary>
    public static ReconnectPolicy Enabled(TimeSpan graceWindow) => new(true, graceWindow);

    /// <summary>
    /// 客户端使用固定间隔自动重试。
    /// </summary>
    public static ReconnectPolicy FixedRetry(int attempts, TimeSpan interval)
    {
        if (attempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(attempts), "Reconnect attempts must be greater than zero.");
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "Reconnect interval must be greater than zero.");

        return new ReconnectPolicy(true, TimeSpan.Zero, attempts, interval);
    }

    private ReconnectPolicy(bool isEnabled, TimeSpan graceWindow, int attempts = 0, TimeSpan interval = default)
    {
        IsEnabled = isEnabled;
        GraceWindow = graceWindow;
        Attempts = attempts;
        Interval = interval;
    }

    /// <summary>
    /// 是否启用重连。
    /// </summary>
    public bool IsEnabled { get; }

    /// <summary>
    /// 允许使用重连令牌恢复身份的时间窗口。
    /// </summary>
    public TimeSpan GraceWindow { get; }

    /// <summary>
    /// 客户端自动重连尝试次数。
    /// </summary>
    public int Attempts { get; }

    /// <summary>
    /// 客户端自动重连间隔。
    /// </summary>
    public TimeSpan Interval { get; }
}

/// <summary>
/// 服务器认证回调的上下文。
/// </summary>
public sealed class AuthContext
{
    /// <summary>
    /// 发起加入请求的传输连接。
    /// </summary>
    public required TransportConnectionId ConnectionId { get; init; }

    /// <summary>
    /// 客户端提供的认证载荷。
    /// </summary>
    public byte[]? AuthPayload { get; init; }
}

/// <summary>
/// 认证结果。
/// </summary>
public readonly record struct AuthResult(bool Succeeded, string? Message = null)
{
    /// <summary>
    /// 创建认证成功结果。
    /// </summary>
    public static AuthResult Ok() => new(true);

    /// <summary>
    /// 创建认证失败结果。
    /// </summary>
    public static AuthResult Fail(string message) => new(false, message);

    /// <summary>
    /// 创建认证拒绝结果。
    /// </summary>
    public static AuthResult Reject(string message) => new(false, message);
}

/// <summary>
/// 客户端加入服务器的结果。
/// </summary>
public readonly record struct JoinResult(NetSessionStatus Status, PeerId PeerId, string? Message = null, string? ReconnectToken = null)
{
    /// <summary>
    /// 是否加入成功。
    /// </summary>
    public bool Succeeded => Status == NetSessionStatus.Ok;
}
