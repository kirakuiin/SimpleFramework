## ADDED Requirements

### Requirement: Stable message registration
The Net messenger SHALL register message types by stable message keys and SHALL detect duplicate keys, message ID collisions, unsupported payload shapes, and incompatible fingerprints.

#### Scenario: Duplicate message key fails startup
- **WHEN** two message types register the same message key
- **THEN** registration fails with a duplicate-key error

#### Scenario: Type rename does not change stable key identity
- **WHEN** a message type is renamed but keeps the same explicit message key
- **THEN** the generated protocol identity remains stable

#### Scenario: Attribute scanning registers messages and handlers
- **WHEN** the messenger scans assemblies containing `[NetMessage]`, `[NetHandler]`, `[NetRequestHandler]`, or `[NetFlowHandler]`
- **THEN** matching messages and handlers are registered in a deterministic order

#### Scenario: Fingerprint mismatch follows configured policy
- **WHEN** peers compare incompatible message table fingerprints
- **THEN** the messenger applies the configured strict, warn, or ignore policy and never ignores the mismatch silently

### Requirement: Message handlers
The Net messenger SHALL support normal message handlers with `NetContext` and SHALL execute multiple handlers in registration order without allowing one handler exception to stop later handlers.

#### Scenario: Multiple handlers run in order
- **WHEN** a message arrives with three registered handlers
- **THEN** the handlers are invoked in registration order

#### Scenario: Handler exception is reported and contained
- **WHEN** one normal message handler throws
- **THEN** the exception is reported as a structured message error and remaining handlers still run

### Requirement: Basic message send APIs
The Net messenger SHALL support client-to-server send, server-to-peer send, and server broadcast using registered typed messages.

#### Scenario: Client sends to server
- **WHEN** a connected client calls `SendToServerAsync` with a registered message
- **THEN** the server receives the message with sender context

#### Scenario: Server broadcasts to clients
- **WHEN** the server calls `BroadcastAsync` with a registered message
- **THEN** all selected connected peers receive the message

### Requirement: Validated relay
The Net messenger SHALL support server-validated client relay to another peer and SHALL preserve the original sender identity.

#### Scenario: Relay preserves sender id
- **WHEN** a client relays a message to another client through the server
- **THEN** the receiving client observes the original sender `PeerId`

#### Scenario: Unauthorized relay is rejected
- **WHEN** relay policy denies the target or message
- **THEN** the relay call returns a permission or target failure result and does not forward the message

#### Scenario: Relay cannot be unlimited forwarding
- **WHEN** a client relays messages through the server
- **THEN** the server applies allowlist, permission, or rate-limit checks before forwarding

### Requirement: Request response
The Net messenger SHALL support request/response with one request handler, correlation, timeout, cancellation cleanup, and late or duplicate response handling.

#### Scenario: Request completes with response
- **WHEN** a caller sends a request to a peer with a registered request handler
- **THEN** the caller receives the typed response associated with the request correlation

#### Scenario: Request timeout cleans pending state
- **WHEN** a response is not received before the timeout
- **THEN** the request completes with a timeout result and removes its pending entry

#### Scenario: Late response is ignored
- **WHEN** a response arrives after the request timed out or was cancelled
- **THEN** the response is ignored and does not recreate pending state

#### Scenario: Disconnect does not recover ordinary request
- **WHEN** a request is interrupted by disconnect or session close
- **THEN** the request fails with a disconnect or session-closed result and is not automatically recovered

### Requirement: Packet and codec behavior
The Net messenger SHALL encode messages into an internal packet envelope and SHALL use a replaceable codec with clear errors for unsupported or invalid payloads.

#### Scenario: Invalid payload reports codec error
- **WHEN** the configured codec cannot serialize or deserialize a message payload
- **THEN** the operation fails with a structured codec error

#### Scenario: Unknown message is handled explicitly
- **WHEN** a packet references an unregistered message id
- **THEN** the messenger reports an unknown-message error and does not invoke business handlers
