namespace SimpleFramework.Net;

/// <summary>
/// 表示当前网络会话的运行状态。
/// </summary>
public sealed class NetSession
{
    /// <summary>
    /// 当前会话角色。
    /// </summary>
    public NetSessionRole Role { get; private set; }

    /// <summary>
    /// 会话是否正在运行。
    /// </summary>
    public bool IsRunning => Role != NetSessionRole.None;

    internal void SetState(NetSessionRole role)
    {
        Role = role;
    }
}

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
