using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NUnit.Framework;
using SimpleFramework.Net;

namespace Test.Net;

[TestFixture]
public class NetCoreTests
{
    [Test]
    public void GameNetOptions_WithEmptyApplicationId_IsRejected()
    {
        var options = new GameNetOptions
        {
            Application = new NetApplicationInfo { ApplicationId = Guid.Empty }
        };

        var ex = Assert.Throws<ArgumentException>(() => GameNet.ValidateOptions(options));
        Assert.That(ex!.Message, Does.Contain("ApplicationId"));
    }

    [Test]
    public void GameNetOptions_WithInvalidProtocolVersion_IsRejected()
    {
        var options = new GameNetOptions
        {
            Application = new NetApplicationInfo
            {
                ApplicationId = Guid.NewGuid(),
                ProtocolVersion = 0
            }
        };

        var ex = Assert.Throws<ArgumentException>(() => GameNet.ValidateOptions(options));
        Assert.That(ex!.Message, Does.Contain("ProtocolVersion"));
    }

    [Test]
    public void GameNetOptions_WithInvalidLimits_AreRejected()
    {
        var app = new NetApplicationInfo { ApplicationId = Guid.NewGuid() };

        Assert.Multiple(() =>
        {
            Assert.That(
                Assert.Throws<ArgumentException>(() => GameNet.ValidateOptions(new GameNetOptions
                {
                    Application = app,
                    MaxPacketSize = 0
                }))!.Message,
                Does.Contain("MaxPacketSize"));
            Assert.That(
                Assert.Throws<ArgumentException>(() => GameNet.ValidateOptions(new GameNetOptions
                {
                    Application = app,
                    MaxSendQueueBytesPerPeer = 0
                }))!.Message,
                Does.Contain("MaxSendQueueBytesPerPeer"));
            Assert.That(
                Assert.Throws<ArgumentException>(() => GameNet.ValidateOptions(new GameNetOptions
                {
                    Application = app,
                    MaxSendQueuePacketsPerPeer = 0
                }))!.Message,
                Does.Contain("MaxSendQueuePacketsPerPeer"));
            Assert.That(
                Assert.Throws<ArgumentException>(() => GameNet.ValidateOptions(new GameNetOptions
                {
                    Application = app,
                    MaxSendsPerSecondPerPeer = 0
                }))!.Message,
                Does.Contain("MaxSendsPerSecondPerPeer"));
        });
    }

    [Test]
    public void DiagnosticsSnapshot_IsReadOnlyCopy()
    {
        var diagnostics = new NetDiagnostics();
        diagnostics.AddPacketSent(10);
        diagnostics.AddPacketReceived(5);
        diagnostics.AddError();

        var snapshot = diagnostics.GetSnapshot();

        Assert.That(snapshot.PacketsSent, Is.EqualTo(1));
        Assert.That(snapshot.BytesSent, Is.EqualTo(10));
        Assert.That(snapshot.PacketsReceived, Is.EqualTo(1));
        Assert.That(snapshot.BytesReceived, Is.EqualTo(5));
        Assert.That(snapshot.ErrorCount, Is.EqualTo(1));
    }

    [Test]
    public void Diagnostics_RecordError_RaisesErrorEventAndUpdatesSnapshot()
    {
        var diagnostics = new NetDiagnostics();
        NetError recorded = null;
        diagnostics.ErrorRecorded += error => recorded = error;

        diagnostics.RecordError(new NetError("TestError", "failed"));

        Assert.That(recorded, Is.Not.Null);
        Assert.That(recorded!.Code, Is.EqualTo("TestError"));
        Assert.That(diagnostics.GetSnapshot().ErrorCount, Is.EqualTo(1));
    }

