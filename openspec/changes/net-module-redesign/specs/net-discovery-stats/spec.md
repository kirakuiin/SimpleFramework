## ADDED Requirements

### Requirement: LAN discovery compatibility filtering
The Net discovery component SHALL include magic, packet version, `ApplicationId`, `ProtocolVersion`, room id, game port, metadata schema id, payload length, and metadata payload in discovery packets.

#### Scenario: Different application is ignored
- **WHEN** a browser receives a discovery packet for a different `ApplicationId`
- **THEN** the packet is ignored and no joinable room is added

#### Scenario: Incompatible protocol is not joinable
- **WHEN** a browser receives a packet with an incompatible `ProtocolVersion`
- **THEN** the room is filtered or marked incompatible and is not treated as joinable

#### Scenario: Oversized discovery packet is discarded
- **WHEN** a discovery packet exceeds the configured maximum payload size
- **THEN** the packet is discarded, no fragmentation is attempted, and a diagnostic event can be recorded

### Requirement: Public metadata
The Net discovery component SHALL treat metadata as public display and filtering data and SHALL keep passwords, tokens, and private room keys out of metadata.

#### Scenario: Password indicator is allowed
- **WHEN** a password-protected room is advertised
- **THEN** metadata can include a public `HasPassword` indicator without including the real password

### Requirement: Advertise lifecycle
The Net discovery component SHALL support starting, updating, and stopping a continuous room advertisement for a host or server.

#### Scenario: Advertise update requires active advertise
- **WHEN** a caller updates advertised metadata before starting advertisement
- **THEN** the operation returns a clear invalid-state error

#### Scenario: Stop advertise ends broadcasts
- **WHEN** a caller stops advertisement
- **THEN** the advertise loop stops sending room packets

### Requirement: Scan and browser modes
The Net discovery component SHALL support one-shot scan and continuous browser modes with room found, updated, and lost events.

#### Scenario: Scan returns discovered rooms
- **WHEN** a compatible room advertises during a scan window
- **THEN** the scan result includes the room endpoint, game port, metadata, and estimated latency when available

#### Scenario: Browser removes expired rooms
- **WHEN** a browser has not received a room advertisement before `RoomTimeout`
- **THEN** the browser raises `RoomLost` and removes the room from its snapshot

### Requirement: Metadata schema identity
The Net discovery component SHALL use stable metadata schema keys or IDs and SHALL reject schema collisions at registration or startup.

#### Scenario: Schema id collision fails
- **WHEN** two metadata types register conflicting schema identities
- **THEN** discovery registration fails with a schema collision error

### Requirement: Application-layer stats
The Net stats component SHALL measure application-layer ping/pong RTT, average RTT, jitter, probe loss, timeout count, and last-seen time without requiring ICMP.

#### Scenario: RTT is measured by ping pong
- **WHEN** a stats probe receives a pong from a peer
- **THEN** the peer stats include current RTT and update average RTT

#### Scenario: Probe timeout contributes to loss
- **WHEN** a stats probe times out
- **THEN** the peer stats increment timeout data and update `ProbeLoss`

#### Scenario: TCP transport loss is unknown
- **WHEN** stats are queried for a peer connected over TCP transport
- **THEN** `TransportLoss` is `null` unless the transport explicitly provides transport-loss data

#### Scenario: Discovery latency can be estimated
- **WHEN** discovery scan traffic includes a measurable request/reply round trip
- **THEN** scan results can expose that value as room-list latency without requiring ICMP
