## Purpose

Define synchronous Command and Query contexts, component capability boundaries, tree-scoped execution guards, and Domain-local event subscription lifetimes.

## Requirements

### Requirement: Component Context exposes categorized capabilities
Model and System components MUST receive Context objects that do not implement `IDomain` and do not expose Parent, component registration, tree mutation, or disposal. Model Context SHALL expose Utility Get/TryGet and local SendEvent. System Context SHALL expose System, Model, and Utility Get/TryGet plus local RegisterEvent and SendEvent.

#### Scenario: Component tries to obtain management capability
- **WHEN** a component receives its framework Context
- **THEN** it cannot cast that Context to `IDomain` or call Register, AddChild, RemoveChild, or Dispose through the declared API

### Requirement: Component Context obeys a minimal lifecycle
Each Model/System Context MUST follow `Initializing -> Ready -> Invalid`. During Model initialization only Utility lookup is allowed. During System initialization only Model/Utility lookup and owned local event registration are allowed. Ready Context SHALL expose its declared runtime capabilities. Entering a Domain's Disposing state MUST invalidate that Domain's component Context capabilities before its release callbacks run. This does not invalidate Contexts owned by other Domains that have not entered Disposing.

#### Scenario: System sends event during initialization
- **WHEN** a System calls SendEvent from its Initialize callback
- **THEN** the Context throws `InvalidOperationException` before dispatch

#### Scenario: Dynamic System initializes in an Active Domain
- **WHEN** a newly registered System initializes while its Domain is already Active
- **THEN** its own Context remains Initializing and cannot GetSystem or SendEvent until initialization succeeds

#### Scenario: Release uses saved Context
- **WHEN** OnDeactivating or a component Release callback calls a previously saved Context owned by the Domain being disposed
- **THEN** the call fails because disposal has removed all Context capabilities

### Requirement: Commands and queries receive non-escaping stack Context
`ICommand`, `ICommand<TResult>`, and `IQuery<TResult>` MUST execute with framework-created `readonly ref struct` CommandContext or QueryContext values whose constructors are not publicly callable. The Context types MUST NOT be boxable, heap-storable, captured by asynchronous closures, or usable across `await`.

#### Scenario: Command executes synchronously
- **WHEN** a Domain sends an ICommand
- **THEN** the framework passes a stack CommandContext, executes the command synchronously, and returns only after Execute completes

#### Scenario: Command implementation tries to retain Context
- **WHEN** an implementation tries to store CommandContext in a normal object field or capture it for asynchronous use
- **THEN** the C# type system rejects that code

### Requirement: Command and query capabilities remain distinct
CommandContext MUST expose three categorized Get/TryGet operations and SendEvent, SendCommand, and SendQuery. QueryContext MUST expose only System/Model Get/TryGet and SendQuery. Query expresses read intent but SHALL NOT guarantee that returned objects are immutable or side-effect free.

#### Scenario: Query tries to send event
- **WHEN** query code is written against QueryContext
- **THEN** no SendEvent or SendCommand API is available

#### Scenario: Query receives mutable model
- **WHEN** QueryContext resolves a Model whose business API contains mutation
- **THEN** the framework does not claim to enforce deep read-only behavior

### Requirement: Runtime execution is synchronous and tree-scoped
Command, Query, and Event dispatch MUST increment the shared tree ExecutionDepth before user code and restore it in `finally`. Synchronous nesting SHALL be allowed. Component registration, tree mutation, and disposal MUST be rejected while ExecutionDepth is nonzero; event subscription changes during dispatch SHALL follow the copy-on-write snapshot rules. Async Command/Query semantics MUST NOT be provided by v2.

#### Scenario: Nested command succeeds
- **WHEN** a Command synchronously sends another Command or Query
- **THEN** both execute in order and the outer execution remains protected until all nested dispatch returns

#### Scenario: User code throws
- **WHEN** Command, Query, or Event user code throws
- **THEN** the original failure propagates and ExecutionDepth is restored in `finally`

### Requirement: Domain events are local and ordered
Event registration and sending through a Domain MUST affect only that Domain. Parent fallback MUST NOT apply to events and the framework MUST NOT provide EventBus.Global. Handlers SHALL execute synchronously in registration order.

#### Scenario: Child sends event also registered by parent
- **WHEN** an attached child sends an event whose type also has handlers on its parent
- **THEN** only handlers registered on the child Domain execute

#### Scenario: Explicit shared bus is used
- **WHEN** an application needs cross-Domain events
- **THEN** it must explicitly own and pass a shared Utility or service outside Domain event propagation guarantees

### Requirement: Event dispatch uses copy-on-write snapshot semantics
Each event type MUST store an immutable current handler array. Registration and unregistration SHALL replace that array; sending SHALL capture one array reference and iterate it without allocating a snapshot. Changes during dispatch MUST affect only later sends.

#### Scenario: Handler is registered during send
- **WHEN** an event handler registers another handler of the same event type
- **THEN** the new handler does not run in the current send and is available on the next send

#### Scenario: Handler is unregistered during send
- **WHEN** a handler unregisters itself or another handler during dispatch
- **THEN** the captured current array still determines the remainder of that send

### Requirement: Event failures are fail-fast and tokens are idempotent
The first event handler exception MUST stop later handlers and propagate unchanged after ExecutionDepth restoration. Every unregistration token MUST be idempotent. System-owned tokens MUST be canceled before that System's Release; external tokens MUST become harmless no-ops after Domain event cleanup.

#### Scenario: First handler throws
- **WHEN** an earlier handler throws during synchronous event dispatch
- **THEN** later handlers do not run, the same exception propagates, and the tree is no longer executing afterward

#### Scenario: Token is used after Domain disposal
- **WHEN** an external caller unregisters a token after its Domain cleared the event bus
- **THEN** the call has no effect and does not throw

#### Scenario: System manually cancels an owned subscription
- **WHEN** a System unregisters a token before its own Release
- **THEN** the framework MUST remove both the event subscription and its ownership record, allowing the canceled token to be collected while the System remains active; remaining subscriptions retain registration order and are canceled in reverse ownership order before Release

### Requirement: Subscription ownership follows the registering Context
A subscription created through a System Context MUST belong to that System, regardless of which business method invoked registration or which objects the callback captures. Failure cleanup of another dynamic candidate MUST NOT automatically cancel it. Direct IDomain event subscriptions SHALL remain caller-managed until canceled or cleared by Domain disposal.

The current System Context registration path validates its own lifecycle state, not the tree transition flag. An Initializing or Ready System Context can therefore create owned subscriptions during a tree transition, while direct IDomain.RegisterEvent calls are rejected during transitions. A transition lock does not freeze every business operation or Context capability.

#### Scenario: Preserved child System subscribes during self-only parent cleanup
- **WHEN** a parent is executing DisposeSelfOnly and a business call registers through a preserved child's Ready System Context
- **THEN** the child's Context remains valid and the subscription belongs to the child System; the parent's cleanup does not cancel it

### Requirement: Runtime follows a single-thread cooperation contract
The framework MUST NOT add locking, thread ownership capture, or automatic main-thread dispatch. Callers SHALL serialize Domain and connected-tree operations on one owning thread and marshal background results back before using Domain or Context APIs.

#### Scenario: Background operation completes
- **WHEN** background work produces a result needed by a Domain
- **THEN** application code returns that result to the owning thread before sending a synchronous Command or Event
