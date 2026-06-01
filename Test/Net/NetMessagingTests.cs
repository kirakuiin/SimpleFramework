using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleFramework.Net;
using Test.Net.TestDoubles;

namespace Test.Net;

[NetMessage("player.ready")]
public sealed record PlayerReady(bool Ready);

[NetMessage("player.ready")]
public sealed record DuplicatePlayerReady(bool Ready);

[NetMessage("player.ready")]
public sealed record PlayerReadyWithSchemaChange(bool Ready, int Revision);

[NetMessage("big.message")]
public sealed record BigMessage(string Text);

[NetMessage("late.message")]
public sealed record LateMessage(int Value);

[NetMessage("handler.check")]
public sealed record HandlerCheck(int Value);

[NetMessage("join.room.request")]
public sealed record JoinRoomRequest(string RoomId);

[NetMessage("join.room.response")]
public sealed record JoinRoomResponse(bool Accepted, string Reason);

[NetMessage("scan.alpha")]
public sealed record ScanAlpha(int Value);

[NetMessage("scan.beta")]
public sealed record ScanBeta(int Value);

[NetMessage("attribute.message")]
public sealed record AttributeMessage(int Value);

[NetMessage("attribute.request")]
public sealed record AttributeRequest(int Value);

[NetMessage("attribute.response")]
public sealed record AttributeResponse(int Value);

public sealed record MissingMessageAttribute(int Value);

[NetMessage("")]
public sealed record EmptyMessageKey(int Value);

public sealed class ScannedNetHandlers
{
    public static TaskCompletionSource<ScanBeta> ReceivedBeta { get; set; } = null!;

    [NetHandler(typeof(ScanBeta))]
    public static void HandleBeta(NetContext context, ScanBeta message)
    {
        ReceivedBeta.TrySetResult(message);
    }

    [NetRequestHandler(typeof(ScanAlpha), typeof(ScanBeta))]
    public static ScanBeta HandleRequest(NetContext context, ScanAlpha request)
    {
        return new ScanBeta(request.Value);
    }

    [NetFlowHandler(typeof(ScanAlpha), typeof(ScanBeta))]
    public static ScanBeta HandleFlow(NetContext context, ScanAlpha proposal)
    {
        return new ScanBeta(proposal.Value);
    }
}

public sealed class RuntimeAttributedHandlers
{
    public static TaskCompletionSource<AttributeMessage> ReceivedMessage { get; set; } = null!;
    public static TaskCompletionSource<AttributeRequest> AsyncRequestStarted { get; set; } = null!;

    [NetHandler(typeof(AttributeMessage))]
    public static void HandleMessage(NetContext context, AttributeMessage message)
    {
        ReceivedMessage.TrySetResult(message);
    }

    [NetRequestHandler(typeof(AttributeRequest), typeof(AttributeResponse))]
    public static AttributeResponse HandleRequest(NetContext context, AttributeRequest request)
    {
        return new AttributeResponse(request.Value + 1);
    }
}

public sealed class RuntimeAsyncAttributedHandlers
{
    [NetRequestHandler(typeof(AttributeRequest), typeof(AttributeResponse))]
    public static async Task<AttributeResponse> HandleRequestAsync(NetContext context, AttributeRequest request)
    {
        RuntimeAttributedHandlers.AsyncRequestStarted.TrySetResult(request);
        await Task.Yield();
        return new AttributeResponse(request.Value + 10);
    }
}

