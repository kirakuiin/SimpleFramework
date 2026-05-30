## Context

The current `Net` module mixes Godot-oriented transport, protocol dispatch, connection state, client info sync, authentication-adjacent behavior, and peer management. This makes it hard to test without Godot, hard to evolve toward different transports, and hard for small multiplayer games to reason about session behavior.

The redesign introduces a standalone Net core centered on `GameNet`. V1 focuses on a runnable session and messaging core. Later increments add request/response, LAN discovery and stats, then server-owned multiplayer flows. The core library must not depend on SimpleFramework `Domain`, `Model`, or `System` lifecycles. A future adapter can integrate with those abstractions without pulling them into Net core.

The implementation follows this repository's current target framework setup: projects target `net8.0`. Do not change target frameworks as part of this Net redesign; framework and SDK version updates should be handled by a separate repository-wide change. Public XML comments should stay in Chinese where surrounding Net code already uses Chinese comments.

## Goals / Non-Goals

**Goals:**

- Provide a clear `GameNet` entry point for small authoritative-server multiplayer games.
- Separate transport, session, messaging, discovery, stats, diagnostics, and flow responsibilities.
- Support replaceable transports through `INetTransport`.
- Provide `TcpNetTransport` for loopback/LAN tests and `MemoryNetTransport` for deterministic unit tests.
- Make `ApplicationId` and `ProtocolVersion` shared compatibility identity for discovery and session handshakes.
- Support host, dedicated server, join, authentication, kick, disconnect reason, peer directory, optional reconnect, typed messages, and structured errors.
- Keep lifecycle and concurrency behavior explicit and testable.
- Stage higher-level features so implementation can land incrementally without blurring contracts.

**Non-Goals:**

- Godot transport in V1.
- SimpleFramework `Domain` integration in V1.
- UDP/LiteNetLib transport in V1.
- Public lobby, NAT traversal, or matchmaking service.
- NetworkObject, NetworkVariable, state snapshots, prediction, rollback, or server reconciliation.
- Compression, encryption, fragmentation, or TLS-level transport security.
- Automatic protocol manifest generation.
- Client-initiated multiplayer flows.
- Automatic flow replay after reconnect.

## Decisions

### Use `GameNet` as the public facade

`GameNet` owns shared options, session, messenger, discovery, stats, diagnostics, and flow components. Common APIs such as host, join, send-to-server, send-to-peer, and broadcast should be reachable from `GameNet`, while advanced APIs remain available through subcomponents.

Alternative considered: expose separate root services and require callers to compose them. That would be more flexible but pushes lifecycle and dependency ordering onto application code. The facade is easier for small games and still leaves internal components testable.

### Keep Net core independent from Godot and SimpleFramework lifecycles

The core library must run in NUnit with only .NET dependencies. Godot and SimpleFramework integration should be separate adapters. This keeps the transport abstraction honest and avoids forcing Domain lifecycle semantics onto networking code.

Alternative considered: update the existing Godot-oriented API in place. That preserves some familiarity but keeps the current coupling that this redesign is meant to remove.

### Model transport with low-level connection IDs, not session peer IDs

`INetTransport` should expose only listening, connecting, disconnecting, raw packet send/receive, transport errors, and `TransportConnectionId`. It must not know `PeerId`, authentication state, room state, or business messages. `NetSession` maps transport connections to stable session peers after handshake.

Alternative considered: let transport expose peer IDs directly. That works for Godot-like peers but leaks session concepts into the lowest layer and makes reconnect and authentication harder to reason about.

### Use explicit project compatibility identity

`GameNetOptions.Application.ApplicationId` must be configured and must not be `Guid.Empty`. `ProtocolVersion` defaults to `1` and must be validated. Both values are used by discovery and session handshake so direct-IP joins cannot accidentally connect to the wrong game or incompatible protocol.

Alternative considered: make discovery own compatibility fields. That would miss direct-IP joins and split compatibility validation across components.

### Make lifecycle serialized and disposal idempotent

