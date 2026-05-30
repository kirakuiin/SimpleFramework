namespace SimpleFramework.Net;

public sealed class NetSession
{
    public NetSessionRole Role { get; private set; }
    public bool IsRunning => Role != NetSessionRole.None;

    internal void SetState(NetSessionRole role)
    {
        Role = role;
    }
}

public enum NetSessionRole
{
    None,
    Host,
    DedicatedServer,
    Client
}
