## ADDED Requirements

### Requirement: GameNet facade
The Net module SHALL expose `GameNet` as the primary entry point for shared options, session operations, messaging operations, discovery, stats, diagnostics, and flow access.

#### Scenario: Common operations are reachable from GameNet
- **WHEN** a caller creates `GameNet` with a transport and valid options
- **THEN** the caller can start hosting or joining, register message handlers, send messages, and access discovery, stats, diagnostics, and flow subcomponents from that instance

### Requirement: Project compatibility options
The Net module SHALL require a non-empty `ApplicationId` and SHALL use `ProtocolVersion` for discovery filtering and session handshake compatibility.

#### Scenario: Invalid application id is rejected
- **WHEN** `GameNet` is created or started with `ApplicationId` equal to `Guid.Empty`
- **THEN** startup fails with a clear validation error

#### Scenario: Incompatible protocol is rejected during join
- **WHEN** a client connects directly by host and port with a different `ProtocolVersion`
- **THEN** join fails with an incompatible protocol result instead of an authentication failure

### Requirement: Explicit lifecycle control
The Net module SHALL serialize host, server, join, leave, stop, and dispose lifecycle operations and SHALL make stop and dispose idempotent.

#### Scenario: Concurrent starts do not both win
- **WHEN** two start operations are invoked concurrently on the same `GameNet` instance
- **THEN** at most one operation starts the session and the other returns or throws a clear invalid-operation result

#### Scenario: Dispose cancels owned work
- **WHEN** `DisposeAsync` is invoked on an active `GameNet` instance
- **THEN** discovery loops, pending requests, pending flows, session operations, transport operations, and owned resources are cancelled or closed

#### Scenario: API use after dispose fails clearly
- **WHEN** send, connection, discovery, stats, or flow APIs are invoked after `DisposeAsync` completes
- **THEN** the operation returns an `ObjectDisposed` result or throws `ObjectDisposedException`

### Requirement: Event dispatching
The Net module SHALL invoke events on the current network task context by default and SHALL use a configured `INetEventDispatcher` to post framework events when provided.

#### Scenario: Dispatcher is used for framework events
- **WHEN** a dispatcher is configured and a session, message, discovery, stats, or diagnostics event is raised
- **THEN** the event callback is posted through the dispatcher

#### Scenario: Dispatcher failure is contained
- **WHEN** the dispatcher throws while posting an event
- **THEN** the failure is captured as a structured event dispatch error and network loops continue

### Requirement: Replaceable time source
The Net module SHALL route timeout, reconnect delay, discovery expiration, and stats probe timing through one replaceable time source.

#### Scenario: Fake time drives timeout behavior
- **WHEN** tests advance a fake or controllable clock beyond a configured timeout
- **THEN** pending network operations that depend on that timeout complete without waiting for real time

### Requirement: Transport abstraction
The Net module SHALL define `INetTransport` in terms of listen, connect, disconnect, raw packet send, transport connection IDs, channels, and transport-layer events only.

#### Scenario: Transport does not expose session peer ids
- **WHEN** a transport peer connects or sends a packet
- **THEN** the transport event payload contains transport-layer identifiers and raw packet data, not `PeerId` or authentication state

#### Scenario: TCP transport frames packets
- **WHEN** `TcpNetTransport` receives multiple packets from a TCP stream
- **THEN** it reconstructs packets using a `[FrameLength][NetPacket bytes]` framing format without sticky-packet or partial-read corruption

#### Scenario: TCP unreliable channel is not silently downgraded
- **WHEN** a caller sends on `NetChannel.Unreliable` through `TcpNetTransport` without explicit degrade-to-reliable configuration
- **THEN** the send returns `ChannelUnsupported`

#### Scenario: Bind address is configurable
- **WHEN** a caller starts a transport listener with a specific bind address
- **THEN** the transport listens on that configured address instead of forcing a default network interface

### Requirement: Traffic limits and send results
The Net module SHALL enforce configurable packet and send-queue limits and SHALL return explicit send failure results instead of allowing unbounded memory growth.

#### Scenario: Packet size limit rejects oversized send
- **WHEN** a caller attempts to send a packet larger than `MaxPacketSize`
- **THEN** the send operation returns an oversized-packet failure result

#### Scenario: Send queue limit rejects backpressure overflow
- **WHEN** a peer send queue reaches the configured byte or packet limit
- **THEN** additional sends return a send-queue-full result

### Requirement: Diagnostics and security boundaries
The Net module SHALL expose read-only diagnostics snapshots and structured errors, and SHALL document that V1 TCP transport and discovery metadata are not encrypted or private.

#### Scenario: Diagnostics snapshot is read-only
- **WHEN** a caller requests a diagnostics snapshot
- **THEN** the caller receives current counts for peers, pending work, packets, bytes, drops, and errors without mutating Net state

#### Scenario: Discovery metadata is public
- **WHEN** a room is advertised through discovery metadata
- **THEN** the framework treats metadata as public data and does not use it as authentication input

#### Scenario: Reconnect token is not predictable
- **WHEN** a reconnect token is issued
- **THEN** the token uses sufficient randomness, is not predictable from peer data, and is only valid inside the server grace window
