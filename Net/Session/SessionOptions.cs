namespace SimpleFramework.Net;

public sealed class HostOptions
{
    public System.Net.IPAddress? BindAddress { get; init; }
    public int Port { get; init; }
    public int MaxPeers { get; init; } = 8;
    public Func<AuthContext, Task<AuthResult>>? Authenticator { get; init; }
    public ReconnectPolicy ReconnectPolicy { get; init; } = ReconnectPolicy.Disabled;
}

public sealed class JoinOptions
{
    public required string Host { get; init; }
    public int Port { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
    public byte[]? AuthPayload { get; init; }
    public string? ReconnectToken { get; init; }
}

public sealed class ReconnectPolicy
{
    public static ReconnectPolicy Disabled { get; } = new(false, TimeSpan.Zero);
    public static ReconnectPolicy Enabled(TimeSpan graceWindow) => new(true, graceWindow);

    private ReconnectPolicy(bool isEnabled, TimeSpan graceWindow)
    {
        IsEnabled = isEnabled;
        GraceWindow = graceWindow;
    }

    public bool IsEnabled { get; }
    public TimeSpan GraceWindow { get; }
}

public sealed class AuthContext
{
    public required TransportConnectionId ConnectionId { get; init; }
    public byte[]? AuthPayload { get; init; }
}

public readonly record struct AuthResult(bool Succeeded, string? Message = null)
{
    public static AuthResult Ok() => new(true);
    public static AuthResult Fail(string message) => new(false, message);
    public static AuthResult Reject(string message) => new(false, message);
}

public readonly record struct JoinResult(NetSessionStatus Status, PeerId PeerId, string? Message = null)
{
    public bool Succeeded => Status == NetSessionStatus.Ok;
}
