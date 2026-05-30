## ADDED Requirements

### Requirement: Authoritative session roles
The Net session SHALL support host, dedicated server, and client join roles under an authoritative-server model.

#### Scenario: Host creates a local participant
- **WHEN** a caller starts a host session
- **THEN** the session starts accepting clients and creates a local host participant

#### Scenario: Dedicated server has no local player
- **WHEN** a caller starts a dedicated server session
- **THEN** the session starts accepting clients without creating a local player participant

### Requirement: Join result semantics
The Net session SHALL provide a structured join result for successful and failed joins, including cancellation, transport failure, incompatible application, incompatible protocol, authentication failure, and capacity failure.

#### Scenario: Successful join returns peer identity
- **WHEN** a client joins a compatible server and passes authentication
- **THEN** the join result includes the assigned client `PeerId` and current peer directory snapshot

#### Scenario: Full server rejects join clearly
- **WHEN** a client joins a server that has reached `MaxPeers`
- **THEN** the join result reports a capacity failure

### Requirement: Handshake compatibility validation
The Net session SHALL validate `ApplicationId` and `ProtocolVersion` during handshake even when the client bypasses discovery and connects by direct address.

#### Scenario: Wrong application is rejected
- **WHEN** a client connects with a different `ApplicationId`
- **THEN** the session rejects the join with an incompatible application result

### Requirement: Authentication payload handling
The Net session SHALL pass `JoinOptions.AuthPayload` to the configured server authenticator and SHALL keep discovery metadata separate from authentication.

#### Scenario: Password room authenticates through auth payload
- **WHEN** a room requires a password and the client supplies it in `AuthPayload`
- **THEN** the authenticator can accept or reject the join using that payload

#### Scenario: Wrong password has a clear reason
- **WHEN** the authenticator rejects a password-protected join
- **THEN** the join result reports an authentication failure such as wrong password

### Requirement: Peer directory
The Net session SHALL maintain a stable `PeerDirectory` using `PeerId` values and session-level peer information.

#### Scenario: Public peer id constants have defined semantics
- **WHEN** session code exposes public peer identifiers
- **THEN** `PeerId.None` represents no peer and `PeerId.Server` represents the authoritative server endpoint

#### Scenario: Server owns authoritative directory
- **WHEN** peers join, disconnect, reconnect, or leave
- **THEN** the server maintains the authoritative peer directory and clients maintain synchronized read-only snapshots

#### Scenario: Peer list updates on join and leave
- **WHEN** clients join and leave a server
- **THEN** server and clients receive peer directory updates and peer joined/left events in order

#### Scenario: PeerInfo excludes game-specific state
- **WHEN** peer information is synchronized
- **THEN** it contains session-generic fields and excludes display name, team, ready state, or other business data

### Requirement: Disconnect and kick semantics
The Net session SHALL expose explicit disconnect reasons and SHALL allow the authoritative server to kick a connected client.

#### Scenario: Server kick notifies client
- **WHEN** the server kicks a client with a reason
- **THEN** the client disconnects and observes that reason

#### Scenario: Host shutdown disconnects clients
- **WHEN** a host or server stops
- **THEN** connected clients are disconnected with a server-closed reason

### Requirement: Optional reconnect
The Net session SHALL make reconnect opt-in and SHALL restore a peer identity only when a valid reconnect token is used within the grace window.

#### Scenario: Reconnect is disabled by default
- **WHEN** a client disconnects without enabling reconnect behavior
- **THEN** the session removes the peer according to normal disconnect rules and does not automatically reconnect

#### Scenario: Valid reconnect restores peer id
- **WHEN** reconnect is enabled and the client reconnects within the grace window with a valid token
- **THEN** the session restores the previous `PeerId`

#### Scenario: Expired reconnect releases peer id
- **WHEN** the reconnect grace window expires before a valid reconnect
- **THEN** the original `PeerId` is released and the peer is removed from the directory

#### Scenario: Temporary disconnect differs from final removal
- **WHEN** a reconnect-enabled peer disconnects during the grace window
- **THEN** the session can report temporary disconnection separately from final peer removal
