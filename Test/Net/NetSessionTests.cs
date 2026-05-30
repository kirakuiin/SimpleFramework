using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleFramework.Net;

namespace Test.Net;

[TestFixture]
public class NetSessionTests
{
    [Test]
    public async Task Join_WithCompatibleApplication_AssignsPeerAndUpdatesDirectories()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));

        await server.HostAsync(new HostOptions { Port = 7777 });
        var join = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        Assert.That(join.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(join.PeerId, Is.Not.EqualTo(PeerId.None));
        Assert.That(server.Peers.Peers.Any(p => p.PeerId == join.PeerId), Is.True);
        Assert.That(client.Peers.LocalPeerId, Is.EqualTo(join.PeerId));
    }

    [Test]
    public async Task Join_WithWrongApplication_IsRejected()
    {
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(Guid.NewGuid()));
        await using var client = new GameNet(network.CreateTransport("client"), Options(Guid.NewGuid()));

        await server.HostAsync(new HostOptions { Port = 7777 });
        var join = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        Assert.That(join.Status, Is.EqualTo(NetSessionStatus.IncompatibleApplication));
    }

    [Test]
    public async Task Join_WithRejectedAuthPayload_ReturnsAuthenticationFailed()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));

        await server.HostAsync(new HostOptions
        {
            Port = 7777,
            Authenticator = ctx => Task.FromResult(AuthResult.Reject("WrongPassword"))
        });

        var join = await client.JoinAsync(new JoinOptions
        {
            Host = "server",
            Port = 7777,
            AuthPayload = new byte[] { 1 }
        });

        Assert.That(join.Status, Is.EqualTo(NetSessionStatus.AuthenticationFailed));
        Assert.That(join.Message, Does.Contain("WrongPassword"));
    }

    private static GameNetOptions Options(Guid appId, int protocolVersion = 1) => new()
    {
        Application = new NetApplicationInfo
        {
            ApplicationId = appId,
            ProtocolVersion = protocolVersion
        }
    };
}