    [Test]
    public void NetProject_TargetFramework_RemainsNet8()
    {
        var projectPath = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "Net", "Net.csproj"));
        var document = XDocument.Load(projectPath);

        Assert.That(document.Root!.Element("PropertyGroup")!.Element("TargetFramework")!.Value, Is.EqualTo("net8.0"));
    }

    [Test]
    public async Task GameNet_Dispose_IsIdempotent_AndApisFailAfterDispose()
    {
        var network = new MemoryNetNetwork();
        var net = new GameNet(network.CreateTransport("server"), new GameNetOptions
        {
            Application = new NetApplicationInfo { ApplicationId = Guid.NewGuid() }
        });

        await net.DisposeAsync();
        await net.DisposeAsync();

        var result = await net.HostAsync(new HostOptions { Port = 7777 });
        Assert.That(result.Status, Is.EqualTo(NetSessionStatus.ObjectDisposed));
    }

    [Test]
    public async Task GameNet_ConnectionApisAfterDispose_ReturnObjectDisposed()
    {
        var network = new MemoryNetNetwork();
        var net = new GameNet(network.CreateTransport("server"), new GameNetOptions
        {
            Application = new NetApplicationInfo { ApplicationId = Guid.NewGuid() }
        });

        await net.DisposeAsync();

        var host = await net.HostAsync(new HostOptions { Port = 7777 });
        var dedicated = await net.StartServerAsync(new HostOptions { Port = 7778 });
        var join = await net.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        var leave = await net.LeaveAsync();
        var stop = await net.StopAsync();

        Assert.That(host.Status, Is.EqualTo(NetSessionStatus.ObjectDisposed));
        Assert.That(dedicated.Status, Is.EqualTo(NetSessionStatus.ObjectDisposed));
        Assert.That(join.Status, Is.EqualTo(NetSessionStatus.ObjectDisposed));
        Assert.That(leave.Status, Is.EqualTo(NetSessionStatus.ObjectDisposed));
        Assert.That(stop.Status, Is.EqualTo(NetSessionStatus.ObjectDisposed));
    }

    [Test]
    public async Task GameNet_MessageApisAfterDispose_ReturnObjectDisposed()
    {
        var network = new MemoryNetNetwork();
        var net = new GameNet(network.CreateTransport("server"), new GameNetOptions
        {
            Application = new NetApplicationInfo { ApplicationId = Guid.NewGuid() }
        });
        net.Messages.RegisterMessage<PlayerReady>();

        await net.DisposeAsync();

        var sendToServer = await net.SendToServerAsync(new PlayerReady(true));
        var sendToPeer = await net.SendAsync(PeerId.Server, new PlayerReady(true));
        var broadcast = await net.BroadcastAsync(new PlayerReady(true));

        Assert.That(sendToServer.Status, Is.EqualTo(NetSendStatus.ObjectDisposed));
        Assert.That(sendToPeer.Status, Is.EqualTo(NetSendStatus.ObjectDisposed));
        Assert.That(broadcast.Status, Is.EqualTo(NetSendStatus.ObjectDisposed));
    }

    [Test]
    public async Task GameNet_StatsAndFlowApisAfterDispose_ReturnObjectDisposedOrThrow()
    {
        var network = new MemoryNetNetwork();
        var net = new GameNet(network.CreateTransport("server"), new GameNetOptions
        {
            Application = new NetApplicationInfo { ApplicationId = Guid.NewGuid() }
        });

        await net.DisposeAsync();

        var stats = await net.Stats.GetLatencyAsync(PeerId.Server, TimeSpan.FromSeconds(1));
        var resend = await net.Flow.ResendPendingToAsync(123, PeerId.Server);

        Assert.That(stats.Status, Is.EqualTo(NetStatsStatus.ObjectDisposed));
        Assert.That(resend.Status, Is.EqualTo(NetSendStatus.ObjectDisposed));
        Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await net.Flow.ProposeAsync<PlayerReady, PlayerReady>(
                new[] { PeerId.Server },
                new PlayerReady(true),
                FlowPolicy.AllAccepted(),
                TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task NetDiscovery_ApisAfterDispose_ReturnObjectDisposedOrThrow()
    {
        var discovery = new NetDiscovery(new GameNetOptions
        {
            Application = new NetApplicationInfo { ApplicationId = Guid.NewGuid() }
        }, new MemoryDiscoveryNetwork());

        await discovery.DisposeAsync();

        var start = await discovery.StartAdvertiseAsync(
            new LanAdvertiseInfo { RoomId = "room-1", GamePort = 7777, MetadataSchemaId = 1 },
            new RoomListMetadata("Room", 1, 4, false));
        var update = await discovery.UpdateAdvertiseMetadataAsync(new RoomListMetadata("Room", 1, 4, false));
        var stop = await discovery.StopAdvertiseAsync();

        Assert.That(start.Status, Is.EqualTo(NetSessionStatus.ObjectDisposed));
        Assert.That(update.Status, Is.EqualTo(NetSessionStatus.ObjectDisposed));
        Assert.That(stop.Status, Is.EqualTo(NetSessionStatus.ObjectDisposed));
        Assert.ThrowsAsync<ObjectDisposedException>(async () => await discovery.ScanAsync<RoomListMetadata>(TimeSpan.Zero));
        Assert.ThrowsAsync<ObjectDisposedException>(async () => await discovery.StartBrowserAsync<RoomListMetadata>());
    }

    [Test]
    public async Task GameNet_ConcurrentHostCalls_OnlyOneStarts()
    {
        var network = new MemoryNetNetwork();
        await using var net = new GameNet(network.CreateTransport("server"), new GameNetOptions
        {
            Application = new NetApplicationInfo { ApplicationId = Guid.NewGuid() }
        });

        var first = net.HostAsync(new HostOptions { Port = 7777 });
        var second = net.HostAsync(new HostOptions { Port = 7778 });

        var results = await Task.WhenAll(first, second);
        Assert.That(results.Count(r => r.Status == NetSessionStatus.Ok), Is.EqualTo(1));
        Assert.That(results.Count(r => r.Status == NetSessionStatus.InvalidState), Is.EqualTo(1));
    }

    [Test]
    public async Task GameNet_Stop_IsIdempotent_AndAllowsRestart()
    {
        var network = new MemoryNetNetwork();
        await using var net = new GameNet(network.CreateTransport("server"), new GameNetOptions
        {
            Application = new NetApplicationInfo { ApplicationId = Guid.NewGuid() }
        });

        var start = await net.HostAsync(new HostOptions { Port = 7777 });
        var firstStop = await net.StopAsync();
        var secondStop = await net.StopAsync();
        var restart = await net.HostAsync(new HostOptions { Port = 7777 });

        Assert.That(start.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(firstStop.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(secondStop.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(restart.Status, Is.EqualTo(NetSessionStatus.Ok));
    }

    [Test]
    public async Task GameNet_Stop_WithCancelledToken_ReturnsCancelled_AndKeepsSessionActive()
    {
        var network = new MemoryNetNetwork();
        await using var net = new GameNet(network.CreateTransport("server"), new GameNetOptions
        {
            Application = new NetApplicationInfo { ApplicationId = Guid.NewGuid() }
        });
        using var cancellation = new CancellationTokenSource();

        await net.HostAsync(new HostOptions { Port = 7777 });
        cancellation.Cancel();

        var stop = await net.StopAsync(cancellation.Token);

        Assert.That(stop.Status, Is.EqualTo(NetSessionStatus.Cancelled));
        Assert.That(net.Session.Role, Is.EqualTo(NetSessionRole.Host));
    }
}
