## 1. Core Foundation

- [x] 1.1 Add Net core identifiers, channels, shared result enums/records, `GameNetOptions`, `NetApplicationInfo`, and `INetEventDispatcher` under `Net/Core`.
- [x] 1.2 Add `NetDiagnostics` and read-only diagnostics snapshots with counters for peers, pending operations, packets, bytes, drops, and errors.
- [x] 1.3 Add NUnit coverage for option validation, invalid `ApplicationId`, invalid `ProtocolVersion`, diagnostics snapshots, and target framework staying `net8.0`.
- [x] 1.4 Add a minimal `GameNet` facade that validates options and exposes diagnostics, session, messaging, discovery, stats, and flow entry points as they become available.

## 2. Transport Layer

- [x] 2.1 Add the new low-level `INetTransport` abstraction with listen, connect, disconnect, send, transport connection IDs, raw packet events, transport errors, and `NetChannel` handling.
- [x] 2.2 Implement deterministic `MemoryNetNetwork` and `MemoryNetTransport` for unit tests, including server registration, paired connection IDs, async event dispatch, disconnect events, and default `Unreliable` channel rejection.
- [x] 2.3 Add memory transport tests for connect, packet delivery, disconnect notification, unsupported unreliable channel, object disposal, and transport result codes.
- [x] 2.4 Implement `TcpNetTransport` using `System.Net.Sockets`, bind address support, ephemeral port support for tests, and `[FrameLength][NetPacket bytes]` framing.
- [x] 2.5 Add TCP loopback tests for connect, multiple packet framing, larger packet delivery, bind address, abnormal disconnect detection, and default `Unreliable` channel rejection.

## 3. GameNet Lifecycle

- [x] 3.1 Add lifecycle state and an async gate so `HostAsync`, `StartServerAsync`, `JoinAsync`, `LeaveAsync`, `StopAsync`, and `DisposeAsync` are serialized.
- [x] 3.2 Make `StopAsync` and `DisposeAsync` idempotent and ensure dispose cancels discovery loops, pending requests, pending flows, session work, transport work, and owned resources.
- [x] 3.3 Make send, connection, discovery, stats, and flow APIs return `ObjectDisposed` results or throw `ObjectDisposedException` after disposal.
- [x] 3.4 Add lifecycle tests for concurrent starts, repeated stop/dispose, API use after dispose, and shutdown cancellation behavior.

## 4. Session And Peer Directory

- [x] 4.1 Add `HostOptions`, `JoinOptions`, `ReconnectPolicy`, `AuthContext`, `AuthResult`, `JoinResult`, `NetSessionStatus`, and disconnect reason models.
- [x] 4.2 Implement host and dedicated server roles, including local host participant creation for host mode and no local player participant for dedicated server mode.
- [x] 4.3 Implement direct join handshake with `ApplicationId`, `ProtocolVersion`, auth payload, max peer validation, structured rejection results, and assigned `PeerId`.
- [x] 4.4 Add `PeerDirectory` with `PeerId.None`, `PeerId.Server`, authoritative server storage, client read-only snapshots, and session-generic `PeerInfo`.
- [x] 4.5 Implement peer joined, peer left, peer disconnected, peer reconnected, state changed, and server closed events with dispatcher-safe delivery.
- [x] 4.6 Implement server kick and client/host/server disconnect semantics with explicit disconnect reasons.
- [x] 4.7 Implement opt-in reconnect using unpredictable reconnect tokens, reconnect grace window, original `PeerId` restoration, expiry cleanup, and temporary-disconnect versus final-removal distinction.
- [x] 4.8 Add session tests for host, dedicated server, successful join, incompatible application, incompatible protocol, auth success/failure, password auth payload, max peers, kick, shutdown, peer directory updates, and reconnect behavior.

## 5. Messaging Core

- [x] 5.1 Add message attributes, handler attributes, `NetContext`, `INetCodec`, default `System.Text.Json` codec, internal packet envelope, and `NetMessageRegistry`.
- [x] 5.2 Implement stable message keys, derived message IDs, duplicate key detection, ID collision detection, deterministic assembly scanning, and fingerprint policy handling.
- [x] 5.3 Implement normal message handlers with `NetContext`, multiple handlers in registration order, handler exception containment, and structured message errors.
- [x] 5.4 Implement `SendToServerAsync`, server `SendAsync(peer)`, and server `BroadcastAsync` for registered typed messages.
- [x] 5.5 Implement server-validated `RelayAsync` with original sender preservation, allowlist/permission/rate-limit checks, missing target errors, and permission errors.
- [x] 5.6 Enforce `MaxPacketSize`, send queue byte/packet limits, `SendQueueFull`, `PacketTooLarge`, `RateLimited`, and structured transport send failures across send, broadcast, relay, request, and flow paths.
- [x] 5.7 Add messaging tests for registration, duplicate keys, type rename stability through explicit keys, deterministic handler scanning, handler order, handler exceptions, codec errors, unknown messages, send-to-server, server send, broadcast, relay, relay denial, packet limits, and queue limits.

## 6. Request Response

- [x] 6.1 Add `OnRequest<TRequest,TResponse>` and `RequestAsync<TRequest,TResponse>` APIs with one handler per request type.
- [x] 6.2 Implement request correlation IDs, pending request table, timeout through the configured `TimeProvider`, cancellation cleanup, no-handler errors, handler exception errors, and session-closed errors.
- [x] 6.3 Ignore late and duplicate responses without recreating pending state or mutating completed requests.
- [x] 6.4 Add deterministic `ManualTimeProvider` test helper for timeout, reconnect, discovery, stats, and flow tests.
- [x] 6.5 Add request tests for success, no handler, handler exception, timeout, cancellation cleanup, late response, duplicate response, disconnect/session close failure, and pending diagnostics count.

