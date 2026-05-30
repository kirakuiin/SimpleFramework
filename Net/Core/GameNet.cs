namespace SimpleFramework.Net;

public sealed class GameNet
{
    private readonly GameNetOptions _options;

    public GameNet(GameNetOptions options)
    {
        ValidateOptions(options);

        _options = options;
        Diagnostics = new NetDiagnostics();
    }

    public GameNetOptions Options => _options;
    public NetDiagnostics Diagnostics { get; }

    public static void ValidateOptions(GameNetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Application);

        if (options.Application.ApplicationId == Guid.Empty)
            throw new ArgumentException("ApplicationId cannot be empty.", nameof(options));
        if (options.Application.ProtocolVersion < 1)
            throw new ArgumentException("ProtocolVersion must be >= 1.", nameof(options));
        if (options.MaxPacketSize <= 0)
            throw new ArgumentException("MaxPacketSize must be > 0.", nameof(options));
        if (options.MaxSendQueueBytesPerPeer <= 0)
            throw new ArgumentException("MaxSendQueueBytesPerPeer must be > 0.", nameof(options));
        if (options.MaxSendQueuePacketsPerPeer <= 0)
            throw new ArgumentException("MaxSendQueuePacketsPerPeer must be > 0.", nameof(options));
    }
}
