using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleFramework.Net;

namespace Test.Net;

[NetMessage("player.ready")]
public sealed record PlayerReady(bool Ready);

[NetMessage("player.ready")]
public sealed record DuplicatePlayerReady(bool Ready);

[NetMessage("big.message")]
public sealed record BigMessage(string Text);

[NetMessage("handler.check")]
public sealed record HandlerCheck(int Value);

public sealed record MissingMessageAttribute(int Value);

[NetMessage("")]
public sealed record EmptyMessageKey(int Value);

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

        await client.SendToServerAsync(new PlayerReady(true));

        Assert.That((await received.Task.WaitAsync(TimeSpan.FromSeconds(1))).Ready, Is.True);
        Assert.That(dispatcher.PostCount, Is.EqualTo(1));
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

    private static GameNetOptions Options(Guid appId, INetEventDispatcher dispatcher = null) => new()
    {
        Application = new NetApplicationInfo { ApplicationId = appId },
        EventDispatcher = dispatcher
    };

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
