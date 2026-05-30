using System;
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
    public void RegisterDuplicateMessageKey_Throws()
    {
        var registry = new NetMessageRegistry();
        registry.Register<PlayerReady>();

        var ex = Assert.Throws<InvalidOperationException>(() => registry.Register<DuplicatePlayerReady>());

        Assert.That(ex!.Message, Does.Contain("player.ready"));
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

    private static GameNetOptions Options(Guid appId) => new()
    {
        Application = new NetApplicationInfo { ApplicationId = appId }
    };
}
