# Net Module Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the redesigned standalone Net module around `GameNet`, replace the old Godot/SimpleFramework-lifecycle-centered API, and deliver the staged V1 through V1.3 networking capabilities described by this change.

**Architecture:** Implement a new Net core inside `Net/` with focused subfolders for core types, transports, session, messaging, discovery, stats, flow, and diagnostics. Land V1 first with `MemoryNetTransport`-driven deterministic tests, then add TCP loopback, request/response, LAN discovery/stats, and server-owned flow coordination in separate green stages.

**Tech Stack:** C# `net8.0`, NUnit, `System.Net.Sockets`, `System.Text.Json`, `TimeProvider`, `IAsyncDisposable`, existing `SimpleFramework.Net` namespace.

---

## Scope Check

This change spans several independent subsystems. Keep execution staged:

1. V1 core, transport, session, basic messaging, diagnostics.
2. V1 TCP loopback transport.
3. V1.1 request/response.
4. V1.2 discovery and stats.
5. V1.3 server-owned flow.
6. Old API cleanup and documentation.

Do not start a later stage until the previous stage builds and its focused tests pass.

## File Structure

Create or modify these files. Keep public XML comments in Chinese where comments are added to public APIs.

- Modify: `Net/Net.csproj`
  Keep `net8.0`; do not add Godot dependencies outside the existing conditional group.
- Create: `Net/Core/GameNet.cs`
  Public facade that owns options, lifecycle, session, messenger, discovery, stats, flow, diagnostics, and transport disposal.
- Create: `Net/Core/GameNetOptions.cs`
  `GameNetOptions`, `NetApplicationInfo`, limits, protocol policy, dispatcher, clock.
- Create: `Net/Core/NetResults.cs`
  Shared result records and enums for send, transport, join, request, flow, and lifecycle failures.
- Create: `Net/Core/NetIds.cs`
  `PeerId`, `TransportConnectionId`, `RoomId`-style small value types.
- Create: `Net/Core/NetChannel.cs`
  `System`, `Reliable`, `Unreliable` logical channels.
- Create: `Net/Core/INetEventDispatcher.cs`
  Event dispatch abstraction.
- Create: `Net/Diagnostics/NetDiagnostics.cs`
  Counters, immutable snapshots, structured error event model.
- Create: `Net/Transports/INetTransport.cs`
  New low-level transport abstraction. Leave old `Net/ITransport.cs` in place until cleanup task.
- Create: `Net/Transports/MemoryNetTransport.cs`
  Deterministic in-memory transport pair/network for unit tests.
- Create: `Net/Transports/TcpNetTransport.cs`
  TCP listener/client transport with `[FrameLength][packet]` framing.
- Create: `Net/Session/NetSession.cs`
  Lifecycle gate, host/server/join/stop/dispose, handshake, auth, reconnect, peer events.
- Create: `Net/Session/SessionOptions.cs`
  `HostOptions`, `JoinOptions`, `ReconnectPolicy`, auth context/result.
- Create: `Net/Session/PeerDirectory.cs`
  Stable peer storage, authoritative server directory, client read-only snapshots.
- Create: `Net/Messaging/NetMessenger.cs`
  Message registration, handler invocation, send APIs, relay, request/response.
- Create: `Net/Messaging/NetMessageRegistry.cs`
  Attributes, stable key/id/fingerprint validation, deterministic assembly scanning.
- Create: `Net/Messaging/NetPacket.cs`
  Internal envelope for system, message, request, response, relay, flow, discovery/stats packets where needed.
- Create: `Net/Messaging/INetCodec.cs`
  JSON codec and replaceable codec interface.
- Create: `Net/Discovery/NetDiscovery.cs`
  LAN advertise, scan, browser, metadata schema registration.
- Create: `Net/Stats/NetStats.cs`
  Application-layer ping/pong stats and peer snapshot.
- Create: `Net/Flow/NetFlow.cs`
  Server-owned proposal/vote/barrier flows over messenger request semantics.
- Create: `Test/Net/NetCoreTests.cs`
  Options, lifecycle, diagnostics, dispatcher, clock, limits.
- Create: `Test/Net/NetSessionTests.cs`
  Host/server/join/auth/peer directory/kick/reconnect behavior using memory transport.
- Create: `Test/Net/NetMessagingTests.cs`
  Message registry, handler, send, relay, request/response tests.
- Create: `Test/Net/NetTransportTests.cs`
  Memory and TCP transport tests.
- Create: `Test/Net/NetDiscoveryStatsTests.cs`
  Discovery metadata, scan/browser, stats tests.
- Create: `Test/Net/NetFlowTests.cs`
  Flow aggregation, pending, resend, timeout tests.
- Create: `Test/Net/TestDoubles/ManualTimeProvider.cs`
  Deterministic `TimeProvider` for timeout tests.

## Shared API Baseline

Use these names consistently across tasks.

