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

[NetMessage("big.message")]
public sealed record BigMessage(string Text);

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

public sealed record MissingMessageAttribute(int Value);

[NetMessage("")]
public sealed record EmptyMessageKey(int Value);

public sealed class ScannedNetHandlers
{
    [NetHandler(typeof(ScanBeta))]
    public static void HandleBeta(NetContext context, ScanBeta message)
    {
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
        server.OnRequest<JoinRoomRequest, JoinRoomResponse>((_, _) =>
            throw new InvalidOperationException("boom"));

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

    private static GameNetOptions Options(Guid appId, INetEventDispatcher dispatcher = null, TimeProvider timeProvider = null) => new()
    {
        Application = new NetApplicationInfo { ApplicationId = appId },
        EventDispatcher = dispatcher,
        TimeProvider = timeProvider ?? TimeProvider.System
    };

    private static bool IsScanFixtureType(Type type)
    {
        return type == typeof(ScanAlpha) ||
               type == typeof(ScanBeta) ||
               type == typeof(ScannedNetHandlers);
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
