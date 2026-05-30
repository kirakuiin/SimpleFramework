using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleFramework.Net;
using Test.Net.TestDoubles;

namespace Test.Net;

public sealed record RoomListMetadata(string RoomName, int CurrentPlayers, int MaxPlayers, bool HasPassword);

public sealed record OtherRoomListMetadata(string Name);

public sealed record UnsafeRoomMetadata(string Password);

[TestFixture]
public class NetDiscoveryStatsTests
{
    [Test]
    public async Task DiscoveryScan_FiltersDifferentApplicationId()
    {
        var network = new MemoryDiscoveryNetwork();
        var appA = Guid.NewGuid();
        var appB = Guid.NewGuid();
        await using var advertiser = new NetDiscovery(Options(appA), network);
        await using var browser = new NetDiscovery(Options(appB), network);

        await advertiser.StartAdvertiseAsync(
            new LanAdvertiseInfo { RoomId = "room-1", GamePort = 7777, MetadataSchemaId = 123 },
            new RoomListMetadata("Room", 1, 4, false));

        var rooms = await browser.ScanAsync<RoomListMetadata>(TimeSpan.FromMilliseconds(100));

        Assert.That(rooms, Is.Empty);
    }

    [Test]
    public async Task DiscoveryScan_ReturnsCompatibleRoomMetadata()
    {
        var network = new MemoryDiscoveryNetwork();
        var appId = Guid.NewGuid();
        var schemaId = DiscoveryMetadataRegistry.GetSchemaId("room.list.v1");
        await using var advertiser = new NetDiscovery(Options(appId), network);
        await using var browser = new NetDiscovery(Options(appId), network);

        await advertiser.StartAdvertiseAsync(
            new LanAdvertiseInfo { RoomId = "room-1", GamePort = 7777, MetadataSchemaId = schemaId },
            new RoomListMetadata("Room", 1, 4, true));

        var rooms = await browser.ScanAsync<RoomListMetadata>(TimeSpan.FromMilliseconds(100));

        Assert.That(rooms.Count, Is.EqualTo(1));
        Assert.That(rooms[0].RoomId, Is.EqualTo("room-1"));
        Assert.That(rooms[0].GamePort, Is.EqualTo(7777));
        Assert.That(rooms[0].IsJoinable, Is.True);
        Assert.That(rooms[0].Metadata.RoomName, Is.EqualTo("Room"));
        Assert.That(rooms[0].Metadata.HasPassword, Is.True);
    }

    [Test]
    public async Task DiscoveryScan_ExposesEstimatedLatencyWhenAvailable()
    {
        var network = new MemoryDiscoveryNetwork();
        var appId = Guid.NewGuid();
        var schemaId = DiscoveryMetadataRegistry.GetSchemaId("room.list.v1");
        await using var advertiser = new NetDiscovery(Options(appId), network);
        await using var browser = new NetDiscovery(Options(appId), network);

        await advertiser.StartAdvertiseAsync(
            new LanAdvertiseInfo { RoomId = "room-1", GamePort = 7777, MetadataSchemaId = schemaId },
            new RoomListMetadata("Room", 1, 4, false));

        var rooms = await browser.ScanAsync<RoomListMetadata>(TimeSpan.FromMilliseconds(100));

        Assert.That(rooms.Single().EstimatedLatency, Is.Not.Null);
    }

    [Test]
    public async Task DiscoveryScan_MarksIncompatibleProtocolAsNotJoinable()
    {
        var network = new MemoryDiscoveryNetwork();
        var appId = Guid.NewGuid();
        var schemaId = DiscoveryMetadataRegistry.GetSchemaId("room.list.v1");
        await using var advertiser = new NetDiscovery(Options(appId, protocolVersion: 2), network);
        await using var browser = new NetDiscovery(Options(appId, protocolVersion: 1), network);

        await advertiser.StartAdvertiseAsync(
            new LanAdvertiseInfo { RoomId = "room-1", GamePort = 7777, MetadataSchemaId = schemaId },
            new RoomListMetadata("Room", 1, 4, false));

        var rooms = await browser.ScanAsync<RoomListMetadata>(TimeSpan.FromMilliseconds(100));

        Assert.That(rooms.Single().IsJoinable, Is.False);
    }

    [Test]
    public void DiscoverySchema_DuplicateSchemaId_Throws()
    {
        var registry = new DiscoveryMetadataRegistry();
        registry.Register<RoomListMetadata>("room.list.v1");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            registry.Register<OtherRoomListMetadata>("room.list.v1"));