## 7. Discovery And Metadata

- [x] 7.1 Add `DiscoveryOptions`, discovery packet envelope, `LanAdvertiseInfo`, metadata schema registry, schema key/hash helpers, scan result models, and browser snapshot models.
- [x] 7.2 Implement continuous advertise start, metadata update, stop, invalid update-before-start result, oversized packet discard, and diagnostics for dropped discovery packets.
- [x] 7.3 Implement one-shot `ScanAsync<TMetadata>` and continuous `StartBrowserAsync<TMetadata>` with room found, updated, lost events and room timeout through the configured time source.
- [x] 7.4 Filter or mark rooms by `ApplicationId` and `ProtocolVersion`, keep incompatible rooms non-joinable, and keep discovery metadata separate from authentication.
- [x] 7.5 Ensure metadata is public data only, supports `HasPassword` indicators, rejects schema collisions, and never carries real passwords, tokens, or private room keys.
- [x] 7.6 Add discovery tests for application filtering, protocol incompatibility, metadata serialization, schema collision, oversized packet discard, advertise lifecycle, scan result content, browser found/updated/lost events, password metadata rules, and manual IP join fallback.

## 8. Application-Layer Stats

- [x] 8.1 Add `NetStats`, internal ping/pong messages, `NetPeerStats`, stats result models, and peer stats snapshots.
- [x] 8.2 Implement application-layer RTT, average RTT, jitter, timeout count, last-seen time, and `ProbeLoss` using the configured time source.
- [x] 8.3 Keep `TransportLoss` null for TCP and memory transports unless a future transport explicitly supplies transport loss.
- [x] 8.4 Surface discovery scan request/reply timing as room-list latency when available without requiring ICMP.
- [x] 8.5 Add stats tests for ping/pong RTT, timeout, probe loss, last seen, TCP transport loss null, timeout peer failure, fake time behavior, and discovery latency estimation.

## 9. Server-Owned Flow

- [x] 9.1 Add `NetFlow`, proposal/vote/barrier packet models, proposal handler registration, `FlowPolicy`, `FlowResult<TResponse>`, per-peer responses, and `FlowEndReason`.
- [x] 9.2 Implement server-only flow initiation and reject client-initiated multiplayer flow attempts.
- [x] 9.3 Implement all accepted, any accepted, majority accepted, quorum, custom policy, rejected, timeout, cancelled, no-targets, and session-closed outcomes.
- [x] 9.4 Track pending flows by stable `PeerId`, expose pending peer queries, and implement manual `ResendPendingTo(peer)`.
- [x] 9.5 Keep flow pending when a peer disconnects until policy, timeout, or manual server action completes it, and ignore late responses after completion.
- [x] 9.6 Ensure V1.3 does not add client-side pending recovery, flow persistence, server restart recovery, nested flows, client-initiated flows, or automatic replay after reconnect.
- [x] 9.7 Add flow tests for all accepted, any accepted, majority, quorum, no targets, rejection, timeout, disconnect while pending, pending query, manual resend, late response ignored, and client initiation rejection.

## 10. Event Dispatching And Error Containment

- [x] 10.1 Route session, messaging, discovery, stats, flow, and diagnostics events through `INetEventDispatcher` when configured.
- [x] 10.2 Keep default event delivery on the current network task context when no dispatcher is configured.
- [x] 10.3 Convert dispatcher `Post` failures and callback exceptions into structured diagnostics errors without stopping transport loops or later events.
- [x] 10.4 Preserve event ordering for events produced by the same background task, including peer disconnect and peer removal ordering.
- [x] 10.5 Add dispatcher tests for default context delivery, dispatcher delivery, dispatcher failure, callback exception containment, diagnostics error creation, and same-peer event ordering.

## 11. Integration Cleanup

- [x] 11.1 Search all production and test references to old `ITransport`, `ITransfer`, `ConnectionModel`, `ProtocolHandler`, `PingExecutor`, and UDP broadcast helpers before deleting or isolating legacy APIs.
- [x] 11.2 Remove old lifecycle APIs or move required compatibility pieces under a clearly marked legacy area with `[Obsolete]` attributes when external guarded code still needs them.
- [x] 11.3 Decide whether `ProtocolHandler`, `PingExecutor`, and `UdpBroadcast` remain supported utilities or are replaced by the new messenger, stats, and discovery code; update tests accordingly.
- [x] 11.4 Keep Godot-specific code guarded with `#if GODOT` and ensure normal .NET builds do not require Godot runtime behavior.
- [x] 11.5 Update `Net/README.md` with minimal `GameNet`, `TcpNetTransport`, host, join, send, discovery, stats, and flow examples.

## 12. Verification

- [x] 12.1 Run `dotnet restore .\SimpleFramework.sln` and resolve restore issues without changing target framework from `net8.0`.
- [x] 12.2 Run `dotnet build .\SimpleFramework.sln` and fix compile errors across Net, GDExt guarded code, and tests.
- [x] 12.3 Run `dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~Net"` and fix focused Net test failures.
- [x] 12.4 Run `dotnet test .\SimpleFramework.sln` and fix full solution test failures.
- [x] 12.5 Run `openspec validate net-module-redesign --type change --strict --no-interactive` and fix artifact validation issues.
- [x] 12.6 Review implementation against `proposal.md`, `design.md`, all five specs, and `implementation-plan.md`; close any coverage gaps before archiving.
