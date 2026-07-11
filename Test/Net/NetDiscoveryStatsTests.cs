using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleFramework.Net;
using Test.Net.TestDoubles;

#nullable enable

namespace Test.Net;

[DiscoveryMetadata("room.list.v1")]
public sealed record RoomListMetadata(string RoomName, int CurrentPlayers, int MaxPlayers, bool HasPassword);

[DiscoveryMetadata("room.other.v1")]
public sealed record OtherRoomListMetadata(string Name);

[DiscoveryMetadata("room.list.v1")]
public sealed record ConflictingRoomListMetadata(string Name);

public sealed record UnsafeRoomMetadata(string Password);

public sealed record NestedUnsafeRoomMetadata(RoomSecret Credentials);

public sealed record RoomSecret(string Token);

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
    public async Task DiscoveryScan_WithNonPositiveDuration_ReturnsEmptyAcrossBackends()
    {
        var network = new MemoryDiscoveryNetwork();
        var appId = Guid.NewGuid();
        var schemaId = DiscoveryMetadataRegistry.GetSchemaId("room.list.v1");
        await using var advertiser = new NetDiscovery(Options(appId), network);
        await using var browser = new NetDiscovery(Options(appId), network);
        await advertiser.StartAdvertiseAsync(
            new LanAdvertiseInfo { RoomId = "room-1", GamePort = 7777, MetadataSchemaId = schemaId },
            new RoomListMetadata("Room", 1, 4, false));

        Assert.That(await browser.ScanAsync<RoomListMetadata>(TimeSpan.Zero), Is.Empty);
        Assert.That(await browser.ScanAsync<RoomListMetadata>(TimeSpan.FromMilliseconds(-1)), Is.Empty);
    }

    [Test]
    public void MemoryDiscoveryNetwork_CancelledOperations_DoNotMutateOrReturnResults()
    {
        var network = new MemoryDiscoveryNetwork();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var packet = CreatePacket(Guid.NewGuid(), "room-1", new RoomListMetadata("Room", 1, 4, false));

        Assert.CatchAsync<OperationCanceledException>(() =>
            network.StartAdvertiseAsync(packet, new DiscoveryOptions(), cancellation.Token));
        Assert.CatchAsync<OperationCanceledException>(() =>
            network.UpdateAdvertiseAsync(new DiscoveryAdvertisementId(Guid.NewGuid()), packet, new DiscoveryOptions(), cancellation.Token));
        Assert.CatchAsync<OperationCanceledException>(() =>
            network.StopAdvertiseAsync(new DiscoveryAdvertisementId(Guid.NewGuid()), cancellation.Token));
        Assert.CatchAsync<OperationCanceledException>(() =>
            network.ScanAsync(TimeSpan.FromMilliseconds(1), new DiscoveryOptions(), cancellation.Token));

        Assert.That(network.ScanAsync(TimeSpan.Zero, new DiscoveryOptions()).Result, Is.Empty);
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

    [TestCase("", 7777)]
    [TestCase("room-1", 0)]
    [TestCase("room-1", 65536)]
    public async Task StartAdvertise_WithInvalidEndpoint_ReturnsInvalidState(string roomId, int gamePort)
    {
        await using var discovery = new NetDiscovery(Options(Guid.NewGuid()), new MemoryDiscoveryNetwork());

        var result = await discovery.StartAdvertiseAsync(
            new LanAdvertiseInfo
            {
                RoomId = roomId,
                GamePort = gamePort,
                MetadataSchemaId = DiscoveryMetadataRegistry.GetSchemaId("room.list.v1")
            },
            new RoomListMetadata("Room", 1, 4, false));

        Assert.That(result.Status, Is.EqualTo(NetSessionStatus.InvalidState));
    }

    [Test]
    public async Task StartAdvertise_WithNullMetadata_ReturnsTransportFailed()
    {
        await using var discovery = new NetDiscovery(Options(Guid.NewGuid()), new MemoryDiscoveryNetwork());

        var result = await discovery.StartAdvertiseAsync<RoomListMetadata>(
            new LanAdvertiseInfo
            {
                RoomId = "room-1",
                GamePort = 7777,
                MetadataSchemaId = DiscoveryMetadataRegistry.GetSchemaId("room.list.v1")
            },
            null!);

        Assert.That(result.Status, Is.EqualTo(NetSessionStatus.TransportFailed));
    }

    [TestCase(0u, DiscoveryPacket.CurrentPacketVersion)]
    [TestCase(DiscoveryPacket.ExpectedMagic, DiscoveryPacket.CurrentPacketVersion + 1)]
    public async Task DiscoveryScan_WithInvalidEnvelope_DropsPacketAcrossBackends(uint magic, int packetVersion)
    {
        var appId = Guid.NewGuid();
        var payload = JsonSerializer.SerializeToUtf8Bytes(new RoomListMetadata("Room", 1, 4, false));
        var backend = new StaticDiscoveryBackend(new DiscoveryPacket(
            magic,
            (ushort)packetVersion,
            appId,
            1,
            "invalid-envelope",
            7777,
            DiscoveryMetadataRegistry.GetSchemaId("room.list.v1"),
            payload.Length,
            payload));
        await using var discovery = new NetDiscovery(Options(appId), backend);

        var rooms = await discovery.ScanAsync<RoomListMetadata>(TimeSpan.FromMilliseconds(1));

        Assert.That(rooms, Is.Empty);
        Assert.That(discovery.Diagnostics.GetSnapshot().DroppedPackets, Is.EqualTo(1));
    }

    [TestCase("", 7777)]
    [TestCase("room-1", 0)]
    [TestCase("room-1", 65536)]
    public async Task DiscoveryScan_WithInvalidRoomEndpoint_DropsPacket(string roomId, int gamePort)
    {
        var appId = Guid.NewGuid();
        var payload = JsonSerializer.SerializeToUtf8Bytes(new RoomListMetadata("Room", 1, 4, false));
        var backend = new StaticDiscoveryBackend(new DiscoveryPacket(
            DiscoveryPacket.ExpectedMagic,
            DiscoveryPacket.CurrentPacketVersion,
            appId,
            1,
            roomId,
            gamePort,
            DiscoveryMetadataRegistry.GetSchemaId("room.list.v1"),
            payload.Length,
            payload));
        await using var discovery = new NetDiscovery(Options(appId), backend);

        var rooms = await discovery.ScanAsync<RoomListMetadata>(TimeSpan.FromMilliseconds(1));

        Assert.That(rooms, Is.Empty);
        Assert.That(discovery.Diagnostics.GetSnapshot().DroppedPackets, Is.EqualTo(1));
    }

    [Test]
    public async Task DiscoveryScan_WithNullMetadataPayload_DropsPacket()
    {
        var appId = Guid.NewGuid();
        var backend = new StaticDiscoveryBackend(new DiscoveryPacket(
            DiscoveryPacket.ExpectedMagic,
            DiscoveryPacket.CurrentPacketVersion,
            appId,
            1,
            "room-1",
            7777,
            DiscoveryMetadataRegistry.GetSchemaId("room.list.v1"),
            0,
            null!));
        await using var discovery = new NetDiscovery(Options(appId), backend);

        var rooms = await discovery.ScanAsync<RoomListMetadata>(TimeSpan.FromMilliseconds(1));

        Assert.That(rooms, Is.Empty);
        Assert.That(discovery.Diagnostics.GetSnapshot().DroppedPackets, Is.EqualTo(1));
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
            2,
            new byte[] { 0xff, 0x00 }));
        await using var discovery = new NetDiscovery(Options(appId), backend);

        var rooms = await discovery.ScanAsync<RoomListMetadata>(TimeSpan.FromMilliseconds(1));

        Assert.That(rooms, Is.Empty);
        Assert.That(discovery.Diagnostics.GetSnapshot().DroppedPackets, Is.EqualTo(1));
        Assert.That(discovery.Diagnostics.GetSnapshot().ErrorCount, Is.EqualTo(1));
    }

    [Test]
    public async Task DiscoveryScan_WithPayloadLengthMismatch_DropsPacketAndRecordsDiagnostic()
    {
        var appId = Guid.NewGuid();
        var payload = JsonSerializer.SerializeToUtf8Bytes(new RoomListMetadata("Room", 1, 4, false));
        var backend = new StaticDiscoveryBackend(new DiscoveryPacket(
            DiscoveryPacket.ExpectedMagic,
            DiscoveryPacket.CurrentPacketVersion,
            appId,
            1,
            "broken-room",
            7777,
            DiscoveryMetadataRegistry.GetSchemaId("room.list.v1"),
            payload.Length + 1,
            payload));
        await using var discovery = new NetDiscovery(Options(appId), backend);

        var rooms = await discovery.ScanAsync<RoomListMetadata>(TimeSpan.FromMilliseconds(1));

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
    public async Task UdpDiscovery_StopAwaitsTrackedAdvertisementLoop()
    {
        var appId = Guid.NewGuid();
        var backend = new UdpDiscoveryNetwork();
        var options = Options(
            appId,
            discoveryPort: GetUnusedUdpPort(),
            advertiseInterval: TimeSpan.FromMinutes(1)).Discovery;
        var id = await backend.StartAdvertiseAsync(
            CreatePacket(appId, "tracked-loop", new RoomListMetadata("Room", 1, 4, false)),
            options);
        var field = typeof(UdpDiscoveryNetwork).GetField(
            "_advertisements",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var advertisements = (System.Collections.IDictionary)field!.GetValue(backend)!;
        var run = advertisements[id]!;
        var loopTask = run.GetType().GetProperty("LoopTask")?.GetValue(run) as Task;

        await backend.StopAdvertiseAsync(id);

        Assert.That(loopTask, Is.Not.Null, "Advertisement lifecycle must retain its background task.");
        Assert.That(loopTask!.IsCompleted, Is.True);
        await backend.DisposeAsync();
    }

    [Test]
    public async Task UdpDiscovery_ConcurrentUpdates_LeaveOnlyOneAdvertisementLoop()
    {
        var clock = new ManualTimeProvider();
        var appId = Guid.NewGuid();
        await using var backend = new UdpDiscoveryNetwork(clock);
        var options = Options(
            appId,
            discoveryPort: GetUnusedUdpPort(),
            advertiseInterval: TimeSpan.FromMinutes(1)).Discovery;
        var packet = CreatePacket(appId, "concurrent-update", new RoomListMetadata("Room", 1, 4, false));
        var id = await backend.StartAdvertiseAsync(packet, options);

        await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => backend.UpdateAdvertiseAsync(id, packet, options)));
        await Task.Delay(50);

        Assert.That(clock.ActiveTimerCount, Is.EqualTo(1));
        await backend.StopAdvertiseAsync(id);
        Assert.That(clock.ActiveTimerCount, Is.Zero);
    }

    [Test]
    public async Task UdpDiscovery_ConcurrentDisposeCallersShareCompletion()
    {
        var backend = new UdpDiscoveryNetwork();
        var lifecycleGate = (SemaphoreSlim)typeof(UdpDiscoveryNetwork)
            .GetField("_lifecycleGate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(backend)!;
        await lifecycleGate.WaitAsync();
        try
        {
            var first = backend.DisposeAsync().AsTask();
            var second = backend.DisposeAsync().AsTask();

            Assert.That(first.IsCompleted, Is.False);
            Assert.That(second.IsCompleted, Is.False);
        }
        finally
        {
            lifecycleGate.Release();
        }

        await backend.DisposeAsync();
    }

    [Test]
    public async Task UdpDiscovery_StartCancellationTokenDoesNotOwnAdvertisementLifetime()
    {
        var clock = new ManualTimeProvider();
        await using var backend = new UdpDiscoveryNetwork(clock);
        var appId = Guid.NewGuid();
        var options = Options(
            appId,
            discoveryPort: GetUnusedUdpPort(),
            advertiseInterval: TimeSpan.FromMinutes(1)).Discovery;
        using var cancellation = new CancellationTokenSource();
        var id = await backend.StartAdvertiseAsync(
            CreatePacket(appId, "token-lifetime", new RoomListMetadata("Room", 1, 4, false)),
            options,
            cancellation.Token);

        cancellation.Cancel();
        await Task.Delay(50);

        Assert.That(clock.ActiveTimerCount, Is.EqualTo(1));
        await backend.StopAdvertiseAsync(id);
    }

    [Test]
    public async Task UdpDiscovery_DisposeCancelsActiveScan()
    {
        var backend = new UdpDiscoveryNetwork();
        var options = Options(Guid.NewGuid(), discoveryPort: GetUnusedUdpPort()).Discovery;
        var scan = backend.ScanAsync(TimeSpan.FromMinutes(1), options);

        await backend.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(scan.IsCompleted, Is.True);
        Assert.That(await scan, Is.Empty);
    }

    [Test]
    public async Task DiscoveryScan_WhenBackendDoesNotMeasureLatency_ReturnsNullLatency()
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

        Assert.That(rooms.Single().EstimatedLatency, Is.Null);
    }

    [Test]
    public async Task DiscoveryScan_WhenPacketIncludesMeasuredLatency_ExposesEstimatedLatency()
    {
        var appId = Guid.NewGuid();
        var payload = JsonSerializer.SerializeToUtf8Bytes(new RoomListMetadata("Room", 1, 4, false));
        var expectedLatency = TimeSpan.FromMilliseconds(42);
        var backend = new StaticDiscoveryBackend(new DiscoveryPacket(
            DiscoveryPacket.ExpectedMagic,
            DiscoveryPacket.CurrentPacketVersion,
            appId,
            1,
            "room-latency",
            7777,
            DiscoveryMetadataRegistry.GetSchemaId("room.list.v1"),
            payload.Length,
            payload)
        {
            EstimatedLatency = expectedLatency
        });
        await using var discovery = new NetDiscovery(Options(appId), backend);

        var rooms = await discovery.ScanAsync<RoomListMetadata>(TimeSpan.FromMilliseconds(1));

        Assert.That(rooms.Single().EstimatedLatency, Is.EqualTo(expectedLatency));
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
    public async Task NetDiscovery_StartAdvertiseWithConflictingMetadataSchema_ReturnsInvalidState()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryDiscoveryNetwork();
        var schemaId = DiscoveryMetadataRegistry.GetSchemaId("room.list.v1");
        await using var discovery = new NetDiscovery(Options(appId), network);

        var first = await discovery.StartAdvertiseAsync(
            new LanAdvertiseInfo { RoomId = "room-1", GamePort = 7777, MetadataSchemaId = schemaId },
            new RoomListMetadata("Room", 1, 4, false));
        await discovery.StopAdvertiseAsync();
        var second = await discovery.StartAdvertiseAsync(
            new LanAdvertiseInfo { RoomId = "room-2", GamePort = 7777, MetadataSchemaId = schemaId },
            new ConflictingRoomListMetadata("Other"));

        Assert.That(first.Status, Is.EqualTo(NetSessionStatus.Ok));
        Assert.That(second.Status, Is.EqualTo(NetSessionStatus.InvalidState));
        Assert.That(second.Message, Does.Contain("conflicts"));
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
    public async Task StartAdvertise_ConcurrentCalls_StartOnlyOneAdvertisement()
    {
        var backend = new BlockingStartDiscoveryBackend();
        var appId = Guid.NewGuid();
        var schemaId = DiscoveryMetadataRegistry.GetSchemaId("room.list.v1");
        await using var discovery = new NetDiscovery(Options(appId), backend);
        var info = new LanAdvertiseInfo { RoomId = "room-1", GamePort = 7777, MetadataSchemaId = schemaId };

        var first = discovery.StartAdvertiseAsync(info, new RoomListMetadata("Room", 1, 4, false));
        await backend.WaitForFirstStartAsync();
        var second = discovery.StartAdvertiseAsync(info, new RoomListMetadata("Room", 1, 4, false));
        backend.ReleaseStarts();

        var results = await Task.WhenAll(first, second);

        Assert.That(results.Count(result => result.Status == NetSessionStatus.Ok), Is.EqualTo(1));
        Assert.That(results.Count(result => result.Status == NetSessionStatus.InvalidState), Is.EqualTo(1));
        Assert.That(backend.StartCount, Is.EqualTo(1));
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
    public async Task BrowserAutomaticRefresh_RecoversAfterTransientScanFailure()
    {
        var appId = Guid.NewGuid();
        var packet = CreatePacket(appId, "room-recovered", new RoomListMetadata("Recovered", 1, 4, false));
        var backend = new FailOnceDiscoveryBackend(packet);
        await using var discovery = new NetDiscovery(
            Options(appId, advertiseInterval: TimeSpan.FromMilliseconds(20)),
            backend);
        await using var browser = await discovery.StartBrowserAsync<RoomListMetadata>();
        var found = new TaskCompletionSource<LanScanResult<RoomListMetadata>>(TaskCreationOptions.RunContinuationsAsynchronously);
        browser.RoomFound += room => found.TrySetResult(room);

        var room = await found.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(room.RoomId, Is.EqualTo("room-recovered"));
        Assert.That(backend.ScanCount, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public async Task ConcurrentBrowserDispose_AllCallersAwaitActiveRefresh()
    {
        var backend = new BlockingUncancellableScanBackend();
        await using var discovery = new NetDiscovery(Options(Guid.NewGuid()), backend);
        var browser = await discovery.StartBrowserAsync<RoomListMetadata>();
        var refresh = browser.RefreshAsync();
        await backend.ScanStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var firstDispose = browser.DisposeAsync().AsTask();
        var secondDispose = browser.DisposeAsync().AsTask();
        Assert.That(firstDispose.IsCompleted, Is.False);
        Assert.That(secondDispose.IsCompleted, Is.False);

        backend.ReleaseScan.TrySetResult();
        await Task.WhenAll(refresh, firstDispose, secondDispose).WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task ConcurrentDiscoveryDispose_AllCallersAwaitBackendCleanup()
    {
        var backend = new BlockingDisposeDiscoveryBackend();
        var discovery = new NetDiscovery(Options(Guid.NewGuid()), backend);

        var firstDispose = discovery.DisposeAsync().AsTask();
        await backend.DisposeStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var secondDispose = discovery.DisposeAsync().AsTask();
        Assert.That(firstDispose.IsCompleted, Is.False);
        Assert.That(secondDispose.IsCompleted, Is.False);

        backend.ReleaseDispose.TrySetResult();
        await Task.WhenAll(firstDispose, secondDispose).WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task BrowserRefresh_ConcurrentCallsAreSerialized()
    {
        var backend = new ConcurrentScanDiscoveryBackend();
        await using var discovery = new NetDiscovery(Options(Guid.NewGuid()), backend);
        await using var browser = await discovery.StartBrowserAsync<RoomListMetadata>();

        var first = browser.RefreshAsync();
        await backend.FirstScanStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var second = browser.RefreshAsync();
        var secondStartedEarly = false;
        try
        {
            await backend.SecondScanStarted.Task.WaitAsync(TimeSpan.FromMilliseconds(100));
            secondStartedEarly = true;
        }
        catch (TimeoutException)
        {
        }
        finally
        {
            backend.ReleaseScans.TrySetResult();
        }

        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(secondStartedEarly, Is.False);
        Assert.That(backend.MaxConcurrentScans, Is.EqualTo(1));
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
    public async Task DiscoveryConvenienceConstructor_UsesConfiguredTimeProviderForScanTimeout()
    {
        var clock = new ManualTimeProvider();
        var options = Options(Guid.NewGuid(), timeProvider: clock, discoveryPort: GetUnusedUdpPort());
        await using var discovery = new NetDiscovery(options);

        var scan = discovery.ScanAsync<RoomListMetadata>(TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromSeconds(5));

        var rooms = await scan.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(rooms, Is.Empty);
    }

    [Test]
    public void DiscoveryConvenienceConstructor_WithNullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new NetDiscovery(null!));
    }

    [Test]
    public async Task BrowserDispose_IsIdempotent()
    {
        await using var discovery = new NetDiscovery(Options(Guid.NewGuid()), new MemoryDiscoveryNetwork());
        var browser = await discovery.StartBrowserAsync<RoomListMetadata>();

        await browser.DisposeAsync();

        Assert.DoesNotThrowAsync(async () => await browser.DisposeAsync());
    }

    [Test]
    public async Task BrowserRoomFoundHandler_CanSynchronouslyRefreshAgain()
    {
        var network = new MemoryDiscoveryNetwork();
        var appId = Guid.NewGuid();
        var schemaId = DiscoveryMetadataRegistry.GetSchemaId("room.list.v1");
        await using var advertiser = new NetDiscovery(Options(appId), network);
        await using var discovery = new NetDiscovery(Options(appId), network);
        await advertiser.StartAdvertiseAsync(
            new LanAdvertiseInfo { RoomId = "room-1", GamePort = 7777, MetadataSchemaId = schemaId },
            new RoomListMetadata("Room", 1, 4, false));
        await using var browser = await discovery.StartBrowserAsync<RoomListMetadata>();
        var reentrantRefreshCompleted = false;
        browser.RoomFound += _ =>
        {
            reentrantRefreshCompleted = browser.RefreshAsync().Wait(TimeSpan.FromMilliseconds(200));
        };

        await browser.RefreshAsync().WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(reentrantRefreshCompleted, Is.True);
    }

    [Test]
    public async Task DiscoveryDispose_CancelsActiveOneShotScan()
    {
        var backend = new BlockingScanDiscoveryBackend();
        var discovery = new NetDiscovery(Options(Guid.NewGuid()), backend);
        var scan = discovery.ScanAsync<RoomListMetadata>(TimeSpan.FromMinutes(1));
        await backend.WaitForScanAsync();

        await discovery.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));

        Assert.CatchAsync<OperationCanceledException>(async () => await scan);
    }

    [Test]
    public async Task BrowserRefresh_UsesPositiveScanDuration()
    {
        var appId = Guid.NewGuid();
        var schemaId = DiscoveryMetadataRegistry.GetSchemaId("room.list.v1");
        var payload = JsonSerializer.SerializeToUtf8Bytes(new RoomListMetadata("Room", 1, 4, false));
        var backend = new DurationCapturingDiscoveryBackend(new DiscoveryPacket(
            DiscoveryPacket.ExpectedMagic,
            DiscoveryPacket.CurrentPacketVersion,
            appId,
            1,
            "room-1",
            7777,
            schemaId,
            payload.Length,
            payload));
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
    public async Task StartAdvertise_WithNestedPrivateMetadataField_ReturnsTransportFailed()
    {
        var network = new MemoryDiscoveryNetwork();
        await using var discovery = new NetDiscovery(Options(Guid.NewGuid()), network);

        var result = await discovery.StartAdvertiseAsync(
            new LanAdvertiseInfo { RoomId = "room-1", GamePort = 7777, MetadataSchemaId = 123 },
            new NestedUnsafeRoomMetadata(new RoomSecret("secret-token")));

        Assert.That(result.Status, Is.EqualTo(NetSessionStatus.TransportFailed));
        Assert.That(result.Message, Does.Contain("Token"));
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
    public async Task Stats_PingHandler_AwaitsPongSendCompletion()
    {
        var sendStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSend = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var diagnostics = new NetDiagnostics();
        var messenger = new NetMessenger(
            (_, _) => new ValueTask<NetSendResult>(NetSendResult.Ok()),
            async (_, _, _) =>
            {
                sendStarted.TrySetResult();
                await releaseSend.Task;
                return NetSendResult.Ok();
            },
            () => Array.Empty<PeerId>(),
            diagnostics,
            64 * 1024,
            1024 * 1024,
            1024,
            int.MaxValue);
        _ = new NetStats(messenger, TimeProvider.System);
        var descriptor = messenger.Registry.Get<NetPing>();
        var payload = new JsonNetCodec().Encode(new NetPing(1, DateTimeOffset.UtcNow));
        var packet = JsonSerializer.SerializeToUtf8Bytes(new NetPacket
        {
            Kind = NetPacket.Message,
            MessageId = descriptor.MessageId,
            Payload = payload
        });

        var handling = messenger.TryHandlePacket(packet, new PeerId(2));
        await sendStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(handling.IsCompleted, Is.False);

        releaseSend.TrySetResult();
        Assert.That(await handling.WaitAsync(TimeSpan.FromSeconds(1)), Is.True);
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
        Assert.That(result.PeerStats.TimeoutCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Stats_NonPositiveTimeout_ReturnsTimeoutWithoutThrowing()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));
        server.Stats.DropProbeResponses = true;

        await server.HostAsync(new HostOptions { Port = 7777 });
        await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var negative = await client.Stats.GetLatencyAsync(PeerId.Server, TimeSpan.FromSeconds(-2));
        var zero = await client.Stats.GetLatencyAsync(PeerId.Server, TimeSpan.Zero);

        Assert.That(negative.Status, Is.EqualTo(NetStatsStatus.Timeout));
        Assert.That(zero.Status, Is.EqualTo(NetStatsStatus.Timeout));
    }

    [Test]
    public async Task Stats_CancellationInterruptsBlockedSend()
    {
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        var blockingTransport = new CancellableStatsSendTransport(network.CreateTransport("server"));
        await using var server = new GameNet(blockingTransport, Options(appId));
        await using var client = new GameNet(network.CreateTransport("client"), Options(appId));
        await server.HostAsync(new HostOptions { Port = 7777 });
        var join = await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        blockingTransport.BlockSends = true;
        using var cancellation = new CancellationTokenSource();

        var probe = server.Stats.GetLatencyAsync(join.PeerId, TimeSpan.FromSeconds(30), cancellation.Token);
        await blockingTransport.SendStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();
        var result = await probe.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(result.Status, Is.EqualTo(NetStatsStatus.TransportFailed));
        var pending = typeof(NetStats)
            .GetField("_pending", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(server.Stats)!;
        Assert.That((int)pending.GetType().GetProperty("Count")!.GetValue(pending)!, Is.Zero);
    }

    [Test]
    public async Task Stats_IgnoresPongFromWrongPeer()
    {
        var clock = new ManualTimeProvider();
        var appId = Guid.NewGuid();
        var network = new MemoryNetNetwork();
        await using var server = new GameNet(network.CreateTransport("server"), Options(appId, timeProvider: clock));
        await using var clientA = new GameNet(network.CreateTransport("client-a"), Options(appId, timeProvider: clock));
        await using var clientB = new GameNet(network.CreateTransport("client-b"), Options(appId, timeProvider: clock));
        clientA.Stats.DropProbeResponses = true;

        await server.HostAsync(new HostOptions { Port = 7777 });
        var joinA = await clientA.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });
        await clientB.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

        var probe = server.Stats.GetLatencyAsync(joinA.PeerId, TimeSpan.FromSeconds(5));
        var wrongPeerPong = await clientB.SendToServerAsync(new NetPong(1, clock.GetUtcNow(), clock.GetUtcNow()));
        var earlyCompletion = await Task.WhenAny(probe, Task.Delay(100));

        Assert.That(wrongPeerPong.Status, Is.EqualTo(NetSendStatus.Ok));
        Assert.That(earlyCompletion, Is.Not.EqualTo(probe));

        clock.Advance(TimeSpan.FromSeconds(6));
        var result = await probe.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(result.Status, Is.EqualTo(NetStatsStatus.Timeout));
        Assert.That(result.PeerStats.Rtt, Is.Null);
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

    private static DiscoveryPacket CreatePacket(Guid appId, string roomId, RoomListMetadata metadata)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(metadata);
        return new DiscoveryPacket(
            DiscoveryPacket.ExpectedMagic,
            DiscoveryPacket.CurrentPacketVersion,
            appId,
            1,
            roomId,
            7777,
            DiscoveryMetadataRegistry.GetSchemaId("room.list.v1"),
            payload.Length,
            payload);
    }

    private static GameNetOptions Options(
        Guid appId,
        int protocolVersion = 1,
        int maxMetadataPayloadSize = 8 * 1024,
        TimeProvider? timeProvider = null,
        TimeSpan? roomTimeout = null,
        INetEventDispatcher? dispatcher = null,
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

    private sealed class FailOnceDiscoveryBackend : IDiscoveryBackend
    {
        private readonly DiscoveryPacket _packet;
        private int _scanCount;

        public FailOnceDiscoveryBackend(DiscoveryPacket packet) => _packet = packet;
        public int ScanCount => Volatile.Read(ref _scanCount);
        public Task<DiscoveryAdvertisementId> StartAdvertiseAsync(DiscoveryPacket packet, DiscoveryOptions options, CancellationToken token = default) =>
            Task.FromResult(new DiscoveryAdvertisementId(Guid.NewGuid()));
        public Task UpdateAdvertiseAsync(DiscoveryAdvertisementId id, DiscoveryPacket packet, DiscoveryOptions options, CancellationToken token = default) => Task.CompletedTask;
        public Task StopAdvertiseAsync(DiscoveryAdvertisementId id, CancellationToken token = default) => Task.CompletedTask;
        public Task<IReadOnlyList<DiscoveryPacket>> ScanAsync(TimeSpan duration, DiscoveryOptions options, CancellationToken token = default)
        {
            if (Interlocked.Increment(ref _scanCount) == 1)
                throw new IOException("transient scan failure");
            return Task.FromResult<IReadOnlyList<DiscoveryPacket>>(new[] { _packet });
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class BlockingUncancellableScanBackend : IDiscoveryBackend
    {
        public TaskCompletionSource ScanStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseScan { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<DiscoveryAdvertisementId> StartAdvertiseAsync(DiscoveryPacket packet, DiscoveryOptions options, CancellationToken token = default) =>
            Task.FromResult(new DiscoveryAdvertisementId(Guid.NewGuid()));
        public Task UpdateAdvertiseAsync(DiscoveryAdvertisementId id, DiscoveryPacket packet, DiscoveryOptions options, CancellationToken token = default) => Task.CompletedTask;
        public Task StopAdvertiseAsync(DiscoveryAdvertisementId id, CancellationToken token = default) => Task.CompletedTask;
        public async Task<IReadOnlyList<DiscoveryPacket>> ScanAsync(TimeSpan duration, DiscoveryOptions options, CancellationToken token = default)
        {
            ScanStarted.TrySetResult();
            await ReleaseScan.Task;
            return Array.Empty<DiscoveryPacket>();
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class BlockingDisposeDiscoveryBackend : IDiscoveryBackend
    {
        public TaskCompletionSource DisposeStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseDispose { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<DiscoveryAdvertisementId> StartAdvertiseAsync(DiscoveryPacket packet, DiscoveryOptions options, CancellationToken token = default) =>
            Task.FromResult(new DiscoveryAdvertisementId(Guid.NewGuid()));
        public Task UpdateAdvertiseAsync(DiscoveryAdvertisementId id, DiscoveryPacket packet, DiscoveryOptions options, CancellationToken token = default) => Task.CompletedTask;
        public Task StopAdvertiseAsync(DiscoveryAdvertisementId id, CancellationToken token = default) => Task.CompletedTask;
        public Task<IReadOnlyList<DiscoveryPacket>> ScanAsync(TimeSpan duration, DiscoveryOptions options, CancellationToken token = default) =>
            Task.FromResult<IReadOnlyList<DiscoveryPacket>>(Array.Empty<DiscoveryPacket>());
        public async ValueTask DisposeAsync()
        {
            DisposeStarted.TrySetResult();
            await ReleaseDispose.Task;
        }
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

    private sealed class CancellableStatsSendTransport : INetTransport
    {
        private readonly INetTransport _inner;

        public CancellableStatsSendTransport(INetTransport inner)
        {
            _inner = inner;
            _inner.PeerConnected += message => PeerConnected?.Invoke(message);
            _inner.PeerDisconnected += message => PeerDisconnected?.Invoke(message);
            _inner.PacketReceived += message => PacketReceived?.Invoke(message);
            _inner.Error += message => Error?.Invoke(message);
        }

        public bool BlockSends { get; set; }
        public TaskCompletionSource SendStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public event Action<TransportPeerConnected>? PeerConnected;
        public event Action<TransportPeerDisconnected>? PeerDisconnected;
        public event Action<TransportPacketReceived>? PacketReceived;
        public event Action<TransportError>? Error;
        public Task<TransportStartResult> StartServerAsync(NetListenOptions options, CancellationToken token = default) => _inner.StartServerAsync(options, token);
        public Task<TransportConnectResult> ConnectAsync(NetConnectOptions options, CancellationToken token = default) => _inner.ConnectAsync(options, token);
        public Task<TransportStartResult> StopServerAsync(CancellationToken token = default) => _inner.StopServerAsync(token);
        public Task DisconnectAsync(TransportConnectionId connectionId, DisconnectReason reason = DisconnectReason.LocalClosed) => _inner.DisconnectAsync(connectionId, reason);

        public async ValueTask<NetSendResult> SendAsync(TransportConnectionId connectionId, ReadOnlyMemory<byte> data, NetChannel channel, CancellationToken token = default)
        {
            if (!BlockSends)
                return await _inner.SendAsync(connectionId, data, channel, token);

            SendStarted.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return NetSendResult.Ok();
            }
            catch (OperationCanceledException)
            {
                return new NetSendResult(NetSendStatus.TransportFailed, "Send was cancelled.");
            }
        }

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }

    private sealed class BlockingStartDiscoveryBackend : IDiscoveryBackend
    {
        private readonly TaskCompletionSource _firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseStarts = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _started;

        public int StartCount => Volatile.Read(ref _started);

        public async Task<DiscoveryAdvertisementId> StartAdvertiseAsync(DiscoveryPacket packet, DiscoveryOptions options, CancellationToken token = default)
        {
            if (Interlocked.Increment(ref _started) == 1)
                _firstStarted.TrySetResult();

            await _releaseStarts.Task.WaitAsync(token).ConfigureAwait(false);
            return new DiscoveryAdvertisementId(Guid.NewGuid());
        }

        public Task WaitForFirstStartAsync() =>
            _firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        public void ReleaseStarts() =>
            _releaseStarts.TrySetResult();

        public Task UpdateAdvertiseAsync(DiscoveryAdvertisementId id, DiscoveryPacket packet, DiscoveryOptions options, CancellationToken token = default) =>
            Task.CompletedTask;

        public Task StopAdvertiseAsync(DiscoveryAdvertisementId id, CancellationToken token = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<DiscoveryPacket>> ScanAsync(TimeSpan duration, DiscoveryOptions options, CancellationToken token = default) =>
            Task.FromResult<IReadOnlyList<DiscoveryPacket>>(Array.Empty<DiscoveryPacket>());

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class BlockingScanDiscoveryBackend : IDiscoveryBackend
    {
        private readonly TaskCompletionSource _scanStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<DiscoveryAdvertisementId> StartAdvertiseAsync(DiscoveryPacket packet, DiscoveryOptions options, CancellationToken token = default) =>
            Task.FromResult(new DiscoveryAdvertisementId(Guid.NewGuid()));

        public Task UpdateAdvertiseAsync(DiscoveryAdvertisementId id, DiscoveryPacket packet, DiscoveryOptions options, CancellationToken token = default) =>
            Task.CompletedTask;

        public Task StopAdvertiseAsync(DiscoveryAdvertisementId id, CancellationToken token = default) =>
            Task.CompletedTask;

        public async Task<IReadOnlyList<DiscoveryPacket>> ScanAsync(TimeSpan duration, DiscoveryOptions options, CancellationToken token = default)
        {
            _scanStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Array.Empty<DiscoveryPacket>();
        }

        public Task WaitForScanAsync() =>
            _scanStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ConcurrentScanDiscoveryBackend : IDiscoveryBackend
    {
        private int _activeScans;
        private int _scanCount;
        private int _maxConcurrentScans;

        public TaskCompletionSource FirstScanStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondScanStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseScans { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int MaxConcurrentScans => Volatile.Read(ref _maxConcurrentScans);

        public Task<DiscoveryAdvertisementId> StartAdvertiseAsync(DiscoveryPacket packet, DiscoveryOptions options, CancellationToken token = default) =>
            Task.FromResult(new DiscoveryAdvertisementId(Guid.NewGuid()));

        public Task UpdateAdvertiseAsync(DiscoveryAdvertisementId id, DiscoveryPacket packet, DiscoveryOptions options, CancellationToken token = default) =>
            Task.CompletedTask;

        public Task StopAdvertiseAsync(DiscoveryAdvertisementId id, CancellationToken token = default) =>
            Task.CompletedTask;

        public async Task<IReadOnlyList<DiscoveryPacket>> ScanAsync(TimeSpan duration, DiscoveryOptions options, CancellationToken token = default)
        {
            var active = Interlocked.Increment(ref _activeScans);
            UpdateMaximum(active);
            if (Interlocked.Increment(ref _scanCount) == 1)
                FirstScanStarted.TrySetResult();
            else
                SecondScanStarted.TrySetResult();

            try
            {
                await ReleaseScans.Task.WaitAsync(token);
                return Array.Empty<DiscoveryPacket>();
            }
            finally
            {
                Interlocked.Decrement(ref _activeScans);
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private void UpdateMaximum(int active)
        {
            while (true)
            {
                var current = Volatile.Read(ref _maxConcurrentScans);
                if (active <= current || Interlocked.CompareExchange(ref _maxConcurrentScans, active, current) == current)
                    return;
            }
        }
    }
}