        Assert.That(ex!.Message, Does.Contain("room.list.v1"));
    }

    [Test]
    public async Task AdvertiseUpdateBeforeStart_ReturnsInvalidState()
    {
        var network = new MemoryDiscoveryNetwork();
        await using var discovery = new NetDiscovery(Options(Guid.NewGuid()), network);

        var result = await discovery.UpdateAdvertiseMetadataAsync(new RoomListMetadata("Room", 1, 4, false));

        Assert.That(result.Status, Is.EqualTo(NetSessionStatus.InvalidState));
    }

    [Test]
    public async Task StopAdvertise_RemovesRoomFromScans()
    {
        var network = new MemoryDiscoveryNetwork();
        var appId = Guid.NewGuid();
        var schemaId = DiscoveryMetadataRegistry.GetSchemaId("room.list.v1");
        await using var advertiser = new NetDiscovery(Options(appId), network);
        await using var browser = new NetDiscovery(Options(appId), network);

        await advertiser.StartAdvertiseAsync(
            new LanAdvertiseInfo { RoomId = "room-1", GamePort = 7777, MetadataSchemaId = schemaId },
            new RoomListMetadata("Room", 1, 4, false));
        await advertiser.StopAdvertiseAsync();

        var rooms = await browser.ScanAsync<RoomListMetadata>(TimeSpan.FromMilliseconds(100));

        Assert.That(rooms, Is.Empty);
    }

    [Test]
    public async Task AdvertiseUpdate_ChangesRoomMetadata()
    {
        var network = new MemoryDiscoveryNetwork();
        var appId = Guid.NewGuid();
        var schemaId = DiscoveryMetadataRegistry.GetSchemaId("room.list.v1");
        await using var advertiser = new NetDiscovery(Options(appId), network);
        await using var browser = new NetDiscovery(Options(appId), network);

        await advertiser.StartAdvertiseAsync(
            new LanAdvertiseInfo { RoomId = "room-1", GamePort = 7777, MetadataSchemaId = schemaId },
            new RoomListMetadata("Room", 1, 4, false));
        await advertiser.UpdateAdvertiseMetadataAsync(new RoomListMetadata("Room", 2, 4, true));

        var rooms = await browser.ScanAsync<RoomListMetadata>(TimeSpan.FromMilliseconds(100));

        Assert.That(rooms.Single().Metadata.CurrentPlayers, Is.EqualTo(2));
        Assert.That(rooms.Single().Metadata.HasPassword, Is.True);
    }

    [Test]
    public async Task StartAdvertise_WithOversizedMetadata_ReturnsTransportFailedAndRecordsDrop()
    {
        var network = new MemoryDiscoveryNetwork();
        await using var discovery = new NetDiscovery(Options(Guid.NewGuid(), maxMetadataPayloadSize: 16), network);

        var result = await discovery.StartAdvertiseAsync(
            new LanAdvertiseInfo { RoomId = "room-1", GamePort = 7777, MetadataSchemaId = 123 },
            new RoomListMetadata(new string('x', 256), 1, 4, false));

        Assert.That(result.Status, Is.EqualTo(NetSessionStatus.TransportFailed));
        Assert.That(discovery.Diagnostics.GetSnapshot().DroppedPackets, Is.EqualTo(1));
    }

    [Test]
    public async Task BrowserRefresh_RaisesFoundUpdatedAndLostEvents()
    {
        var network = new MemoryDiscoveryNetwork();
        var clock = new ManualTimeProvider();
        var appId = Guid.NewGuid();
        var schemaId = DiscoveryMetadataRegistry.GetSchemaId("room.list.v1");
        await using var advertiser = new NetDiscovery(Options(appId, timeProvider: clock, roomTimeout: TimeSpan.FromSeconds(5)), network);
        await using var discovery = new NetDiscovery(Options(appId, timeProvider: clock, roomTimeout: TimeSpan.FromSeconds(5)), network);
        await using var browser = await discovery.StartBrowserAsync<RoomListMetadata>();
        var found = 0;
        var updated = 0;
        var lost = 0;
        browser.RoomFound += _ => found++;
        browser.RoomUpdated += _ => updated++;
        browser.RoomLost += _ => lost++;

        await advertiser.StartAdvertiseAsync(
            new LanAdvertiseInfo { RoomId = "room-1", GamePort = 7777, MetadataSchemaId = schemaId },
            new RoomListMetadata("Room", 1, 4, false));
        await browser.RefreshAsync();

        await advertiser.UpdateAdvertiseMetadataAsync(new RoomListMetadata("Room", 2, 4, false));
        await browser.RefreshAsync();

        await advertiser.StopAdvertiseAsync();
        clock.Advance(TimeSpan.FromSeconds(6));
        await browser.RefreshAsync();

        Assert.That(found, Is.EqualTo(1));
        Assert.That(updated, Is.EqualTo(1));
        Assert.That(lost, Is.EqualTo(1));
        Assert.That(browser.Snapshot.Rooms, Is.Empty);
    }

    [Test]
    public async Task StartAdvertise_WithPrivateMetadataField_ReturnsTransportFailed()
    {
        var network = new MemoryDiscoveryNetwork();
        await using var discovery = new NetDiscovery(Options(Guid.NewGuid()), network);

        var result = await discovery.StartAdvertiseAsync(
            new LanAdvertiseInfo { RoomId = "room-1", GamePort = 7777, MetadataSchemaId = 123 },
            new UnsafeRoomMetadata("secret"));

        Assert.That(result.Status, Is.EqualTo(NetSessionStatus.TransportFailed));
        Assert.That(result.Message, Does.Contain("Password"));
    }

    [Test]
    public async Task ManualIpJoin_WorksWithoutDiscoveryRoom()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));

        await server.HostAsync(new HostOptions { Port = 7777 });

        var join = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        Assert.That(join.Status, Is.EqualTo(NetSessionStatus.Ok));
    }

    [Test]
    public async Task Stats_PingPong_UpdatesRtt()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));

        await server.HostAsync(new HostOptions { Port = 7777 });
        await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var stats = await client.Stats.GetLatencyAsync(PeerId.Server);

        Assert.That(stats.Status, Is.EqualTo(NetStatsStatus.Ok));
        Assert.That(stats.PeerStats.Rtt, Is.Not.Null);
        Assert.That(stats.PeerStats.TransportLoss, Is.Null);
        Assert.That(stats.PeerStats.LastSeenAt, Is.Not.EqualTo(default(DateTimeOffset)));
    }

    [Test]
    public async Task Stats_Timeout_ContributesToProbeLoss()
    {
        var clock = new ManualTimeProvider();
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId, timeProvider: clock));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId, timeProvider: clock));
        server.Stats.DropProbeResponses = true;

        await server.HostAsync(new HostOptions { Port = 7777 });
        await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var probe = client.Stats.GetLatencyAsync(PeerId.Server, TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromSeconds(6));
        var result = await probe.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(result.Status, Is.EqualTo(NetStatsStatus.Timeout));
        Assert.That(result.PeerStats.ProbeLoss, Is.GreaterThan(0));
    }

    [Test]
    public async Task Stats_RepeatedPing_UpdatesAverageRttAndJitter()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));

        await server.HostAsync(new HostOptions { Port = 7777 });
        await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        await client.Stats.GetLatencyAsync(PeerId.Server);
        var second = await client.Stats.GetLatencyAsync(PeerId.Server);

        Assert.That(second.Status, Is.EqualTo(NetStatsStatus.Ok));
        Assert.That(second.PeerStats.AverageRtt, Is.Not.Null);
        Assert.That(second.PeerStats.Jitter, Is.Not.Null);
    }

    [Test]
    public async Task Stats_ToMissingPeer_ReturnsPeerUnavailable()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));

        await server.HostAsync(new HostOptions { Port = 7777 });

        var result = await server.Stats.GetLatencyAsync(new PeerId(999), TimeSpan.FromSeconds(1));

        Assert.That(result.Status, Is.EqualTo(NetStatsStatus.PeerUnavailable));
    }

    [Test]
    public async Task Stats_TcpTransportLoss_IsNull()
    {
        var appId = Guid.NewGuid();
        await using var serverTransport = new TcpNetTransport();
        await using var clientTransport = new TcpNetTransport();
        await using var server = new GameNet(serverTransport, Options(appId));
        await using var client = new GameNet(clientTransport, Options(appId));

        await server.HostAsync(new HostOptions { BindAddress = IPAddress.Loopback, Port = 0 });
        await client.JoinAsync(new JoinOptions { Host = "127.0.0.1", Port = serverTransport.LocalEndPoint!.Port });

        var result = await client.Stats.GetLatencyAsync(PeerId.Server, TimeSpan.FromSeconds(2));

        Assert.That(result.Status, Is.EqualTo(NetStatsStatus.Ok));
        Assert.That(result.PeerStats.TransportLoss, Is.Null);
    }

    private static GameNetOptions Options(
        Guid appId,
        int protocolVersion = 1,
        int maxMetadataPayloadSize = 8 * 1024,
        TimeProvider timeProvider = null,
        TimeSpan? roomTimeout = null) => new()
    {
        Application = new NetApplicationInfo
        {
            ApplicationId = appId,
            ProtocolVersion = protocolVersion
        },
        TimeProvider = timeProvider ?? TimeProvider.System,
        Discovery = new DiscoveryOptions
        {
            MaxMetadataPayloadSize = maxMetadataPayloadSize,
            RoomTimeout = roomTimeout ?? TimeSpan.FromSeconds(5)
        }
    };
}
