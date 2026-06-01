using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace SimpleFramework.Net;

/// <summary>
/// 负责类型化消息注册、编码、发送、请求响应和接收处理。
/// </summary>
public sealed class NetMessenger
{
    private readonly Func<byte[], ValueTask<NetSendResult>> _sendToServer;
    private readonly Func<PeerId, byte[], ValueTask<NetSendResult>> _sendToPeer;
    private readonly Func<IReadOnlyCollection<PeerId>> _getBroadcastTargets;
    private readonly NetDiagnostics _diagnostics;
    private readonly INetCodec _codec;
    private readonly INetEventDispatcher? _dispatcher;
    private readonly TimeProvider _timeProvider;
    private readonly int _maxPacketSize;
    private readonly int _maxSendQueueBytesPerPeer;
    private readonly int _maxSendQueuePacketsPerPeer;
    private readonly int _maxSendsPerSecondPerPeer;
    private readonly object _sendLimitGate = new();
    private readonly Dictionary<Type, List<Func<NetContext, object, ValueTask>>> _handlers = new();
    private readonly Dictionary<Type, RequestHandler> _requestHandlers = new();
    private readonly Dictionary<Type, Func<NetRelayContext, object, bool>> _relayPolicies = new();
    private readonly Dictionary<PeerId, SendLimitState> _sendLimits = new();
    private readonly ConcurrentDictionary<long, PendingRequest> _pendingRequests = new();
    private readonly ConcurrentDictionary<long, TaskCompletionSource<NetSendResult>> _pendingRelays = new();
    private long _nextCorrelationId;
    private int _protocolManifestFrozen;

