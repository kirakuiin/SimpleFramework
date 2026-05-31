using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleFramework.Net;
using Test.Net.TestDoubles;

namespace Test.Net;

[DiscoveryMetadata("room.list.v1")]
public sealed record RoomListMetadata(string RoomName, int CurrentPlayers, int MaxPlayers, bool HasPassword);

[DiscoveryMetadata("room.other.v1")]
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
    public async Task DiscoveryScan_FiltersDifferentMetadataSchemaBeforeDecode()
    {
        var network = new MemoryDiscoveryNetwork();
        var appId = Guid.NewGuid();
        await using var advertiser = new NetDiscovery(Options(appId), network);
        await using var browser = new NetDiscovery(Options(appId), network);

        await advertiser.StartAdvertiseAsync(
            new LanAdvertiseInfo
            {
                RoomId = "room-other",
                GamePort = 7777,
                MetadataSchemaId = DiscoveryMetadataRegistry.GetSchemaId("room.other.v1")
            },
            new OtherRoomListMetadata("Other"));

        var rooms = await browser.ScanAsync<RoomListMetadata>(TimeSpan.FromMilliseconds(100));

        Assert.That(rooms, Is.Empty);
    }

    [Test]
    public async Task StartAdvertise_WithMismatchedMetadataSchema_ReturnsInvalidState()
    {
        var network = new MemoryDiscoveryNetwork();
        await using var discovery = new NetDiscovery(Options(Guid.NewGuid()), network);

        var result = await discovery.StartAdvertiseAsync(
            new LanAdvertiseInfo
            {
                RoomId = "room-1",
                GamePort = 7777,
                MetadataSchemaId = DiscoveryMetadataRegistry.GetSchemaId("room.other.v1")
            },
            new RoomListMetadata("Room", 1, 4, false));

        Assert.That(result.Status, Is.EqualTo(NetSessionStatus.InvalidState));
        Assert.That(result.Message, Does.Contain("MetadataSchemaId"));
    }

    [Test]
    public async Task DiscoveryScan_WithMalformedMetadata_DropsPacketAndRecordsDiagnostic()
    {
        var appId = Guid.NewGuid();
        var backend = new StaticDiscoveryBackend(new DiscoveryPacket(
            DiscoveryPacket.ExpectedMagic,
            DiscoveryPacket.CurrentPacketVersion,
            appId,
            1,
            "broken-room",
            7777,
            DiscoveryMetadataRegistry.GetSchemaId("room.list.v1"),
            new byte[] { 0xff, 0x00 }));
        await using var discovery = new NetDiscovery(Options(appId), backend);

        var rooms = await discovery.ScanAsync<RoomListMetadata>(TimeSpan.Zero);

        Assert.That(rooms, Is.Empty);
        Assert.That(discovery.Diagnostics.GetSnapshot().DroppedPackets, Is.EqualTo(1));
        Assert.That(discovery.Diagnostics.GetSnapshot().ErrorCount, Is.EqualTo(1));
    }

    [Test]
    public async Task DiscoveryScan_WithoutMetadataSchemaAttribute_Throws()
    {
        var network = new MemoryDiscoveryNetwork();
        await using var discovery = new NetDiscovery(Options(Guid.NewGuid()), network);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await discovery.ScanAsync<UnregisteredDiscoveryMetadata>(TimeSpan.Zero));

        Assert.That(ex!.Message, Does.Contain(nameof(DiscoveryMetadataAttribute)));
    }

    [Test]
    public async Task DiscoveryDispose_DisposesBackend()
    {
        var backend = new DisposableDiscoveryBackend();
        var discovery = new NetDiscovery(Options(Guid.NewGuid()), backend);

        await discovery.DisposeAsync();

        Assert.That(backend.DisposeCount, Is.EqualTo(1));
    }

    [Test]
    public async Task UdpDiscoveryNetwork_ScanFindsLanAdvertisement()
    {
        var appId = Guid.NewGuid();
        var port = GetUnusedUdpPort();
        var schemaId = DiscoveryMetadataRegistry.GetSchemaId("room.list.v1");
        await using var advertiser = new NetDiscovery(
            Options(appId, discoveryPort: port, advertiseInterval: TimeSpan.FromMilliseconds(25)),
            new UdpDiscoveryNetwork());
        await using var browser = new NetDiscovery(
            Options(appId, discoveryPort: port, advertiseInterval: TimeSpan.FromMilliseconds(25)),
            new UdpDiscoveryNetwork());

        await advertiser.StartAdvertiseAsync(
            new LanAdvertiseInfo { RoomId = "room-udp", GamePort = 7777, MetadataSchemaId = schemaId },
            new RoomListMetadata("UdpRoom", 1, 4, false));

        var rooms = await browser.ScanAsync<RoomListMetadata>(TimeSpan.FromMilliseconds(500));

        Assert.That(rooms, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(rooms.Any(room => room.RoomId == "room-udp" && room.Metadata.RoomName == "UdpRoom"), Is.True);
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
            new LanAdvertiseInfo
            {
                RoomId = "room-1",
                GamePort = 7777,
                MetadataSchemaId = DiscoveryMetadataRegistry.GetSchemaId("room.list.v1")
            },
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
    public async Task StartBrowserAsync_RefreshesContinuously()
    {
        var network = new MemoryDiscoveryNetwork();
        var appId = Guid.NewGuid();
        var schemaId = DiscoveryMetadataRegistry.GetSchemaId("room.list.v1");
        await using var advertiser = new NetDiscovery(Options(appId, advertiseInterval: TimeSpan.FromMilliseconds(25)), network);
        await using var discovery = new NetDiscovery(Options(appId, advertiseInterval: TimeSpan.FromMilliseconds(25)), network);
        await using var browser = await discovery.StartBrowserAsync<RoomListMetadata>();
        var found = new TaskCompletionSource<LanScanResult<RoomListMetadata>>(TaskCreationOptions.RunContinuationsAsynchronously);
        browser.RoomFound += room => found.TrySetResult(room);

        await advertiser.StartAdvertiseAsync(
            new LanAdvertiseInfo { RoomId = "room-auto", GamePort = 7777, MetadataSchemaId = schemaId },
            new RoomListMetadata("Auto", 1, 4, false));

        var room = await found.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(room.RoomId, Is.EqualTo("room-auto"));
    }

    [Test]
    public async Task DiscoveryDispose_CancelsActiveBrowserLoop()
    {
        var clock = new ManualTimeProvider();
        var discovery = new NetDiscovery(Options(Guid.NewGuid(), timeProvider: clock), new MemoryDiscoveryNetwork());
        var browser = await discovery.StartBrowserAsync<RoomListMetadata>();

        await discovery.DisposeAsync();
        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.DoesNotThrowAsync(async () => await browser.DisposeAsync());
    }

    [Test]
    public async Task BrowserRefresh_UsesPositiveScanDuration()
    {
        var appId = Guid.NewGuid();
        var schemaId = DiscoveryMetadataRegistry.GetSchemaId("room.list.v1");
        var backend = new DurationCapturingDiscoveryBackend(new DiscoveryPacket(
            DiscoveryPacket.ExpectedMagic,
            DiscoveryPacket.CurrentPacketVersion,
            appId,
            1,
            "room-1",
            7777,
            schemaId,
            JsonSerializer.SerializeToUtf8Bytes(new RoomListMetadata("Room", 1, 4, false))));
        await using var discovery = new NetDiscovery(Options(appId), backend);
        await using var browser = await discovery.StartBrowserAsync<RoomListMetadata>();

        await browser.RefreshAsync();

        Assert.That(backend.LastScanDuration, Is.GreaterThan(TimeSpan.Zero));
        Assert.That(browser.Snapshot.Rooms, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task BrowserEvents_UseDispatcherAndContainCallbackFailures()
    {
        var network = new MemoryDiscoveryNetwork();
        var dispatcher = new InlineCountingDispatcher();
        var appId = Guid.NewGuid();
        var schemaId = DiscoveryMetadataRegistry.GetSchemaId("room.list.v1");
        await using var advertiser = new NetDiscovery(Options(appId), network);
        await using var secondAdvertiser = new NetDiscovery(Options(appId), network);
        await using var discovery = new NetDiscovery(Options(appId, dispatcher: dispatcher), network);
        await using var browser = await discovery.StartBrowserAsync<RoomListMetadata>();
        var found = 0;

        browser.RoomFound += _ => found++;
        await advertiser.StartAdvertiseAsync(
            new LanAdvertiseInfo { RoomId = "room-1", GamePort = 7777, MetadataSchemaId = schemaId },
            new RoomListMetadata("Room", 1, 4, false));

        await browser.RefreshAsync();

        Assert.That(found, Is.EqualTo(1));
        Assert.That(dispatcher.PostCount, Is.EqualTo(1));

        browser.RoomFound += _ => throw new InvalidOperationException("room callback failed");
        await secondAdvertiser.StartAdvertiseAsync(
            new LanAdvertiseInfo { RoomId = "room-2", GamePort = 7778, MetadataSchemaId = schemaId },
            new RoomListMetadata("Room2", 1, 4, false));

        await browser.RefreshAsync();

        Assert.That(discovery.Diagnostics.GetSnapshot().ErrorCount, Is.EqualTo(1));
    }

    [Test]
    public async Task BrowserEvents_DispatcherFailureBecomesDiagnosticsError()
    {
        var network = new MemoryDiscoveryNetwork();
        var appId = Guid.NewGuid();
        var schemaId = DiscoveryMetadataRegistry.GetSchemaId("room.list.v1");
        await using var advertiser = new NetDiscovery(Options(appId), network);
        await using var discovery = new NetDiscovery(Options(appId, dispatcher: new ThrowingDispatcher()), network);
        await using var browser = await discovery.StartBrowserAsync<RoomListMetadata>();

        browser.RoomFound += _ => { };
        await advertiser.StartAdvertiseAsync(
            new LanAdvertiseInfo { RoomId = "room-1", GamePort = 7777, MetadataSchemaId = schemaId },
            new RoomListMetadata("Room", 1, 4, false));

        await browser.RefreshAsync();

        Assert.That(discovery.Diagnostics.GetSnapshot().ErrorCount, Is.EqualTo(1));
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
    public async Task Stats_DisposeWhileProbePending_CompletesObjectDisposed()
    {
        var clock = new ManualTimeProvider();
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId, timeProvider: clock));
        var client = new GameNet(network.CreateTransport("client"), Options(appId, timeProvider: clock));
        server.Stats.DropProbeResponses = true;

        await server.HostAsync(new HostOptions { Port = 7777 });
        await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var probe = client.Stats.GetLatencyAsync(PeerId.Server, TimeSpan.FromSeconds(30));
        await client.DisposeAsync();

        var result = await probe.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(result.Status, Is.EqualTo(NetStatsStatus.ObjectDisposed));
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
        TimeSpan? roomTimeout = null,
        INetEventDispatcher dispatcher = null,
        int discoveryPort = 3344,
        TimeSpan? advertiseInterval = null) => new()
    {
        Application = new NetApplicationInfo
        {
            ApplicationId = appId,
            ProtocolVersion = protocolVersion
        },
        TimeProvider = timeProvider ?? TimeProvider.System,
        EventDispatcher = dispatcher,
        Discovery = new DiscoveryOptions
        {
            MaxMetadataPayloadSize = maxMetadataPayloadSize,
            RoomTimeout = roomTimeout ?? TimeSpan.FromSeconds(5),
            Port = discoveryPort,
            AdvertiseInterval = advertiseInterval ?? TimeSpan.FromSeconds(1)
        }
    };

    private static int GetUnusedUdpPort()
    {
        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)client.Client.LocalEndPoint!).Port;
    }

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

    private sealed record UnregisteredDiscoveryMetadata(string Name);

    private sealed class StaticDiscoveryBackend : IDiscoveryBackend
    {
        private readonly DiscoveryPacket _packet;

        public StaticDiscoveryBackend(DiscoveryPacket packet)
        {
            _packet = packet;
        }

        public Task<DiscoveryAdvertisementId> StartAdvertiseAsync(DiscoveryPacket packet, DiscoveryOptions options, CancellationToken token = default) =>
            Task.FromResult(new DiscoveryAdvertisementId(Guid.NewGuid()));

        public Task UpdateAdvertiseAsync(DiscoveryAdvertisementId id, DiscoveryPacket packet, DiscoveryOptions options, CancellationToken token = default) =>
            Task.CompletedTask;

        public Task StopAdvertiseAsync(DiscoveryAdvertisementId id, CancellationToken token = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<DiscoveryPacket>> ScanAsync(TimeSpan duration, DiscoveryOptions options, CancellationToken token = default) =>
            Task.FromResult<IReadOnlyList<DiscoveryPacket>>(new[] { _packet });

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class DurationCapturingDiscoveryBackend : IDiscoveryBackend
    {
        private readonly DiscoveryPacket _packet;

        public DurationCapturingDiscoveryBackend(DiscoveryPacket packet)
        {
            _packet = packet;
        }

        public TimeSpan LastScanDuration { get; private set; }

        public Task<DiscoveryAdvertisementId> StartAdvertiseAsync(DiscoveryPacket packet, DiscoveryOptions options, CancellationToken token = default) =>
            Task.FromResult(new DiscoveryAdvertisementId(Guid.NewGuid()));

        public Task UpdateAdvertiseAsync(DiscoveryAdvertisementId id, DiscoveryPacket packet, DiscoveryOptions options, CancellationToken token = default) =>
            Task.CompletedTask;

        public Task StopAdvertiseAsync(DiscoveryAdvertisementId id, CancellationToken token = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<DiscoveryPacket>> ScanAsync(TimeSpan duration, DiscoveryOptions options, CancellationToken token = default)
        {
            LastScanDuration = duration;
            var packets = duration > TimeSpan.Zero ? new[] { _packet } : Array.Empty<DiscoveryPacket>();
            return Task.FromResult<IReadOnlyList<DiscoveryPacket>>(packets);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class DisposableDiscoveryBackend : IDiscoveryBackend
    {
        public int DisposeCount { get; private set; }

        public Task<DiscoveryAdvertisementId> StartAdvertiseAsync(DiscoveryPacket packet, DiscoveryOptions options, CancellationToken token = default) =>
            Task.FromResult(new DiscoveryAdvertisementId(Guid.NewGuid()));

        public Task UpdateAdvertiseAsync(DiscoveryAdvertisementId id, DiscoveryPacket packet, DiscoveryOptions options, CancellationToken token = default) =>
            Task.CompletedTask;

        public Task StopAdvertiseAsync(DiscoveryAdvertisementId id, CancellationToken token = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<DiscoveryPacket>> ScanAsync(TimeSpan duration, DiscoveryOptions options, CancellationToken token = default) =>
            Task.FromResult<IReadOnlyList<DiscoveryPacket>>(Array.Empty<DiscoveryPacket>());

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