```csharp
namespace SimpleFramework.Net;

public readonly record struct PeerId(ulong Value)
{
    public static readonly PeerId None = new(0);
    public static readonly PeerId Server = new(1);
}

public readonly record struct TransportConnectionId(ulong Value)
{
    public static readonly TransportConnectionId None = new(0);
}

public enum NetChannel
{
    System,
    Reliable,
    Unreliable
}

public sealed class NetApplicationInfo
{
    public required Guid ApplicationId { get; init; }
    public int ProtocolVersion { get; init; } = 1;
}

public sealed class GameNetOptions
{
    public string? DebugName { get; init; }
    public required NetApplicationInfo Application { get; init; }
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
    public INetEventDispatcher? EventDispatcher { get; init; }
    public int MaxPacketSize { get; init; } = 64 * 1024;
    public int MaxSendQueueBytesPerPeer { get; init; } = 1024 * 1024;
    public int MaxSendQueuePacketsPerPeer { get; init; } = 1024;
}

public interface INetEventDispatcher
{
    void Post(Action action);
}
```

## Tasks

### Task 1: Core Options, IDs, Results, Diagnostics

**Files:**
- Create: `Net/Core/NetIds.cs`
- Create: `Net/Core/NetChannel.cs`
- Create: `Net/Core/GameNetOptions.cs`
- Create: `Net/Core/NetResults.cs`
- Create: `Net/Core/INetEventDispatcher.cs`
- Create: `Net/Diagnostics/NetDiagnostics.cs`
- Create: `Test/Net/NetCoreTests.cs`

- [ ] **Step 1: Write failing tests for option validation and diagnostics**

Add this test file:

```csharp
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
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~NetCoreTests"`

Expected: FAIL with missing `GameNet`, `GameNetOptions`, or `NetDiagnostics`.

- [ ] **Step 3: Implement minimal core types**

Create the files from this task using the shared API baseline, plus:

```csharp
namespace SimpleFramework.Net;

public enum NetSendStatus
{
    Ok,
    ObjectDisposed,
    SessionClosed,
    PeerUnavailable,
    ConnectionUnavailable,
    ChannelUnsupported,
    PacketTooLarge,
    SendQueueFull,
    RateLimited,
    TransportFailed
}

public readonly record struct NetSendResult(NetSendStatus Status, string? Message = null)
{
    public bool Succeeded => Status == NetSendStatus.Ok;
    public static NetSendResult Ok() => new(NetSendStatus.Ok);
}

public enum NetTransportStatus
{
    Ok,
    ObjectDisposed,
    InvalidEndpoint,
    AlreadyRunning,
    ConnectionRefused,
    Timeout,
    Cancelled,
    TransportFailed
}

public sealed class GameNet
{
    public static void ValidateOptions(GameNetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Application);

        if (options.Application.ApplicationId == Guid.Empty)
            throw new ArgumentException("ApplicationId cannot be empty.", nameof(options));
        if (options.Application.ProtocolVersion < 1)
            throw new ArgumentException("ProtocolVersion must be >= 1.", nameof(options));
        if (options.MaxPacketSize <= 0)
            throw new ArgumentException("MaxPacketSize must be > 0.", nameof(options));
        if (options.MaxSendQueueBytesPerPeer <= 0)
            throw new ArgumentException("MaxSendQueueBytesPerPeer must be > 0.", nameof(options));
        if (options.MaxSendQueuePacketsPerPeer <= 0)
            throw new ArgumentException("MaxSendQueuePacketsPerPeer must be > 0.", nameof(options));
    }
}
```

```csharp
namespace SimpleFramework.Net;

public sealed class NetDiagnostics
{
    private long _packetsSent;
    private long _packetsReceived;
    private long _bytesSent;
    private long _bytesReceived;
    private long _droppedPackets;
    private long _errorCount;

    public void AddPacketSent(long bytes)
    {
        Interlocked.Increment(ref _packetsSent);
        Interlocked.Add(ref _bytesSent, bytes);
    }

    public void AddPacketReceived(long bytes)
    {
        Interlocked.Increment(ref _packetsReceived);
        Interlocked.Add(ref _bytesReceived, bytes);
    }

    public void AddDroppedPacket() => Interlocked.Increment(ref _droppedPackets);
    public void AddError() => Interlocked.Increment(ref _errorCount);

    public NetDiagnosticsSnapshot GetSnapshot() => new()
    {
        PacketsSent = Interlocked.Read(ref _packetsSent),
        PacketsReceived = Interlocked.Read(ref _packetsReceived),
        BytesSent = Interlocked.Read(ref _bytesSent),
        BytesReceived = Interlocked.Read(ref _bytesReceived),
        DroppedPackets = Interlocked.Read(ref _droppedPackets),
        ErrorCount = Interlocked.Read(ref _errorCount)
    };
}

public sealed class NetDiagnosticsSnapshot
{
    public int ConnectedPeerCount { get; init; }
    public int PendingRequestCount { get; init; }
    public int PendingFlowCount { get; init; }
    public long PacketsSent { get; init; }
    public long PacketsReceived { get; init; }
    public long BytesSent { get; init; }
    public long BytesReceived { get; init; }
    public long DroppedPackets { get; init; }
    public long ErrorCount { get; init; }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~NetCoreTests"`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add Net/Core Net/Diagnostics Test/Net/NetCoreTests.cs
git commit -m "feat(net): add core options and diagnostics"
```

### Task 2: Transport Abstraction and Memory Transport

**Files:**
- Create: `Net/Transports/INetTransport.cs`
- Create: `Net/Transports/MemoryNetTransport.cs`
- Create: `Test/Net/NetTransportTests.cs`

- [ ] **Step 1: Write failing memory transport tests**

Add these tests to `Test/Net/NetTransportTests.cs`:

```csharp
using NUnit.Framework;
using SimpleFramework.Net;

namespace Test.Net;

