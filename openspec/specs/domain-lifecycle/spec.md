## Purpose

Define the one-shot Domain lifecycle, its operation guards, transactional initialization, and deterministic terminal teardown.

## Requirements

### Requirement: Domain lifecycle is explicit and one-shot
Each Domain instance MUST follow the private lifecycle `Created -> Initializing -> Active -> Uninitializing -> Disposed`, except that initialization failure SHALL transition through internal cleanup to `Disposed`. A disposed instance MUST NOT return to an earlier state.

#### Scenario: Successful initialization
- **WHEN** a newly created Domain completes its initialization hooks
- **THEN** it becomes active exactly once

#### Scenario: Initialization fails
- **WHEN** a Domain initialization hook throws
- **THEN** the framework cleans all partially initialized Domain resources and leaves that instance disposed

#### Scenario: A disposed singleton is requested again
- **WHEN** the cached singleton Domain has been disposed and `Instance` is accessed again
- **THEN** the framework creates a new Domain instance rather than reactivating the disposed instance

### Requirement: Singleton creation rejects initialization reentrancy
`AbstractDomain<T>.Instance` MUST NOT expose or create another singleton of `T` while any `T` instance is initializing. It SHALL throw a clear `InvalidOperationException`, leave the singleton cache empty after failure, and reset its creation guard.

#### Scenario: Init accesses Instance of the same Domain type
- **WHEN** `Init` for `T` accesses `T.Instance`
- **THEN** the access throws `InvalidOperationException` without recursion or exposure of the partial instance

#### Scenario: Singleton initialization can be retried with a fresh instance
- **WHEN** singleton creation fails and a later `Instance` access is made after the cause is corrected
- **THEN** the framework attempts creation with a new instance

### Requirement: Operations obey the lifecycle phase
The Domain MUST centralize phase checks for its public operations. Component lookup (`Get`, `TryGet`, and `Require`), `Parent`, and `ToString` SHALL remain observable during teardown and after disposal. Registration, relationship mutation, event registration or sending, commands, and queries MUST fail after disposal.

#### Scenario: Observation after disposal
- **WHEN** lookup, `Parent`, or `ToString` is called on a disposed Domain
- **THEN** it observes the cleared Domain without reactivating or mutating it

#### Scenario: Mutation after disposal
- **WHEN** a caller tries to register a component, change a relationship, or use a Domain event API after disposal
- **THEN** the Domain throws `InvalidOperationException`

#### Scenario: Execution after disposal
- **WHEN** a caller sends a command or query through a disposed Domain
- **THEN** the Domain throws `InvalidOperationException` before executing user code

### Requirement: UnInitialize has defined reentrancy behavior
Calling `UnInitialize` from `Created` or `Active` SHALL perform terminal cleanup. Calling it while already `Uninitializing` or `Disposed` SHALL be an idempotent no-op. Calling it while `Initializing`, including from a Domain or component initializer, MUST throw `InvalidOperationException` and cause initialization to fail.

#### Scenario: Component initializer tears down its Domain
- **WHEN** a System or Model calls `Domain.UnInitialize()` from `Initialize`
- **THEN** initialization fails, framework-local changes are rolled back, and the Domain ends disposed if Domain initialization was in progress

#### Scenario: Cleanup reenters UnInitialize
- **WHEN** a cleanup callback calls `Domain.UnInitialize()`
- **THEN** the nested call returns without repeating lifecycle callbacks

#### Scenario: Teardown is repeated after completion
- **WHEN** `UnInitialize` is called more than once on a disposed Domain
- **THEN** subsequent calls have no effect and do not throw

### Requirement: Component initialization is transactional for framework-local effects
A top-level lifecycle component registration MUST establish a transaction that includes nested new-key System, Model, and Utility registrations plus local event subscriptions. If the outer initialization fails, all enlisted entries and subscriptions SHALL be removed; every System or Model whose initialization started SHALL receive cleanup, while Utilities SHALL receive no lifecycle callback.

#### Scenario: Nested registrations succeed
- **WHEN** a component initializer registers components under absent keys and the outer initializer succeeds
- **THEN** every nested entry remains registered and each lifecycle component is initialized exactly once

