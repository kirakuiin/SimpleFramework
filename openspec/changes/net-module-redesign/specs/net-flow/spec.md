## ADDED Requirements

### Requirement: Server-initiated flow
The Net flow component SHALL support server-initiated multiplayer proposal, vote, and barrier flows over the messenger layer.

#### Scenario: Server proposes to target peers
- **WHEN** the server starts a flow with target peers and a proposal payload
- **THEN** each connected target peer receives the proposal and can respond with the expected acknowledgement type

#### Scenario: Client cannot initiate flow
- **WHEN** a client attempts to initiate a multiplayer flow
- **THEN** the operation is rejected because V1 flow initiation is server-owned

### Requirement: Aggregation policies
The Net flow component SHALL support aggregation policies including all accepted, any accepted, majority accepted, quorum, custom policy, rejection, timeout, and no-target outcomes.

#### Scenario: All accepted succeeds
- **WHEN** every target peer accepts before the timeout under an all-accepted policy
- **THEN** the flow completes with an accepted result

#### Scenario: Any accepted succeeds
- **WHEN** at least one target peer accepts before the timeout under an any-accepted policy
- **THEN** the flow completes with an accepted result

#### Scenario: Rejection ends rejected flow
- **WHEN** any target peer rejects under an all-accepted policy
- **THEN** the flow completes with a rejected result

#### Scenario: No targets returns explicit result
- **WHEN** a flow is started with no target peers
- **THEN** the flow completes with a no-targets result

### Requirement: Pending flow query
The Net flow component SHALL expose pending flow state so the server can inspect which peers have not responded.

#### Scenario: Pending peers are visible
- **WHEN** a flow is waiting for responses from a subset of target peers
- **THEN** the server can query the pending peer list for that flow

### Requirement: Manual resend
The Net flow component SHALL allow the server to manually resend a pending proposal to a selected pending peer before timeout.

#### Scenario: Resend pending proposal
- **WHEN** a target peer has not responded and the server calls resend for that peer
- **THEN** the peer receives the same pending proposal again

### Requirement: Timeout and disconnect handling
The Net flow component SHALL complete flows predictably when peers timeout or disconnect and SHALL ignore late responses after completion.

#### Scenario: Timeout completes flow
- **WHEN** one or more required peers do not respond before the timeout
- **THEN** the flow completes with a timeout result according to its policy

#### Scenario: Disconnect does not immediately fail every flow
- **WHEN** a target peer disconnects while a flow is pending
- **THEN** the flow remains pending until policy, timeout, or manual server action completes it

#### Scenario: Late response is ignored
- **WHEN** a peer response arrives after a flow has completed
- **THEN** the response is ignored and does not change the completed result

### Requirement: Flow limitations
The Net flow component SHALL not provide V1.3 client-side pending recovery, flow persistence, server restart recovery, nested flows, or automatic resend after reconnect.

#### Scenario: Reconnect does not replay flow automatically
- **WHEN** a peer reconnects after missing a flow proposal
- **THEN** the flow component does not automatically replay the proposal unless the server explicitly resends pending work