[TestFixture]
public class NetTransportTests
{
    [Test]
    public async Task MemoryTransport_ClientConnectsToServer_AndServerReceivesPacket()
    {
        var network = new MemoryNetNetwork();
        await using var server = network.CreateTransport("server");
        await using var client = network.CreateTransport("client");

        var connected = new TaskCompletionSource<TransportConnectionId>();
        var received = new TaskCompletionSource<byte[]>();

        server.PeerConnected += e => connected.TrySetResult(e.ConnectionId);
        server.PacketReceived += e => received.TrySetResult(e.Data.ToArray());

        var start = await server.StartServerAsync(new NetListenOptions { Port = 7777 });
        var connect = await client.ConnectAsync(new NetConnectOptions { Host = "server", Port = 7777 });

        Assert.That(start.Status, Is.EqualTo(NetTransportStatus.Ok));
        Assert.That(connect.Status, Is.EqualTo(NetTransportStatus.Ok));

        await client.SendAsync(connect.ConnectionId, new byte[] { 1, 2, 3 }, NetChannel.Reliable);

        Assert.That(await connected.Task.WaitAsync(TimeSpan.FromSeconds(1)), Is.Not.EqualTo(TransportConnectionId.None));
        Assert.That(await received.Task.WaitAsync(TimeSpan.FromSeconds(1)), Is.EqualTo(new byte[] { 1, 2, 3 }));
    }