#### Scenario: Outer initialization fails after nested registration
- **WHEN** an initializer registers a System, Model, Utility, and local event subscription and then throws
- **THEN** none of those additions remains visible and no rolled-back event callback can be invoked

#### Scenario: Rollback cleanup also fails
- **WHEN** initialization throws and one or more rollback cleanup callbacks also throw
- **THEN** the framework throws an `AggregateException` containing the initialization and cleanup failures after attempting all rollback work

### Requirement: Initialization permits only recoverable Domain operations
During a component `Initialize` callback, the Domain SHALL allow component lookup, queries, local event registration, and nested registration under absent non-initializing keys. It MUST reject commands, local event sending, relationship mutation, replacement of an existing key, duplicate instance registration, and direct or indirect initialization cycles.

#### Scenario: Initializer reads and queries
- **WHEN** a component initializer resolves dependencies or sends a query
- **THEN** the Domain performs the operation using the currently published transaction state

#### Scenario: Initializer performs an irreversible Domain operation
- **WHEN** a component initializer sends a command or local event or changes a parent/child relationship
- **THEN** the Domain throws `InvalidOperationException` before the operation takes effect

#### Scenario: Initializer replaces an existing key
- **WHEN** a component initializer registers any component under a key that already exists
- **THEN** registration fails without releasing or replacing the existing entry

#### Scenario: Initializer creates a cycle
- **WHEN** nested initialization attempts to register a key or instance already present in the active initialization chain
- **THEN** the Domain throws `InvalidOperationException` and rolls back the chain

### Requirement: Full teardown has deterministic dependency order
Full Domain teardown MUST clean owned children before local components, Systems before Models, and each lifecycle category in reverse successful activation order. The framework MUST unpublish each component before its cleanup callback, while leaving later components available for read-only lookup until their own turn.

#### Scenario: Components are released in order
- **WHEN** Systems and Models were activated in a known sequence and the Domain is torn down
- **THEN** children finish first, Systems clean before Models, and each category cleans in reverse activation order

#### Scenario: Cleanup reads remaining dependencies
- **WHEN** a component cleanup callback performs component lookup
- **THEN** the component being cleaned is absent and components not yet selected for cleanup remain readable

### Requirement: Teardown is exhaustive and terminal despite failures
The Domain MUST attempt every child, component, framework clear, and derived cleanup hook even when earlier work throws. It SHALL aggregate all failures, clear Utilities without lifecycle callbacks, clear local events and the parent link, and enter `Disposed` in a `finally` path. A component whose cleanup fails MUST still be permanently released.

#### Scenario: Multiple cleanup stages fail
- **WHEN** child, component, or Domain cleanup callbacks throw
- **THEN** all remaining cleanup stages run and the caller receives one `AggregateException` containing every failure

#### Scenario: Derived UnInit tries to repopulate the Domain
- **WHEN** a derived `UnInit` callback tries to register a component or local event
- **THEN** the operation is rejected because the Domain remains uninitializing and the Domain finishes disposed and empty

#### Scenario: Utility implements a lifecycle interface
- **WHEN** an object is registered only as a Utility even though its runtime type also implements `ISystem` or `IModel`
- **THEN** Domain teardown removes it without invoking lifecycle callbacks

### Requirement: Domain events remain local
Registration and sending through a Domain MUST affect only that Domain's local event bus. Component parent fallback MUST NOT apply to events; cross-Domain publication SHALL require explicit use of `EventBus.Global`.

#### Scenario: Parent and child register the same event type
- **WHEN** the child sends that event through its Domain
- **THEN** only listeners registered on the child Domain are invoked

### Requirement: Framework transactions exclude external side effects
The framework MUST NOT claim to roll back arbitrary Utility calls, global event publication, I/O, or other external effects performed by initializer code. Component authors SHALL remain responsible for compensating those effects.

#### Scenario: Initializer mutates an external service and then fails
- **WHEN** component initialization performs an external side effect before throwing
- **THEN** the Domain rolls back only its enlisted registry entries and local event subscriptions
