## ADDED Requirements

### Requirement: BindableProperty notifications reject different-value reentrant writes
`BindableProperty<T>` MUST notify listeners synchronously after committing a non-equal value. While that notification is active, an attempted write of a different value to the same property MUST throw `InvalidOperationException` before mutating the stored value. An attempted equal value SHALL remain a comparer-controlled no-op. Notification state MUST reset in a `finally` path.

#### Scenario: Values are assigned sequentially
- **WHEN** a caller assigns a second value after the first setter and all of its listeners have returned
- **THEN** listeners receive both changes in assignment order

#### Scenario: Listener writes a different value reentrantly
- **WHEN** a listener attempts to assign a different value to the same property before the outer notification completes
- **THEN** the nested setter throws before changing the stored value

#### Scenario: Listener writes an equal value reentrantly
- **WHEN** a listener assigns a value that the property's comparer considers equal to the current value
- **THEN** the nested setter returns without throwing or starting another notification

#### Scenario: Listener writes silently to the notifying property
- **WHEN** a listener calls `SetValueWithoutNotify` with a comparer-different value on the property currently notifying
- **THEN** the silent write throws before changing the stored value, so later listeners observe state consistent with the outer notification arguments

#### Scenario: Listener throws
- **WHEN** any listener throws during notification
- **THEN** the exception propagates under existing multicast semantics and a later non-reentrant assignment is not rejected by stale notification state

### Requirement: Reentrancy protection does not imply thread safety
The reentrancy guard MUST define only same-call-stack notification behavior. It SHALL NOT claim to serialize concurrent writes or make `BindableProperty<T>` thread-safe.

#### Scenario: Another property is updated by a listener
- **WHEN** a listener writes a different BindableProperty and the resulting call chain does not write back to the notifying property
- **THEN** the other property performs its normal synchronous notification