    [Test]
    public async Task MemoryTransport_UnreliableChannel_ReturnsChannelUnsupportedByDefault()
    {
        var network = new MemoryNetNetwork();
        await using var server = network.CreateTransport("server");
        await using var client = network.CreateTransport("client");

        await server.StartServerAsync(new NetListenOptions { Port = 7777 });
        var connect = await client.ConnectAsync(new NetConnectOptions { Host = "server", Port = 7777 });

        var result = await client.SendAsync(connect.ConnectionId, new byte[] { 1 }, NetChannel.Unreliable);

        Assert.That(result.Status, Is.EqualTo(NetSendStatus.ChannelUnsupported));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~NetTransportTests"`

Expected: FAIL with missing transport types.

- [ ] **Step 3: Implement transport contracts**

Use this public shape:

```csharp
namespace SimpleFramework.Net;

public interface INetTransport : IAsyncDisposable
{
    Task<TransportStartResult> StartServerAsync(NetListenOptions options, CancellationToken token = default);
    Task<TransportConnectResult> ConnectAsync(NetConnectOptions options, CancellationToken token = default);
    Task DisconnectAsync(TransportConnectionId connectionId, DisconnectReason reason = DisconnectReason.LocalClosed);
    ValueTask<NetSendResult> SendAsync(TransportConnectionId connectionId, ReadOnlyMemory<byte> data, NetChannel channel, CancellationToken token = default);

    event Action<TransportPeerConnected>? PeerConnected;
    event Action<TransportPeerDisconnected>? PeerDisconnected;
    event Action<TransportPacketReceived>? PacketReceived;
    event Action<TransportError>? Error;
}

public sealed class NetListenOptions
{
    public System.Net.IPAddress? BindAddress { get; init; }
    public required int Port { get; init; }
    public int MaxConnections { get; init; } = 32;
}

public sealed class NetConnectOptions
{
    public required string Host { get; init; }
    public required int Port { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
}

public readonly record struct TransportStartResult(NetTransportStatus Status, string? Message = null);
public readonly record struct TransportConnectResult(NetTransportStatus Status, TransportConnectionId ConnectionId, string? Message = null);
public readonly record struct TransportPeerConnected(TransportConnectionId ConnectionId);
public readonly record struct TransportPeerDisconnected(TransportConnectionId ConnectionId, DisconnectReason Reason);
public readonly record struct TransportPacketReceived(TransportConnectionId ConnectionId, ReadOnlyMemory<byte> Data, NetChannel Channel);
public readonly record struct TransportError(TransportConnectionId ConnectionId, string Message, Exception? Exception = null);

public enum DisconnectReason
{
    LocalClosed,
    RemoteClosed,
    ServerClosed,
    Kicked,
    TransportFailed,
    RateLimited
}
```

- [ ] **Step 4: Implement `MemoryNetTransport`**

Use an in-memory network object that maps `(host, port)` to a server transport, allocates paired connection IDs, and dispatches events asynchronously with `Task.Run` so tests exercise event flow without sockets.

- [ ] **Step 5: Run transport tests**

Run: `dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~NetTransportTests"`

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add Net/Transports Test/Net/NetTransportTests.cs
git commit -m "feat(net): add memory transport"
```

### Task 3: GameNet Facade and Lifecycle Gate

**Files:**
- Modify: `Net/Core/GameNet.cs`
- Create: `Net/Session/NetSession.cs`
- Create: `Net/Session/SessionOptions.cs`
- Modify: `Test/Net/NetCoreTests.cs`
- Create: `Test/Net/NetSessionTests.cs`

- [ ] **Step 1: Write failing lifecycle tests**

Add:

```csharp
[Test]
public async Task GameNet_Dispose_IsIdempotent_AndApisFailAfterDispose()
{
    var network = new MemoryNetNetwork();
    await using var transport = network.CreateTransport("server");
    var net = new GameNet(transport, new GameNetOptions
    {
        Application = new NetApplicationInfo { ApplicationId = Guid.NewGuid() }
    });

    await net.DisposeAsync();
    await net.DisposeAsync();

    var result = await net.HostAsync(new HostOptions { Port = 7777 });
    Assert.That(result.Status, Is.EqualTo(NetSessionStatus.ObjectDisposed));
}

[Test]
public async Task GameNet_ConcurrentHostCalls_OnlyOneStarts()
{
    var network = new MemoryNetNetwork();
    await using var transport = network.CreateTransport("server");
    await using var net = new GameNet(transport, new GameNetOptions
    {
        Application = new NetApplicationInfo { ApplicationId = Guid.NewGuid() }
    });

    var first = net.HostAsync(new HostOptions { Port = 7777 });
    var second = net.HostAsync(new HostOptions { Port = 7778 });

    var results = await Task.WhenAll(first, second);
    Assert.That(results.Count(r => r.Status == NetSessionStatus.Ok), Is.EqualTo(1));
    Assert.That(results.Count(r => r.Status == NetSessionStatus.InvalidState), Is.EqualTo(1));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~GameNet_"`

Expected: FAIL with missing lifecycle APIs.

- [ ] **Step 3: Implement session result and lifecycle methods**

Create:

```csharp
namespace SimpleFramework.Net;

public enum NetSessionStatus
{
    Ok,
    ObjectDisposed,
    InvalidState,
    TransportFailed,
    Cancelled,
    IncompatibleApplication,
    IncompatibleProtocol,
    AuthenticationFailed,
    CapacityFull
}

public readonly record struct NetSessionResult(NetSessionStatus Status, string? Message = null)
{
    public bool Succeeded => Status == NetSessionStatus.Ok;
}

public sealed class HostOptions
{
    public System.Net.IPAddress? BindAddress { get; init; }
    public int Port { get; init; }
    public int MaxPeers { get; init; } = 8;
    public TimeSpan ReconnectGrace { get; init; } = TimeSpan.FromSeconds(30);
    public Func<AuthContext, Task<AuthResult>>? Authenticator { get; init; }
}

public sealed class AuthContext
{
    public required byte[] AuthPayload { get; init; }
}

public readonly record struct AuthResult(bool Accepted, string? Reason = null)
{
    public static AuthResult Accept() => new(true);
    public static AuthResult Reject(string reason) => new(false, reason);
}
```

Update `GameNet` to own `SemaphoreSlim _lifecycleGate`, `_disposed`, and a `NetSession` instance. Implement `HostAsync`, `StopAsync`, and `DisposeAsync`.

- [ ] **Step 4: Run focused tests**

Run: `dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~GameNet_"`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add Net/Core/GameNet.cs Net/Session Test/Net/NetCoreTests.cs Test/Net/NetSessionTests.cs
git commit -m "feat(net): add gamenet lifecycle"
```

### Task 4: Peer Directory, Handshake, Join, Auth

**Files:**
- Modify: `Net/Session/NetSession.cs`
- Modify: `Net/Session/SessionOptions.cs`
- Create: `Net/Session/PeerDirectory.cs`
- Modify: `Test/Net/NetSessionTests.cs`

- [ ] **Step 1: Write failing join and auth tests**

Add tests for:

```csharp
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
```

Include this helper in the test class:

```csharp
private static GameNetOptions Options(Guid appId, int protocolVersion = 1) => new()
{
    Application = new NetApplicationInfo
    {
        ApplicationId = appId,
        ProtocolVersion = protocolVersion
    }
};
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~NetSessionTests"`

Expected: FAIL with missing join, handshake, or peer directory behavior.

- [ ] **Step 3: Implement handshake packet and peer directory**

Use internal system packets:

```csharp
internal sealed record JoinRequest(Guid ApplicationId, int ProtocolVersion, byte[] AuthPayload);
internal sealed record JoinAccepted(PeerId PeerId, IReadOnlyList<PeerInfo> Peers, byte[] ReconnectToken);
internal sealed record JoinRejected(NetSessionStatus Status, string Message);

public sealed record PeerInfo(PeerId PeerId, bool IsServer, bool IsLocal, DateTimeOffset JoinedAt);

public sealed class PeerDirectory
{
    private readonly Dictionary<PeerId, PeerInfo> _peers = new();
    public PeerId LocalPeerId { get; private set; } = PeerId.None;
    public IReadOnlyCollection<PeerInfo> Peers => _peers.Values.ToArray();
    public void SetLocalPeer(PeerId peerId) => LocalPeerId = peerId;
    public void Upsert(PeerInfo peer) => _peers[peer.PeerId] = peer;
    public bool Remove(PeerId peerId) => _peers.Remove(peerId);
}
```

Implement join through `MemoryNetTransport` first. Use the same packet envelope that messaging will later share.

- [ ] **Step 4: Run session tests**

Run: `dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~NetSessionTests"`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add Net/Session Test/Net/NetSessionTests.cs
git commit -m "feat(net): add session join and peer directory"
```

### Task 5: Basic Messaging and JSON Codec

**Files:**
- Create: `Net/Messaging/NetPacket.cs`
- Create: `Net/Messaging/INetCodec.cs`
- Create: `Net/Messaging/NetMessageRegistry.cs`
- Create: `Net/Messaging/NetMessenger.cs`
- Modify: `Net/Core/GameNet.cs`
- Create: `Test/Net/NetMessagingTests.cs`

- [ ] **Step 1: Write failing message tests**

Add:

```csharp
[NetMessage("player.ready")]
public sealed record PlayerReady(bool Ready);

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

[NetMessage("player.ready")]
public sealed record DuplicatePlayerReady(bool Ready);
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~NetMessagingTests"`

Expected: FAIL with missing messaging types.

- [ ] **Step 3: Implement message attributes, registry, codec, send path**

Implement:

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class NetMessageAttribute(string key) : Attribute
{
    public string Key { get; } = key;
}

public interface INetCodec
{
    byte[] Serialize<T>(T message);
    object? Deserialize(ReadOnlySpan<byte> data, Type type);
}

public sealed class JsonNetCodec : INetCodec
{
    public byte[] Serialize<T>(T message) => System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(message);
    public object? Deserialize(ReadOnlySpan<byte> data, Type type) => System.Text.Json.JsonSerializer.Deserialize(data, type);
}

public sealed record NetContext(PeerId SenderId, NetChannel Channel);
```

Use a stable hash derived from message key for message ID. Validate duplicate keys and ID collisions during registration.

- [ ] **Step 4: Run messaging tests**

Run: `dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~NetMessagingTests"`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add Net/Messaging Net/Core/GameNet.cs Test/Net/NetMessagingTests.cs
git commit -m "feat(net): add typed messaging"
```

### Task 6: Server Send, Broadcast, Relay, Limits, Dispatcher Errors

**Files:**
- Modify: `Net/Messaging/NetMessenger.cs`
- Modify: `Net/Core/GameNetOptions.cs`
- Modify: `Net/Diagnostics/NetDiagnostics.cs`
- Modify: `Test/Net/NetMessagingTests.cs`
- Modify: `Test/Net/NetCoreTests.cs`

- [ ] **Step 1: Write failing tests**

Add tests for:

```csharp
[Test]
public async Task ServerBroadcast_DeliversToAllClients()
{
    var fixture = await ThreePeerFixture.StartAsync();
    var receivedA = fixture.ClientA.ReceiveOne<PlayerReady>();
    var receivedB = fixture.ClientB.ReceiveOne<PlayerReady>();

    var send = await fixture.Server.BroadcastAsync(new PlayerReady(true));

    Assert.That(send.Status, Is.EqualTo(NetSendStatus.Ok));
    Assert.That((await receivedA).Ready, Is.True);
    Assert.That((await receivedB).Ready, Is.True);
}

[Test]
public async Task OversizedMessage_ReturnsPacketTooLarge()
{
    var appId = Guid.NewGuid();
    var network = new MemoryNetNetwork();
    var options = new GameNetOptions
    {
        Application = new NetApplicationInfo { ApplicationId = appId },
        MaxPacketSize = 8
    };
    await using var server = new GameNet(network.CreateTransport("server"), options);
    await using var client = new GameNet(network.CreateTransport("client"), options);

    server.Messages.RegisterMessage<BigMessage>();
    client.Messages.RegisterMessage<BigMessage>();
    await server.HostAsync(new HostOptions { Port = 7777 });
    await client.JoinAsync(new JoinOptions { Host = "server", Port = 7777 });

    var result = await client.SendToServerAsync(new BigMessage(new string('x', 100)));

    Assert.That(result.Status, Is.EqualTo(NetSendStatus.PacketTooLarge));
}

[NetMessage("big.message")]
public sealed record BigMessage(string Text);
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~NetMessagingTests|FullyQualifiedName~NetCoreTests"`

Expected: FAIL with missing broadcast, relay, or limit behavior.

- [ ] **Step 3: Implement send APIs**

Add `SendAsync(PeerId, T)`, `BroadcastAsync<T>`, `RelayAsync<T>`, packet size checks before transport send, and dispatcher wrapping:

```csharp
private void DispatchEvent(Action action)
{
    try
    {
        if (_options.EventDispatcher is null)
        {
            action();
            return;
        }

        _options.EventDispatcher.Post(() =>
        {
            try { action(); }
            catch (Exception ex) { _diagnostics.RecordError(new NetError("EventCallbackError", ex.Message, ex)); }
        });
    }
    catch (Exception ex)
    {
        _diagnostics.RecordError(new NetError("EventDispatchError", ex.Message, ex));
    }
}
```

- [ ] **Step 4: Run focused tests**

Run: `dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~NetMessagingTests|FullyQualifiedName~NetCoreTests"`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add Net/Messaging Net/Core Net/Diagnostics Test/Net
git commit -m "feat(net): add broadcast relay and send limits"
```

### Task 7: TCP Transport

**Files:**
- Create: `Net/Transports/TcpNetTransport.cs`
- Modify: `Test/Net/NetTransportTests.cs`

- [ ] **Step 1: Write failing TCP loopback tests**

Add tests:

```csharp
[Test]
public async Task TcpTransport_SendsMultiplePacketsWithoutFrameCorruption()
{
    await using var server = new TcpNetTransport();
    await using var client = new TcpNetTransport();
    var packets = new List<byte[]>();
    var received = new TaskCompletionSource();

    server.PacketReceived += e =>
    {
        packets.Add(e.Data.ToArray());
        if (packets.Count == 3) received.TrySetResult();
    };

    var start = await server.StartServerAsync(new NetListenOptions
    {
        BindAddress = System.Net.IPAddress.Loopback,
        Port = 0
    });
    var port = server.LocalEndPoint!.Port;
    var connect = await client.ConnectAsync(new NetConnectOptions { Host = "127.0.0.1", Port = port });

    await client.SendAsync(connect.ConnectionId, new byte[] { 1 }, NetChannel.Reliable);
    await client.SendAsync(connect.ConnectionId, new byte[] { 2, 2 }, NetChannel.Reliable);
    await client.SendAsync(connect.ConnectionId, new byte[] { 3, 3, 3 }, NetChannel.Reliable);

    await received.Task.WaitAsync(TimeSpan.FromSeconds(2));

    Assert.That(packets[0], Is.EqualTo(new byte[] { 1 }));
    Assert.That(packets[1], Is.EqualTo(new byte[] { 2, 2 }));
    Assert.That(packets[2], Is.EqualTo(new byte[] { 3, 3, 3 }));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~TcpTransport"`

Expected: FAIL with missing `TcpNetTransport`.

- [ ] **Step 3: Implement TCP listener/client and framing**

Implement:

```csharp
private static async Task WriteFrameAsync(NetworkStream stream, ReadOnlyMemory<byte> data, CancellationToken token)
{
    var length = BitConverter.GetBytes(data.Length);
    await stream.WriteAsync(length, token);
    await stream.WriteAsync(data, token);
}

private static async Task<byte[]?> ReadFrameAsync(NetworkStream stream, CancellationToken token)
{
    var lengthBytes = await ReadExactlyOrNullAsync(stream, 4, token);
    if (lengthBytes is null) return null;
    var length = BitConverter.ToInt32(lengthBytes, 0);
    if (length < 0) throw new InvalidDataException("Frame length cannot be negative.");
    return await ReadExactlyOrNullAsync(stream, length, token);
}
```

Expose `LocalEndPoint` for tests after binding. Return `ChannelUnsupported` for `NetChannel.Unreliable`.

- [ ] **Step 4: Run TCP tests**

Run: `dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~TcpTransport"`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add Net/Transports/TcpNetTransport.cs Test/Net/NetTransportTests.cs
git commit -m "feat(net): add tcp transport"
```

### Task 8: Request/Response

**Files:**
- Modify: `Net/Messaging/NetMessenger.cs`
- Modify: `Net/Messaging/NetPacket.cs`
- Modify: `Test/Net/NetMessagingTests.cs`
- Create: `Test/Net/TestDoubles/ManualTimeProvider.cs`

- [ ] **Step 1: Write failing request tests**

Add:

```csharp
[NetMessage("join.room.request")]
public sealed record JoinRoomRequest(string RoomId);

[NetMessage("join.room.response")]
public sealed record JoinRoomResponse(bool Accepted, string Reason);

[Test]
public async Task RequestAsync_ReturnsTypedResponse()
{
    var fixture = await TwoPeerFixture.StartAsync();
    fixture.Server.OnRequest<JoinRoomRequest, JoinRoomResponse>((ctx, req) =>
        new JoinRoomResponse(req.RoomId == "room-1", ""));

    var response = await fixture.Client.RequestAsync<JoinRoomRequest, JoinRoomResponse>(
        PeerId.Server,
        new JoinRoomRequest("room-1"),
        TimeSpan.FromSeconds(1));

    Assert.That(response.Status, Is.EqualTo(NetRequestStatus.Ok));
    Assert.That(response.Response!.Accepted, Is.True);
}

[Test]
public async Task RequestAsync_Timeout_RemovesPendingEntry()
{
    var clock = new ManualTimeProvider();
    var fixture = await TwoPeerFixture.StartAsync(clock);

    var request = fixture.Client.RequestAsync<JoinRoomRequest, JoinRoomResponse>(
        PeerId.Server,
        new JoinRoomRequest("room-1"),
        TimeSpan.FromSeconds(5));

    clock.Advance(TimeSpan.FromSeconds(6));
    var result = await request;

    Assert.That(result.Status, Is.EqualTo(NetRequestStatus.Timeout));
    Assert.That(fixture.Client.Diagnostics.GetSnapshot().PendingRequestCount, Is.EqualTo(0));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~RequestAsync"`

Expected: FAIL with missing request APIs.

- [ ] **Step 3: Implement request registry and pending table**

Add:

```csharp
public enum NetRequestStatus
{
    Ok,
    Timeout,
    Cancelled,
    NoHandler,
    HandlerException,
    SessionClosed,
    TransportFailed
}

public sealed class NetRequestResult<TResponse>
{
    public required NetRequestStatus Status { get; init; }
    public TResponse? Response { get; init; }
    public string? Message { get; init; }
}
```

Use `long correlationId`, a `ConcurrentDictionary<long, PendingRequest>`, and `Task.Delay(timeout, options.TimeProvider, token)` for timeout.

- [ ] **Step 4: Run request tests**

Run: `dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~RequestAsync"`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add Net/Messaging Test/Net
git commit -m "feat(net): add request response messaging"
```

### Task 9: LAN Discovery and Metadata

**Files:**
- Create: `Net/Discovery/NetDiscovery.cs`
- Modify: `Net/Core/GameNetOptions.cs`
- Modify: `Net/Core/GameNet.cs`
- Create: `Test/Net/NetDiscoveryStatsTests.cs`

- [ ] **Step 1: Write failing discovery metadata tests**

Add:

```csharp
public sealed record RoomListMetadata(string RoomName, int CurrentPlayers, int MaxPlayers, bool HasPassword);

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
public void DiscoverySchema_DuplicateSchemaId_Throws()
{
    var registry = new DiscoveryMetadataRegistry();
    registry.Register<RoomListMetadata>("room.list.v1");
    var ex = Assert.Throws<InvalidOperationException>(() =>
        registry.Register<OtherRoomListMetadata>("room.list.v1"));
    Assert.That(ex!.Message, Does.Contain("room.list.v1"));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~Discovery"`

Expected: FAIL with missing discovery types.

- [ ] **Step 3: Implement discovery packet, registry, scan, browser**

Use packet fields:

```csharp
internal sealed record DiscoveryPacket(
    uint Magic,
    ushort PacketVersion,
    Guid ApplicationId,
    int ProtocolVersion,
    string RoomId,
    int GamePort,
    uint MetadataSchemaId,
    byte[] MetadataPayload);
```

Implement `StartAdvertiseAsync`, `UpdateAdvertiseMetadataAsync`, `StopAdvertiseAsync`, `ScanAsync<TMetadata>`, and `StartBrowserAsync<TMetadata>`. Use an in-memory discovery network for tests and UDP broadcast implementation behind the same internal sender/listener abstraction.

- [ ] **Step 4: Run discovery tests**

Run: `dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~Discovery"`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add Net/Discovery Net/Core Test/Net/NetDiscoveryStatsTests.cs
git commit -m "feat(net): add lan discovery"
```

### Task 10: Application-Layer Stats

**Files:**
- Create: `Net/Stats/NetStats.cs`
- Modify: `Net/Core/GameNet.cs`
- Modify: `Net/Messaging/NetMessageRegistry.cs`
- Modify: `Test/Net/NetDiscoveryStatsTests.cs`

- [ ] **Step 1: Write failing stats tests**

Add:

```csharp
[Test]
public async Task Stats_PingPong_UpdatesRtt()
{
    var fixture = await TwoPeerFixture.StartAsync();

    var stats = await fixture.Client.Stats.GetLatencyAsync(PeerId.Server);

    Assert.That(stats.Status, Is.EqualTo(NetStatsStatus.Ok));
    Assert.That(stats.PeerStats.Rtt, Is.Not.Null);
    Assert.That(stats.PeerStats.TransportLoss, Is.Null);
}

[Test]
public async Task Stats_Timeout_ContributesToProbeLoss()
{
    var clock = new ManualTimeProvider();
    var fixture = await TwoPeerFixture.StartAsync(clock);
    fixture.Server.Stats.DropProbeResponses = true;

    var probe = fixture.Client.Stats.GetLatencyAsync(PeerId.Server, TimeSpan.FromSeconds(5));
    clock.Advance(TimeSpan.FromSeconds(6));
    var result = await probe;

    Assert.That(result.Status, Is.EqualTo(NetStatsStatus.Timeout));
    Assert.That(result.PeerStats.ProbeLoss, Is.GreaterThan(0));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~Stats_"`

Expected: FAIL with missing stats types.

- [ ] **Step 3: Implement stats probe messages**

Add internal messages:

```csharp
internal sealed record NetPing(long Sequence, DateTimeOffset SentAt);
internal sealed record NetPong(long Sequence, DateTimeOffset SentAt, DateTimeOffset ReceivedAt);

public sealed class NetPeerStats
{
    public TimeSpan? Rtt { get; init; }
    public TimeSpan? AverageRtt { get; init; }
    public TimeSpan? Jitter { get; init; }
    public double ProbeLoss { get; init; }
    public double? TransportLoss { get; init; }
    public DateTimeOffset LastSeenAt { get; init; }
}
```

Calculate `ProbeLoss` from the recent probe window. Keep `TransportLoss` null for TCP and memory transport.

- [ ] **Step 4: Run stats tests**

Run: `dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~Stats_"`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add Net/Stats Net/Core Net/Messaging Test/Net/NetDiscoveryStatsTests.cs
git commit -m "feat(net): add application layer stats"
```

### Task 11: Server-Owned Flow

**Files:**
- Create: `Net/Flow/NetFlow.cs`
- Modify: `Net/Core/GameNet.cs`
- Modify: `Net/Messaging/NetMessenger.cs`
- Create: `Test/Net/NetFlowTests.cs`

- [ ] **Step 1: Write failing flow tests**

Add:

```csharp
[NetMessage("load.scene.proposal")]
public sealed record LoadSceneProposal(string SceneName);

[NetMessage("load.scene.ack")]
public sealed record LoadSceneAck(bool Accepted, string Reason);

[Test]
public async Task Flow_AllAccepted_CompletesAccepted()
{
    var fixture = await ThreePeerFixture.StartAsync();
    fixture.ClientA.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((ctx, p) => new LoadSceneAck(true, ""));
    fixture.ClientB.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((ctx, p) => new LoadSceneAck(true, ""));

    var result = await fixture.Server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
        fixture.Server.Peers.RemoteParticipants(),
        new LoadSceneProposal("Battle01"),
        FlowPolicy.AllAccepted(),
        TimeSpan.FromSeconds(1));

    Assert.That(result.Reason, Is.EqualTo(FlowEndReason.Accepted));
    Assert.That(result.Accepted, Is.True);
}

[Test]
public async Task Flow_ClientCannotInitiate()
{
    var fixture = await TwoPeerFixture.StartAsync();

    var result = await fixture.Client.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
        new[] { PeerId.Server },
        new LoadSceneProposal("Battle01"),
        FlowPolicy.AllAccepted(),
        TimeSpan.FromSeconds(1));

    Assert.That(result.Reason, Is.EqualTo(FlowEndReason.NotServer));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~NetFlowTests"`

Expected: FAIL with missing flow types.

- [ ] **Step 3: Implement flow policies and pending table**

Add:

```csharp
public enum FlowEndReason
{
    Accepted,
    Rejected,
    Timeout,
    Cancelled,
    NoTargets,
    SessionClosed,
    NotServer
}

public sealed class FlowPolicy
{
    public static FlowPolicy AllAccepted() => new("all", 0);
    public static FlowPolicy AnyAccepted() => new("any", 1);
    public static FlowPolicy MajorityAccepted() => new("majority", 0);
    public static FlowPolicy Quorum(int count) => new("quorum", count);
    private FlowPolicy(string mode, int count) { Mode = mode; Count = count; }
    public string Mode { get; }
    public int Count { get; }
}
```

Track pending flow by correlation ID and `PeerId`. Implement `GetPendingPeers(flowId)` and `ResendPendingTo(peerId)`.

- [ ] **Step 4: Run flow tests**

Run: `dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~NetFlowTests"`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add Net/Flow Net/Core Net/Messaging Test/Net/NetFlowTests.cs
git commit -m "feat(net): add server owned flow"
```

### Task 12: Cleanup Old API and Documentation

**Files:**
- Modify or delete after migration decision: `Net/ITransport.cs`
- Modify or delete after migration decision: `Net/Connection/*`
- Modify or keep as legacy utility: `Net/Utils/ProtocolHandler.cs`
- Modify or keep as discovery implementation helper: `Net/Utils/UdpBroadcast.cs`
- Modify: `Net/README.md`
- Modify: `Test/Net/UnitTestProtocolHandler.cs`
- Modify: `Test/Net/UnitTestNetwork.cs`

- [ ] **Step 1: Decide old API removal boundary by compiling references**

Run: `rg -n "ITransport|ITransfer|ConnectionModel|ProtocolHandler|BroadcastListener|PingExecutor" --glob "*.cs" --glob "!bin/**" --glob "!obj/**"`

Expected: output only in `Net/`, `GDExt/`, and `Test/Net/`. If other modules depend on old Net APIs, record those files in this task before deleting old APIs.

- [ ] **Step 2: Remove or isolate old lifecycle API**

If no production code outside Net/GDExt uses old `ITransport`, `ITransfer`, and `ConnectionModel`, delete old connection lifecycle files. If `GDExt` still requires them under `#if GODOT`, keep compatibility files under `Net/Legacy/` and mark them obsolete:

```csharp
[Obsolete("Use GameNet and INetTransport from the redesigned Net core.")]
```

- [ ] **Step 3: Update tests**

Keep `ProtocolHandler` tests only if `ProtocolHandler` remains as a supported utility. If message registry replaces it, remove `UnitTestProtocolHandler.cs` and ensure `NetMessagingTests` covers stable keys, duplicate keys, codec errors, handler order, and fingerprint policy.

- [ ] **Step 4: Update Net README**

Document minimal usage:

```csharp
var options = new GameNetOptions
{
    Application = new NetApplicationInfo
    {
        ApplicationId = Guid.Parse("2f2db4b5-4f54-47c1-b0e8-2f9e2fd7f741")
    }
};

await using var server = new GameNet(new TcpNetTransport(), options);
await using var client = new GameNet(new TcpNetTransport(), options);

await server.HostAsync(new HostOptions { Port = 7777 });
await client.JoinAsync(new JoinOptions { Host = "127.0.0.1", Port = 7777 });
await client.SendToServerAsync(new PlayerReady(true));
```

- [ ] **Step 5: Run full verification**

Run:

```powershell
dotnet restore .\SimpleFramework.sln
dotnet build .\SimpleFramework.sln
dotnet test .\SimpleFramework.sln
openspec validate net-module-redesign --type change --strict --no-interactive
```

Expected: all commands exit with code 0.

- [ ] **Step 6: Commit**

```powershell
git add Net Test openspec/changes/net-module-redesign
git commit -m "docs(net): update net redesign documentation"
```

## Self-Review Checklist

- Spec coverage:
  - `net-core`: Tasks 1, 2, 3, 6, 7, 12.
  - `net-session`: Tasks 3 and 4.
  - `net-messaging`: Tasks 5, 6, and 8.
  - `net-discovery-stats`: Tasks 9 and 10.
  - `net-flow`: Task 11.
- Placeholder scan:
  - No unresolved placeholder markers.
  - No empty implementation steps.
  - Every task has a focused test command and expected result.
- Type consistency:
  - Use `GameNet`, `GameNetOptions`, `NetApplicationInfo`, `PeerId`, `TransportConnectionId`, `NetChannel`, `INetTransport`, `NetSessionStatus`, `NetSendStatus`, `NetRequestStatus`, and `FlowEndReason` consistently.
  - Keep target framework `net8.0`.

## Execution Handoff

Plan complete and saved to `openspec/changes/net-module-redesign/implementation-plan.md`. Two execution options:

1. Subagent-Driven (recommended) - dispatch a fresh subagent per task, review between tasks, fast iteration.
2. Inline Execution - execute tasks in this session using executing-plans, batch execution with checkpoints.