[TestFixture]
public class NetMessagingTests
{
    [Test]
    public async Task ClientSendToServer_DeliversTypedMessageWithSenderContext()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));
        var received = new TaskCompletionSource<(PeerId Sender, PlayerReady Message)>();

        server.Messages.RegisterMessage<PlayerReady>();
        client.Messages.RegisterMessage<PlayerReady>();
        server.On<PlayerReady>((ctx, msg) =>
        {
            received.TrySetResult((ctx.SenderId, msg));
        });

        await server.HostAsync(new HostOptions { Port = 7777 });
        var join = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var send = await client.SendToServerAsync(new PlayerReady(true));

        Assert.That(send.Status, Is.EqualTo(NetSendStatus.Ok));
        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(result.Sender, Is.EqualTo(join.PeerId));
        Assert.That(result.Message.Ready, Is.True);
    }

    [Test]
    public async Task SendToServer_AfterJoinWithUnregisteredMessage_ReturnsFailureAndDoesNotMutateFingerprint()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));
        server.Messages.RegisterMessage<PlayerReady>();
        client.Messages.RegisterMessage<PlayerReady>();
        var fingerprintBeforeJoin = client.Messages.Registry.GetFingerprint();

        await server.HostAsync(new HostOptions { Port = 7777 });
        var join = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var send = await client.SendToServerAsync(new LateMessage(1));

        Assert.That(join.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(send.Status, Is.EqualTo(NetSendStatus.TransportFailed));
        Assert.That(send.Message, Does.Contain("frozen"));
        Assert.That(client.Messages.Registry.GetFingerprint(), Is.EqualTo(fingerprintBeforeJoin));
    }

    [Test]
    public async Task SuccessfulMessage_UpdatesSenderAndReceiverDiagnostics()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));
        var received = new TaskCompletionSource<PlayerReady>();

        server.Messages.RegisterMessage<PlayerReady>();
        client.Messages.RegisterMessage<PlayerReady>();
        server.On<PlayerReady>((_, msg) => received.TrySetResult(msg));

        await server.HostAsync(new HostOptions { Port = 7777 });
        await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var send = await client.SendToServerAsync(new PlayerReady(true));
        await received.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var clientSnapshot = client.Diagnostics.GetSnapshot();
        var serverSnapshot = server.Diagnostics.GetSnapshot();

        Assert.That(send.Status, Is.EqualTo(NetSendStatus.Ok));
        Assert.That(clientSnapshot.PacketsSent, Is.EqualTo(1));
        Assert.That(clientSnapshot.BytesSent, Is.GreaterThan(0));
        Assert.That(serverSnapshot.PacketsReceived, Is.EqualTo(1));
        Assert.That(serverSnapshot.BytesReceived, Is.GreaterThan(0));
    }

    [Test]
    public async Task ConfiguredEventDispatcher_ReceivesMessageHandlerCallbacks()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        var dispatcher = new InlineCountingDispatcher();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId, dispatcher));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));
        var received = new TaskCompletionSource<PlayerReady>();

        server.Messages.RegisterMessage<PlayerReady>();
        client.Messages.RegisterMessage<PlayerReady>();
        server.On<PlayerReady>((_, msg) => received.TrySetResult(msg));

        await server.HostAsync(new HostOptions { Port = 7777 });
        await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var postsBeforeMessage = dispatcher.PostCount;
        await client.SendToServerAsync(new PlayerReady(true));

        Assert.That((await received.Task.WaitAsync(TimeSpan.FromSeconds(1))).Ready, Is.True);
        Assert.That(dispatcher.PostCount, Is.GreaterThan(postsBeforeMessage));
    }

    [Test]
    public void RegisterDuplicateMessageKey_Throws()
    {
        var registry = new NetMessageRegistry();
        registry.Register<PlayerReady>();

        var ex = Assert.Throws<InvalidOperationException>(() => registry.Register<DuplicatePlayerReady>());

        Assert.That(ex!.Message, Does.Contain("player.ready"));
    }

    [Test]
    public void RegisterMessageWithoutAttribute_Throws()
    {
        var registry = new NetMessageRegistry();

        var ex = Assert.Throws<InvalidOperationException>(() => registry.Register<MissingMessageAttribute>());

        Assert.That(ex!.Message, Does.Contain(nameof(NetMessageAttribute)));
    }

    [Test]
    public void RegisterMessageWithEmptyKey_Throws()
    {
        var registry = new NetMessageRegistry();

        var ex = Assert.Throws<InvalidOperationException>(() => registry.Register<EmptyMessageKey>());

        Assert.That(ex!.Message, Does.Contain("empty message key"));
    }

    [Test]
    public async Task ServerBroadcast_DeliversToAllClients()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var clientA = new GameNet(network.CreateTransport("client-a"), Options(appId));
        await using var clientB = new GameNet(network.CreateTransport("client-b"), Options(appId));
        var receivedA = new TaskCompletionSource<PlayerReady>();
        var receivedB = new TaskCompletionSource<PlayerReady>();

        server.Messages.RegisterMessage<PlayerReady>();
        clientA.Messages.RegisterMessage<PlayerReady>();
        clientB.Messages.RegisterMessage<PlayerReady>();
        clientA.On<PlayerReady>((_, msg) => receivedA.TrySetResult(msg));
        clientB.On<PlayerReady>((_, msg) => receivedB.TrySetResult(msg));

        await server.HostAsync(new HostOptions { Port = 7777 });
        await clientA.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        await clientB.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var send = await server.BroadcastAsync(new PlayerReady(true));

        Assert.That(send.Status, Is.EqualTo(NetSendStatus.Ok));
        Assert.That((await receivedA.Task.WaitAsync(TimeSpan.FromSeconds(1))).Ready, Is.True);
        Assert.That((await receivedB.Task.WaitAsync(TimeSpan.FromSeconds(1))).Ready, Is.True);
    }

    [Test]
    public async Task ServerSend_DeliversOnlyToTargetPeer()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var clientA = new GameNet(network.CreateTransport("client-a"), Options(appId));
        await using var clientB = new GameNet(network.CreateTransport("client-b"), Options(appId));
        var receivedA = new TaskCompletionSource<PlayerReady>();
        var receivedB = new TaskCompletionSource<PlayerReady>();

        server.Messages.RegisterMessage<PlayerReady>();
        clientA.Messages.RegisterMessage<PlayerReady>();
        clientB.Messages.RegisterMessage<PlayerReady>();
        clientA.On<PlayerReady>((_, msg) => receivedA.TrySetResult(msg));
        clientB.On<PlayerReady>((_, msg) => receivedB.TrySetResult(msg));

        await server.HostAsync(new HostOptions { Port = 7777 });
        var joinA = await clientA.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        await clientB.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var send = await server.SendAsync(joinA.PeerId, new PlayerReady(true));

        Assert.That(send.Status, Is.EqualTo(NetSendStatus.Ok));
        Assert.That((await receivedA.Task.WaitAsync(TimeSpan.FromSeconds(1))).Ready, Is.True);
        Assert.That(receivedB.Task.IsCompleted, Is.False);
    }

    [Test]
    public async Task SendToMissingPeer_ReturnsPeerUnavailable()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        server.Messages.RegisterMessage<PlayerReady>();

        await server.HostAsync(new HostOptions { Port = 7777 });

        var send = await server.SendAsync(new PeerId(999), new PlayerReady(true));

        Assert.That(send.Status, Is.EqualTo(NetSendStatus.PeerUnavailable));
    }

    [Test]
    public async Task SendToServerBeforeJoin_ReturnsSessionClosed()
    {
        var network = new MemoryNetNetwork();
        await using var client = new GameNet(network.CreateTransport("client"), Options(Guid.NewGuid()));
        client.Messages.RegisterMessage<PlayerReady>();

        var send = await client.SendToServerAsync(new PlayerReady(true));

        Assert.That(send.Status, Is.EqualTo(NetSendStatus.SessionClosed));
    }

    [Test]
    public async Task RelayAsync_PreservesOriginalSender()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var clientA = new GameNet(network.CreateTransport("client-a"), Options(appId));
        await using var clientB = new GameNet(network.CreateTransport("client-b"), Options(appId));
        var received = new TaskCompletionSource<(PeerId Sender, PlayerReady Message)>();

        server.Messages.RegisterMessage<PlayerReady>();
        clientA.Messages.RegisterMessage<PlayerReady>();
        clientB.Messages.RegisterMessage<PlayerReady>();
        server.Messages.AllowRelay<PlayerReady>((ctx, msg) => ctx.TargetPeerId == ctx.SenderId ? false : msg.Ready);
        clientB.On<PlayerReady>((ctx, msg) => received.TrySetResult((ctx.SenderId, msg)));

        await server.HostAsync(new HostOptions { Port = 7777 });
        var joinA = await clientA.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        var joinB = await clientB.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var relay = await clientA.RelayAsync(joinB.PeerId, new PlayerReady(true), TimeSpan.FromSeconds(1));
        var delivered = await received.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(relay.Status, Is.EqualTo(NetSendStatus.Ok));
        Assert.That(delivered.Sender, Is.EqualTo(joinA.PeerId));
        Assert.That(delivered.Message.Ready, Is.True);
    }

    [Test]
    public async Task RelayAsync_WhenPolicyDenies_ReturnsPermissionDeniedAndDoesNotForward()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var clientA = new GameNet(network.CreateTransport("client-a"), Options(appId));
        await using var clientB = new GameNet(network.CreateTransport("client-b"), Options(appId));
        var received = new TaskCompletionSource<PlayerReady>();

        server.Messages.RegisterMessage<PlayerReady>();
        clientA.Messages.RegisterMessage<PlayerReady>();
        clientB.Messages.RegisterMessage<PlayerReady>();
        server.Messages.AllowRelay<PlayerReady>((_, _) => false);
        clientB.On<PlayerReady>((_, msg) => received.TrySetResult(msg));

        await server.HostAsync(new HostOptions { Port = 7777 });
        await clientA.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        var joinB = await clientB.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var relay = await clientA.RelayAsync(joinB.PeerId, new PlayerReady(true), TimeSpan.FromSeconds(1));

        Assert.That(relay.Status, Is.EqualTo(NetSendStatus.PermissionDenied));
        Assert.That(received.Task.IsCompleted, Is.False);
    }

    [Test]
    public async Task RelayAsync_ToMissingTarget_ReturnsPeerUnavailable()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));

        server.Messages.RegisterMessage<PlayerReady>();
        client.Messages.RegisterMessage<PlayerReady>();
        server.Messages.AllowRelay<PlayerReady>((_, _) => true);

        await server.HostAsync(new HostOptions { Port = 7777 });
        await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var relay = await client.RelayAsync(new PeerId(999), new PlayerReady(true), TimeSpan.FromSeconds(1));

        Assert.That(relay.Status, Is.EqualTo(NetSendStatus.PeerUnavailable));
    }

    [Test]
    public async Task MultipleHandlers_RunInOrder_AndExceptionIsContained()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));
        var calls = new List<int>();
        var completed = new TaskCompletionSource();

        server.Messages.RegisterMessage<HandlerCheck>();
        client.Messages.RegisterMessage<HandlerCheck>();
        server.On<HandlerCheck>((_, _) => calls.Add(1));
        server.On<HandlerCheck>((_, _) => throw new InvalidOperationException("handler failed"));
        server.On<HandlerCheck>((_, _) =>
        {
            calls.Add(3);
            completed.TrySetResult();
        });

        await server.HostAsync(new HostOptions { Port = 7777 });
        await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var send = await client.SendToServerAsync(new HandlerCheck(1));
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(send.Status, Is.EqualTo(NetSendStatus.Ok));
        Assert.That(calls, Is.EqualTo(new[] { 1, 3 }));
        Assert.That(server.Diagnostics.GetSnapshot().ErrorCount, Is.EqualTo(1));
    }

    [Test]
    public async Task OversizedMessage_ReturnsPacketTooLarge()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        var options = new GameNetOptions
        {
            Application = new NetApplicationInfo { ApplicationId = appId },
            MaxPacketSize = 8
        };
        await using var server = new GameNet(network.CreateTransport("server"), options);
        await using var client = new GameNet(network.CreateTransport("client"), options);

        server.Messages.RegisterMessage<BigMessage>();
        client.Messages.RegisterMessage<BigMessage>();
        await server.HostAsync(new HostOptions { Port = 7777 });
        await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var result = await client.SendToServerAsync(new BigMessage(new string('x', 100)));

        Assert.That(result.Status, Is.EqualTo(NetSendStatus.PacketTooLarge));
    }

    [Test]
    public async Task SendToServer_WhenSendQueuePacketLimitExceeded_ReturnsSendQueueFull()
    {
        var diagnostics = new NetDiagnostics();
        var sendStarted = new TaskCompletionSource();
        var releaseSend = new TaskCompletionSource();
        var messenger = CreateMessenger(
            diagnostics,
            async _ =>
            {
                sendStarted.TrySetResult();
                await releaseSend.Task;
                return NetSendResult.Ok();
            },
            maxSendQueuePacketsPerPeer: 1);
        messenger.RegisterMessage<PlayerReady>();

        var first = messenger.SendToServerAsync(new PlayerReady(true)).AsTask();
        await sendStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var second = await messenger.SendToServerAsync(new PlayerReady(true));

        releaseSend.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(second.Status, Is.EqualTo(NetSendStatus.SendQueueFull));
        Assert.That(diagnostics.GetSnapshot().DroppedPackets, Is.EqualTo(1));
    }

    [Test]
    public async Task SendToServer_WhenSendQueueByteLimitExceeded_ReturnsSendQueueFull()
    {
        var diagnostics = new NetDiagnostics();
        var messenger = CreateMessenger(
            diagnostics,
            _ => new ValueTask<NetSendResult>(NetSendResult.Ok()),
            maxSendQueueBytesPerPeer: 8);
        messenger.RegisterMessage<BigMessage>();

        var result = await messenger.SendToServerAsync(new BigMessage(new string('x', 100)));

        Assert.That(result.Status, Is.EqualTo(NetSendStatus.SendQueueFull));
        Assert.That(diagnostics.GetSnapshot().DroppedPackets, Is.EqualTo(1));
    }

    [Test]
    public async Task SendToServer_WhenRateLimitExceeded_ReturnsRateLimitedUntilWindowResets()
    {
        var diagnostics = new NetDiagnostics();
        var clock = new ManualTimeProvider();
        var messenger = CreateMessenger(
            diagnostics,
            _ => new ValueTask<NetSendResult>(NetSendResult.Ok()),
            timeProvider: clock,
            maxSendsPerSecondPerPeer: 1);
        messenger.RegisterMessage<PlayerReady>();

        var first = await messenger.SendToServerAsync(new PlayerReady(true));
        var second = await messenger.SendToServerAsync(new PlayerReady(true));
        clock.Advance(TimeSpan.FromSeconds(1));
        var third = await messenger.SendToServerAsync(new PlayerReady(true));

        Assert.That(first.Status, Is.EqualTo(NetSendStatus.Ok));
        Assert.That(second.Status, Is.EqualTo(NetSendStatus.RateLimited));
        Assert.That(third.Status, Is.EqualTo(NetSendStatus.Ok));
        Assert.That(diagnostics.GetSnapshot().DroppedPackets, Is.EqualTo(1));
    }

    [Test]
    public async Task SendToServer_WhenTransportFails_RecordsStructuredError()
    {
        var diagnostics = new NetDiagnostics();
        NetError recorded = null;
        diagnostics.ErrorRecorded += error => recorded = error;
        var messenger = CreateMessenger(
            diagnostics,
            _ => new ValueTask<NetSendResult>(new NetSendResult(NetSendStatus.TransportFailed, "boom")));
        messenger.RegisterMessage<PlayerReady>();

        var result = await messenger.SendToServerAsync(new PlayerReady(true));

        Assert.That(result.Status, Is.EqualTo(NetSendStatus.TransportFailed));
        Assert.That(recorded, Is.Not.Null);
        Assert.That(recorded!.Code, Is.EqualTo("TransportSendFailed"));
        Assert.That(diagnostics.GetSnapshot().ErrorCount, Is.EqualTo(1));
    }

    [Test]
    public async Task SendToServer_WhenCodecEncodeFails_ReturnsTransportFailedAndRecordsCodecError()
    {
        var diagnostics = new NetDiagnostics();
        NetError recorded = null;
        diagnostics.ErrorRecorded += error => recorded = error;
        var messenger = CreateMessenger(
            diagnostics,
            _ => new ValueTask<NetSendResult>(NetSendResult.Ok()),
            codec: new ThrowingCodec(throwOnEncode: true));
        messenger.RegisterMessage<PlayerReady>();

        var result = await messenger.SendToServerAsync(new PlayerReady(true));

        Assert.That(result.Status, Is.EqualTo(NetSendStatus.TransportFailed));
        Assert.That(recorded, Is.Not.Null);
        Assert.That(recorded!.Code, Is.EqualTo("MessageEncodeFailed"));
    }

    [Test]
    public async Task TryHandlePacket_WithUnknownMessage_RecordsStructuredError()
    {
        var diagnostics = new NetDiagnostics();
        NetError recorded = null;
        diagnostics.ErrorRecorded += error => recorded = error;
        var messenger = CreateMessenger(diagnostics, _ => new ValueTask<NetSendResult>(NetSendResult.Ok()));
        var packet = JsonSerializer.SerializeToUtf8Bytes(new NetPacket
        {
            Kind = NetPacket.Message,
            MessageId = 999,
            Payload = Array.Empty<byte>()
        });

        var handled = await messenger.TryHandlePacket(packet, PeerId.Server);

        Assert.That(handled, Is.True);
        Assert.That(recorded, Is.Not.Null);
        Assert.That(recorded!.Code, Is.EqualTo("UnknownMessage"));
    }

    [Test]
    public async Task TryHandlePacket_WhenCodecDecodeFails_RecordsCodecErrorAndDoesNotInvokeHandler()
    {
        var diagnostics = new NetDiagnostics();
        NetError recorded = null;
        var invoked = false;
        diagnostics.ErrorRecorded += error => recorded = error;
        var messenger = CreateMessenger(
            diagnostics,
            _ => new ValueTask<NetSendResult>(NetSendResult.Ok()),
            codec: new ThrowingCodec(throwOnDecode: true));
        var descriptor = messenger.RegisterMessage<PlayerReady>();
        messenger.On<PlayerReady>((_, _) => invoked = true);
        var packet = JsonSerializer.SerializeToUtf8Bytes(new NetPacket
        {
            Kind = NetPacket.Message,
            MessageId = descriptor.MessageId,
            Payload = Array.Empty<byte>()
        });

        var handled = await messenger.TryHandlePacket(packet, PeerId.Server);

        Assert.That(handled, Is.True);
        Assert.That(invoked, Is.False);
        Assert.That(recorded, Is.Not.Null);
        Assert.That(recorded!.Code, Is.EqualTo("MessageDecodeFailed"));
    }

    [Test]
    public async Task SendAsync_WhenTargetQueueIsFull_ReturnsSendQueueFull()
    {
        var target = new PeerId(2);
        var diagnostics = new NetDiagnostics();
        var sendStarted = new TaskCompletionSource();
        var releaseSend = new TaskCompletionSource();
        var messenger = CreateMessenger(
            diagnostics,
            _ => new ValueTask<NetSendResult>(NetSendResult.Ok()),
            async (_, _) =>
            {
                sendStarted.TrySetResult();
                await releaseSend.Task;
                return NetSendResult.Ok();
            },
            maxSendQueuePacketsPerPeer: 1);
        messenger.RegisterMessage<PlayerReady>();

        var first = messenger.SendAsync(target, new PlayerReady(true)).AsTask();
        await sendStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var second = await messenger.SendAsync(target, new PlayerReady(true));

        releaseSend.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(second.Status, Is.EqualTo(NetSendStatus.SendQueueFull));
    }

    [Test]
    public async Task BroadcastAsync_WhenTargetQueueIsFull_ReturnsSendQueueFull()
    {
        var target = new PeerId(2);
        var diagnostics = new NetDiagnostics();
        var sendStarted = new TaskCompletionSource();
        var releaseSend = new TaskCompletionSource();
        var messenger = CreateMessenger(
            diagnostics,
            _ => new ValueTask<NetSendResult>(NetSendResult.Ok()),
            async (_, _) =>
            {
                sendStarted.TrySetResult();
                await releaseSend.Task;
                return NetSendResult.Ok();
            },
            broadcastTargets: new[] { target },
            maxSendQueuePacketsPerPeer: 1);
        messenger.RegisterMessage<PlayerReady>();

        var first = messenger.SendAsync(target, new PlayerReady(true)).AsTask();
        await sendStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var broadcast = await messenger.BroadcastAsync(new PlayerReady(true));

        releaseSend.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(broadcast.Status, Is.EqualTo(NetSendStatus.SendQueueFull));
    }

    [Test]
    public async Task RelayAsync_WhenServerQueueIsFull_ReturnsSendQueueFull()
    {
        var diagnostics = new NetDiagnostics();
        var sendStarted = new TaskCompletionSource();
        var releaseSend = new TaskCompletionSource();
        var messenger = CreateMessenger(
            diagnostics,
            async _ =>
            {
                sendStarted.TrySetResult();
                await releaseSend.Task;
                return NetSendResult.Ok();
            },
            maxSendQueuePacketsPerPeer: 1);
        messenger.RegisterMessage<PlayerReady>();

        var first = messenger.SendToServerAsync(new PlayerReady(true)).AsTask();
        await sendStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var relay = await messenger.RelayAsync(new PeerId(2), new PlayerReady(true), TimeSpan.FromSeconds(1));

        releaseSend.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(relay.Status, Is.EqualTo(NetSendStatus.SendQueueFull));
    }

    [Test]
    public async Task RequestAsync_WhenServerQueueIsFull_ReturnsSendQueueFullAndCleansPending()
    {
        var diagnostics = new NetDiagnostics();
        var sendStarted = new TaskCompletionSource();
        var releaseSend = new TaskCompletionSource();
        var messenger = CreateMessenger(
            diagnostics,
            async _ =>
            {
                sendStarted.TrySetResult();
                await releaseSend.Task;
                return NetSendResult.Ok();
            },
            maxSendQueuePacketsPerPeer: 1);
        messenger.RegisterMessage<PlayerReady>();
        messenger.RegisterMessage<JoinRoomRequest>();
        messenger.RegisterMessage<JoinRoomResponse>();

        var first = messenger.SendToServerAsync(new PlayerReady(true)).AsTask();
        await sendStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var request = await messenger.RequestAsync<JoinRoomRequest, JoinRoomResponse>(
            PeerId.Server,
            new JoinRoomRequest("room-1"),
            TimeSpan.FromSeconds(1));

        releaseSend.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(request.Status, Is.EqualTo(NetRequestStatus.SendQueueFull));
        Assert.That(diagnostics.GetSnapshot().PendingRequestCount, Is.EqualTo(0));
    }

    [Test]
    public void MessageRegistry_ExplicitKeyProducesStableId()
    {
        var registry = new NetMessageRegistry();

        var first = registry.Register<PlayerReady>();
        var second = registry.Get<PlayerReady>();

        Assert.That(second.MessageId, Is.EqualTo(first.MessageId));
        Assert.That(first.MessageId, Is.EqualTo(new NetMessageRegistry().Register<PlayerReady>().MessageId));
    }

    [Test]
    public void RegisterAssembly_RegistersMessagesAndAttributedHandlersDeterministically()
    {
        var first = new NetMessageRegistry();
        var second = new NetMessageRegistry();

        first.RegisterAssembly(typeof(ScanAlpha).Assembly, IsScanFixtureType);
        second.RegisterAssembly(typeof(ScanAlpha).Assembly, IsScanFixtureType);

        Assert.That(first.Get<ScanAlpha>().Key, Is.EqualTo("scan.alpha"));
        Assert.That(first.Get<ScanBeta>().Key, Is.EqualTo("scan.beta"));
        Assert.That(
            first.HandlerDescriptors.Select(h => h.MessageType).ToArray(),
            Is.EqualTo(second.HandlerDescriptors.Select(h => h.MessageType).ToArray()));
        Assert.That(first.HandlerDescriptors.Any(h => h.MessageType == typeof(ScanBeta)), Is.True);
        Assert.That(first.RequestHandlerDescriptors.Any(h => h.RequestType == typeof(ScanAlpha) && h.ResponseType == typeof(ScanBeta)), Is.True);
        Assert.That(first.FlowHandlerDescriptors.Any(h => h.ProposalType == typeof(ScanAlpha) && h.ResponseType == typeof(ScanBeta)), Is.True);
    }

    [Test]
    public async Task RegisterAssemblyHandlers_BindsAttributedMessageAndRequestHandlers()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));
        RuntimeAttributedHandlers.ReceivedMessage = new TaskCompletionSource<AttributeMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        server.Messages.RegisterAssemblyHandlers(typeof(AttributeMessage).Assembly, typeFilter: IsRuntimeAttributedFixtureType);
        client.Messages.RegisterMessage<AttributeMessage>();
        client.Messages.RegisterMessage<AttributeRequest>();
        client.Messages.RegisterMessage<AttributeResponse>();

        await server.HostAsync(new HostOptions { Port = 7777 });
        await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var send = await client.SendToServerAsync(new AttributeMessage(7));
        var response = await client.RequestAsync<AttributeRequest, AttributeResponse>(
            PeerId.Server,
            new AttributeRequest(42),
            TimeSpan.FromSeconds(1));

        Assert.That(send.Status, Is.EqualTo(NetSendStatus.Ok));
        Assert.That((await RuntimeAttributedHandlers.ReceivedMessage.Task.WaitAsync(TimeSpan.FromSeconds(1))).Value, Is.EqualTo(7));
        Assert.That(response.Status, Is.EqualTo(NetRequestStatus.Ok));
        Assert.That(response.Response!.Value, Is.EqualTo(43));
    }

    [Test]
    public async Task RegisterAssemblyHandlers_AwaitsAsyncAttributedRequestHandlers()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));
        RuntimeAttributedHandlers.AsyncRequestStarted = new TaskCompletionSource<AttributeRequest>(TaskCreationOptions.RunContinuationsAsynchronously);

        server.Messages.RegisterAssemblyHandlers(typeof(RuntimeAsyncAttributedHandlers).Assembly, typeFilter: type => type == typeof(RuntimeAsyncAttributedHandlers));
        client.Messages.RegisterMessage<AttributeRequest>();
        client.Messages.RegisterMessage<AttributeResponse>();

        await server.HostAsync(new HostOptions { Port = 7777 });
        await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var response = await client.RequestAsync<AttributeRequest, AttributeResponse>(
            PeerId.Server,
            new AttributeRequest(5),
            TimeSpan.FromSeconds(1));

        Assert.That((await RuntimeAttributedHandlers.AsyncRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(1))).Value, Is.EqualTo(5));
        Assert.That(response.Status, Is.EqualTo(NetRequestStatus.Ok));
        Assert.That(response.Response!.Value, Is.EqualTo(15));
    }

    [Test]
    public async Task CancelPendingRelays_CompletesInFlightRelay()
    {
        var diagnostics = new NetDiagnostics();
        var messenger = CreateMessenger(
            diagnostics,
            _ => new ValueTask<NetSendResult>(NetSendResult.Ok()));
        messenger.RegisterMessage<PlayerReady>();

        var relay = messenger.RelayAsync(new PeerId(2), new PlayerReady(true), TimeSpan.FromSeconds(30));

        messenger.CancelPendingRelays(NetSendStatus.SessionClosed, "Session closed.");
        var result = await relay.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(result.Status, Is.EqualTo(NetSendStatus.SessionClosed));
        Assert.That(result.Message, Is.EqualTo("Session closed."));
    }

    [Test]
    public async Task RelayAsync_WhenPolicyErrorMessageIsLarge_ReturnsBoundedError()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId, maxPacketSize: 2048));
        await using var clientA = new GameNet(network.CreateTransport("client-a"), Options(appId, maxPacketSize: 2048));
        await using var clientB = new GameNet(network.CreateTransport("client-b"), Options(appId, maxPacketSize: 2048));
        server.Messages.RegisterMessage<PlayerReady>();
        clientA.Messages.RegisterMessage<PlayerReady>();
        clientB.Messages.RegisterMessage<PlayerReady>();
        server.Messages.AllowRelay<PlayerReady>((_, _) => throw new InvalidOperationException(new string('x', 4096)));

        await server.HostAsync(new HostOptions { Port = 7777 });
        await clientA.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        var joinB = await clientB.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var result = await clientA.RelayAsync(joinB.PeerId, new PlayerReady(true), TimeSpan.FromSeconds(1));

        Assert.That(result.Status, Is.EqualTo(NetSendStatus.PermissionDenied));
        Assert.That(result.Message, Does.Not.Contain(new string('x', 128)));
    }

    [Test]
    public void Fingerprint_IsStableAndPolicyControlsMismatch()
    {
        var first = new NetMessageRegistry();
        var second = new NetMessageRegistry();
        var different = new NetMessageRegistry();

        first.Register<PlayerReady>();
        second.Register<PlayerReady>();
        different.Register<BigMessage>();

        Assert.That(first.GetFingerprint(), Is.EqualTo(second.GetFingerprint()));
        Assert.That(first.GetFingerprint(), Is.Not.EqualTo(different.GetFingerprint()));
        Assert.That(first.CheckFingerprint(different.GetFingerprint(), NetFingerprintPolicy.Strict).Status, Is.EqualTo(NetFingerprintStatus.Rejected));
        Assert.That(first.CheckFingerprint(different.GetFingerprint(), NetFingerprintPolicy.Warn).Status, Is.EqualTo(NetFingerprintStatus.Warning));
        Assert.That(first.CheckFingerprint(different.GetFingerprint(), NetFingerprintPolicy.Ignore).Status, Is.EqualTo(NetFingerprintStatus.Ignored));
    }

    [Test]
    public void Fingerprint_ChangesWhenStableKeyPayloadShapeChanges()
    {
        var first = new NetMessageRegistry();
        var changed = new NetMessageRegistry();

        first.Register<PlayerReady>();
        changed.Register<PlayerReadyWithSchemaChange>();

        Assert.That(changed.GetFingerprint(), Is.Not.EqualTo(first.GetFingerprint()));
    }

    [Test]
    public async Task JoinAsync_WithStrictFingerprintSameKeyDifferentPayloadShape_IsRejected()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId, fingerprintPolicy: NetFingerprintPolicy.Strict));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId, fingerprintPolicy: NetFingerprintPolicy.Strict));

        server.Messages.RegisterMessage<PlayerReady>();
        client.Messages.RegisterMessage<PlayerReadyWithSchemaChange>();

        await server.HostAsync(new HostOptions { Port = 7777 });
        var join = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        Assert.That(join.Status, Is.EqualTo(NetSessionStatus.IncompatibleProtocol));
    }

    [Test]
    public async Task JoinAsync_WithStrictFingerprintMismatch_IsRejected()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId, fingerprintPolicy: NetFingerprintPolicy.Strict));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId, fingerprintPolicy: NetFingerprintPolicy.Strict));

        server.Messages.RegisterMessage<PlayerReady>();
        client.Messages.RegisterMessage<BigMessage>();

        await server.HostAsync(new HostOptions { Port = 7777 });
        var join = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        Assert.That(join.Status, Is.EqualTo(NetSessionStatus.IncompatibleProtocol));
        Assert.That(join.Message, Does.Contain("fingerprint"));
        Assert.That(server.Diagnostics.GetSnapshot().ErrorCount, Is.EqualTo(1));
    }

    [Test]
    public async Task JoinAsync_WithWarnFingerprintMismatch_RecordsDiagnosticAndAllowsJoin()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId, fingerprintPolicy: NetFingerprintPolicy.Warn));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId, fingerprintPolicy: NetFingerprintPolicy.Warn));

        server.Messages.RegisterMessage<PlayerReady>();
        client.Messages.RegisterMessage<BigMessage>();

        await server.HostAsync(new HostOptions { Port = 7777 });
        var join = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        Assert.That(join.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(server.Diagnostics.GetSnapshot().ErrorCount, Is.EqualTo(1));
    }

    [Test]
    public async Task JoinAsync_WithIgnoreFingerprintMismatch_RecordsDiagnosticAndAllowsJoin()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId, fingerprintPolicy: NetFingerprintPolicy.Ignore));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId, fingerprintPolicy: NetFingerprintPolicy.Ignore));

        server.Messages.RegisterMessage<PlayerReady>();
        client.Messages.RegisterMessage<BigMessage>();

        await server.HostAsync(new HostOptions { Port = 7777 });
        var join = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        Assert.That(join.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(server.Diagnostics.GetSnapshot().ErrorCount, Is.EqualTo(1));
    }

    [Test]
    public async Task MessageRegistry_AfterSessionStart_RejectsProtocolMutation()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));

        await server.HostAsync(new HostOptions { Port = 7777 });

        Assert.Throws<InvalidOperationException>(() => server.Messages.RegisterMessage<PlayerReady>());
        Assert.Throws<InvalidOperationException>(() => server.On<PlayerReady>((_, _) => { }));
        Assert.Throws<InvalidOperationException>(() =>
            server.OnRequest<JoinRoomRequest, JoinRoomResponse>((_, _) => new JoinRoomResponse(true, string.Empty)));
    }

    [Test]
    public async Task RequestAsync_ReturnsTypedResponse()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));

        server.Messages.RegisterMessage<JoinRoomRequest>();
        server.Messages.RegisterMessage<JoinRoomResponse>();
        client.Messages.RegisterMessage<JoinRoomRequest>();
        client.Messages.RegisterMessage<JoinRoomResponse>();
        server.OnRequest<JoinRoomRequest, JoinRoomResponse>((_, req) =>
            new JoinRoomResponse(req.RoomId == "room-1", string.Empty));

        await server.HostAsync(new HostOptions { Port = 7777 });
        await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var response = await client.RequestAsync<JoinRoomRequest, JoinRoomResponse>(
            PeerId.Server,
            new JoinRoomRequest("room-1"),
            TimeSpan.FromSeconds(1));

        Assert.That(response.Status, Is.EqualTo(NetRequestStatus.Ok));
        Assert.That(response.Response!.Accepted, Is.True);
    }

    [Test]
    public async Task RequestAsync_AwaitsAsyncHandler()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        server.Messages.RegisterMessage<JoinRoomRequest>();
        server.Messages.RegisterMessage<JoinRoomResponse>();
        client.Messages.RegisterMessage<JoinRoomRequest>();
        client.Messages.RegisterMessage<JoinRoomResponse>();
        server.OnRequest<JoinRoomRequest, JoinRoomResponse>(async (_, req) =>
        {
            handlerStarted.TrySetResult();
            await Task.Yield();
            return new JoinRoomResponse(req.RoomId == "room-1", "async");
        });

        await server.HostAsync(new HostOptions { Port = 7777 });
        await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var response = await client.RequestAsync<JoinRoomRequest, JoinRoomResponse>(
            PeerId.Server,
            new JoinRoomRequest("room-1"),
            TimeSpan.FromSeconds(1));

        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(response.Status, Is.EqualTo(NetRequestStatus.Ok));
        Assert.That(response.Response!.Accepted, Is.True);
        Assert.That(response.Response.Reason, Is.EqualTo("async"));
    }

    [Test]
    public async Task RequestAsync_WithoutHandler_ReturnsNoHandler()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));

        server.Messages.RegisterMessage<JoinRoomRequest>();
        server.Messages.RegisterMessage<JoinRoomResponse>();
        client.Messages.RegisterMessage<JoinRoomRequest>();
        client.Messages.RegisterMessage<JoinRoomResponse>();

        await server.HostAsync(new HostOptions { Port = 7777 });
        await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var response = await client.RequestAsync<JoinRoomRequest, JoinRoomResponse>(
            PeerId.Server,
            new JoinRoomRequest("room-1"),
            TimeSpan.FromSeconds(1));

        Assert.That(response.Status, Is.EqualTo(NetRequestStatus.NoHandler));
    }

    [Test]
    public async Task RequestAsync_HandlerException_ReturnsHandlerException()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));

        server.Messages.RegisterMessage<JoinRoomRequest>();
        server.Messages.RegisterMessage<JoinRoomResponse>();
        client.Messages.RegisterMessage<JoinRoomRequest>();
        client.Messages.RegisterMessage<JoinRoomResponse>();
        server.OnRequest<JoinRoomRequest, JoinRoomResponse>(
            (Func<NetContext, JoinRoomRequest, JoinRoomResponse>)((_, _) =>
                throw new InvalidOperationException("boom")));

        await server.HostAsync(new HostOptions { Port = 7777 });
        await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var response = await client.RequestAsync<JoinRoomRequest, JoinRoomResponse>(
            PeerId.Server,
            new JoinRoomRequest("room-1"),
            TimeSpan.FromSeconds(1));

        Assert.That(response.Status, Is.EqualTo(NetRequestStatus.HandlerException));
        Assert.That(response.Message, Does.Contain("boom"));
    }

    [Test]
    public async Task RequestAsync_WhenResponseExceedsPacketLimit_ReturnsPacketTooLarge()
    {
        var appId = Guid.NewGuid();
        var clock = new ManualTimeProvider();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId, timeProvider: clock, maxPacketSize: 2048));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId, timeProvider: clock, maxPacketSize: 2048));

        server.Messages.RegisterMessage<JoinRoomRequest>();
        server.Messages.RegisterMessage<JoinRoomResponse>();
        client.Messages.RegisterMessage<JoinRoomRequest>();
        client.Messages.RegisterMessage<JoinRoomResponse>();
        server.OnRequest<JoinRoomRequest, JoinRoomResponse>((_, _) =>
            new JoinRoomResponse(true, new string('x', 4096)));

        await server.HostAsync(new HostOptions { Port = 7777 });
        await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var request = client.RequestAsync<JoinRoomRequest, JoinRoomResponse>(
            PeerId.Server,
            new JoinRoomRequest("room-1"),
            TimeSpan.FromSeconds(5));
        await Task.Delay(50);
        if (!request.IsCompleted)
            clock.Advance(TimeSpan.FromSeconds(6));

        var response = await request.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(response.Status, Is.EqualTo(NetRequestStatus.PacketTooLarge));
        Assert.That(client.Diagnostics.GetSnapshot().PendingRequestCount, Is.EqualTo(0));
    }

    [Test]
    public async Task RequestAsync_Timeout_RemovesPendingEntry_AndIgnoresLateResponse()
    {
        var appId = Guid.NewGuid();
        var clock = new ManualTimeProvider();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId, timeProvider: clock));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId, timeProvider: clock));
        var handlerStarted = new TaskCompletionSource();
        var releaseHandler = new TaskCompletionSource();

        server.Messages.RegisterMessage<JoinRoomRequest>();
        server.Messages.RegisterMessage<JoinRoomResponse>();
        client.Messages.RegisterMessage<JoinRoomRequest>();
        client.Messages.RegisterMessage<JoinRoomResponse>();
        server.OnRequest<JoinRoomRequest, JoinRoomResponse>((_, _) =>
        {
            handlerStarted.TrySetResult();
            releaseHandler.Task.GetAwaiter().GetResult();
            return new JoinRoomResponse(true, string.Empty);
        });

        await server.HostAsync(new HostOptions { Port = 7777 });
        await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var request = client.RequestAsync<JoinRoomRequest, JoinRoomResponse>(
            PeerId.Server,
            new JoinRoomRequest("room-1"),
            TimeSpan.FromSeconds(5));
        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(client.Diagnostics.GetSnapshot().PendingRequestCount, Is.EqualTo(1));

        clock.Advance(TimeSpan.FromSeconds(6));
        var result = await request.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(result.Status, Is.EqualTo(NetRequestStatus.Timeout));
        Assert.That(client.Diagnostics.GetSnapshot().PendingRequestCount, Is.EqualTo(0));

        releaseHandler.SetResult();
        await Task.Delay(50);

        Assert.That(client.Diagnostics.GetSnapshot().PendingRequestCount, Is.EqualTo(0));
    }

    [Test]
    public async Task RequestAsync_DuplicateResponse_DoesNotMutateCompletedRequest()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));

        server.Messages.RegisterMessage<JoinRoomRequest>();
        server.Messages.RegisterMessage<JoinRoomResponse>();
        client.Messages.RegisterMessage<JoinRoomRequest>();
        client.Messages.RegisterMessage<JoinRoomResponse>();
        server.OnRequest<JoinRoomRequest, JoinRoomResponse>((_, _) =>
            new JoinRoomResponse(true, "original"));

        await server.HostAsync(new HostOptions { Port = 7777 });
        await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var result = await client.RequestAsync<JoinRoomRequest, JoinRoomResponse>(
            PeerId.Server,
            new JoinRoomRequest("room-1"),
            TimeSpan.FromSeconds(1));
        var duplicatePayload = new JsonNetCodec().Encode(new JoinRoomResponse(false, "duplicate"));
        var duplicatePacket = JsonSerializer.SerializeToUtf8Bytes(new NetPacket
        {
            Kind = NetPacket.Response,
            MessageId = client.Messages.Registry.Get<JoinRoomResponse>().MessageId,
            ResponseMessageId = client.Messages.Registry.Get<JoinRoomResponse>().MessageId,
            CorrelationId = 1,
            SenderId = PeerId.Server,
            Payload = duplicatePayload,
            RequestStatus = NetRequestStatus.Ok
        });

        var handled = await client.Messages.TryHandlePacket(duplicatePacket, PeerId.Server);

        Assert.That(handled, Is.True);
        Assert.That(result.Status, Is.EqualTo(NetRequestStatus.Ok));
        Assert.That(result.Response!.Accepted, Is.True);
        Assert.That(result.Response.Reason, Is.EqualTo("original"));
        Assert.That(client.Diagnostics.GetSnapshot().PendingRequestCount, Is.EqualTo(0));
    }

    [Test]
    public async Task RequestAsync_IgnoresResponseFromUnexpectedPeer()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        server.Messages.RegisterMessage<JoinRoomRequest>();
        server.Messages.RegisterMessage<JoinRoomResponse>();
        client.Messages.RegisterMessage<JoinRoomRequest>();
        client.Messages.RegisterMessage<JoinRoomResponse>();
        server.OnRequest<JoinRoomRequest, JoinRoomResponse>((_, _) =>
        {
            handlerStarted.TrySetResult();
            releaseHandler.Task.GetAwaiter().GetResult();
            return new JoinRoomResponse(true, "original");
        });

        await server.HostAsync(new HostOptions { Port = 7777 });
        await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var request = client.RequestAsync<JoinRoomRequest, JoinRoomResponse>(
            PeerId.Server,
            new JoinRoomRequest("room-1"),
            TimeSpan.FromSeconds(1));
        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var fakePayload = new JsonNetCodec().Encode(new JoinRoomResponse(false, "fake"));
        var fakePacket = JsonSerializer.SerializeToUtf8Bytes(new NetPacket
        {
            Kind = NetPacket.Response,
            MessageId = client.Messages.Registry.Get<JoinRoomResponse>().MessageId,
            ResponseMessageId = client.Messages.Registry.Get<JoinRoomResponse>().MessageId,
            CorrelationId = 1,
            SenderId = new PeerId(999),
            Payload = fakePayload,
            RequestStatus = NetRequestStatus.Ok
        });

        await client.Messages.TryHandlePacket(fakePacket, new PeerId(999));
        await Task.Delay(50);
        var completedEarly = request.IsCompleted;

        releaseHandler.SetResult();
        var result = await request.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(completedEarly, Is.False);
        Assert.That(result.Status, Is.EqualTo(NetRequestStatus.Ok));
        Assert.That(result.Response!.Reason, Is.EqualTo("original"));
    }

    [Test]
    public async Task RequestAsync_IgnoresResponseWithUnexpectedMessageType()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        server.Messages.RegisterMessage<JoinRoomRequest>();
        server.Messages.RegisterMessage<JoinRoomResponse>();
        server.Messages.RegisterMessage<PlayerReady>();
        client.Messages.RegisterMessage<JoinRoomRequest>();
        client.Messages.RegisterMessage<JoinRoomResponse>();
        client.Messages.RegisterMessage<PlayerReady>();
        server.OnRequest<JoinRoomRequest, JoinRoomResponse>((_, _) =>
        {
            handlerStarted.TrySetResult();
            releaseHandler.Task.GetAwaiter().GetResult();
            return new JoinRoomResponse(true, "original");
        });

        await server.HostAsync(new HostOptions { Port = 7777 });
        await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var request = client.RequestAsync<JoinRoomRequest, JoinRoomResponse>(
            PeerId.Server,
            new JoinRoomRequest("room-1"),
            TimeSpan.FromSeconds(1));
        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var wrongTypePayload = new JsonNetCodec().Encode(new PlayerReady(true));
        var wrongTypePacket = JsonSerializer.SerializeToUtf8Bytes(new NetPacket
        {
            Kind = NetPacket.Response,
            MessageId = client.Messages.Registry.Get<PlayerReady>().MessageId,
            ResponseMessageId = client.Messages.Registry.Get<PlayerReady>().MessageId,
            CorrelationId = 1,
            SenderId = PeerId.Server,
            Payload = wrongTypePayload,
            RequestStatus = NetRequestStatus.Ok
        });

        await client.Messages.TryHandlePacket(wrongTypePacket, PeerId.Server);
        await Task.Delay(50);
        var completedEarly = request.IsCompleted;

        releaseHandler.SetResult();
        var result = await request.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(completedEarly, Is.False);
        Assert.That(result.Status, Is.EqualTo(NetRequestStatus.Ok));
        Assert.That(result.Response!.Reason, Is.EqualTo("original"));
    }

    [Test]
    public async Task RequestAsync_Cancellation_RemovesPendingEntry()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));
        using var cancellation = new CancellationTokenSource();
        var handlerStarted = new TaskCompletionSource();
        var releaseHandler = new TaskCompletionSource();

        server.Messages.RegisterMessage<JoinRoomRequest>();
        server.Messages.RegisterMessage<JoinRoomResponse>();
        client.Messages.RegisterMessage<JoinRoomRequest>();
        client.Messages.RegisterMessage<JoinRoomResponse>();
        server.OnRequest<JoinRoomRequest, JoinRoomResponse>((_, _) =>
        {
            handlerStarted.TrySetResult();
            releaseHandler.Task.GetAwaiter().GetResult();
            return new JoinRoomResponse(true, string.Empty);
        });

        await server.HostAsync(new HostOptions { Port = 7777 });
        await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var request = client.RequestAsync<JoinRoomRequest, JoinRoomResponse>(
            PeerId.Server,
            new JoinRoomRequest("room-1"),
            TimeSpan.FromSeconds(30),
            cancellation.Token);
        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();

        var result = await request.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(result.Status, Is.EqualTo(NetRequestStatus.Cancelled));
        Assert.That(client.Diagnostics.GetSnapshot().PendingRequestCount, Is.EqualTo(0));

        releaseHandler.SetResult();
    }

    [Test]
    public async Task RequestAsync_ReconnectEnabledPeerDisconnect_CompletesSessionClosed()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        server.Messages.RegisterMessage<JoinRoomRequest>();
        server.Messages.RegisterMessage<JoinRoomResponse>();
        client.Messages.RegisterMessage<JoinRoomRequest>();
        client.Messages.RegisterMessage<JoinRoomResponse>();
        client.OnRequest<JoinRoomRequest, JoinRoomResponse>((_, _) =>
        {
            handlerStarted.TrySetResult();
            releaseHandler.Task.GetAwaiter().GetResult();
            return new JoinRoomResponse(true, string.Empty);
        });

        await server.HostAsync(new HostOptions
        {
            Port = 7777,
            ReconnectPolicy = ReconnectPolicy.Enabled(TimeSpan.FromSeconds(30))
        });
        var join = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var request = server.RequestAsync<JoinRoomRequest, JoinRoomResponse>(
            join.PeerId,
            new JoinRoomRequest("room-1"),
            TimeSpan.FromSeconds(30));
        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        await client.LeaveAsync();
        var result = await request.WaitAsync(TimeSpan.FromSeconds(1));
        releaseHandler.SetResult();

        Assert.That(result.Status, Is.EqualTo(NetRequestStatus.SessionClosed));
    }

    [Test]
    public async Task RequestAsync_BeforeJoin_ReturnsSessionClosed()
    {
        var network = new MemoryNetNetwork();
        await using var client = new GameNet(network.CreateTransport("client"), Options(Guid.NewGuid()));

        client.Messages.RegisterMessage<JoinRoomRequest>();
        client.Messages.RegisterMessage<JoinRoomResponse>();

        var result = await client.RequestAsync<JoinRoomRequest, JoinRoomResponse>(
            PeerId.Server,
            new JoinRoomRequest("room-1"),
            TimeSpan.FromSeconds(1));

        Assert.That(result.Status, Is.EqualTo(NetRequestStatus.SessionClosed));
        Assert.That(client.Diagnostics.GetSnapshot().PendingRequestCount, Is.EqualTo(0));
    }

    private static GameNetOptions Options(
        Guid appId,
        INetEventDispatcher dispatcher = null,
        TimeProvider timeProvider = null,
        NetFingerprintPolicy fingerprintPolicy = NetFingerprintPolicy.Strict,
        int maxPacketSize = 64 * 1024) => new()
    {
        Application = new NetApplicationInfo { ApplicationId = appId },
        EventDispatcher = dispatcher,
        TimeProvider = timeProvider ?? TimeProvider.System,
        MessageFingerprintPolicy = fingerprintPolicy,
        MaxPacketSize = maxPacketSize
    };

    private static bool IsScanFixtureType(Type type)
    {
        return type == typeof(ScanAlpha) ||
               type == typeof(ScanBeta) ||
               type == typeof(ScannedNetHandlers);
    }

    private static bool IsRuntimeAttributedFixtureType(Type type)
    {
        return type == typeof(AttributeMessage) ||
               type == typeof(AttributeRequest) ||
               type == typeof(AttributeResponse) ||
               type == typeof(RuntimeAttributedHandlers);
    }

    private static NetMessenger CreateMessenger(
        NetDiagnostics diagnostics,
        Func<byte[], ValueTask<NetSendResult>> sendToServer,
        Func<PeerId, byte[], ValueTask<NetSendResult>> sendToPeer = null,
        IReadOnlyCollection<PeerId> broadcastTargets = null,
        TimeProvider timeProvider = null,
        int maxPacketSize = 64 * 1024,
        int maxSendQueueBytesPerPeer = 1024 * 1024,
        int maxSendQueuePacketsPerPeer = 1024,
        int maxSendsPerSecondPerPeer = int.MaxValue,
        INetCodec codec = null)
    {
        return new NetMessenger(
            sendToServer,
            sendToPeer ?? ((_, _) => new ValueTask<NetSendResult>(NetSendResult.Ok())),
            () => broadcastTargets ?? Array.Empty<PeerId>(),
            diagnostics,
            maxPacketSize,
            maxSendQueueBytesPerPeer,
            maxSendQueuePacketsPerPeer,
            maxSendsPerSecondPerPeer,
            timeProvider,
            codec: codec);
    }

    private sealed class ThrowingCodec : INetCodec
    {
        private readonly bool _throwOnEncode;
        private readonly bool _throwOnDecode;

        public ThrowingCodec(bool throwOnEncode = false, bool throwOnDecode = false)
        {
            _throwOnEncode = throwOnEncode;
            _throwOnDecode = throwOnDecode;
        }

        public byte[] Encode<T>(T message)
        {
            if (_throwOnEncode)
                throw new InvalidOperationException("encode failed");

            return new JsonNetCodec().Encode(message);
        }

        public object Decode(ReadOnlySpan<byte> payload, Type messageType)
        {
            if (_throwOnDecode)
                throw new InvalidOperationException("decode failed");

            return new JsonNetCodec().Decode(payload, messageType);
        }
    }

    private sealed class InlineCountingDispatcher : INetEventDispatcher
    {
        public int PostCount { get; private set; }

        public void Post(Action action)
        {
            PostCount++;
            action();
        }
    }
}
