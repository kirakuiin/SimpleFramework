using System;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleFramework.Net;

namespace Test.Net;

[NetMessage("load.scene.proposal")]
public sealed record LoadSceneProposal(string SceneName);

[NetMessage("load.scene.ack")]
public sealed record LoadSceneAck(bool Accepted, string Reason);

[TestFixture]
public class NetFlowTests
{
    [Test]
    public async Task Flow_AllAccepted_CompletesAccepted()
    {
        var fixture = await ThreePeerFixture.StartAsync();
        await using (fixture)
        {
            fixture.ClientA.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((_, _) => new LoadSceneAck(true, string.Empty));
            fixture.ClientB.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((_, _) => new LoadSceneAck(true, string.Empty));

            var result = await fixture.Server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
                fixture.Server.Peers.RemoteParticipants(),
                new LoadSceneProposal("Battle01"),
                FlowPolicy.AllAccepted(),
                TimeSpan.FromSeconds(1));

            Assert.That(result.Reason, Is.EqualTo(FlowEndReason.Accepted));
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Responses.Count, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Flow_ClientCannotInitiate()
    {
        var fixture = await TwoPeerFixture.StartAsync();
        await using (fixture)
        {
            var result = await fixture.Client.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
                new[] { PeerId.Server },
                new LoadSceneProposal("Battle01"),
                FlowPolicy.AllAccepted(),
                TimeSpan.FromSeconds(1));

            Assert.That(result.Reason, Is.EqualTo(FlowEndReason.NotServer));
        }
    }

    private sealed class TwoPeerFixture : IAsyncDisposable
    {
        private TwoPeerFixture(GameNet server, GameNet client)
        {
            Server = server;
            Client = client;
        }

        public GameNet Server { get; }
        public GameNet Client { get; }

        public static async Task<TwoPeerFixture> StartAsync()
        {
            var appId = Guid.NewGuid();
            var network = new MemoryNetNetwork();
            var server = new GameNet(network.CreateTransport("server"), Options(appId));
            var client = new GameNet(network.CreateTransport("client"), Options(appId));

            await server.HostAsync(new HostOptions { Port = 7777 });
            await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
            return new TwoPeerFixture(server, client);
        }

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync();
            await Server.DisposeAsync();
        }
    }

    private sealed class ThreePeerFixture : IAsyncDisposable
    {
        private ThreePeerFixture(GameNet server, GameNet clientA, GameNet clientB)
        {
            Server = server;
            ClientA = clientA;
            ClientB = clientB;
        }

        public GameNet Server { get; }
        public GameNet ClientA { get; }
        public GameNet ClientB { get; }

        public static async Task<ThreePeerFixture> StartAsync()
        {
            var appId = Guid.NewGuid();
            var network = new MemoryNetNetwork();
            var server = new GameNet(network.CreateTransport("server"), Options(appId));
            var clientA = new GameNet(network.CreateTransport("client-a"), Options(appId));
            var clientB = new GameNet(network.CreateTransport("client-b"), Options(appId));

            await server.HostAsync(new HostOptions { Port = 7777 });
            await clientA.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
            await clientB.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
            return new ThreePeerFixture(server, clientA, clientB);
        }

        public async ValueTask DisposeAsync()
        {
            await ClientB.DisposeAsync();
            await ClientA.DisposeAsync();
            await Server.DisposeAsync();
        }
    }

    private static GameNetOptions Options(Guid appId) => new()
    {
        Application = new NetApplicationInfo { ApplicationId = appId }
    };
}