    internal NetMessenger(
        Func<byte[], ValueTask<NetSendResult>> sendToServer,
        Func<PeerId, byte[], ValueTask<NetSendResult>> sendToPeer,
        Func<IReadOnlyCollection<PeerId>> getBroadcastTargets,
        NetDiagnostics diagnostics,
        int maxPacketSize,
        int maxSendQueueBytesPerPeer,
        int maxSendQueuePacketsPerPeer,
        int maxSendsPerSecondPerPeer,
        TimeProvider? timeProvider = null,
        INetEventDispatcher? dispatcher = null,
        INetCodec? codec = null)
    {
        _sendToServer = sendToServer;
        _sendToPeer = sendToPeer;
        _getBroadcastTargets = getBroadcastTargets;
        _diagnostics = diagnostics;
        _maxPacketSize = maxPacketSize;
        _maxSendQueueBytesPerPeer = maxSendQueueBytesPerPeer;
        _maxSendQueuePacketsPerPeer = maxSendQueuePacketsPerPeer;
        _maxSendsPerSecondPerPeer = maxSendsPerSecondPerPeer;
        _timeProvider = timeProvider ?? TimeProvider.System;
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
    public NetMessageDescriptor RegisterMessage<T>()
    {
        EnsureProtocolManifestCanMutate();
        return Registry.Register<T>();
    }

    /// <summary>
    /// 注册普通消息处理器。
    /// </summary>
    public void On<T>(Action<NetContext, T> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        EnsureProtocolTypesCanBind(typeof(T));
        RegisterHandler(typeof(T), (ctx, message) =>
        {
            handler(ctx, (T)message);
            return ValueTask.CompletedTask;
        });
    }

    /// <summary>
    /// 注册异步普通消息处理器。
    /// </summary>
    public void On<T>(Func<NetContext, T, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        EnsureProtocolTypesCanBind(typeof(T));
        RegisterHandler(typeof(T), async (ctx, message) => await handler(ctx, (T)message).ConfigureAwait(false));
    }

    private void RegisterHandler(Type messageType, Func<NetContext, object, ValueTask> handler)
    {
        Registry.Register(messageType);
        if (!_handlers.TryGetValue(messageType, out var handlers))
        {
            handlers = new List<Func<NetContext, object, ValueTask>>();
            _handlers[messageType] = handlers;
        }

        handlers.Add(handler);
    }

    /// <summary>
    /// 注册请求处理器；每个请求类型只允许一个处理器。
    /// </summary>
    public void OnRequest<TRequest, TResponse>(Func<NetContext, TRequest, TResponse> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        EnsureProtocolTypesCanBind(typeof(TRequest), typeof(TResponse));
        Registry.Register<TRequest>();
        Registry.Register<TResponse>();
        RegisterRequestHandler(
            typeof(TRequest),
            typeof(TResponse),
            (ctx, request) => ValueTask.FromResult<object>(handler(ctx, (TRequest)request)!));
    }

    /// <summary>
    /// 注册异步请求处理器；每个请求类型只允许一个处理器。
    /// </summary>
    public void OnRequest<TRequest, TResponse>(Func<NetContext, TRequest, Task<TResponse>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        EnsureProtocolTypesCanBind(typeof(TRequest), typeof(TResponse));
        Registry.Register<TRequest>();
        Registry.Register<TResponse>();
        RegisterRequestHandler(
            typeof(TRequest),
            typeof(TResponse),
            async (ctx, request) => (await handler(ctx, (TRequest)request).ConfigureAwait(false))!);
    }

    private void RegisterRequestHandler(Type requestType, Type responseType, Func<NetContext, object, ValueTask<object>> handler)
    {
        Registry.Register(requestType);
        Registry.Register(responseType);
        if (_requestHandlers.ContainsKey(requestType))
            throw new InvalidOperationException($"A request handler for '{requestType.FullName}' has already been registered.");

        _requestHandlers[requestType] = new RequestHandler(
            responseType,
            handler);
    }

    /// <summary>
    /// 扫描程序集并绑定标注的消息、请求和流程处理器。
    /// </summary>
    public void RegisterAssemblyHandlers(Assembly assembly, object? target = null, Func<Type, object>? targetFactory = null, Func<Type, bool>? typeFilter = null)
    {
        EnsureProtocolManifestCanMutate();
        var handlerCount = Registry.HandlerDescriptors.Count;
        var requestHandlerCount = Registry.RequestHandlerDescriptors.Count;
        var flowHandlerCount = Registry.FlowHandlerDescriptors.Count;
        Registry.RegisterAssembly(assembly, typeFilter);
        foreach (var descriptor in Registry.HandlerDescriptors.Skip(handlerCount))
        {
            if (typeFilter?.Invoke(descriptor.Method.DeclaringType!) == false)
                continue;

            var method = descriptor.Method;
            RegisterHandler(descriptor.MessageType, (context, message) =>
                InvokeHandlerMethodAsync(method, ResolveHandlerTarget(method, target, targetFactory), context, message));
        }

        foreach (var descriptor in Registry.RequestHandlerDescriptors.Skip(requestHandlerCount))
        {
            if (typeFilter?.Invoke(descriptor.Method.DeclaringType!) == false)
                continue;

            var method = descriptor.Method;
            RegisterRequestHandler(descriptor.RequestType, descriptor.ResponseType, (context, request) =>
                InvokeRequestHandlerMethodAsync(method, ResolveHandlerTarget(method, target, targetFactory), context, request));
        }

        foreach (var descriptor in Registry.FlowHandlerDescriptors.Skip(flowHandlerCount))
        {
            if (typeFilter?.Invoke(descriptor.Method.DeclaringType!) == false)
                continue;

            var method = descriptor.Method;
            RegisterRequestHandler(descriptor.ProposalType, descriptor.ResponseType, (context, proposal) =>
                InvokeRequestHandlerMethodAsync(method, ResolveHandlerTarget(method, target, targetFactory), context, proposal));
        }
    }

    /// <summary>
    /// 注册服务器端中继许可策略。
    /// </summary>
    public void AllowRelay<T>(Func<NetRelayContext, T, bool> policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        EnsureProtocolTypesCanBind(typeof(T));
        Registry.Register<T>();
        _relayPolicies[typeof(T)] = (context, message) => policy(context, (T)message);
    }

    internal void FreezeProtocolManifest()
    {
        Interlocked.Exchange(ref _protocolManifestFrozen, 1);
    }

    /// <summary>
    /// 从客户端向服务器发送类型化消息。
    /// </summary>
    public async ValueTask<NetSendResult> SendToServerAsync<T>(T message)
    {
        var packet = EncodeMessagePacket(message);
        if (packet.Status != NetSendStatus.Ok)
            return new NetSendResult(packet.Status, packet.Message);

        var send = await SendPacketToServerAsync(packet.Data!).ConfigureAwait(false);
        if (send.Succeeded)
            _diagnostics.AddPacketSent(packet.Data!.Length);

        return send;
    }

    /// <summary>
    /// 从服务器向指定对等体发送类型化消息。
    /// </summary>
    public async ValueTask<NetSendResult> SendAsync<T>(PeerId peerId, T message)
    {
        var packet = EncodeMessagePacket(message);
        if (packet.Status != NetSendStatus.Ok)
            return new NetSendResult(packet.Status, packet.Message);

        var send = await SendPacketToPeerAsync(peerId, packet.Data!).ConfigureAwait(false);
        if (send.Succeeded)
            _diagnostics.AddPacketSent(packet.Data!.Length);

        return send;
    }

    /// <summary>
    /// 从服务器向所有远端对等体广播类型化消息。
    /// </summary>
    public async ValueTask<NetSendResult> BroadcastAsync<T>(T message)
    {
        var packet = EncodeMessagePacket(message);
        if (packet.Status != NetSendStatus.Ok)
            return new NetSendResult(packet.Status, packet.Message);

        foreach (var peerId in _getBroadcastTargets())
        {
            var send = await SendPacketToPeerAsync(peerId, packet.Data!).ConfigureAwait(false);
            if (!send.Succeeded)
                return send;

            _diagnostics.AddPacketSent(packet.Data!.Length);
        }

        return NetSendResult.Ok();
    }

    /// <summary>
    /// 通过服务器向另一个对等体中继消息，并等待服务器校验结果。
    /// </summary>
    public async Task<NetSendResult> RelayAsync<T>(
        PeerId targetPeerId,
        T message,
        TimeSpan timeout,
        CancellationToken token = default)
    {
        if (token.IsCancellationRequested)
            return new NetSendResult(NetSendStatus.TransportFailed, "Relay was cancelled.");
        if (timeout <= TimeSpan.Zero)
            return new NetSendResult(NetSendStatus.TransportFailed, "Relay timed out.");

        if (!TryEnsureProtocolTypesCanBind(out var bindError, typeof(T)))
            return new NetSendResult(NetSendStatus.TransportFailed, bindError);

        var descriptor = Registry.Get<T>();
        byte[] payload;
        try
        {
            payload = _codec.Encode(message);
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError(new NetError("RelayEncodeFailed", ex.Message, ex));
            return new NetSendResult(NetSendStatus.TransportFailed, ex.Message);
        }

        var correlationId = Interlocked.Increment(ref _nextCorrelationId);
        var packet = new NetPacket
        {
            Kind = NetPacket.Relay,
            MessageId = descriptor.MessageId,
            TargetPeerId = targetPeerId,
            CorrelationId = correlationId,
            Payload = payload
        };
        var packetBytes = JsonSerializer.SerializeToUtf8Bytes(packet);
        if (packetBytes.Length > _maxPacketSize)
            return new NetSendResult(NetSendStatus.PacketTooLarge);

        var pending = new TaskCompletionSource<NetSendResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRelays[correlationId] = pending;

        var send = await SendPacketToServerAsync(packetBytes).ConfigureAwait(false);
        if (!send.Succeeded)
        {
            _pendingRelays.TryRemove(correlationId, out _);
            return send;
        }

        _diagnostics.AddPacketSent(packetBytes.Length);

        try
        {
            var delay = Task.Delay(timeout, _timeProvider, token);
            var completed = await Task.WhenAny(pending.Task, delay).ConfigureAwait(false);
            if (completed == pending.Task)
                return await pending.Task.ConfigureAwait(false);

            await delay.ConfigureAwait(false);
            _pendingRelays.TryRemove(correlationId, out _);
            return new NetSendResult(NetSendStatus.TransportFailed, "Relay timed out.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            _pendingRelays.TryRemove(correlationId, out _);
            return new NetSendResult(NetSendStatus.TransportFailed, "Relay was cancelled.");
        }
    }

    /// <summary>
    /// 向指定对等体发送请求，并等待与相关 ID 匹配的响应。
    /// </summary>
    public async Task<NetRequestResult<TResponse>> RequestAsync<TRequest, TResponse>(
        PeerId peerId,
        TRequest request,
        TimeSpan timeout,
        CancellationToken token = default)
    {
        if (token.IsCancellationRequested)
            return new NetRequestResult<TResponse> { Status = NetRequestStatus.Cancelled };
        if (timeout <= TimeSpan.Zero)
            return new NetRequestResult<TResponse> { Status = NetRequestStatus.Timeout };

        if (!TryEnsureProtocolTypesCanBind(out var bindError, typeof(TRequest), typeof(TResponse)))
            return new NetRequestResult<TResponse> { Status = NetRequestStatus.TransportFailed, Message = bindError };

        var requestDescriptor = Registry.Get<TRequest>();
        var responseDescriptor = Registry.Get<TResponse>();
        byte[] payload;
        try
        {
            payload = _codec.Encode(request);
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError(new NetError("RequestEncodeFailed", ex.Message, ex));
            return new NetRequestResult<TResponse> { Status = NetRequestStatus.TransportFailed, Message = ex.Message };
        }

        var correlationId = Interlocked.Increment(ref _nextCorrelationId);
        var packet = new NetPacket
        {
            Kind = NetPacket.Request,
            MessageId = requestDescriptor.MessageId,
            ResponseMessageId = responseDescriptor.MessageId,
            CorrelationId = correlationId,
            SenderId = PeerId.None,
            Payload = payload
        };
        var packetBytes = JsonSerializer.SerializeToUtf8Bytes(packet);
        if (packetBytes.Length > _maxPacketSize)
            return new NetRequestResult<TResponse> { Status = NetRequestStatus.PacketTooLarge, Message = "Packet is too large." };

        var pending = new PendingRequest(peerId, responseDescriptor.MessageId);
        _pendingRequests[correlationId] = pending;
        _diagnostics.SetPendingRequestCount(_pendingRequests.Count);
        using var cancellationRegistration = token.CanBeCanceled
            ? token.Register(static state =>
            {
                var cancellation = (PendingRequestCancellation)state!;
                cancellation.Owner.CancelPendingRequest(
                    cancellation.CorrelationId,
                    NetRequestStatus.Cancelled,
                    "Request was cancelled.");
            }, new PendingRequestCancellation(this, correlationId))
            : default;

        if (token.IsCancellationRequested)
            return new NetRequestResult<TResponse> { Status = NetRequestStatus.Cancelled };

        var send = await SendPacketToPeerAsync(peerId, packetBytes).ConfigureAwait(false);
        if (!send.Succeeded)
        {
            RemovePending(correlationId);
            return new NetRequestResult<TResponse>
            {
                Status = MapSendStatusToRequestStatus(send.Status),
                Message = send.Message
            };
        }

        _diagnostics.AddPacketSent(packetBytes.Length);

        PendingResponse response;
        try
        {
            response = await WaitForResponseAsync(correlationId, pending, timeout, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            RemovePending(correlationId);
            return new NetRequestResult<TResponse> { Status = NetRequestStatus.Cancelled };
        }
        catch (TimeoutException)
        {
            RemovePending(correlationId);
            return new NetRequestResult<TResponse> { Status = NetRequestStatus.Timeout };
        }

        if (response.Status != NetRequestStatus.Ok)
            return new NetRequestResult<TResponse> { Status = response.Status, Message = response.Message };

        try
        {
            var decoded = (TResponse?)_codec.Decode(response.Payload, typeof(TResponse));
            return new NetRequestResult<TResponse> { Status = NetRequestStatus.Ok, Response = decoded };
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError(new NetError("ResponseDecodeFailed", ex.Message, ex));
            return new NetRequestResult<TResponse> { Status = NetRequestStatus.TransportFailed, Message = ex.Message };
        }
    }

    internal async Task<bool> TryHandlePacket(ReadOnlyMemory<byte> data, PeerId senderId)
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

        if (packet is null)
            return false;

        return packet.Kind switch
        {
            NetPacket.Message => await HandleMessagePacketAsync(data.Length, packet, senderId).ConfigureAwait(false),
            NetPacket.Request => await HandleRequestPacketAsync(data.Length, packet, senderId).ConfigureAwait(false),
            NetPacket.Response => HandleResponsePacket(data.Length, packet, senderId),
            NetPacket.Relay => await HandleRelayPacketAsync(data.Length, packet, senderId).ConfigureAwait(false),
            NetPacket.RelayResult => HandleRelayResultPacket(data.Length, packet),
            _ => false
        };
    }

    internal void CancelPendingRequests(NetRequestStatus status, string? message = null)
    {
        foreach (var pair in _pendingRequests.ToArray())
        {
            if (_pendingRequests.TryRemove(pair.Key, out var pending))
                pending.Completion.TrySetResult(new PendingResponse(status, Array.Empty<byte>(), message));
        }

        _diagnostics.SetPendingRequestCount(_pendingRequests.Count);
    }

    internal void CancelPendingRequestsForPeer(PeerId peerId, NetRequestStatus status, string? message = null)
    {
        foreach (var pair in _pendingRequests.ToArray())
        {
            if (pair.Value.PeerId != peerId)
                continue;

            if (_pendingRequests.TryRemove(pair.Key, out var pending))
                pending.Completion.TrySetResult(new PendingResponse(status, Array.Empty<byte>(), message));
        }

        _diagnostics.SetPendingRequestCount(_pendingRequests.Count);
    }

    internal void CancelPendingRelays(NetSendStatus status, string? message = null)
    {
        foreach (var pair in _pendingRelays.ToArray())
        {
            if (_pendingRelays.TryRemove(pair.Key, out var pending))
                pending.TrySetResult(new NetSendResult(status, message));
        }
    }

    private bool IsProtocolManifestFrozen => Volatile.Read(ref _protocolManifestFrozen) == 1;

    private void EnsureProtocolManifestCanMutate()
    {
        if (IsProtocolManifestFrozen)
            throw new InvalidOperationException("Message protocol manifest is frozen after the session starts.");
    }

    private void EnsureProtocolTypesCanBind(params Type[] messageTypes)
    {
        if (!IsProtocolManifestFrozen)
            return;

        foreach (var messageType in messageTypes)
        {
            if (!Registry.Contains(messageType))
                throw new InvalidOperationException("Message protocol manifest is frozen after the session starts.");
        }
    }

    private bool TryEnsureProtocolTypesCanBind(out string? error, params Type[] messageTypes)
    {
        try
        {
            EnsureProtocolTypesCanBind(messageTypes);
            error = null;
            return true;
        }
        catch (InvalidOperationException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private async Task<bool> HandleMessagePacketAsync(int byteCount, NetPacket packet, PeerId senderId)
    {
        _diagnostics.AddPacketReceived(byteCount);
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

        var context = new NetContext(packet.SenderId == PeerId.None ? senderId : packet.SenderId);
        foreach (var handler in handlers.ToArray())
        {
            await DispatchHandlerAsync(async () =>
            {
                try
                {
                    await handler(context, message).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _diagnostics.RecordError(new NetError("MessageHandlerError", ex.Message, ex));
                }
            }).ConfigureAwait(false);
        }

        return true;
    }

    private async Task<bool> HandleRequestPacketAsync(int byteCount, NetPacket packet, PeerId senderId)
    {
        _diagnostics.AddPacketReceived(byteCount);
        if (!Registry.TryGet(packet.MessageId, out var requestDescriptor))
        {
            _diagnostics.RecordError(new NetError("UnknownRequest", $"Unknown request message id '{packet.MessageId}'."));
            return true;
        }

        if (!_requestHandlers.TryGetValue(requestDescriptor.MessageType, out var handler))
        {
            await SendRequestResponseAsync(senderId, packet.CorrelationId, packet.ResponseMessageId, NetRequestStatus.NoHandler, Array.Empty<byte>(), null).ConfigureAwait(false);
            return true;
        }

        if (!Registry.TryGet(packet.ResponseMessageId, out var responseDescriptor) || responseDescriptor.MessageType != handler.ResponseType)
        {
            await SendRequestResponseAsync(senderId, packet.CorrelationId, packet.ResponseMessageId, NetRequestStatus.NoHandler, Array.Empty<byte>(), "Response type is not registered for this request.").ConfigureAwait(false);
            return true;
        }

        try
        {
            var request = _codec.Decode(packet.Payload, requestDescriptor.MessageType);
            if (request is null)
            {
                await SendRequestResponseAsync(senderId, packet.CorrelationId, packet.ResponseMessageId, NetRequestStatus.HandlerException, Array.Empty<byte>(), "Request payload decoded to null.").ConfigureAwait(false);
                return true;
            }

            var response = await handler.Invoke(new NetContext(senderId), request).ConfigureAwait(false);
            var responsePayload = _codec.Encode(response);
            await SendRequestResponseAsync(senderId, packet.CorrelationId, packet.ResponseMessageId, NetRequestStatus.Ok, responsePayload, null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError(new NetError("RequestHandlerError", ex.Message, ex));
            await SendRequestResponseAsync(senderId, packet.CorrelationId, packet.ResponseMessageId, NetRequestStatus.HandlerException, Array.Empty<byte>(), ex.Message).ConfigureAwait(false);
        }

        return true;
    }

    private bool HandleResponsePacket(int byteCount, NetPacket packet, PeerId senderId)
    {
        _diagnostics.AddPacketReceived(byteCount);
        if (!_pendingRequests.TryGetValue(packet.CorrelationId, out var pending))
            return true;

        if (pending.PeerId != senderId ||
            packet.MessageId != pending.ResponseMessageId ||
            packet.ResponseMessageId != pending.ResponseMessageId)
        {
            _diagnostics.RecordError(new NetError("UnexpectedResponse", "Response sender or message type did not match the pending request."));
            return true;
        }

        if (!_pendingRequests.TryRemove(packet.CorrelationId, out pending))
            return true;

        _diagnostics.SetPendingRequestCount(_pendingRequests.Count);
        pending.Completion.TrySetResult(new PendingResponse(packet.RequestStatus, packet.Payload, packet.Error));
        return true;
    }

    private async Task<bool> HandleRelayPacketAsync(int byteCount, NetPacket packet, PeerId senderId)
    {
        _diagnostics.AddPacketReceived(byteCount);
        if (!Registry.TryGet(packet.MessageId, out var descriptor))
        {
            _diagnostics.RecordError(new NetError("UnknownRelayMessage", $"Unknown relay message id '{packet.MessageId}'."));
            await SendRelayResultAsync(senderId, packet.CorrelationId, new NetSendResult(NetSendStatus.TransportFailed, "Unknown relay message.")).ConfigureAwait(false);
            return true;
        }

        object? message;
        try
        {
            message = _codec.Decode(packet.Payload, descriptor.MessageType);
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError(new NetError("RelayDecodeFailed", ex.Message, ex));
            await SendRelayResultAsync(senderId, packet.CorrelationId, new NetSendResult(NetSendStatus.TransportFailed, ex.Message)).ConfigureAwait(false);
            return true;
        }

        if (message is null || !_relayPolicies.TryGetValue(descriptor.MessageType, out var policy))
        {
            await SendRelayResultAsync(senderId, packet.CorrelationId, new NetSendResult(NetSendStatus.PermissionDenied)).ConfigureAwait(false);
            return true;
        }

        var relayContext = new NetRelayContext(senderId, packet.TargetPeerId);
        bool allowed;
        try
        {
            allowed = policy(relayContext, message);
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError(new NetError("RelayPolicyError", ex.Message, ex));
            await SendRelayResultAsync(senderId, packet.CorrelationId, new NetSendResult(NetSendStatus.PermissionDenied, "Relay policy denied the message.")).ConfigureAwait(false);
            return true;
        }

        if (!allowed)
        {
            await SendRelayResultAsync(senderId, packet.CorrelationId, new NetSendResult(NetSendStatus.PermissionDenied)).ConfigureAwait(false);
            return true;
        }

        var forwarded = new NetPacket
        {
            Kind = NetPacket.Message,
            MessageId = packet.MessageId,
            SenderId = senderId,
            Payload = packet.Payload
        };
        var forwardedBytes = JsonSerializer.SerializeToUtf8Bytes(forwarded);
        if (forwardedBytes.Length > _maxPacketSize)
        {
            await SendRelayResultAsync(senderId, packet.CorrelationId, new NetSendResult(NetSendStatus.PacketTooLarge)).ConfigureAwait(false);
            return true;
        }

        var send = await SendPacketToPeerAsync(packet.TargetPeerId, forwardedBytes).ConfigureAwait(false);
        if (send.Succeeded)
            _diagnostics.AddPacketSent(forwardedBytes.Length);

        await SendRelayResultAsync(senderId, packet.CorrelationId, send).ConfigureAwait(false);
        return true;
    }

    private bool HandleRelayResultPacket(int byteCount, NetPacket packet)
    {
        _diagnostics.AddPacketReceived(byteCount);
        if (!_pendingRelays.TryRemove(packet.CorrelationId, out var pending))
            return true;

        pending.TrySetResult(new NetSendResult(packet.SendStatus, packet.Error));
        return true;
    }

    private async Task SendRequestResponseAsync(PeerId recipient, long correlationId, ulong responseMessageId, NetRequestStatus status, byte[] payload, string? error)
    {
        var packet = new NetPacket
        {
            Kind = NetPacket.Response,
            MessageId = responseMessageId,
            ResponseMessageId = responseMessageId,
            CorrelationId = correlationId,
            SenderId = PeerId.None,
            Payload = payload,
            RequestStatus = status,
            Error = LimitError(error)
        };
        var packetBytes = JsonSerializer.SerializeToUtf8Bytes(packet);
        if (packetBytes.Length > _maxPacketSize)
        {
            if (status != NetRequestStatus.PacketTooLarge)
            {
                packet = packet with
                {
                    Payload = Array.Empty<byte>(),
                    RequestStatus = NetRequestStatus.PacketTooLarge,
                    Error = "Response packet exceeds MaxPacketSize."
                };
                packetBytes = JsonSerializer.SerializeToUtf8Bytes(packet);
            }

            if (packetBytes.Length > _maxPacketSize)
            {
                _diagnostics.RecordError(new NetError("ResponsePacketTooLarge", "Response packet exceeds MaxPacketSize."));
                return;
            }
        }

        var send = await SendPacketToPeerAsync(recipient, packetBytes).ConfigureAwait(false);
        if (send.Succeeded)
            _diagnostics.AddPacketSent(packetBytes.Length);
    }

    private async Task SendRelayResultAsync(PeerId recipient, long correlationId, NetSendResult result)
    {
        var packet = new NetPacket
        {
            Kind = NetPacket.RelayResult,
            CorrelationId = correlationId,
            SendStatus = result.Status,
            Error = LimitError(result.Message)
        };
        var packetBytes = JsonSerializer.SerializeToUtf8Bytes(packet);
        if (packetBytes.Length > _maxPacketSize)
        {
            packet = packet with { Error = "Relay result exceeded MaxPacketSize." };
            packetBytes = JsonSerializer.SerializeToUtf8Bytes(packet);
        }

        if (packetBytes.Length > _maxPacketSize)
        {
            _diagnostics.RecordError(new NetError("RelayResultPacketTooLarge", "Relay result packet exceeds MaxPacketSize."));
            return;
        }

        var send = await SendPacketToPeerAsync(recipient, packetBytes).ConfigureAwait(false);
        if (send.Succeeded)
            _diagnostics.AddPacketSent(packetBytes.Length);
    }

    private static NetRequestStatus MapSendStatusToRequestStatus(NetSendStatus status)
    {
        return status switch
        {
            NetSendStatus.ObjectDisposed => NetRequestStatus.ObjectDisposed,
            NetSendStatus.SessionClosed => NetRequestStatus.SessionClosed,
            NetSendStatus.PacketTooLarge => NetRequestStatus.PacketTooLarge,
            NetSendStatus.SendQueueFull => NetRequestStatus.SendQueueFull,
            NetSendStatus.RateLimited => NetRequestStatus.RateLimited,
            _ => NetRequestStatus.TransportFailed
        };
    }

    private async Task<PendingResponse> WaitForResponseAsync(long correlationId, PendingRequest pending, TimeSpan timeout, CancellationToken token)
    {
        var delay = Task.Delay(timeout, _timeProvider, token);
        var completed = await Task.WhenAny(pending.Completion.Task, delay).ConfigureAwait(false);
        if (completed == pending.Completion.Task)
            return await pending.Completion.Task.ConfigureAwait(false);

        await delay.ConfigureAwait(false);
        RemovePending(correlationId);
        throw new TimeoutException();
    }

    private ValueTask<NetSendResult> SendPacketToPeerAsync(PeerId peerId, byte[] packet)
    {
        return SendPacketWithLimitsAsync(
            peerId,
            packet,
            () => peerId == PeerId.Server ? _sendToServer(packet) : _sendToPeer(peerId, packet));
    }

    private ValueTask<NetSendResult> SendPacketToServerAsync(byte[] packet)
    {
        return SendPacketWithLimitsAsync(PeerId.Server, packet, () => _sendToServer(packet));
    }

    private async ValueTask<NetSendResult> SendPacketWithLimitsAsync(PeerId peerId, byte[] packet, Func<ValueTask<NetSendResult>> send)
    {
        var reservation = TryReserveSend(peerId, packet.Length);
        if (reservation.Status != NetSendStatus.Ok)
            return new NetSendResult(reservation.Status);

        try
        {
            var result = await send().ConfigureAwait(false);
            if (!result.Succeeded)
                _diagnostics.RecordError(new NetError("TransportSendFailed", result.Message ?? result.Status.ToString()));

            return result;
        }
        finally
        {
            ReleaseSend(peerId, packet.Length);
        }
    }

    private NetSendResult TryReserveSend(PeerId peerId, int byteCount)
    {
        lock (_sendLimitGate)
        {
            var state = GetSendLimitState(peerId);
            var now = _timeProvider.GetUtcNow();
            if (now - state.WindowStartedAt >= TimeSpan.FromSeconds(1))
            {
                state.WindowStartedAt = now;
                state.SentInWindow = 0;
            }

            if (state.SentInWindow >= _maxSendsPerSecondPerPeer)
            {
                _diagnostics.AddDroppedPacket();
                return new NetSendResult(NetSendStatus.RateLimited);
            }

            if (state.InFlightPackets + 1 > _maxSendQueuePacketsPerPeer ||
                state.InFlightBytes + byteCount > _maxSendQueueBytesPerPeer)
            {
                _diagnostics.AddDroppedPacket();
                return new NetSendResult(NetSendStatus.SendQueueFull);
            }

            state.InFlightPackets++;
            state.InFlightBytes += byteCount;
            state.SentInWindow++;
            return NetSendResult.Ok();
        }
    }

    private void ReleaseSend(PeerId peerId, int byteCount)
    {
        lock (_sendLimitGate)
        {
            if (!_sendLimits.TryGetValue(peerId, out var state))
                return;

            state.InFlightPackets = Math.Max(0, state.InFlightPackets - 1);
            state.InFlightBytes = Math.Max(0, state.InFlightBytes - byteCount);
        }
    }

    private SendLimitState GetSendLimitState(PeerId peerId)
    {
        if (_sendLimits.TryGetValue(peerId, out var state))
            return state;

        state = new SendLimitState
        {
            WindowStartedAt = _timeProvider.GetUtcNow()
        };
        _sendLimits[peerId] = state;
        return state;
    }

    private void RemovePending(long correlationId)
    {
        _pendingRequests.TryRemove(correlationId, out _);
        _diagnostics.SetPendingRequestCount(_pendingRequests.Count);
    }

    private void CancelPendingRequest(long correlationId, NetRequestStatus status, string? message)
    {
        if (_pendingRequests.TryRemove(correlationId, out var pending))
        {
            _diagnostics.SetPendingRequestCount(_pendingRequests.Count);
            pending.Completion.TrySetResult(new PendingResponse(status, Array.Empty<byte>(), message));
        }
    }

    private static string? LimitError(string? error)
    {
        const int maxErrorLength = 256;
        return error is null || error.Length <= maxErrorLength
            ? error
            : error[..maxErrorLength] + "...";
    }

    private async ValueTask DispatchHandlerAsync(Func<ValueTask> action)
    {
        if (_dispatcher is null)
        {
            await action().ConfigureAwait(false);
            return;
        }

        try
        {
            _dispatcher.Post(() =>
            {
                try
                {
                    action().AsTask().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _diagnostics.RecordError(new NetError("MessageHandlerError", ex.Message, ex));
                }
            });
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError(new NetError("EventDispatchFailed", ex.Message, ex));
        }
    }

    private static async ValueTask InvokeHandlerMethodAsync(MethodInfo method, object? target, NetContext context, object message)
    {
        var result = method.Invoke(target, new[] { context, message });
        await AwaitPossibleAsyncResult(result).ConfigureAwait(false);
    }

    private static async ValueTask<object> InvokeRequestHandlerMethodAsync(MethodInfo method, object? target, NetContext context, object request)
    {
        var result = method.Invoke(target, new[] { context, request });
        return await UnwrapPossibleAsyncResultAsync(result).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Request handler returned null.");
    }

    private static async ValueTask AwaitPossibleAsyncResult(object? result)
    {
        switch (result)
        {
            case Task task:
                await task.ConfigureAwait(false);
                break;
            case ValueTask valueTask:
                await valueTask.ConfigureAwait(false);
                break;
        }
    }

    private static async ValueTask<object?> UnwrapPossibleAsyncResultAsync(object? result)
    {
        if (result is not null && IsGenericValueTask(result.GetType()))
        {
            var task = (Task)result.GetType().GetMethod("AsTask")!.Invoke(result, Array.Empty<object>())!;
            await task.ConfigureAwait(false);
            return GetTaskResult(task);
        }

        switch (result)
        {
            case Task task:
                await task.ConfigureAwait(false);
                return GetTaskResult(task);
            case ValueTask valueTask:
                await valueTask.ConfigureAwait(false);
                return null;
            default:
                return result;
        }

        static bool IsGenericValueTask(Type type) =>
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>);

        static object? GetTaskResult(Task task)
        {
            var type = task.GetType();
            return type.IsGenericType ? type.GetProperty("Result")?.GetValue(task) : null;
        }
    }

    private static object? ResolveHandlerTarget(MethodInfo method, object? target, Func<Type, object>? targetFactory)
    {
        if (method.IsStatic)
            return null;

        var declaringType = method.DeclaringType
            ?? throw new InvalidOperationException($"Handler method '{method.Name}' has no declaring type.");
        if (target is not null && declaringType.IsInstanceOfType(target))
            return target;
        if (targetFactory is not null)
            return targetFactory(declaringType);

        throw new InvalidOperationException($"Handler method '{declaringType.FullName}.{method.Name}' requires a target instance.");
    }

    private EncodedPacket EncodeMessagePacket<T>(T message)
    {
        if (!TryEnsureProtocolTypesCanBind(out var bindError, typeof(T)))
            return new EncodedPacket(NetSendStatus.TransportFailed, null, bindError);

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

        var packet = new NetPacket
        {
            Kind = NetPacket.Message,
            MessageId = descriptor.MessageId,
            SenderId = PeerId.None,
            Payload = payload
        };
        var packetBytes = JsonSerializer.SerializeToUtf8Bytes(packet);
        return packetBytes.Length > _maxPacketSize
            ? new EncodedPacket(NetSendStatus.PacketTooLarge, null, null)
            : new EncodedPacket(NetSendStatus.Ok, packetBytes, null);
    }

    private readonly record struct EncodedPacket(NetSendStatus Status, byte[]? Data, string? Message);
    private readonly record struct PendingResponse(NetRequestStatus Status, byte[] Payload, string? Message);
    private sealed record RequestHandler(Type ResponseType, Func<NetContext, object, ValueTask<object>> Invoke);
    private sealed record PendingRequestCancellation(NetMessenger Owner, long CorrelationId);

    private sealed class SendLimitState
    {
        public int InFlightPackets { get; set; }
        public int InFlightBytes { get; set; }
        public DateTimeOffset WindowStartedAt { get; set; }
        public int SentInWindow { get; set; }
    }

    private sealed class PendingRequest(PeerId peerId, ulong responseMessageId)
    {
        public PeerId PeerId { get; } = peerId;
        public ulong ResponseMessageId { get; } = responseMessageId;

        public TaskCompletionSource<PendingResponse> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
