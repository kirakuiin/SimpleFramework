using System;
using System.Linq;
using System.Threading;
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
                fixture.Server.Peers.RemoteParticipantIds(),
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

    [Test]
    public async Task Flow_NoTargets_ReturnsNoTargets()
    {
        var fixture = await TwoPeerFixture.StartAsync();
        await using (fixture)
        {
            var result = await fixture.Server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
                Array.Empty<PeerId>(),
                new LoadSceneProposal("Battle01"),
                FlowPolicy.AllAccepted(),
                TimeSpan.FromSeconds(1));

            Assert.That(result.Reason, Is.EqualTo(FlowEndReason.NoTargets));
        }
    }

    [Test]
    public async Task Flow_Rejection_CompletesRejected()
    {
        var fixture = await TwoPeerFixture.StartAsync();
        await using (fixture)
        {
            fixture.Client.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((_, _) => new LoadSceneAck(false, "busy"));

            var result = await fixture.Server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
                fixture.Server.Peers.RemoteParticipantIds(),
                new LoadSceneProposal("Battle01"),
                FlowPolicy.AllAccepted(),
                TimeSpan.FromSeconds(1));

            Assert.That(result.Reason, Is.EqualTo(FlowEndReason.Rejected));
            Assert.That(result.Accepted, Is.False);
        }
    }

    [Test]
    public async Task Flow_AnyAccepted_CompletesAccepted()
    {
        var fixture = await ThreePeerFixture.StartAsync();
        await using (fixture)
        {
            fixture.ClientA.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((_, _) => new LoadSceneAck(false, "busy"));
            fixture.ClientB.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((_, _) => new LoadSceneAck(true, string.Empty));

            var result = await fixture.Server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
                fixture.Server.Peers.RemoteParticipantIds(),
                new LoadSceneProposal("Battle01"),
                FlowPolicy.AnyAccepted(),
                TimeSpan.FromSeconds(1));

            Assert.That(result.Reason, Is.EqualTo(FlowEndReason.Accepted));
        }
    }

    [Test]
    public async Task Flow_EarlyAccepted_CancelsOutstandingRequests()
    {
        var fixture = await ThreePeerFixture.StartAsync();
        var releaseSlowPeer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using (fixture)
        {
            try
            {
                fixture.ClientA.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((_, _) => new LoadSceneAck(true, string.Empty));
                fixture.ClientB.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((_, _) =>
                {
                    releaseSlowPeer.Task.GetAwaiter().GetResult();
                    return new LoadSceneAck(true, string.Empty);
                });

                var result = await fixture.Server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
                    fixture.Server.Peers.RemoteParticipantIds(),
                    new LoadSceneProposal("Battle01"),
                    FlowPolicy.AnyAccepted(),
                    TimeSpan.FromSeconds(30));

                Assert.That(result.Reason, Is.EqualTo(FlowEndReason.Accepted));
                Assert.That(fixture.Server.Diagnostics.GetSnapshot().PendingRequestCount, Is.EqualTo(0));
            }
            finally
            {
                releaseSlowPeer.TrySetResult();
            }
        }
    }

    [Test]
    public async Task Flow_MajorityAndQuorum_UseAcceptedCounts()
    {
        var fixture = await ThreePeerFixture.StartAsync();
        await using (fixture)
        {
            fixture.ClientA.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((_, _) => new LoadSceneAck(true, string.Empty));
            fixture.ClientB.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((_, _) => new LoadSceneAck(false, "busy"));

            var majority = await fixture.Server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
                fixture.Server.Peers.RemoteParticipantIds(),
                new LoadSceneProposal("Battle01"),
                FlowPolicy.MajorityAccepted(),
                TimeSpan.FromSeconds(1));
            var quorum = await fixture.Server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
                fixture.Server.Peers.RemoteParticipantIds(),
                new LoadSceneProposal("Battle01"),
                FlowPolicy.Quorum(1),
                TimeSpan.FromSeconds(1));

            Assert.That(majority.Reason, Is.EqualTo(FlowEndReason.Rejected));
            Assert.That(quorum.Reason, Is.EqualTo(FlowEndReason.Accepted));
        }
    }

    [Test]
    public async Task Flow_CustomPolicy_UsesAcceptedAndTargetCounts()
    {
        var fixture = await ThreePeerFixture.StartAsync();
        await using (fixture)
        {
            fixture.ClientA.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((_, _) => new LoadSceneAck(true, string.Empty));
            fixture.ClientB.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((_, _) => new LoadSceneAck(false, "busy"));

            var result = await fixture.Server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
                fixture.Server.Peers.RemoteParticipantIds(),
                new LoadSceneProposal("Battle01"),
                FlowPolicy.Custom((accepted, total) => accepted == 1 && total == 2),
                TimeSpan.FromSeconds(1));

            Assert.That(result.Reason, Is.EqualTo(FlowEndReason.Accepted));
        }
    }

    [Test]
    public async Task Flow_ThrowingCustomPolicy_CompletesRejectedAndRecordsDiagnostic()
    {
        var fixture = await TwoPeerFixture.StartAsync();
        await using (fixture)
        {
            fixture.Client.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((_, _) => new LoadSceneAck(true, string.Empty));

            var flow = fixture.Server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
                fixture.Server.Peers.RemoteParticipantIds(),
                new LoadSceneProposal("Battle01"),
                FlowPolicy.Custom((_, _) => throw new InvalidOperationException("policy failed")),
                TimeSpan.FromSeconds(5));
            var result = await flow.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.That(result.Reason, Is.EqualTo(FlowEndReason.Rejected));
            Assert.That(fixture.Server.Diagnostics.GetSnapshot().ErrorCount, Is.GreaterThanOrEqualTo(1));
        }
    }

    [Test]
    public async Task Flow_Timeout_CompletesTimeout()
    {
        var fixture = await TwoPeerFixture.StartAsync();
        await using (fixture)
        {
            var release = new TaskCompletionSource();
            fixture.Client.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((_, _) =>
            {
                release.Task.GetAwaiter().GetResult();
                return new LoadSceneAck(true, string.Empty);
            });

            var result = await fixture.Server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
                fixture.Server.Peers.RemoteParticipantIds(),
                new LoadSceneProposal("Battle01"),
                FlowPolicy.AllAccepted(),
                TimeSpan.FromMilliseconds(50));
            release.SetResult();

            Assert.That(result.Reason, Is.EqualTo(FlowEndReason.Timeout));
        }
    }

    [Test]
    public async Task Flow_NonPositiveTimeout_ReturnsTimeoutWithoutStartingFlow()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));

        server.Messages.RegisterMessage<LoadSceneProposal>();
        server.Messages.RegisterMessage<LoadSceneAck>();
        client.Messages.RegisterMessage<LoadSceneProposal>();
        client.Messages.RegisterMessage<LoadSceneAck>();
        client.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((_, _) => new LoadSceneAck(true, string.Empty));
        await server.HostAsync(new HostOptions { Port = 7777 });
        var join = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var result = await server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
            new[] { join.PeerId },
            new LoadSceneProposal("ready"),
            FlowPolicy.AllAccepted(),
            TimeSpan.FromSeconds(-1));

        Assert.That(result.Reason, Is.EqualTo(FlowEndReason.Timeout));
        Assert.That(result.FlowId, Is.EqualTo(0));
        Assert.That(server.Flow.PendingFlowIds, Is.Empty);
    }

    [Test]
    public async Task Flow_CancelledBeforeStart_CompletesCancelled()
    {
        var fixture = await TwoPeerFixture.StartAsync();
        await using (fixture)
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            var result = await fixture.Server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
                fixture.Server.Peers.RemoteParticipantIds(),
                new LoadSceneProposal("Battle01"),
                FlowPolicy.AllAccepted(),
                TimeSpan.FromSeconds(1),
                cancellation.Token);

            Assert.That(result.Reason, Is.EqualTo(FlowEndReason.Cancelled));
        }
    }

    [Test]
    public async Task Flow_CancelledWhilePending_CompletesCancelled()
    {
        var fixture = await TwoPeerFixture.StartAsync();
        await using (fixture)
        {
            using var cancellation = new CancellationTokenSource();
            var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Client.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>(async (_, _) =>
            {
                received.TrySetResult();
                await release.Task;
                return new LoadSceneAck(true, string.Empty);
            });

            var flow = fixture.Server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
                fixture.Server.Peers.RemoteParticipantIds(),
                new LoadSceneProposal("Battle01"),
                FlowPolicy.AllAccepted(),
                TimeSpan.FromSeconds(30),
                cancellation.Token);
            await received.Task.WaitAsync(TimeSpan.FromSeconds(1));

            cancellation.Cancel();
            var result = await flow.WaitAsync(TimeSpan.FromSeconds(1));
            release.SetResult();

            Assert.That(result.Reason, Is.EqualTo(FlowEndReason.Cancelled));
        }
    }

    [Test]
    public async Task Flow_PendingQuery_ShowsPeersWaitingForResponse()
    {
        var fixture = await TwoPeerFixture.StartAsync();
        await using (fixture)
        {
            var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Client.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((_, _) =>
            {
                received.TrySetResult();
                release.Task.GetAwaiter().GetResult();
                return new LoadSceneAck(true, string.Empty);
            });

            var flow = fixture.Server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
                fixture.Server.Peers.RemoteParticipantIds(),
                new LoadSceneProposal("Battle01"),
                FlowPolicy.AllAccepted(),
                TimeSpan.FromSeconds(1));

            await received.Task.WaitAsync(TimeSpan.FromSeconds(1));
            var flowIds = fixture.Server.Flow.PendingFlowIds;

            Assert.That(flowIds, Has.Count.EqualTo(1));
            Assert.That(fixture.Server.Flow.GetPendingPeers(flowIds[0]), Is.EqualTo(new[] { fixture.Client.Peers.LocalPeerId }));

            release.SetResult();
            var result = await flow;
            Assert.That(result.Reason, Is.EqualTo(FlowEndReason.Accepted));
            Assert.That(fixture.Server.Flow.GetPendingPeers(flowIds[0]), Is.Empty);
        }
    }

    [Test]
    public async Task Flow_ManualResend_SendsSamePendingProposalAgain()
    {
        var fixture = await TwoPeerFixture.StartAsync();
        await using (fixture)
        {
            var received = 0;
            var firstReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            fixture.Client.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((_, proposal) =>
            {
                var count = Interlocked.Increment(ref received);
                if (count == 1)
                {
                    firstReceived.TrySetResult();
                    releaseFirst.Task.GetAwaiter().GetResult();
                    return new LoadSceneAck(true, proposal.SceneName);
                }

                secondReceived.TrySetResult();
                return new LoadSceneAck(true, proposal.SceneName);
            });

            var flow = fixture.Server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
                fixture.Server.Peers.RemoteParticipantIds(),
                new LoadSceneProposal("Battle01"),
                FlowPolicy.AllAccepted(),
                TimeSpan.FromSeconds(2));

            await firstReceived.Task.WaitAsync(TimeSpan.FromSeconds(1));
            var flowId = fixture.Server.Flow.PendingFlowIds[0];
            var resend = await fixture.Server.Flow.ResendPendingToAsync(flowId, fixture.Client.Peers.LocalPeerId);

            Assert.That(resend.Status, Is.EqualTo(NetSendStatus.Ok));
            await secondReceived.Task.WaitAsync(TimeSpan.FromSeconds(1));

            var result = await flow;
            releaseFirst.SetResult();

            Assert.That(result.Reason, Is.EqualTo(FlowEndReason.Accepted));
            Assert.That(result.Responses.Count, Is.EqualTo(1));
            Assert.That(Volatile.Read(ref received), Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Flow_ServerStopWhilePending_CompletesSessionClosed()
    {
        var fixture = await TwoPeerFixture.StartAsync();
        await using (fixture)
        {
            var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Client.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((_, _) =>
            {
                received.TrySetResult();
                release.Task.GetAwaiter().GetResult();
                return new LoadSceneAck(true, string.Empty);
            });

            var flow = fixture.Server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
                fixture.Server.Peers.RemoteParticipantIds(),
                new LoadSceneProposal("Battle01"),
                FlowPolicy.AllAccepted(),
                TimeSpan.FromSeconds(10));

            await received.Task.WaitAsync(TimeSpan.FromSeconds(1));
            await fixture.Server.StopAsync();

            var result = await flow.WaitAsync(TimeSpan.FromSeconds(1));
            release.SetResult();
            Assert.That(result.Reason, Is.EqualTo(FlowEndReason.SessionClosed));
        }
    }

    [Test]
    public async Task Flow_DisconnectWhilePending_RemainsPendingUntilTimeout()
    {
        var fixture = await TwoPeerFixture.StartAsync();
        await using (fixture)
        {
            var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Client.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((_, _) =>
            {
                received.TrySetResult();
                release.Task.GetAwaiter().GetResult();
                return new LoadSceneAck(true, string.Empty);
            });

            var flow = fixture.Server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
                fixture.Server.Peers.RemoteParticipantIds(),
                new LoadSceneProposal("Battle01"),
                FlowPolicy.AllAccepted(),
                TimeSpan.FromMilliseconds(150));

            await received.Task.WaitAsync(TimeSpan.FromSeconds(1));
            var flowId = fixture.Server.Flow.PendingFlowIds[0];
            var targetPeer = fixture.Client.Peers.LocalPeerId;
            await fixture.Server.KickAsync(targetPeer);

            Assert.That(fixture.Server.Flow.GetPendingPeers(flowId), Is.EqualTo(new[] { targetPeer }));

            var result = await flow.WaitAsync(TimeSpan.FromSeconds(1));
            release.SetResult();
            Assert.That(result.Reason, Is.EqualTo(FlowEndReason.Timeout));
            Assert.That(fixture.Server.Flow.GetPendingPeers(flowId), Is.Empty);
        }
    }

    [Test]
    public async Task Flow_ResendToUnavailablePendingPeer_ReturnsPeerUnavailableAndKeepsPending()
    {
        var fixture = await TwoPeerFixture.StartAsync();
        await using (fixture)
        {
            var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Client.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((_, _) =>
            {
                received.TrySetResult();
                release.Task.GetAwaiter().GetResult();
                return new LoadSceneAck(true, string.Empty);
            });

            var flow = fixture.Server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
                fixture.Server.Peers.RemoteParticipantIds(),
                new LoadSceneProposal("Battle01"),
                FlowPolicy.AllAccepted(),
                TimeSpan.FromMilliseconds(150));

            await received.Task.WaitAsync(TimeSpan.FromSeconds(1));
            var flowId = fixture.Server.Flow.PendingFlowIds[0];
            var targetPeer = fixture.Client.Peers.LocalPeerId;
            await fixture.Server.KickAsync(targetPeer);

            var resend = await fixture.Server.Flow.ResendPendingToAsync(flowId, targetPeer);

            Assert.That(resend.Status, Is.EqualTo(NetSendStatus.PeerUnavailable));
            Assert.That(fixture.Server.Flow.GetPendingPeers(flowId), Is.EqualTo(new[] { targetPeer }));

            var result = await flow.WaitAsync(TimeSpan.FromSeconds(1));
            release.SetResult();
            Assert.That(result.Reason, Is.EqualTo(FlowEndReason.Timeout));
        }
    }

    [Test]
    public async Task Flow_DisconnectOnePeer_DoesNotCancelOtherPeerPendingResponse()
    {
        var fixture = await ThreePeerFixture.StartAsync();
        await using (fixture)
        {
            var clientAReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseB = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            fixture.ClientA.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((_, _) =>
            {
                clientAReceived.TrySetResult();
                releaseA.Task.GetAwaiter().GetResult();
                return new LoadSceneAck(true, "a");
            });
            fixture.ClientB.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((_, _) =>
            {
                releaseB.Task.GetAwaiter().GetResult();
                return new LoadSceneAck(true, "b");
            });

            var flow = fixture.Server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
                fixture.Server.Peers.RemoteParticipantIds(),
                new LoadSceneProposal("Battle01"),
                FlowPolicy.AllAccepted(),
                TimeSpan.FromMilliseconds(300));

            await clientAReceived.Task.WaitAsync(TimeSpan.FromSeconds(1));
            var clientAPeer = fixture.ClientA.Peers.LocalPeerId;
            var clientBPeer = fixture.ClientB.Peers.LocalPeerId;
            await fixture.Server.KickAsync(clientAPeer);
            releaseB.SetResult();

            var result = await flow.WaitAsync(TimeSpan.FromSeconds(1));
            releaseA.SetResult();

            Assert.That(result.Reason, Is.EqualTo(FlowEndReason.Timeout));
            Assert.That(result.Responses.Select(response => response.PeerId), Is.EqualTo(new[] { clientBPeer }));
        }
    }

    [Test]
    public async Task Flow_LateResponseAfterCompletion_IsIgnored()
    {
        var fixture = await ThreePeerFixture.StartAsync();
        await using (fixture)
        {
            var releaseLate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.ClientA.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((_, _) => new LoadSceneAck(true, string.Empty));
            fixture.ClientB.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((_, _) =>
            {
                releaseLate.Task.GetAwaiter().GetResult();
                return new LoadSceneAck(false, "late");
            });

            var result = await fixture.Server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
                fixture.Server.Peers.RemoteParticipantIds(),
                new LoadSceneProposal("Battle01"),
                FlowPolicy.AnyAccepted(),
                TimeSpan.FromSeconds(1));

            Assert.That(result.Reason, Is.EqualTo(FlowEndReason.Accepted));
            Assert.That(result.Responses.Count, Is.EqualTo(1));

            var packetsBeforeLateResponse = fixture.Server.Diagnostics.GetSnapshot().PacketsReceived;
            releaseLate.SetResult();
            await WaitUntilAsync(
                () => fixture.Server.Diagnostics.GetSnapshot().PacketsReceived > packetsBeforeLateResponse);

            Assert.That(result.Reason, Is.EqualTo(FlowEndReason.Accepted));
            Assert.That(result.Responses.Count, Is.EqualTo(1));
            Assert.That(fixture.Server.Flow.PendingFlowIds, Is.Empty);
        }
    }

    [Test]
    public async Task Flow_OversizedProposal_ReturnsRejectedWithPacketTooLargeResponse()
    {
        var fixture = await TwoPeerFixture.StartAsync(appId => Options(appId, maxPacketSize: 1024));
        await using (fixture)
        {
            var result = await fixture.Server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
                fixture.Server.Peers.RemoteParticipantIds(),
                new LoadSceneProposal(new string('x', 5000)),
                FlowPolicy.AllAccepted(),
                TimeSpan.FromSeconds(1));

            Assert.That(result.Reason, Is.EqualTo(FlowEndReason.Rejected));
            Assert.That(result.Responses.Single().Message, Is.EqualTo(NetRequestStatus.PacketTooLarge.ToString()));
        }
    }

    [Test]
    public async Task Flow_SendQueueFull_ReturnsRejectedWithSendQueueFullResponse()
    {
        var fixture = await TwoPeerFixture.StartAsync(appId => Options(
            appId,
            maxPacketSize: 4096,
            maxSendQueueBytesPerPeer: 8));
        await using (fixture)
        {
            var result = await fixture.Server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
                fixture.Server.Peers.RemoteParticipantIds(),
                new LoadSceneProposal("Battle01"),
                FlowPolicy.AllAccepted(),
                TimeSpan.FromSeconds(1));

            Assert.That(result.Reason, Is.EqualTo(FlowEndReason.Rejected));
            Assert.That(result.Responses.Single().Message, Is.EqualTo(NetRequestStatus.SendQueueFull.ToString()));
            Assert.That(fixture.Server.Diagnostics.GetSnapshot().DroppedPackets, Is.EqualTo(1));
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

        public static Task<TwoPeerFixture> StartAsync()
        {
            return StartAsync(appId => Options(appId));
        }

        public static async Task<TwoPeerFixture> StartAsync(Func<Guid, GameNetOptions> createOptions)
        {
            var appId = Guid.NewGuid();
            var network = new MemoryNetNetwork();
            var serverOptions = createOptions(appId);
            var clientOptions = createOptions(appId);
            var server = new GameNet(network.CreateTransport("server"), serverOptions);
            var client = new GameNet(network.CreateTransport("client"), clientOptions);
            RegisterFlowMessages(server, client);

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
            RegisterFlowMessages(server, clientA, clientB);

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

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        while (!condition())
            await Task.Delay(10, timeout.Token);
    }

    private static GameNetOptions Options(
        Guid appId,
        int maxPacketSize = 64 * 1024,
        int maxSendQueueBytesPerPeer = 1024 * 1024) => new()
    {
        Application = new NetApplicationInfo { ApplicationId = appId },
        MaxPacketSize = maxPacketSize,
        MaxSendQueueBytesPerPeer = maxSendQueueBytesPerPeer
    };

    private static void RegisterFlowMessages(params GameNet[] peers)
    {
        foreach (var peer in peers)
        {
            peer.Messages.RegisterMessage<LoadSceneProposal>();
            peer.Messages.RegisterMessage<LoadSceneAck>();
        }
    }
}
