using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleFramework.Net;
using Test.Net.TestDoubles;

#nullable enable

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
        Assert.That(join.PeerDirectorySnapshot.Select(peer => peer.PeerId), Does.Contain(PeerId.Server));
        Assert.That(join.PeerDirectorySnapshot.Select(peer => peer.PeerId), Does.Contain(join.PeerId));
        Assert.That(server.Peers.Peers.Any(p => p.PeerId == join.PeerId), Is.True);
        Assert.That(client.Peers.LocalPeerId, Is.EqualTo(join.PeerId));
    }

    [Test]
    public void PeerDirectory_DoesNotExposePublicMutationMethods()
    {
        var methods = typeof(PeerDirectory)
            .GetMethods()
            .Where(method => method.DeclaringType == typeof(PeerDirectory))
            .Where(method => method.IsPublic)
            .Select(method => method.Name)
            .ToArray();

        Assert.That(methods, Does.Not.Contain("SetLocalPeer"));
        Assert.That(methods, Does.Not.Contain("Upsert"));
        Assert.That(methods, Does.Not.Contain("Replace"));
        Assert.That(methods, Does.Not.Contain("Remove"));
    }

    [Test]
    public async Task Join_WithOversizedAuthPayload_ReturnsTransportFailedBeforeSending()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(
            network.CreateTransport("client"),
            Options(appId, maxPacketSize: 128));

        await server.HostAsync(new HostOptions { Port = 7777 });

        var join = await client.JoinAsync(new JoinOptions
        {
            Host = "server",
            Port = 7777,
            AuthPayload = new byte[1024]
        });

        Assert.That(join.Status, Is.EqualTo(NetSessionStatus.TransportFailed));
        Assert.That(join.Message, Does.Contain("MaxPacketSize"));
        Assert.That(server.Peers.RemoteParticipants(), Is.Empty);
    }

    [Test]
    public async Task Join_WhenAcceptedPacketExceedsServerLimit_ReturnsTransportFailedAndRollsBackPeer()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(
            network.CreateTransport("server"),
            Options(appId, maxPacketSize: 128));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));

        await server.HostAsync(new HostOptions { Port = 7777 });

        var join = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        Assert.That(join.Status, Is.EqualTo(NetSessionStatus.TransportFailed));
        Assert.That(join.Message, Does.Contain("Transport disconnected"));
        Assert.That(server.Peers.RemoteParticipants(), Is.Empty);
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
    public async Task JoinRejected_ServerClosesUnderlyingTransportConnection()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        var clientTransport = network.CreateTransport("client");
        await using var client = new GameNet(clientTransport, Options(Guid.NewGuid()));
        var disconnected = new TaskCompletionSource<TransportPeerDisconnected>();
        clientTransport.PeerDisconnected += e => disconnected.TrySetResult(e);

        await server.HostAsync(new HostOptions { Port = 7777 });
        var join = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        Assert.That(join.Status, Is.EqualTo(NetSessionStatus.IncompatibleApplication));
        Assert.That((await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(1))).Reason, Is.EqualTo(DisconnectReason.RemoteClosed));
    }

    [Test]
    public async Task JoinTimeout_ClientClosesUnderlyingTransportConnection()
    {
        var network = new MemoryNetNetwork();
        var silentServer = network.CreateTransport("silent-server");
        await using var client = new GameNet(network.CreateTransport("client"), Options(Guid.NewGuid()));
        var disconnected = new TaskCompletionSource<TransportPeerDisconnected>();
        silentServer.PeerDisconnected += e => disconnected.TrySetResult(e);

        await silentServer.StartServerAsync(new NetListenOptions { Port = 7777 });
        var join = await client.JoinAsync(new JoinOptions
        {
            Host = "silent-server",
            Port = 7777,
            Timeout = TimeSpan.FromMilliseconds(50)
        });

        Assert.That(join.Status, Is.EqualTo(NetSessionStatus.TransportFailed));
        Assert.That((await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(1))).Reason, Is.EqualTo(DisconnectReason.RemoteClosed));
        await silentServer.DisposeAsync();
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
    public async Task ConcurrentJoin_WhenServerHasOneSlot_AllowsOnlyOneClient()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        var authEntered = 0;
        var bothAuthStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseAuth = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var first = new GameNet(network.CreateTransport("first"), Options(appId));
        await using var second = new GameNet(network.CreateTransport("second"), Options(appId));

        await server.HostAsync(new HostOptions
        {
            Port = 7777,
            MaxPeers = 1,
            Authenticator = async _ =>
            {
                if (Interlocked.Increment(ref authEntered) == 2)
                    bothAuthStarted.TrySetResult();

                await releaseAuth.Task.ConfigureAwait(false);
                return AuthResult.Ok();
            }
        });

        var firstJoin = first.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        var secondJoin = second.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        await bothAuthStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        releaseAuth.SetResult();

        var joins = await Task.WhenAll(firstJoin, secondJoin);

        Assert.That(joins.Count(join => join.Status == NetSessionStatus.Ok), Is.EqualTo(1));
        Assert.That(joins.Count(join => join.Status == NetSessionStatus.CapacityFull), Is.EqualTo(1));
        Assert.That(server.Peers.RemoteParticipants(), Has.Count.EqualTo(1));
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
    public async Task Join_WhenAuthenticatorThrows_ReturnsAuthenticationFailedAndRecordsError()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));

        await server.HostAsync(new HostOptions
        {
            Port = 7777,
            Authenticator = _ => throw new InvalidOperationException("auth failed")
        });

        var join = await client.JoinAsync(new JoinOptions
        {
            Host = "server",
            Port = 7777,
            Timeout = TimeSpan.FromMilliseconds(200)
        });

        Assert.That(join.Status, Is.EqualTo(NetSessionStatus.AuthenticationFailed));
        Assert.That(join.Message, Does.Contain("auth failed"));
        Assert.That(server.Diagnostics.GetSnapshot().ErrorCount, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task StopAsync_DuringAuthentication_DoesNotAddPeerAfterShutdown()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        var authStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseAuth = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTransport = new BlockingPostStopSendTransport(network.CreateTransport("server"));
        await using var server = new GameNet(serverTransport, Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));

        await server.HostAsync(new HostOptions
        {
            Port = 7777,
            Authenticator = async _ =>
            {
                authStarted.TrySetResult();
                await releaseAuth.Task;
                return AuthResult.Ok();
            }
        });
        var joinTask = client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        await authStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        await server.StopAsync();
        serverTransport.BlockSends = true;
        releaseAuth.TrySetResult();
        await joinTask.WaitAsync(TimeSpan.FromSeconds(1));
        var postStopSend = await Task.WhenAny(serverTransport.SendStarted.Task, Task.Delay(100));
        serverTransport.ReleaseSend.TrySetResult();

        Assert.That(postStopSend, Is.Not.SameAs(serverTransport.SendStarted.Task));
        Assert.That(server.Session.Role, Is.EqualTo(NetSessionRole.None));
        Assert.That(server.Peers.Peers, Is.Empty);
        Assert.That(server.Diagnostics.GetSnapshot().ConnectedPeerCount, Is.EqualTo(0));
    }

    [Test]
    public async Task StopAsync_DuringBlockedInitialAcceptance_DoesNotRaiseStaleJoin()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        var serverTransport = new BlockingPostStopSendTransport(network.CreateTransport("server"));
        await using var server = new GameNet(serverTransport, Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));
        var joinedCount = 0;
        server.Session.PeerJoined += _ => Interlocked.Increment(ref joinedCount);
        await server.HostAsync(new HostOptions { Port = 7777 });

        serverTransport.BlockSends = true;
        serverTransport.SucceedBlockedSendAfterRelease = true;
        var join = client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        await serverTransport.SendStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        serverTransport.BlockSends = false;
        var stop = await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(1));
        serverTransport.ReleaseSend.TrySetResult();
        var joinResult = await join.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(stop.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(joinResult.Status, Is.Not.EqualTo(NetSessionStatus.Ok));
        Assert.That(Volatile.Read(ref joinedCount), Is.Zero);
        Assert.That(server.Peers.Peers, Is.Empty);
    }

    [Test]
    public async Task StopAsync_WhenTransportStopFails_ReturnsFailureAndCanRetry()
    {
        var transport = new FailingStopResultTransport();
        await using var server = new GameNet(transport, Options(Guid.NewGuid()));
        await server.HostAsync(new HostOptions { Port = 7777 });

        var failed = await server.StopAsync();

        Assert.That(failed.Status, Is.EqualTo(NetSessionStatus.TransportFailed));
        Assert.That(server.Session.Role, Is.EqualTo(NetSessionRole.Host));
        transport.FailStop = false;
        var retry = await server.StopAsync();
        Assert.That(retry.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(server.Session.Role, Is.EqualTo(NetSessionRole.None));
    }

    [Test]
    public async Task Join_WithAcceptedPasswordAuthPayload_Succeeds()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        var expectedPassword = new byte[] { 1, 2, 3, 4 };
        byte[]? receivedPayload = null;
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
        var participantIds = participants.Select(peer => peer.PeerId).ToArray();

        Assert.That(participantIds, Is.EquivalentTo(new[] { joinA.PeerId, joinB.PeerId }));
        Assert.That(participantIds, Does.Not.Contain(PeerId.Server));
        Assert.That(server.Peers.RemoteParticipantIds(), Is.EquivalentTo(new[] { joinA.PeerId, joinB.PeerId }));
    }

    [Test]
    public async Task PeerDirectory_ReadOnlyHelpersReflectSinglePeerStore()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));

        await server.HostAsync(new HostOptions { Port = 7777 });
        var join = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var participant = server.Peers.Get(join.PeerId);

        Assert.That(participant, Is.Not.Null);
        Assert.That(server.Peers.IsServer(PeerId.Server), Is.True);
        Assert.That(server.Peers.IsLocal(PeerId.Server), Is.True);
        Assert.That(server.Peers.IsLocal(join.PeerId), Is.False);
        Assert.That(server.Peers.Participants().Select(peer => peer.PeerId), Is.EquivalentTo(new[] { PeerId.Server, join.PeerId }));
        Assert.That(server.Peers.RemoteParticipants().Select(peer => peer.PeerId), Is.EquivalentTo(new[] { join.PeerId }));
    }

    [Test]
    public async Task PeerDirectory_ExistingClientsReceiveJoinAndLeaveUpdates()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var clientA = new GameNet(network.CreateTransport("client-a"), Options(appId));
        await using var clientB = new GameNet(network.CreateTransport("client-b"), Options(appId));
        var clientAObservedJoin = new TaskCompletionSource<NetPeerJoined>();
        var clientAObservedLeft = new TaskCompletionSource<NetPeerLeft>();

        clientA.Session.PeerJoined += e =>
        {
            if (!e.Peer.IsServer && e.Peer.PeerId != clientA.Peers.LocalPeerId)
                clientAObservedJoin.TrySetResult(e);
        };
        clientA.Session.PeerLeft += e => clientAObservedLeft.TrySetResult(e);

        await server.HostAsync(new HostOptions { Port = 7777 });
        await clientA.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        var joinB = await clientB.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var joined = await clientAObservedJoin.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(joined.Peer.PeerId, Is.EqualTo(joinB.PeerId));
        await WaitUntilAsync(() => clientA.Peers.Peers.Any(p => p.PeerId == joinB.PeerId));

        await clientB.LeaveAsync();

        var left = await clientAObservedLeft.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(left.PeerId, Is.EqualTo(joinB.PeerId));
        await WaitUntilAsync(() => clientA.Peers.Peers.All(p => p.PeerId != joinB.PeerId));
    }

    [Test]
    public async Task PeerDirectory_ClientUpdateEventsPreserveDisconnectReconnectAndLeaveOrder()
    {
        var appId = Guid.NewGuid();
        var clock = new ManualTimeProvider();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId, timeProvider: clock));
        await using var observer = new GameNet(network.CreateTransport("observer"), Options(appId, timeProvider: clock));
        await using var firstClient = new GameNet(network.CreateTransport("client-a"), Options(appId, timeProvider: clock));
        await using var secondClient = new GameNet(network.CreateTransport("client-b"), Options(appId, timeProvider: clock));
        var events = new List<string>();

        observer.Session.PeerDisconnected += e => { lock (events) events.Add($"disconnected:{e.PeerId.Value}"); };
        observer.Session.PeerReconnected += e => { lock (events) events.Add($"reconnected:{e.PeerId.Value}"); };
        observer.Session.PeerLeft += e => { lock (events) events.Add($"left:{e.PeerId.Value}"); };

        await server.HostAsync(new HostOptions
        {
            Port = 7777,
            ReconnectPolicy = ReconnectPolicy.Enabled(TimeSpan.FromSeconds(5))
        });
        await observer.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        var firstJoin = await firstClient.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        await firstClient.LeaveAsync();
        await WaitUntilAsync(() => observer.Peers.Peers.Any(p => p.PeerId == firstJoin.PeerId && !p.IsConnected));

        var reconnect = await secondClient.JoinAsync(new JoinOptions
        {
            Host = "server",
            Port = 7777,
            ReconnectToken = firstJoin.ReconnectToken
        });
        await WaitUntilAsync(() => observer.Peers.Peers.Any(p => p.PeerId == firstJoin.PeerId && p.IsConnected));

        await secondClient.LeaveAsync();
        await WaitUntilAsync(() => observer.Peers.Peers.Any(p => p.PeerId == firstJoin.PeerId && !p.IsConnected));
        clock.Advance(TimeSpan.FromSeconds(6));
        await WaitUntilAsync(() => observer.Peers.Peers.All(p => p.PeerId != firstJoin.PeerId));
        await WaitUntilAsync(() => { lock (events) return events.Count == 4; });

        Assert.That(reconnect.PeerId, Is.EqualTo(firstJoin.PeerId));
        lock (events)
            Assert.That(events, Is.EqualTo(new[]
            {
                $"disconnected:{firstJoin.PeerId.Value}",
                $"reconnected:{firstJoin.PeerId.Value}",
                $"disconnected:{firstJoin.PeerId.Value}",
                $"left:{firstJoin.PeerId.Value}"
            }));
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
    public async Task ServerKick_WhenTransportAlsoRaisesLocalDisconnect_RemovesPeerOnce()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        var serverTransport = new LocalDisconnectEchoTransport(network.CreateTransport("server"));
        await using var server = new GameNet(serverTransport, Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));
        var disconnectedCount = 0;
        var leftCount = 0;

        server.Session.PeerDisconnected += _ => Interlocked.Increment(ref disconnectedCount);
        server.Session.PeerLeft += _ => Interlocked.Increment(ref leftCount);

        await server.HostAsync(new HostOptions { Port = 7777 });
        var join = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var kick = await server.KickAsync(join.PeerId);
        await WaitUntilAsync(() => Volatile.Read(ref leftCount) > 0);
        await Task.Delay(50);

        Assert.That(kick.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(disconnectedCount, Is.EqualTo(1));
        Assert.That(leftCount, Is.EqualTo(1));
    }

    [Test]
    public async Task TcpSession_ServerKick_NotifiesClientWithKickedReason()
    {
        var appId = Guid.NewGuid();
        await using var serverTransport = new TcpNetTransport();
        await using var clientTransport = new TcpNetTransport();
        await using var server = new GameNet(serverTransport, Options(appId));
        await using var client = new GameNet(clientTransport, Options(appId));
        var clientDisconnected = new TaskCompletionSource<NetPeerDisconnected>(TaskCreationOptions.RunContinuationsAsynchronously);

        client.Session.PeerDisconnected += e =>
        {
            if (e.PeerId == PeerId.Server)
                clientDisconnected.TrySetResult(e);
        };

        await server.HostAsync(new HostOptions { BindAddress = System.Net.IPAddress.Loopback, Port = 0 });
        var join = await client.JoinAsync(new JoinOptions { Host = "127.0.0.1", Port = serverTransport.LocalEndPoint!.Port });

        var kick = await server.KickAsync(join.PeerId);
        var disconnected = await clientDisconnected.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.That(kick.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(disconnected.Reason, Is.EqualTo(DisconnectReason.Kicked));
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
    public async Task Join_WithNonPositiveTimeout_ReturnsStableFailureWithoutConnecting()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));
        await server.HostAsync(new HostOptions { Port = 7777 });

        var zero = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777, Timeout = TimeSpan.Zero });
        var negative = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777, Timeout = TimeSpan.FromMilliseconds(-2) });
        var valid = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        Assert.That(zero.Status, Is.EqualTo(NetSessionStatus.InvalidState));
        Assert.That(negative.Status, Is.EqualTo(NetSessionStatus.InvalidState));
        Assert.That(valid.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(server.Peers.RemoteParticipants(), Has.Count.EqualTo(1));
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
    public void ReconnectPolicy_EnabledRejectsNonPositiveGraceWindow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ReconnectPolicy.Enabled(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReconnectPolicy.Enabled(TimeSpan.FromSeconds(-1)));
    }

    [Test]
    public async Task DisposeAsync_CancelsPendingJoinWithoutWaitingForJoinTimeout()
    {
        var network = new MemoryNetNetwork();
        await using var silentServer = network.CreateTransport("silent-server");
        var client = new GameNet(network.CreateTransport("client"), Options(Guid.NewGuid()));

        await silentServer.StartServerAsync(new NetListenOptions { Port = 7777 });
        var joinTask = client.JoinAsync(new JoinOptions
        {
            Host = "silent-server",
            Port = 7777,
            Timeout = TimeSpan.FromSeconds(30)
        });
        await Task.Delay(50);

        var disposeTask = client.DisposeAsync().AsTask();
        var completed = await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(1)));

        Assert.That(completed, Is.SameAs(disposeTask));
        var join = await joinTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(join.Status, Is.AnyOf(NetSessionStatus.Cancelled, NetSessionStatus.ObjectDisposed, NetSessionStatus.TransportFailed));
    }

    [Test]
    public async Task ConcurrentDisposeAsync_AllCallersAwaitOwnedResourceCleanup()
    {
        var transport = new BlockingDisposeTransport();
        var net = new GameNet(transport, Options(Guid.NewGuid()));

        var firstDispose = net.DisposeAsync().AsTask();
        await transport.DisposeStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var secondDispose = net.DisposeAsync().AsTask();

        Assert.That(firstDispose.IsCompleted, Is.False);
        Assert.That(secondDispose.IsCompleted, Is.False);
        transport.ReleaseDispose.TrySetResult();
        await Task.WhenAll(firstDispose, secondDispose).WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task StopAsync_CancelsConnectInProgress_AndJoinCannotRestartSession()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        var transport = new BlockingConnectTransport(network.CreateTransport("client"));
        await using var client = new GameNet(transport, Options(appId));

        await server.HostAsync(new HostOptions { Port = 7777 });
        var joinTask = client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        await transport.ConnectStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var stop = await client.StopAsync().WaitAsync(TimeSpan.FromSeconds(1));
        transport.ReleaseConnect.TrySetResult();
        var join = await joinTask.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(stop.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(join.Status, Is.EqualTo(NetSessionStatus.Cancelled));
        Assert.That(client.Session.Role, Is.EqualTo(NetSessionRole.None));
        Assert.That(client.Peers.Peers, Is.Empty);
    }

    [Test]
    public async Task StopAsync_CancellationAfterShutdownStarts_CompletesConsistentShutdown()
    {
        var transport = new BlockingStopTransport(new MemoryNetNetwork().CreateTransport("server"));
        await using var server = new GameNet(transport, Options(Guid.NewGuid()));
        using var cancellation = new CancellationTokenSource();

        await server.HostAsync(new HostOptions { Port = 7777 });
        var stopTask = server.StopAsync(cancellation.Token);
        await transport.StopStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();
        transport.ReleaseStop.TrySetResult();
        var stop = await stopTask.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(stop.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(server.Session.Role, Is.EqualTo(NetSessionRole.None));
        Assert.That(server.Peers.Peers, Is.Empty);
    }

    [Test]
    public async Task StopAsync_DropsBusinessPacketsBufferedDuringJoin()
    {
        var appId = Guid.NewGuid();
        var transport = new ControlledJoinTransport();
        await using var client = new GameNet(transport, Options(appId));
        var markerHandled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        client.Messages.RegisterMessage<PlayerReady>();
        client.On<PlayerReady>((_, _) => markerHandled.TrySetResult());

        var joinTask = client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        await transport.JoinRequestSent.Task.WaitAsync(TimeSpan.FromSeconds(1));
        transport.EmitMessage(client.Messages.Registry.Get<PlayerReady>().MessageId, new PlayerReady(true));

        var stop = await client.StopAsync().WaitAsync(TimeSpan.FromSeconds(1));
        var join = await joinTask.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(stop.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(join.Status, Is.EqualTo(NetSessionStatus.Cancelled));
        Assert.That(markerHandled.Task.IsCompleted, Is.False);
        Assert.That(client.Session.Role, Is.EqualTo(NetSessionRole.None));
        Assert.That(client.Peers.Peers, Is.Empty);
    }

    [Test]
    public async Task Join_ImmediateBusinessPacket_WaitsUntilClientSessionIsCommitted()
    {
        var appId = Guid.NewGuid();
        var transport = new ControlledJoinTransport();
        await using var client = new GameNet(transport, Options(appId));
        var received = new TaskCompletionSource<(PeerId Sender, NetSessionRole Role)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.Messages.RegisterMessage<PlayerReady>();
        client.On<PlayerReady>((context, _) => received.TrySetResult((context.SenderId, client.Session.Role)));

        var joinTask = client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        var connectionId = await transport.WaitForJoinRequestAsync();
        var lifecycleGate = (SemaphoreSlim)typeof(GameNet)
            .GetField("_lifecycleGate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(client)!;
        await lifecycleGate.WaitAsync();
        try
        {
            transport.EmitJoinAccepted(connectionId, appId, new PeerId(2), client.Messages.Registry.GetFingerprint());
            transport.EmitMessage(client.Messages.Registry.Get<PlayerReady>().MessageId, new PlayerReady(true));
            await Task.Delay(50);
            Assert.That(received.Task.IsCompleted, Is.False);
        }
        finally
        {
            lifecycleGate.Release();
        }

        Assert.That((await joinTask.WaitAsync(TimeSpan.FromSeconds(1))).Status, Is.EqualTo(NetSessionStatus.Ok));
        var handled = await received.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(handled.Sender, Is.EqualTo(PeerId.Server));
        Assert.That(handled.Role, Is.EqualTo(NetSessionRole.Client));
    }

    [Test]
    public async Task Join_BusinessPacketBeforeAccept_WaitsUntilClientSessionIsCommitted()
    {
        var appId = Guid.NewGuid();
        var transport = new ControlledJoinTransport();
        await using var client = new GameNet(transport, Options(appId));
        var received = new TaskCompletionSource<PeerId>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Messages.RegisterMessage<PlayerReady>();
        client.On<PlayerReady>((context, _) => received.TrySetResult(context.SenderId));

        var joinTask = client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        var connectionId = await transport.WaitForJoinRequestAsync();
        transport.EmitMessage(client.Messages.Registry.Get<PlayerReady>().MessageId, new PlayerReady(true));
        await Task.Delay(50);
        Assert.That(received.Task.IsCompleted, Is.False);

        transport.EmitJoinAccepted(connectionId, appId, new PeerId(2), client.Messages.Registry.GetFingerprint());

        Assert.That((await joinTask.WaitAsync(TimeSpan.FromSeconds(1))).Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(await received.Task.WaitAsync(TimeSpan.FromSeconds(1)), Is.EqualTo(PeerId.Server));
    }

    [Test]
    public async Task JoinRejected_DoesNotPermanentlyFreezeMessageRegistration()
    {
        var appId = Guid.NewGuid();
        var transport = new ControlledJoinTransport();
        await using var client = new GameNet(transport, Options(appId));

        var joinTask = client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        var connectionId = await transport.WaitForJoinRequestAsync();
        transport.EmitJoinRejected(connectionId, appId, NetSessionStatus.AuthenticationFailed, "Rejected.");

        var join = await joinTask.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(join.Status, Is.EqualTo(NetSessionStatus.AuthenticationFailed));
        Assert.DoesNotThrow(() => client.Messages.RegisterMessage<PlayerReady>());
    }

    [Test]
    public async Task JoinPending_DirectRegistryMutation_IsRejectedAndFingerprintStaysStable()
    {
        var appId = Guid.NewGuid();
        var transport = new ControlledJoinTransport();
        await using var client = new GameNet(transport, Options(appId));
        client.Messages.RegisterMessage<PlayerReady>();
        var fingerprintBeforeJoin = client.Messages.Registry.GetFingerprint();

        var joinTask = client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        var connectionId = await transport.WaitForJoinRequestAsync();

        Assert.Throws<InvalidOperationException>(() => client.Messages.Registry.Register<LateMessage>());
        Assert.That(client.Messages.Registry.GetFingerprint(), Is.EqualTo(fingerprintBeforeJoin));

        transport.EmitJoinRejected(connectionId, appId, NetSessionStatus.AuthenticationFailed, "Rejected.");
        Assert.That((await joinTask.WaitAsync(TimeSpan.FromSeconds(1))).Status, Is.EqualTo(NetSessionStatus.AuthenticationFailed));
    }

    [Test]
    public async Task Join_StaleDisconnectFromPreviousAttempt_DoesNotFailCurrentAttempt()
    {
        var appId = Guid.NewGuid();
        var transport = new ControlledJoinTransport();
        await using var client = new GameNet(transport, Options(appId));
        using var firstCancellation = new CancellationTokenSource();

        var firstJoinTask = client.JoinAsync(
            new JoinOptions { Host = "server", Port = 7777 },
            firstCancellation.Token);
        var firstConnection = await transport.WaitForJoinRequestAsync();
        firstCancellation.Cancel();
        Assert.That((await firstJoinTask).Status, Is.EqualTo(NetSessionStatus.Cancelled));

        var secondJoinTask = client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        var secondConnection = await transport.WaitForJoinRequestAsync();
        transport.EmitDisconnected(firstConnection);
        transport.EmitJoinAccepted(secondConnection, appId, new PeerId(2), client.Messages.Registry.GetFingerprint());

        var secondJoin = await secondJoinTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(secondJoin.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(client.Session.Role, Is.EqualTo(NetSessionRole.Client));
    }

    [Test]
    public async Task TransportDisconnect_PreemptsBlockedBusinessHandler()
    {
        var appId = Guid.NewGuid();
        var transport = new ControlledJoinTransport();
        await using var client = new GameNet(transport, Options(appId));
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        client.Messages.RegisterMessage<PlayerReady>();
        client.On<PlayerReady>(async (_, _) =>
        {
            handlerStarted.TrySetResult();
            await releaseHandler.Task;
        });

        var joinTask = client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        var connectionId = await transport.WaitForJoinRequestAsync();
        transport.EmitJoinAccepted(connectionId, appId, new PeerId(2), client.Messages.Registry.GetFingerprint());
        Assert.That((await joinTask).Status, Is.EqualTo(NetSessionStatus.Ok));

        transport.EmitMessage(client.Messages.Registry.Get<PlayerReady>().MessageId, new PlayerReady(false));
        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        transport.EmitDisconnected(connectionId);

        try
        {
            await WaitUntilAsync(() => client.Session.Role == NetSessionRole.None);
            Assert.That(client.Peers.Peers, Is.Empty);
        }
        finally
        {
            releaseHandler.TrySetResult();
        }
    }

    [Test]
    public async Task Join_AcceptImmediatelyFollowedByDisconnect_DoesNotCommitClosedConnection()
    {
        var appId = Guid.NewGuid();
        var transport = new ControlledJoinTransport();
        await using var client = new GameNet(transport, Options(appId));

        var joinTask = client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        var connectionId = await transport.WaitForJoinRequestAsync();
        var lifecycleGate = (SemaphoreSlim)typeof(GameNet)
            .GetField("_lifecycleGate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(client)!;
        await lifecycleGate.WaitAsync();
        try
        {
            transport.EmitJoinAccepted(connectionId, appId, new PeerId(2), client.Messages.Registry.GetFingerprint());
            transport.EmitDisconnected(connectionId);
        }
        finally
        {
            lifecycleGate.Release();
        }

        var join = await joinTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(join.Status, Is.Not.EqualTo(NetSessionStatus.Ok));
        Assert.That(client.Session.Role, Is.EqualTo(NetSessionRole.None));
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
    public async Task SessionEvents_DefaultDelivery_RunsBeforeLifecycleCallReturns()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        var observed = false;

        server.Session.StateChanged += _ => observed = true;

        var host = await server.HostAsync(new HostOptions { Port = 7777 });

        Assert.That(host.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(observed, Is.True);
    }

    [Test]
    public async Task SessionEvents_DispatcherFailure_IsRecordedAndJoinStillCompletes()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId, dispatcher: new ThrowingDispatcher()));

        await server.HostAsync(new HostOptions { Port = 7777 });
        var join = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        Assert.That(join.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(client.Diagnostics.GetSnapshot().ErrorCount, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task SessionEvents_CallbackException_IsRecordedAndLaterEventsContinue()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));
        var serverClosed = new TaskCompletionSource<NetServerClosed>();

        client.Session.StateChanged += _ => throw new InvalidOperationException("callback failed");
        client.Session.ServerClosed += e => serverClosed.TrySetResult(e);

        await server.HostAsync(new HostOptions { Port = 7777 });
        var join = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        await server.StopAsync();
        await serverClosed.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(join.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(client.Diagnostics.GetSnapshot().ErrorCount, Is.GreaterThanOrEqualTo(1));
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
    public async Task Reconnect_AtGraceBoundary_NeverRemovesSuccessfulReconnect()
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var appId = Guid.NewGuid();
            var clock = new ManualTimeProvider();
            var network = new MemoryNetNetwork();
            await using var server = new GameNet(network.CreateTransport("server"), Options(appId, timeProvider: clock));
            await using var firstClient = new GameNet(network.CreateTransport("first"), Options(appId, timeProvider: clock));
            await using var secondClient = new GameNet(network.CreateTransport("second"), Options(appId, timeProvider: clock));
            await server.HostAsync(new HostOptions
            {
                Port = 7777,
                ReconnectPolicy = ReconnectPolicy.Enabled(TimeSpan.FromSeconds(5))
            });
            var firstJoin = await firstClient.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
            await firstClient.LeaveAsync();
            await WaitUntilAsync(() => server.Peers.Peers.Any(peer => peer.PeerId == firstJoin.PeerId && !peer.IsConnected));

            var reconnectTask = secondClient.JoinAsync(new JoinOptions
            {
                Host = "server",
                Port = 7777,
                ReconnectToken = firstJoin.ReconnectToken
            });
            clock.Advance(TimeSpan.FromSeconds(5));
            var reconnect = await reconnectTask.WaitAsync(TimeSpan.FromSeconds(1));

            if (reconnect.Status == NetSessionStatus.Ok)
            {
                await Task.Yield();
                Assert.That(server.Peers.Peers.Any(peer =>
                    peer.PeerId == firstJoin.PeerId && peer.IsConnected), Is.True, $"Attempt {attempt}");
            }
        }
    }

    [Test]
    public async Task Reconnect_WithClientPolicy_AutomaticallyRestoresOriginalPeerIdAfterTransportDrop()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        var serverTransport = network.CreateTransport("server");
        var clientTransport = network.CreateTransport("client");
        await using var server = new GameNet(serverTransport, Options(appId));
        await using var client = new GameNet(clientTransport, Options(appId));
        var clientReconnected = new TaskCompletionSource<NetPeerReconnected>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Session.PeerReconnected += e => clientReconnected.TrySetResult(e);

        await server.HostAsync(new HostOptions
        {
            Port = 7777,
            ReconnectPolicy = ReconnectPolicy.Enabled(TimeSpan.FromSeconds(30))
        });
        var firstJoin = await client.JoinAsync(new JoinOptions
        {
            Host = "server",
            Port = 7777,
            Reconnect = ReconnectPolicy.FixedRetry(3, TimeSpan.FromMilliseconds(10))
        });

        clientTransport.RemoveRemoteConnection(GetOnlyConnectionId(clientTransport), DisconnectReason.TransportFailed);
        serverTransport.RemoveRemoteConnection(GetOnlyConnectionId(serverTransport), DisconnectReason.RemoteClosed);

        var reconnected = await clientReconnected.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(firstJoin.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(reconnected.PeerId, Is.EqualTo(firstJoin.PeerId));
        Assert.That(client.Peers.LocalPeerId, Is.EqualTo(firstJoin.PeerId));
        Assert.That(client.Session.Role, Is.EqualTo(NetSessionRole.Client));
        await WaitUntilAsync(() => server.Peers.Peers.Single(p => p.PeerId == firstJoin.PeerId).IsConnected);
    }

    [Test]
    public async Task StopAsync_DuringReconnectDelay_PreventsSessionRestart()
    {
        var appId = Guid.NewGuid();
        var clock = new ManualTimeProvider();
        var network = new MemoryNetNetwork();
        var serverTransport = network.CreateTransport("server");
        var clientTransport = network.CreateTransport("client");
        await using var server = new GameNet(serverTransport, Options(appId, timeProvider: clock));
        await using var client = new GameNet(clientTransport, Options(appId, timeProvider: clock));

        await server.HostAsync(new HostOptions
        {
            Port = 7777,
            ReconnectPolicy = ReconnectPolicy.Enabled(TimeSpan.FromSeconds(30))
        });
        await client.JoinAsync(new JoinOptions
        {
            Host = "server",
            Port = 7777,
            Reconnect = ReconnectPolicy.FixedRetry(3, TimeSpan.FromSeconds(5))
        });

        clientTransport.RemoveRemoteConnection(GetOnlyConnectionId(clientTransport), DisconnectReason.TransportFailed);
        serverTransport.RemoveRemoteConnection(GetOnlyConnectionId(serverTransport), DisconnectReason.RemoteClosed);
        await WaitUntilAsync(() => client.Session.Role == NetSessionRole.None);
        await Task.Delay(20);

        var stop = await client.StopAsync();
        clock.Advance(TimeSpan.FromSeconds(6));
        await Task.Delay(100);

        Assert.That(stop.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(client.Session.Role, Is.EqualTo(NetSessionRole.None));
        Assert.That(client.Peers.Peers, Is.Empty);
    }

    [Test]
    public async Task Reconnect_TokenCannotBeReusedAfterSuccessfulReconnect()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var firstClient = new GameNet(network.CreateTransport("client-a"), Options(appId));
        await using var secondClient = new GameNet(network.CreateTransport("client-b"), Options(appId));
        await using var thirdClient = new GameNet(network.CreateTransport("client-c"), Options(appId));

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
        var thirdJoin = await thirdClient.JoinAsync(new JoinOptions
        {
            Host = "server",
            Port = 7777,
            ReconnectToken = firstJoin.ReconnectToken
        });

        Assert.That(secondJoin.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(thirdJoin.Status, Is.EqualTo(NetSessionStatus.AuthenticationFailed));
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

    [Test]
    public async Task Reconnect_WhenAcceptedSendFails_RetainsPendingReconnectUntilGraceExpires()
    {
        var appId = Guid.NewGuid();
        var clock = new ManualTimeProvider();
        var network = new MemoryNetNetwork();
        var serverTransport = new FailingJoinAcceptedTransport(network.CreateTransport("server"));
        await using var server = new GameNet(serverTransport, Options(appId, timeProvider: clock));
        await using var firstClient = new GameNet(network.CreateTransport("client-a"), Options(appId, timeProvider: clock));
        await using var secondClient = new GameNet(network.CreateTransport("client-b"), Options(appId, timeProvider: clock));
        await using var thirdClient = new GameNet(network.CreateTransport("client-c"), Options(appId, timeProvider: clock));

        await server.HostAsync(new HostOptions
        {
            Port = 7777,
            ReconnectPolicy = ReconnectPolicy.Enabled(TimeSpan.FromSeconds(5))
        });
        var firstJoin = await firstClient.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        await firstClient.LeaveAsync();
        await WaitUntilAsync(() => server.Peers.Peers.Any(p => p.PeerId == firstJoin.PeerId && !p.IsConnected));

        serverTransport.FailNextJoinAcceptedSend = true;
        var failedReconnect = await secondClient.JoinAsync(new JoinOptions
        {
            Host = "server",
            Port = 7777,
            ReconnectToken = firstJoin.ReconnectToken
        });

        var retryReconnect = await thirdClient.JoinAsync(new JoinOptions
        {
            Host = "server",
            Port = 7777,
            ReconnectToken = firstJoin.ReconnectToken
        });

        Assert.That(failedReconnect.Status, Is.EqualTo(NetSessionStatus.TransportFailed));
        Assert.That(retryReconnect.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(retryReconnect.PeerId, Is.EqualTo(firstJoin.PeerId));
    }

    [Test]
    public async Task StopAsync_DuringBlockedReconnectAcceptance_CannotRestoreStoppedSession()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        var serverTransport = new BlockingPostStopSendTransport(network.CreateTransport("server"));
        await using var server = new GameNet(serverTransport, Options(appId));
        await using var firstClient = new GameNet(network.CreateTransport("first"), Options(appId));
        await using var secondClient = new GameNet(network.CreateTransport("second"), Options(appId));
        await server.HostAsync(new HostOptions
        {
            Port = 7777,
            ReconnectPolicy = ReconnectPolicy.Enabled(TimeSpan.FromSeconds(30))
        });
        var firstJoin = await firstClient.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        await firstClient.LeaveAsync();
        await WaitUntilAsync(() => server.Peers.Peers.Any(peer => peer.PeerId == firstJoin.PeerId && !peer.IsConnected));

        serverTransport.BlockSends = true;
        var reconnect = secondClient.JoinAsync(new JoinOptions
        {
            Host = "server",
            Port = 7777,
            ReconnectToken = firstJoin.ReconnectToken
        });
        await serverTransport.SendStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        serverTransport.BlockSends = false;
        var stop = await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(1));
        serverTransport.ReleaseSend.TrySetResult();
        var reconnectResult = await reconnect.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(stop.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(reconnectResult.Status, Is.Not.EqualTo(NetSessionStatus.Ok));
        Assert.That(server.Session.Role, Is.EqualTo(NetSessionRole.None));
        Assert.That(server.Peers.Peers, Is.Empty);
    }

    [Test]
    public async Task ReconnectExpiry_WaitsForBlockedFailedAcceptanceThenRemovesPeer()
    {
        var appId = Guid.NewGuid();
        var clock = new ManualTimeProvider();
        var network = new MemoryNetNetwork();
        var serverTransport = new BlockingPostStopSendTransport(network.CreateTransport("server"));
        await using var server = new GameNet(serverTransport, Options(appId, timeProvider: clock));
        await using var firstClient = new GameNet(network.CreateTransport("first"), Options(appId, timeProvider: clock));
        await using var secondClient = new GameNet(network.CreateTransport("second"), Options(appId, timeProvider: clock));
        await server.HostAsync(new HostOptions
        {
            Port = 7777,
            MaxPeers = 1,
            ReconnectPolicy = ReconnectPolicy.Enabled(TimeSpan.FromSeconds(5))
        });
        var firstJoin = await firstClient.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        await firstClient.LeaveAsync();
        await WaitUntilAsync(() => server.Peers.Peers.Any(peer => peer.PeerId == firstJoin.PeerId && !peer.IsConnected));

        serverTransport.BlockSends = true;
        serverTransport.FailBlockedSendAfterRelease = true;
        var reconnect = secondClient.JoinAsync(new JoinOptions
        {
            Host = "server",
            Port = 7777,
            ReconnectToken = firstJoin.ReconnectToken
        });
        await serverTransport.SendStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        clock.Advance(TimeSpan.FromSeconds(6));
        await Task.Yield();
        serverTransport.ReleaseSend.TrySetResult();
        Assert.That((await reconnect.WaitAsync(TimeSpan.FromSeconds(1))).Status, Is.Not.EqualTo(NetSessionStatus.Ok));

        await WaitUntilAsync(() => server.Peers.Peers.All(peer => peer.PeerId != firstJoin.PeerId));
        Assert.That(server.Peers.RemoteParticipants(), Is.Empty);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        while (!condition())
        {
            await Task.Delay(10, cts.Token);
        }
    }

    private static TransportConnectionId GetOnlyConnectionId(MemoryNetTransport transport)
    {
        var field = typeof(MemoryNetTransport).GetField("_connections", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var connections = field.GetValue(transport)!;
        var keys = (IEnumerable<TransportConnectionId>)connections.GetType().GetProperty("Keys")!.GetValue(connections)!;
        return keys.Single();
    }

    private static GameNetOptions Options(
        Guid appId,
        int protocolVersion = 1,
        INetEventDispatcher? dispatcher = null,
        TimeProvider? timeProvider = null,
        int maxPacketSize = 64 * 1024) => new()
    {
        Application = new NetApplicationInfo
        {
            ApplicationId = appId,
            ProtocolVersion = protocolVersion
        },
        EventDispatcher = dispatcher,
        TimeProvider = timeProvider ?? TimeProvider.System,
        MaxPacketSize = maxPacketSize
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

    private sealed class ThrowingDispatcher : INetEventDispatcher
    {
        public void Post(Action action)
        {
            throw new InvalidOperationException("post failed");
        }
    }

    private sealed class FailingJoinAcceptedTransport : INetTransport
    {
        private readonly INetTransport _inner;

        public FailingJoinAcceptedTransport(INetTransport inner)
        {
            _inner = inner;
            _inner.PeerConnected += connected => PeerConnected?.Invoke(connected);
            _inner.PeerDisconnected += disconnected => PeerDisconnected?.Invoke(disconnected);
            _inner.PacketReceived += received => PacketReceived?.Invoke(received);
            _inner.Error += error => Error?.Invoke(error);
        }

        public bool FailNextJoinAcceptedSend { get; set; }

        public event Action<TransportPeerConnected>? PeerConnected;
        public event Action<TransportPeerDisconnected>? PeerDisconnected;
        public event Action<TransportPacketReceived>? PacketReceived;
        public event Action<TransportError>? Error;

        public Task<TransportStartResult> StartServerAsync(NetListenOptions options, CancellationToken token = default) =>
            _inner.StartServerAsync(options, token);

        public Task<TransportConnectResult> ConnectAsync(NetConnectOptions options, CancellationToken token = default) =>
            _inner.ConnectAsync(options, token);

        public Task<TransportStartResult> StopServerAsync(CancellationToken token = default) =>
            _inner.StopServerAsync(token);

        public Task DisconnectAsync(TransportConnectionId connectionId, DisconnectReason reason = DisconnectReason.LocalClosed) =>
            _inner.DisconnectAsync(connectionId, reason);

        public ValueTask<NetSendResult> SendAsync(TransportConnectionId connectionId, ReadOnlyMemory<byte> data, NetChannel channel, CancellationToken token = default)
        {
            if (FailNextJoinAcceptedSend && IsJoinAccepted(data))
            {
                FailNextJoinAcceptedSend = false;
                return ValueTask.FromResult(new NetSendResult(NetSendStatus.TransportFailed, "join accepted send failed"));
            }

            return _inner.SendAsync(connectionId, data, channel, token);
        }

        public ValueTask DisposeAsync() => _inner.DisposeAsync();

        private static bool IsJoinAccepted(ReadOnlyMemory<byte> data)
        {
            try
            {
                return JsonSerializer.Deserialize<SessionPacket>(data.Span)?.Kind == SessionPacket.JoinAccepted;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }

    private sealed class BlockingConnectTransport : INetTransport
    {
        private readonly INetTransport _inner;

        public BlockingConnectTransport(INetTransport inner)
        {
            _inner = inner;
            _inner.PeerConnected += connected => PeerConnected?.Invoke(connected);
            _inner.PeerDisconnected += disconnected => PeerDisconnected?.Invoke(disconnected);
            _inner.PacketReceived += received => PacketReceived?.Invoke(received);
            _inner.Error += error => Error?.Invoke(error);
        }

        public TaskCompletionSource ConnectStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseConnect { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public event Action<TransportPeerConnected>? PeerConnected;
        public event Action<TransportPeerDisconnected>? PeerDisconnected;
        public event Action<TransportPacketReceived>? PacketReceived;
        public event Action<TransportError>? Error;

        public Task<TransportStartResult> StartServerAsync(NetListenOptions options, CancellationToken token = default) =>
            _inner.StartServerAsync(options, token);

        public async Task<TransportConnectResult> ConnectAsync(NetConnectOptions options, CancellationToken token = default)
        {
            ConnectStarted.TrySetResult();
            await ReleaseConnect.Task.WaitAsync(token);
            return await _inner.ConnectAsync(options, token);
        }

        public Task<TransportStartResult> StopServerAsync(CancellationToken token = default) =>
            _inner.StopServerAsync(token);

        public Task DisconnectAsync(TransportConnectionId connectionId, DisconnectReason reason = DisconnectReason.LocalClosed) =>
            _inner.DisconnectAsync(connectionId, reason);

        public ValueTask<NetSendResult> SendAsync(TransportConnectionId connectionId, ReadOnlyMemory<byte> data, NetChannel channel, CancellationToken token = default) =>
            _inner.SendAsync(connectionId, data, channel, token);

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }

    private sealed class BlockingDisposeTransport : INetTransport
    {
        public TaskCompletionSource DisposeStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseDispose { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public event Action<TransportPeerConnected>? PeerConnected { add { } remove { } }
        public event Action<TransportPeerDisconnected>? PeerDisconnected { add { } remove { } }
        public event Action<TransportPacketReceived>? PacketReceived { add { } remove { } }
        public event Action<TransportError>? Error { add { } remove { } }

        public Task<TransportStartResult> StartServerAsync(NetListenOptions options, CancellationToken token = default) =>
            Task.FromResult(new TransportStartResult(NetTransportStatus.Ok));

        public Task<TransportConnectResult> ConnectAsync(NetConnectOptions options, CancellationToken token = default) =>
            Task.FromResult(new TransportConnectResult(NetTransportStatus.ConnectionRefused, TransportConnectionId.None));

        public Task<TransportStartResult> StopServerAsync(CancellationToken token = default) =>
            Task.FromResult(new TransportStartResult(NetTransportStatus.Ok));

        public Task DisconnectAsync(TransportConnectionId connectionId, DisconnectReason reason = DisconnectReason.LocalClosed) =>
            Task.CompletedTask;

        public ValueTask<NetSendResult> SendAsync(TransportConnectionId connectionId, ReadOnlyMemory<byte> data, NetChannel channel, CancellationToken token = default) =>
            ValueTask.FromResult(NetSendResult.Ok());

        public async ValueTask DisposeAsync()
        {
            DisposeStarted.TrySetResult();
            await ReleaseDispose.Task;
        }
    }

    private sealed class ControlledJoinTransport : INetTransport
    {
        private readonly ConcurrentQueue<TransportConnectionId> _joinRequests = new();
        private readonly SemaphoreSlim _joinRequestSignal = new(0);
        private long _nextConnectionId;
        private TransportConnectionId _currentConnectionId = TransportConnectionId.None;

        public TaskCompletionSource JoinRequestSent { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public event Action<TransportPeerConnected>? PeerConnected { add { } remove { } }
        public event Action<TransportPeerDisconnected>? PeerDisconnected;
        public event Action<TransportPacketReceived>? PacketReceived;
        public event Action<TransportError>? Error { add { } remove { } }

        public Task<TransportStartResult> StartServerAsync(NetListenOptions options, CancellationToken token = default) =>
            Task.FromResult(new TransportStartResult(NetTransportStatus.Ok));

        public Task<TransportConnectResult> ConnectAsync(NetConnectOptions options, CancellationToken token = default)
        {
            _currentConnectionId = new TransportConnectionId((ulong)Interlocked.Increment(ref _nextConnectionId));
            return Task.FromResult(new TransportConnectResult(NetTransportStatus.Ok, _currentConnectionId));
        }

        public Task<TransportStartResult> StopServerAsync(CancellationToken token = default) =>
            Task.FromResult(new TransportStartResult(NetTransportStatus.Ok));

        public Task DisconnectAsync(TransportConnectionId connectionId, DisconnectReason reason = DisconnectReason.LocalClosed) =>
            Task.CompletedTask;

        public ValueTask<NetSendResult> SendAsync(TransportConnectionId connectionId, ReadOnlyMemory<byte> data, NetChannel channel, CancellationToken token = default)
        {
            _joinRequests.Enqueue(connectionId);
            _joinRequestSignal.Release();
            JoinRequestSent.TrySetResult();
            return ValueTask.FromResult(NetSendResult.Ok());
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void EmitJoinAccepted(Guid appId, PeerId peerId, string fingerprint)
        {
            EmitJoinAccepted(_currentConnectionId, appId, peerId, fingerprint);
        }

        public void EmitJoinAccepted(TransportConnectionId connectionId, Guid appId, PeerId peerId, string fingerprint)
        {
            var packet = new SessionPacket(
                SessionPacket.JoinAccepted,
                appId,
                1,
                null,
                null,
                peerId,
                new[]
                {
                    new PeerInfo(PeerId.Server, true, false, DateTimeOffset.UtcNow),
                    new PeerInfo(peerId, false, true, DateTimeOffset.UtcNow)
                },
                fingerprint,
                NetSessionStatus.Ok,
                null);
            PacketReceived?.Invoke(new TransportPacketReceived(
                connectionId,
                JsonSerializer.SerializeToUtf8Bytes(packet),
                NetChannel.System));
        }

        public void EmitJoinRejected(TransportConnectionId connectionId, Guid appId, NetSessionStatus status, string message)
        {
            var packet = new SessionPacket(
                SessionPacket.JoinRejected,
                appId,
                1,
                null,
                null,
                PeerId.None,
                null,
                null,
                status,
                message);
            PacketReceived?.Invoke(new TransportPacketReceived(
                connectionId,
                JsonSerializer.SerializeToUtf8Bytes(packet),
                NetChannel.System));
        }

        public async Task<TransportConnectionId> WaitForJoinRequestAsync()
        {
            await _joinRequestSignal.WaitAsync(TimeSpan.FromSeconds(1));
            return _joinRequests.TryDequeue(out var connectionId)
                ? connectionId
                : throw new InvalidOperationException("Join request signal had no connection.");
        }

        public void EmitDisconnected(TransportConnectionId connectionId) =>
            PeerDisconnected?.Invoke(new TransportPeerDisconnected(connectionId, DisconnectReason.RemoteClosed));

        public void EmitMessage<T>(ulong messageId, T message)
        {
            var packet = new NetPacket
            {
                Kind = NetPacket.Message,
                MessageId = messageId,
                Payload = new JsonNetCodec().Encode(message)
            };
            PacketReceived?.Invoke(new TransportPacketReceived(
                _currentConnectionId,
                JsonSerializer.SerializeToUtf8Bytes(packet),
                NetChannel.Reliable));
        }
    }

    private sealed class BlockingStopTransport : INetTransport
    {
        private readonly INetTransport _inner;

        public BlockingStopTransport(INetTransport inner)
        {
            _inner = inner;
            _inner.PeerConnected += connected => PeerConnected?.Invoke(connected);
            _inner.PeerDisconnected += disconnected => PeerDisconnected?.Invoke(disconnected);
            _inner.PacketReceived += received => PacketReceived?.Invoke(received);
            _inner.Error += error => Error?.Invoke(error);
        }

        public TaskCompletionSource StopStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseStop { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public event Action<TransportPeerConnected>? PeerConnected;
        public event Action<TransportPeerDisconnected>? PeerDisconnected;
        public event Action<TransportPacketReceived>? PacketReceived;
        public event Action<TransportError>? Error;

        public Task<TransportStartResult> StartServerAsync(NetListenOptions options, CancellationToken token = default) =>
            _inner.StartServerAsync(options, token);

        public Task<TransportConnectResult> ConnectAsync(NetConnectOptions options, CancellationToken token = default) =>
            _inner.ConnectAsync(options, token);

        public async Task<TransportStartResult> StopServerAsync(CancellationToken token = default)
        {
            StopStarted.TrySetResult();
            await ReleaseStop.Task;
            if (token.IsCancellationRequested)
                return new TransportStartResult(NetTransportStatus.Cancelled);

            return await _inner.StopServerAsync(token);
        }

        public Task DisconnectAsync(TransportConnectionId connectionId, DisconnectReason reason = DisconnectReason.LocalClosed) =>
            _inner.DisconnectAsync(connectionId, reason);

        public ValueTask<NetSendResult> SendAsync(TransportConnectionId connectionId, ReadOnlyMemory<byte> data, NetChannel channel, CancellationToken token = default) =>
            _inner.SendAsync(connectionId, data, channel, token);

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }

    private sealed class BlockingPostStopSendTransport : INetTransport
    {
        private readonly INetTransport _inner;

        public BlockingPostStopSendTransport(INetTransport inner)
        {
            _inner = inner;
            _inner.PeerConnected += connected => PeerConnected?.Invoke(connected);
            _inner.PeerDisconnected += disconnected => PeerDisconnected?.Invoke(disconnected);
            _inner.PacketReceived += received => PacketReceived?.Invoke(received);
            _inner.Error += error => Error?.Invoke(error);
        }

        public bool BlockSends { get; set; }
        public bool FailBlockedSendAfterRelease { get; set; }
        public bool SucceedBlockedSendAfterRelease { get; set; }
        public TaskCompletionSource SendStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseSend { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public event Action<TransportPeerConnected>? PeerConnected;
        public event Action<TransportPeerDisconnected>? PeerDisconnected;
        public event Action<TransportPacketReceived>? PacketReceived;
        public event Action<TransportError>? Error;

        public Task<TransportStartResult> StartServerAsync(NetListenOptions options, CancellationToken token = default) =>
            _inner.StartServerAsync(options, token);

        public Task<TransportConnectResult> ConnectAsync(NetConnectOptions options, CancellationToken token = default) =>
            _inner.ConnectAsync(options, token);

        public Task<TransportStartResult> StopServerAsync(CancellationToken token = default) =>
            _inner.StopServerAsync(token);

        public Task DisconnectAsync(TransportConnectionId connectionId, DisconnectReason reason = DisconnectReason.LocalClosed) =>
            _inner.DisconnectAsync(connectionId, reason);

        public async ValueTask<NetSendResult> SendAsync(TransportConnectionId connectionId, ReadOnlyMemory<byte> data, NetChannel channel, CancellationToken token = default)
        {
            if (BlockSends)
            {
                SendStarted.TrySetResult();
                await ReleaseSend.Task;
                if (FailBlockedSendAfterRelease)
                    return new NetSendResult(NetSendStatus.TransportFailed, "blocked send failed");
                if (SucceedBlockedSendAfterRelease)
                    return NetSendResult.Ok();
            }

            return await _inner.SendAsync(connectionId, data, channel, token);
        }

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }

    private sealed class FailingStopResultTransport : INetTransport
    {
        public bool FailStop { get; set; } = true;
        public event Action<TransportPeerConnected>? PeerConnected { add { } remove { } }
        public event Action<TransportPeerDisconnected>? PeerDisconnected { add { } remove { } }
        public event Action<TransportPacketReceived>? PacketReceived { add { } remove { } }
        public event Action<TransportError>? Error { add { } remove { } }
        public Task<TransportStartResult> StartServerAsync(NetListenOptions options, CancellationToken token = default) =>
            Task.FromResult(new TransportStartResult(NetTransportStatus.Ok));
        public Task<TransportConnectResult> ConnectAsync(NetConnectOptions options, CancellationToken token = default) =>
            Task.FromResult(new TransportConnectResult(NetTransportStatus.ConnectionRefused, TransportConnectionId.None));
        public Task<TransportStartResult> StopServerAsync(CancellationToken token = default) =>
            Task.FromResult(FailStop
                ? new TransportStartResult(NetTransportStatus.TransportFailed, "stop failed")
                : new TransportStartResult(NetTransportStatus.Ok));
        public Task DisconnectAsync(TransportConnectionId connectionId, DisconnectReason reason = DisconnectReason.LocalClosed) => Task.CompletedTask;
        public ValueTask<NetSendResult> SendAsync(TransportConnectionId connectionId, ReadOnlyMemory<byte> data, NetChannel channel, CancellationToken token = default) =>
            ValueTask.FromResult(NetSendResult.Ok());
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class LocalDisconnectEchoTransport : INetTransport
    {
        private readonly INetTransport _inner;

        public LocalDisconnectEchoTransport(INetTransport inner)
        {
            _inner = inner;
            _inner.PeerConnected += connected => PeerConnected?.Invoke(connected);
            _inner.PeerDisconnected += disconnected => PeerDisconnected?.Invoke(disconnected);
            _inner.PacketReceived += received => PacketReceived?.Invoke(received);
            _inner.Error += error => Error?.Invoke(error);
        }

        public event Action<TransportPeerConnected>? PeerConnected;
        public event Action<TransportPeerDisconnected>? PeerDisconnected;
        public event Action<TransportPacketReceived>? PacketReceived;
        public event Action<TransportError>? Error;

        public Task<TransportStartResult> StartServerAsync(NetListenOptions options, CancellationToken token = default) =>
            _inner.StartServerAsync(options, token);

        public Task<TransportConnectResult> ConnectAsync(NetConnectOptions options, CancellationToken token = default) =>
            _inner.ConnectAsync(options, token);

        public Task<TransportStartResult> StopServerAsync(CancellationToken token = default) =>
            _inner.StopServerAsync(token);

        public async Task DisconnectAsync(TransportConnectionId connectionId, DisconnectReason reason = DisconnectReason.LocalClosed)
        {
            await _inner.DisconnectAsync(connectionId, reason);
            PeerDisconnected?.Invoke(new TransportPeerDisconnected(connectionId, reason));
        }

        public ValueTask<NetSendResult> SendAsync(TransportConnectionId connectionId, ReadOnlyMemory<byte> data, NetChannel channel, CancellationToken token = default) =>
            _inner.SendAsync(connectionId, data, channel, token);

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }
}
