using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleFramework.Net;
using Test.Net.TestDoubles;

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
    public async Task Join_WithWrongProtocol_IsRejected()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId, protocolVersion: 1));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId, protocolVersion: 2));

        await server.HostAsync(new HostOptions { Port = 7777 });
        var join = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        Assert.That(join.Status, Is.EqualTo(NetSessionStatus.IncompatibleProtocol));
    }

    [Test]
    public async Task Join_WhenServerIsFull_ReturnsCapacityFull()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var first = new GameNet(network.CreateTransport("first"), Options(appId));
        await using var second = new GameNet(network.CreateTransport("second"), Options(appId));

        await server.HostAsync(new HostOptions { Port = 7777, MaxPeers = 1 });
        var firstJoin = await first.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        var secondJoin = await second.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        Assert.That(firstJoin.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(secondJoin.Status, Is.EqualTo(NetSessionStatus.CapacityFull));
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

    [Test]
    public async Task Join_WithAcceptedPasswordAuthPayload_Succeeds()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        var expectedPassword = new byte[] { 1, 2, 3, 4 };
        byte[] receivedPayload = null;
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));

        await server.HostAsync(new HostOptions
        {
            Port = 7777,
            Authenticator = ctx =>
            {
                receivedPayload = ctx.AuthPayload;
                return Task.FromResult(ctx.AuthPayload is not null && ctx.AuthPayload.SequenceEqual(expectedPassword)
                    ? AuthResult.Ok()
                    : AuthResult.Reject("WrongPassword"));
            }
        });

        var join = await client.JoinAsync(new JoinOptions
        {
            Host = "server",
            Port = 7777,
            AuthPayload = expectedPassword
        });

        Assert.That(join.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(receivedPayload, Is.EqualTo(expectedPassword));
    }

    [Test]
    public async Task HostAndDedicatedServer_SetExpectedLocalServerPeer()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var host = new GameNet(network.CreateTransport("host"), Options(appId));
        await using var dedicated = new GameNet(network.CreateTransport("dedicated"), Options(appId));

        await host.HostAsync(new HostOptions { Port = 7777 });
        await dedicated.StartServerAsync(new HostOptions { Port = 8888 });

        var hostServerPeer = host.Peers.Peers.Single(p => p.PeerId == PeerId.Server);
        var dedicatedServerPeer = dedicated.Peers.Peers.Single(p => p.PeerId == PeerId.Server);

        Assert.That(host.Session.Role, Is.EqualTo(NetSessionRole.Host));
        Assert.That(hostServerPeer.IsLocal, Is.True);
        Assert.That(dedicated.Session.Role, Is.EqualTo(NetSessionRole.DedicatedServer));
        Assert.That(dedicatedServerPeer.IsLocal, Is.False);
    }

    [Test]
    public async Task PeerDirectory_RemoteParticipants_ExcludesServerAndLocalPeer()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var clientA = new GameNet(network.CreateTransport("client-a"), Options(appId));
        await using var clientB = new GameNet(network.CreateTransport("client-b"), Options(appId));

        await server.HostAsync(new HostOptions { Port = 7777 });
        var joinA = await clientA.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        var joinB = await clientB.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var participants = server.Peers.RemoteParticipants();

        Assert.That(participants, Is.EquivalentTo(new[] { joinA.PeerId, joinB.PeerId }));
        Assert.That(participants, Does.Not.Contain(PeerId.Server));
    }

    [Test]
    public async Task ClientLeave_ClearsClientSession_AndRemovesPeerFromServer()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));

        await server.HostAsync(new HostOptions { Port = 7777 });
        var join = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var leave = await client.LeaveAsync();

        Assert.That(leave.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(client.Session.Role, Is.EqualTo(NetSessionRole.None));
        Assert.That(client.Peers.LocalPeerId, Is.EqualTo(PeerId.None));
        await WaitUntilAsync(() => server.Peers.Peers.All(p => p.PeerId != join.PeerId));
        Assert.That(server.Diagnostics.GetSnapshot().ConnectedPeerCount, Is.EqualTo(1));
    }

    [Test]
    public async Task ServerStop_ClearsClientSession()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));

        await server.HostAsync(new HostOptions { Port = 7777 });
        await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var stop = await server.StopAsync();

        Assert.That(stop.Status, Is.EqualTo(NetSessionStatus.Ok));
        await WaitUntilAsync(() => client.Session.Role == NetSessionRole.None);
        Assert.That(client.Peers.LocalPeerId, Is.EqualTo(PeerId.None));
        Assert.That(client.Peers.Peers, Is.Empty);
    }

    [Test]
    public async Task ServerKick_DisconnectsClientWithKickedReason_AndRemovesPeer()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));
        var clientDisconnected = new TaskCompletionSource<NetPeerDisconnected>();

        client.Session.PeerDisconnected += e =>
        {
            if (e.PeerId == PeerId.Server)
                clientDisconnected.TrySetResult(e);
        };

        await server.HostAsync(new HostOptions { Port = 7777 });
        var join = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var kick = await server.KickAsync(join.PeerId);
        var disconnected = await clientDisconnected.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(kick.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(disconnected.Reason, Is.EqualTo(DisconnectReason.Kicked));
        Assert.That(client.Session.Role, Is.EqualTo(NetSessionRole.None));
        await WaitUntilAsync(() => server.Peers.Peers.All(p => p.PeerId != join.PeerId));
    }

    [Test]
    public async Task ClientLeave_ReportsLocalClosedReasonToServer()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));
        var left = new TaskCompletionSource<NetPeerLeft>();

        server.Session.PeerLeft += e => left.TrySetResult(e);

        await server.HostAsync(new HostOptions { Port = 7777 });
        var join = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        await client.LeaveAsync();
        var leftEvent = await left.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(leftEvent.PeerId, Is.EqualTo(join.PeerId));
        Assert.That(leftEvent.Reason, Is.EqualTo(DisconnectReason.RemoteClosed));
    }

    [Test]
    public async Task Join_WhenServerDoesNotReply_TimesOutAndCanRetry()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var silentServer = network.CreateTransport("silent-server");
        await using var realServer = new GameNet(network.CreateTransport("real-server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));

        await silentServer.StartServerAsync(new NetListenOptions { Port = 7777 });

        var timeout = await client.JoinAsync(new JoinOptions
        {
            Host = "silent-server",
            Port = 7777,
            Timeout = TimeSpan.FromMilliseconds(50)
        });

        await realServer.HostAsync(new HostOptions { Port = 8888 });
        var retry = await client.JoinAsync(new JoinOptions { Host = "real-server", Port = 8888 });

        Assert.That(timeout.Status, Is.EqualTo(NetSessionStatus.TransportFailed));
        Assert.That(timeout.Message, Does.Contain("timed out"));
        Assert.That(retry.Status, Is.EqualTo(NetSessionStatus.Ok));
    }

    [Test]
    public async Task Join_WithCancelledToken_ReturnsCancelled()
    {
        var network = new MemoryNetNetwork();
        await using var client = new GameNet(network.CreateTransport("client"), Options(Guid.NewGuid()));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var join = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 }, cts.Token);

        Assert.That(join.Status, Is.EqualTo(NetSessionStatus.Cancelled));
    }

    [Test]
    public async Task Join_WhenAlreadyInSession_ReturnsInvalidState()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var host = new GameNet(network.CreateTransport("host"), Options(appId));

        await host.HostAsync(new HostOptions { Port = 7777 });

        var join = await host.JoinAsync(new JoinOptions { Host = "host", Port = 7777 });

        Assert.That(join.Status, Is.EqualTo(NetSessionStatus.InvalidState));
    }

    [Test]
    public async Task SessionEvents_PeerJoinAndLeave_AreRaisedInOrderOnServer()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));
        var events = new List<string>();
        var left = new TaskCompletionSource();

        server.Session.PeerJoined += e => events.Add($"joined:{e.Peer.PeerId.Value}");
        server.Session.PeerDisconnected += e => events.Add($"disconnected:{e.PeerId.Value}");
        server.Session.PeerLeft += e =>
        {
            events.Add($"left:{e.PeerId.Value}");
            left.TrySetResult();
        };

        await server.HostAsync(new HostOptions { Port = 7777 });
        var join = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        await client.LeaveAsync();
        await left.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(events, Is.EqualTo(new[]
        {
            $"joined:{join.PeerId.Value}",
            $"disconnected:{join.PeerId.Value}",
            $"left:{join.PeerId.Value}"
        }));
    }

    [Test]
    public async Task SessionEvents_StateChangedAndServerClosed_UseConfiguredDispatcher()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        var dispatcher = new InlineCountingDispatcher();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId, dispatcher: dispatcher));
        var stateChanged = new TaskCompletionSource<NetSessionStateChanged>();
        var serverClosed = new TaskCompletionSource<NetServerClosed>();

        client.Session.StateChanged += e =>
        {
            if (e.NewRole == NetSessionRole.Client)
                stateChanged.TrySetResult(e);
        };
        client.Session.ServerClosed += e => serverClosed.TrySetResult(e);

        await server.HostAsync(new HostOptions { Port = 7777 });
        await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        await stateChanged.Task.WaitAsync(TimeSpan.FromSeconds(1));

        await server.StopAsync();
        await serverClosed.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(dispatcher.PostCount, Is.GreaterThanOrEqualTo(2));
        Assert.That(serverClosed.Task.Result.Reason, Is.EqualTo(DisconnectReason.ServerClosed));
    }

    [Test]
    public async Task Reconnect_DisabledByDefault_RemovesDisconnectedPeer()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));

        await server.HostAsync(new HostOptions { Port = 7777 });
        var join = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        await client.LeaveAsync();

        await WaitUntilAsync(() => server.Peers.Peers.All(p => p.PeerId != join.PeerId));
    }

    [Test]
    public async Task Reconnect_WithValidToken_RestoresOriginalPeerId()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        var reconnected = new TaskCompletionSource<NetPeerReconnected>();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var firstClient = new GameNet(network.CreateTransport("client-a"), Options(appId));
        await using var secondClient = new GameNet(network.CreateTransport("client-b"), Options(appId));

        server.Session.PeerReconnected += e => reconnected.TrySetResult(e);

        await server.HostAsync(new HostOptions
        {
            Port = 7777,
            ReconnectPolicy = ReconnectPolicy.Enabled(TimeSpan.FromSeconds(30))
        });
        var firstJoin = await firstClient.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        await firstClient.LeaveAsync();
        await WaitUntilAsync(() => server.Peers.Peers.Any(p => p.PeerId == firstJoin.PeerId && !p.IsConnected));

        var secondJoin = await secondClient.JoinAsync(new JoinOptions
        {
            Host = "server",
            Port = 7777,
            ReconnectToken = firstJoin.ReconnectToken
        });
        var eventArgs = await reconnected.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(firstJoin.ReconnectToken, Is.Not.Null.And.Not.Empty);
        Assert.That(secondJoin.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(secondJoin.PeerId, Is.EqualTo(firstJoin.PeerId));
        Assert.That(eventArgs.PeerId, Is.EqualTo(firstJoin.PeerId));
        Assert.That(server.Peers.Peers.Single(p => p.PeerId == firstJoin.PeerId).IsConnected, Is.True);
    }

    [Test]
    public async Task Reconnect_IssuesDifferentTokensForDifferentPeers()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var firstClient = new GameNet(network.CreateTransport("client-a"), Options(appId));
        await using var secondClient = new GameNet(network.CreateTransport("client-b"), Options(appId));

        await server.HostAsync(new HostOptions
        {
            Port = 7777,
            ReconnectPolicy = ReconnectPolicy.Enabled(TimeSpan.FromSeconds(30))
        });

        var firstJoin = await firstClient.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        var secondJoin = await secondClient.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        Assert.That(firstJoin.ReconnectToken, Is.Not.Null.And.Not.Empty);
        Assert.That(secondJoin.ReconnectToken, Is.Not.Null.And.Not.Empty);
        Assert.That(secondJoin.ReconnectToken, Is.Not.EqualTo(firstJoin.ReconnectToken));
    }

    [Test]
    public async Task Reconnect_ExpiredToken_RemovesPeerAndRejectsRestore()
    {
        var appId = Guid.NewGuid();
        var clock = new ManualTimeProvider();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId, timeProvider: clock));
        await using var firstClient = new GameNet(network.CreateTransport("client-a"), Options(appId, timeProvider: clock));
        await using var secondClient = new GameNet(network.CreateTransport("client-b"), Options(appId, timeProvider: clock));

        await server.HostAsync(new HostOptions
        {
            Port = 7777,
            ReconnectPolicy = ReconnectPolicy.Enabled(TimeSpan.FromSeconds(5))
        });
        var firstJoin = await firstClient.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        await firstClient.LeaveAsync();
        await WaitUntilAsync(() => server.Peers.Peers.Any(p => p.PeerId == firstJoin.PeerId && !p.IsConnected));
        clock.Advance(TimeSpan.FromSeconds(6));
        await WaitUntilAsync(() => server.Peers.Peers.All(p => p.PeerId != firstJoin.PeerId));

        var secondJoin = await secondClient.JoinAsync(new JoinOptions
        {
            Host = "server",
            Port = 7777,
            ReconnectToken = firstJoin.ReconnectToken
        });

        Assert.That(secondJoin.Status, Is.EqualTo(NetSessionStatus.AuthenticationFailed));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        while (!condition())
        {
            await Task.Delay(10, cts.Token);
        }
    }

    private static GameNetOptions Options(Guid appId, int protocolVersion = 1, INetEventDispatcher dispatcher = null, TimeProvider timeProvider = null) => new()
    {
        Application = new NetApplicationInfo
        {
            ApplicationId = appId,
            ProtocolVersion = protocolVersion
        },
        EventDispatcher = dispatcher,
        TimeProvider = timeProvider ?? TimeProvider.System
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