Host, dedicated server, join, leave, stop, and dispose operations should pass through an internal async gate/state lock. Long-running sends, handlers, and network loops must not hold the lifecycle gate. `StopAsync` and `DisposeAsync` must cancel discovery loops, pending requests, pending flows, session state, transport operations, and resources in a predictable order.

Alternative considered: rely on transport state exceptions. That would make races observable through inconsistent errors and is harder to test.

### Use optional event dispatching

Net core does not promise callbacks on the main thread. If callers need UI/game-thread dispatch, they provide `INetEventDispatcher`. Framework events are posted through it, and dispatcher or callback failures become structured errors rather than crashing transport loops.

Alternative considered: always capture a synchronization context. That is not portable across console, test, Godot, and other hosts.

### Use `TimeProvider` or a small clock abstraction for time-sensitive behavior

Timeouts, reconnect delays, discovery room expiration, and stats probes must use one replaceable time source. Tests should be able to use a fake or controllable clock.

Alternative considered: directly use `DateTime.UtcNow`, `Stopwatch`, and `Task.Delay`. That would make deterministic tests brittle.

### Make typed messaging explicit and version-tolerant

Messages use stable message keys, derived IDs, fingerprints, codecs, and handler registration. Renaming C# types should not implicitly break protocol identity when a stable key is provided. Duplicate keys, ID collisions, unsupported payloads, incompatible fingerprints, and missing handlers must produce explicit failures or diagnostics.

Alternative considered: route only by runtime type name. That is simple but fragile under refactoring and cross-version play.

### Stage features by runtime value

V1 delivers `GameNet`, transport, session, messenger, traffic limits, and diagnostics. V1.1 adds request/response. V1.2 adds LAN discovery and stats. V1.3 adds server-owned flow coordination. Specs can describe the full intended contract, while implementation tasks should be phased to keep each merge testable.

Alternative considered: implement all features as one large change. That increases risk because background loops, protocol semantics, and the test matrix would all change at once.

## Risks / Trade-offs

- [Breaking API replacement] -> Mitigate by making the new API coherent and documenting old API removal in release notes when implemented.
- [Large scope] -> Mitigate with staged tasks and tests that land V1 first before V1.1/V1.2/V1.3.
- [Async lifecycle races] -> Mitigate with a lifecycle gate, state machine tests, idempotent stop/dispose, and cancellation tests.
- [Transport abstraction too narrow] -> Mitigate by preserving `NetChannel`, connection IDs, and extension points for future unreliable transports.
- [TCP-only V1 can mislead callers about loss] -> Mitigate by making `Unreliable` unsupported by default and keeping transport loss nullable.
- [Discovery metadata misuse] -> Mitigate by making metadata explicitly public and keeping auth/password data in session auth payloads.
- [Handler exceptions destabilize loops] -> Mitigate by catching callback failures and reporting structured errors.
- [Protocol identity collisions] -> Mitigate with startup validation for duplicate keys, ID collisions, schema collisions, and fingerprint mismatch policy.

## Migration Plan

1. Create the new Net core API surface alongside or in place of the current Net files, keeping the root project boundaries unchanged.
2. Add deterministic `MemoryNetTransport` tests for lifecycle, session, peer directory, messaging, and error semantics before relying on TCP tests.
3. Add `TcpNetTransport` loopback coverage for framing, multi-packet send, large message send, bind address, and abnormal disconnects.
4. Replace or retire tests that assert old Godot-oriented API behavior.
5. Keep Godot code behind `#if GODOT`; do not make normal solution builds require Godot runtime behavior.
6. Implement staged feature sets in order: V1, V1.1, V1.2, V1.3.

Rollback is source-control based because this is a library API redesign rather than a deployable runtime migration. Each stage should compile and pass Net tests independently before later stages are added.

## Open Questions

- Whether to use .NET `TimeProvider` directly everywhere or wrap it in a small `INetClock` for simpler tests and API stability.
- Whether TCP `Unreliable` sends should always return `ChannelUnsupported` or allow an explicit degrade-to-reliable option.
- How strict the default message fingerprint policy should be (`Strict`, `Warn`, or caller-configured with no implicit silent ignore).
- Exact names and folder layout for new Net source files during implementation.
