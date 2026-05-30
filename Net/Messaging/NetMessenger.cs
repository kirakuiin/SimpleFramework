using System.Text.Json;

namespace SimpleFramework.Net;

/// <summary>
/// 负责类型化消息注册、编码、发送和处理。
/// </summary>
public sealed class NetMessenger
{
    private readonly Func<byte[], ValueTask<NetSendResult>> _sendToServer;
    private readonly Func<PeerId, byte[], ValueTask<NetSendResult>> _sendToPeer;
    private readonly Func<IReadOnlyCollection<PeerId>> _getBroadcastTargets;
    private readonly NetDiagnostics _diagnostics;
    private readonly INetCodec _codec;
    private readonly INetEventDispatcher? _dispatcher;
    private readonly int _maxPacketSize;
    private readonly Dictionary<Type, List<Action<NetContext, object>>> _handlers = new();

    internal NetMessenger(
        Func<byte[], ValueTask<NetSendResult>> sendToServer,
        Func<PeerId, byte[], ValueTask<NetSendResult>> sendToPeer,
        Func<IReadOnlyCollection<PeerId>> getBroadcastTargets,
        NetDiagnostics diagnostics,
        int maxPacketSize,
        INetEventDispatcher? dispatcher = null,
        INetCodec? codec = null)
    {
        _sendToServer = sendToServer;
        _sendToPeer = sendToPeer;
        _getBroadcastTargets = getBroadcastTargets;
        _diagnostics = diagnostics;
        _maxPacketSize = maxPacketSize;
        _dispatcher = dispatcher;
        _codec = codec ?? new JsonNetCodec();
    }

    /// <summary>
    /// 当前消息注册表。
    /// </summary>
    public NetMessageRegistry Registry { get; } = new();

    /// <summary>
    /// 注册消息类型。
    /// </summary>
    public NetMessageDescriptor RegisterMessage<T>() => Registry.Register<T>();

    /// <summary>
    /// 注册普通消息处理器。
    /// </summary>
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

    /// <summary>
    /// 从客户端向服务器发送消息。
    /// </summary>
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
        var packetBytes = JsonSerializer.SerializeToUtf8Bytes(packet);
        if (packetBytes.Length > _maxPacketSize)
            return new NetSendResult(NetSendStatus.PacketTooLarge);

        var send = await _sendToServer(packetBytes).ConfigureAwait(false);
        if (send.Succeeded)
            _diagnostics.AddPacketSent(packetBytes.Length);

        return send;
    }

    /// <summary>
    /// 从服务器向指定对等体发送消息。
    /// </summary>
    public async ValueTask<NetSendResult> SendAsync<T>(PeerId peerId, T message)
    {
        var packet = EncodePacket(message);
        if (packet.Status != NetSendStatus.Ok)
            return new NetSendResult(packet.Status, packet.Message);

        var send = await _sendToPeer(peerId, packet.Data!).ConfigureAwait(false);
        if (send.Succeeded)
            _diagnostics.AddPacketSent(packet.Data!.Length);

        return send;
    }

    /// <summary>
    /// 从服务器向所有远端对等体广播消息。
    /// </summary>
    public async ValueTask<NetSendResult> BroadcastAsync<T>(T message)
    {
        var packet = EncodePacket(message);
        if (packet.Status != NetSendStatus.Ok)
            return new NetSendResult(packet.Status, packet.Message);

        foreach (var peerId in _getBroadcastTargets())
        {
            var send = await _sendToPeer(peerId, packet.Data!).ConfigureAwait(false);
            if (!send.Succeeded)
                return send;

            _diagnostics.AddPacketSent(packet.Data!.Length);
        }

        return NetSendResult.Ok();
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
        _diagnostics.AddPacketReceived(data.Length);
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
            DispatchHandler(() =>
            {
                try
                {
                    handler(context, message);
                }
                catch (Exception ex)
                {
                    _diagnostics.RecordError(new NetError("MessageHandlerError", ex.Message, ex));
                }
            });
        }

        return true;
    }

    private void DispatchHandler(Action action)
    {
        if (_dispatcher is null)
        {
            action();
            return;
        }

        try
        {
            _dispatcher.Post(action);
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError(new NetError("EventDispatchFailed", ex.Message, ex));
        }
    }

    private EncodedPacket EncodePacket<T>(T message)
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
            return new EncodedPacket(NetSendStatus.TransportFailed, null, ex.Message);
        }

        var packet = new NetPacket(NetPacket.Message, descriptor.MessageId, PeerId.None, payload);
        var packetBytes = JsonSerializer.SerializeToUtf8Bytes(packet);
        return packetBytes.Length > _maxPacketSize
            ? new EncodedPacket(NetSendStatus.PacketTooLarge, null, null)
            : new EncodedPacket(NetSendStatus.Ok, packetBytes, null);
    }

    private readonly record struct EncodedPacket(NetSendStatus Status, byte[]? Data, string? Message);
}
