## Why

The current `Net` module can run Godot-oriented connection and protocol flows, but connection lifecycle, protocol handling, authentication, reconnect behavior, peer lists, and sync concerns are too tightly coupled. This change redesigns the module as a standalone, testable networking session library for small multiplayer games, with replaceable transports and staged delivery.

## What Changes

- **BREAKING**: Replace the current Net public API with a new `GameNet` entry point and explicit session, messaging, discovery, stats, diagnostics, and flow components.
- Introduce a transport abstraction that is not tied to Godot, plus built-in TCP and deterministic in-memory transports.
- Introduce authoritative-server session management for host, dedicated server, join, authentication, peer directory, kick, disconnect reasons, and optional reconnect.
- Introduce typed messaging with registration, codecs, direct send, server send, broadcast, validated relay, and later request/response.
- Add project-level compatibility options for `ApplicationId`, `ProtocolVersion`, event dispatching, and network limits.
- Add LAN room discovery, application-level latency stats, structured diagnostics, and server-owned multiplayer flows in staged follow-up increments.
- Keep V1 out of scope for Godot transport, SimpleFramework `Domain` integration, UDP transport, public lobbies, NAT traversal, realtime snapshots, prediction, rollback, encryption, compression, fragmentation, and automatic protocol manifest generation.

## Capabilities

### New Capabilities

- `net-core`: GameNet entry point, shared options, lifecycle, event dispatching, clock, transport abstraction, traffic protection, diagnostics, and security boundaries.
- `net-session`: Authoritative-server session management, joining, authentication, peer directory, disconnect/kick semantics, and optional reconnect.
- `net-messaging`: Typed message registration, packet/codec behavior, point-to-point send, server send, broadcast, relay, and request/response.
- `net-discovery-stats`: LAN room discovery, metadata rules, browser/scan behavior, and application-layer latency statistics.
- `net-flow`: Server-initiated multiplayer proposal, vote, barrier, aggregation, pending query, timeout, and manual resend flows.

### Modified Capabilities

None.

## Impact

- Affected projects: `Net/Net.csproj` and `Test/Test.csproj`.
- Affected existing code: `Net/ITransport.cs`, `Net/NetworkDefine.cs`, `Net/ClientInfoSystem.cs`, `Net/Connection/*`, `Net/Utils/ProtocolHandler.cs`, `Net/Utils/PingExecutor.cs`, `Net/Utils/UdpBroadcast.cs`, and Net tests.
- API impact: existing Godot-oriented and SimpleFramework-lifecycle-oriented Net APIs are not preserved as the primary API.
- Dependency impact: V1 should rely on .NET libraries only for core behavior; Godot-specific integration remains guarded or deferred.
- Test impact: requires focused NUnit coverage for lifecycle, session, messaging, transport framing, discovery metadata, stats, diagnostics, and staged flow behavior.
