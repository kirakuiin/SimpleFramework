using System.Text.Json;

namespace SimpleFramework.Net;

public sealed class NetMessenger
{
    private readonly Func<byte[], ValueTask<NetSendResult>> _sendToServer;
    private readonly NetDiagnostics _diagnostics;
    private readonly INetCodec _codec;
    private readonly Dictionary<Type, List<Action<NetContext, object>>> _handlers = new();

    internal NetMessenger(Func<byte[], ValueTask<NetSendResult>> sendToServer, NetDiagnostics diagnostics, INetCodec? codec = null)
    {
        _sendToServer = sendToServer;
        _diagnostics = diagnostics;
        _codec = codec ?? new JsonNetCodec();
    }

    public NetMessageRegistry Registry { get; } = new();

    public NetMessageDescriptor RegisterMessage<T>() => Registry.Register<T>();

    public void On<T>(Action<NetContext, T> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Registry.Register<T>();

        if (!_handlers.TryGetValue(typeof(T), out var handlers))
        {
            handlers = new List<Action<NetContext, object>>();
            _handlers[typeof(T)] = handlers;
        }

        handlers.Add((ctx, message) => handler(ctx, (T)message));
    }

    public async ValueTask<NetSendResult> SendToServerAsync<T>(T message)
    {
        var descriptor = Registry.Get<T>();
        byte[] payload;
        try
        {
            payload = _codec.Encode(message);
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError(new NetError("MessageEncodeFailed", ex.Message, ex));
            return new NetSendResult(NetSendStatus.TransportFailed, ex.Message);
        }

        var packet = new NetPacket(NetPacket.Message, descriptor.MessageId, PeerId.None, payload);
        return await _sendToServer(JsonSerializer.SerializeToUtf8Bytes(packet)).ConfigureAwait(false);
    }

    internal bool TryHandlePacket(ReadOnlyMemory<byte> data, PeerId senderId)
    {
        NetPacket? packet;
        try
        {
            packet = JsonSerializer.Deserialize<NetPacket>(data.Span);
        }
        catch
        {
            return false;
        }

        if (packet?.Kind != NetPacket.Message)
            return false;
        if (!Registry.TryGet(packet.MessageId, out var descriptor))
        {
            _diagnostics.RecordError(new NetError("UnknownMessage", $"Unknown message id '{packet.MessageId}'."));
            return true;
        }

        object? message;
        try
        {
            message = _codec.Decode(packet.Payload, descriptor.MessageType);
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError(new NetError("MessageDecodeFailed", ex.Message, ex));
            return true;
        }

        if (message is null)
            return true;
        if (!_handlers.TryGetValue(descriptor.MessageType, out var handlers))
            return true;

        var context = new NetContext(senderId);
        foreach (var handler in handlers.ToArray())
        {
            try
            {
                handler(context, message);
            }
            catch (Exception ex)
            {
                _diagnostics.RecordError(new NetError("MessageHandlerError", ex.Message, ex));
            }
        }

        return true;
    }
}
