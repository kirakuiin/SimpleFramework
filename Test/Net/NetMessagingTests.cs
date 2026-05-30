using System;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleFramework.Net;

namespace Test.Net;

[NetMessage("player.ready")]
public sealed record PlayerReady(bool Ready);

[NetMessage("player.ready")]
public sealed record DuplicatePlayerReady(bool Ready);

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

    private static GameNetOptions Options(Guid appId) => new()
    {
        Application = new NetApplicationInfo { ApplicationId = appId }
    };
}
