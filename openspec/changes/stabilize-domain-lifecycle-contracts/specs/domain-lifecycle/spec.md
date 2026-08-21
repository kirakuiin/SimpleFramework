## MODIFIED Requirements

### Requirement: Component initialization is transactional for framework-local effects
A top-level lifecycle component registration MUST establish a transaction that includes nested new-key System, Model, and Utility registrations plus local event subscriptions. If any nested lifecycle registration fails, the transaction MUST remain poisoned even when an outer initializer catches that exception, and the top-level registration MUST fail at its commit barrier. Every enlisted entry and subscription SHALL be removed; every System or Model whose initialization started SHALL receive cleanup, while Utilities SHALL receive no lifecycle callback.

#### Scenario: Nested registrations succeed
- **WHEN** a component initializer registers components under absent keys and every initializer succeeds
- **THEN** every nested entry remains registered and each lifecycle component is initialized exactly once

#### Scenario: Outer initialization fails after nested registration
- **WHEN** an initializer registers a System, Model, Utility, and local event subscription and then throws
- **THEN** none of those additions remains visible and no rolled-back event callback can be invoked

#### Scenario: Caught nested initialization failure poisons the outer transaction
- **WHEN** component A catches an exception from registering component B and A's initializer otherwise returns successfully
- **THEN** A's top-level registration still fails and every framework-local effect enlisted by A, B, or their nested registrations is rolled back

#### Scenario: Caught nested registration preflight failure poisons the outer transaction
- **WHEN** component A catches a duplicate-instance, self-registration, or forbidden-replacement failure raised before component B initialization begins
- **THEN** A's top-level registration still fails at the commit barrier and every enlisted framework-local effect is rolled back

#### Scenario: Registration continues after transaction poison
- **WHEN** an outer initializer catches a nested registration failure and attempts another framework-local registration in the same transaction
- **THEN** the later registration is rejected and cannot commit new state

#### Scenario: Rollback cleanup also fails
- **WHEN** initialization throws and one or more rollback cleanup callbacks or owned-subscription cancellations also throw
- **THEN** the framework throws an `AggregateException` containing the initialization and cleanup failures after attempting all rollback work

### Requirement: Lifecycle binding runs under component initialization guards
`AbstractDomain` MUST treat externally implemented `BindDomain` and component `Initialize` callbacks as one guarded component-initialization phase. During either callback, queries, nested new-key registration, and owner-aware System event registration SHALL remain available, while Domain teardown, commands, local event dispatch or unregistration, relationship mutation, and replacement of an existing component key MUST be rejected before mutation or execution. Every caller-facing `IUnRegister` returned by `AbstractDomain` SHALL enforce the same event-unregistration guard through both `UnRegister()` and `Dispose()` without consuming the handle when validation fails; framework-owned rollback and cleanup SHALL retain an internal unguarded cancellation path.

For a standard ownership tree, an ancestor `AbstractDomain` MUST also reject component or local-event registration, local-event unregistration, Command or event execution, and relationship mutation while an owned standard descendant is initializing itself, binding a component, or initializing a component. These operations SHALL fail before committing effects outside the descendant's initialization transaction or Domain creation boundary. During owned descendant component release, ancestor component/event registration, Command/event/Query execution, and relationship mutation SHALL follow the same rejection rules as the releasing Domain itself. Event unregistration and initialization-time Query availability SHALL retain their existing phase behavior. Arbitrary custom and lookup-only Domain relationships remain outside this internal preflight.

#### Scenario: Binding callback attempts runtime-only operations
- **WHEN** a direct custom System or Model attempts Domain teardown, command or event execution, event unregistration, relationship mutation, or existing-key replacement from `BindDomain`
- **THEN** the operation throws before changing Domain state, executing user behavior, changing relationships, or releasing the existing component

#### Scenario: Binding callback fails under the guard
- **WHEN** a forbidden binding operation escapes `BindDomain`
- **THEN** component registration rolls back, component initialization does not begin, and the active Domain remains usable

#### Scenario: Component callback cancels through a returned handle
- **WHEN** `BindDomain` or `Initialize` invokes `UnRegister()` or `Dispose()` on an existing Domain-scoped or System-owned subscription handle
- **THEN** cancellation is rejected before removing the subscription or consuming the handle, and a failed registration leaves the existing subscription active

#### Scenario: Rejected handle cancellation is retried while active
- **WHEN** a handle cancellation was rejected during component binding or initialization and the caller retries after the Domain returns to its active phase
- **THEN** the retry cancels the subscription normally

#### Scenario: Descendant initializer targets an ancestor Domain
- **WHEN** an owned standard descendant component initializer attempts component/event registration, event unregistration, Command/event execution, or relationship mutation through an ancestor Domain
- **THEN** the ancestor rejects every operation before side effects can escape the descendant initialization transaction

#### Scenario: Owned Domain initialization targets its ancestor
- **WHEN** a standard Domain establishes ownership during its own initialization and then attempts a guarded framework mutation in the ancestor
- **THEN** the ancestor rejects the mutation until the owned Domain reaches its active state

#### Scenario: Descendant release targets an ancestor Domain
- **WHEN** an owned standard descendant component release callback attempts component/event registration, Command/event/Query execution, or relationship mutation through an ancestor Domain
- **THEN** the ancestor applies the same release-phase guard before executing or committing framework effects

## ADDED Requirements

### Requirement: Synchronous Domain execution cannot interleave terminal teardown
`AbstractDomain` MUST track the complete synchronous execution scope of every public local-event, Command, and Query entry point, including the full invocation of overridable `ExecuteCommand` and `ExecuteQuery` methods. `UnInitialize()` MUST reject terminal teardown while any such scope is active. Replacement of an existing System or Model lifecycle key and mutation of standard ownership relationships MUST also be rejected when the target Domain or any standard Domain descendant it owns is synchronously executing. These operations SHALL fail before release or topology mutation, while new-key registration and Utility replacement remain allowed. Nested event, Command, and Query execution SHALL remain allowed, and execution depth MUST be restored in a `finally` path when user code throws. This guard defines synchronous call-stack behavior only and SHALL NOT extend to asynchronous work started by user code.

#### Scenario: Event listener attempts Domain teardown
- **WHEN** a local-event listener calls `UnInitialize()` during synchronous dispatch
- **THEN** teardown is rejected and no later listener runs against a released Domain

#### Scenario: Command or Query attempts Domain teardown
- **WHEN** a Command, Query, or overridden execution hook calls `UnInitialize()` before the public send method returns
- **THEN** teardown is rejected and the Domain remains active

#### Scenario: Execution callback throws
- **WHEN** an event listener, Command, or Query throws during execution
- **THEN** the original exception propagates and a later `UnInitialize()` call is not rejected by stale execution depth

#### Scenario: Execution is nested synchronously
- **WHEN** an event, Command, or Query synchronously invokes another public execution entry point on the same Domain
- **THEN** nested execution proceeds normally and teardown remains forbidden until the outermost execution returns

#### Scenario: Event callback replaces a subscribed System
- **WHEN** an earlier listener attempts to replace a System whose owned listener is present later in the current EventBus snapshot
- **THEN** replacement is rejected before cleanup, so the old System remains active while its snapshot callback executes

#### Scenario: Command or Query replaces a lifecycle component
- **WHEN** synchronous Command or Query execution attempts to replace an existing System or Model key
- **THEN** replacement is rejected before the old lifecycle component is unpublished or released

#### Scenario: Owned descendant execution replaces an ancestor lifecycle component
- **WHEN** a standard owned descendant synchronously executes a Command or Query and attempts to replace an existing System or Model key in its ancestor Domain
- **THEN** ancestor replacement is rejected before releasing the inherited component retained by descendant execution

#### Scenario: Executing descendant detaches before ancestor replacement
- **WHEN** an owned standard descendant attempts to remove or reparent itself during synchronous execution before replacing an ancestor lifecycle component
- **THEN** relationship mutation is rejected, the ownership tree remains intact, and the later replacement is still rejected

#### Scenario: Execution registers a new key or replaces a Utility
- **WHEN** synchronous execution registers an absent System or Model key, or replaces an existing Utility key
- **THEN** registration proceeds under the existing lifecycle and transaction rules

### Requirement: System subscriptions are System-owned by default
A local event subscription registered through a System's event-registration capability MUST be owned by that System entry. Owner-aware registration SHALL be accepted while the local System entry is published during binding, initialization, or active execution, and rejected after it is unpublished for cleanup. Replacement, failed initialization, and terminal Domain teardown MUST cancel every owned subscription even when System cleanup throws. The returned `IUnRegister` SHALL remain available for idempotent early cancellation. Direct registration through the Domain event API SHALL remain Domain-scoped and caller-managed until explicit cancellation or Domain teardown. Models SHALL NOT gain an owner-aware event-registration capability as part of this requirement.

#### Scenario: Active System is replaced
- **WHEN** a System that registered a local event through its component capability is replaced under its lifecycle key
- **THEN** the old System is cleaned and no longer receives that local event while the replacement may receive it

#### Scenario: Caller cancels before component release
- **WHEN** a caller invokes the returned unregister token before the owning System is released
- **THEN** the callback stops immediately and later component cleanup does not invoke cancellation behavior twice

#### Scenario: Component cleanup throws
- **WHEN** an owned System's cleanup callback throws during replacement or teardown
- **THEN** the Domain still attempts every owned subscription cancellation and reports all failures according to lifecycle aggregation rules

#### Scenario: Binding fails after registering an owned subscription
- **WHEN** a direct custom System registers a System-owned local subscription during `BindDomain` and binding then throws before initialization starts
- **THEN** registration fails and the owned subscription is still cancelled without invoking the component cleanup callback

#### Scenario: Existing System subscribes during a failing component transaction
- **WHEN** an already-active System creates a System-owned local subscription while another component is initializing and that top-level registration rolls back
- **THEN** the new subscription is cancelled by the transaction without releasing the existing System

#### Scenario: Existing System subscribes during a successful component transaction
- **WHEN** an already-active System creates a System-owned local subscription while another component initializes successfully
- **THEN** the subscription remains owned by the existing System until early cancellation, System release, or terminal Domain teardown

#### Scenario: Domain-scoped registration outlives component replacement
- **WHEN** a caller explicitly registers a callback through the Domain event API rather than a component registration capability
- **THEN** replacing an unrelated component does not cancel that callback and the callback remains until manually cancelled or the Domain is torn down

### Requirement: Parent changes are exception-atomic for framework Domains
`AbstractDomain.SetParent` MUST NOT commit a candidate parent until required removal from the old parent's ownership list succeeds. It SHALL retain the old parent reference when old-relationship validation or removal fails. Standard `AbstractDomain` parent and child views MUST agree after every successful or failed relationship operation.

#### Scenario: Owned child changes parent successfully
- **WHEN** an active child owned by one active `AbstractDomain` changes to another valid parent
- **THEN** the old parent no longer owns the child and the child's parent reference identifies the new parent

#### Scenario: Old parent rejects relationship mutation
- **WHEN** removal from an old owning parent is forbidden by its lifecycle phase
- **THEN** `SetParent` throws before committing the new reference and the old parent continues to own the child

#### Scenario: Lookup-only parent changes
- **WHEN** a child changes a parent relationship that did not create old-parent lifecycle ownership
- **THEN** the child commits the new lookup parent without creating ownership in the new parent

#### Scenario: Direct child removal is rejected by the child
- **WHEN** an owning parent calls `RemoveChild` while the standard child lifecycle phase rejects changing its parent
- **THEN** the operation throws without removing the parent's strong ownership reference or changing the child's parent reference

### Requirement: Standard owned Domain trees preflight terminal teardown
Before an `AbstractDomain` changes state or releases any resource, it MUST recursively verify that every owned descendant implemented by `AbstractDomain` can begin terminal teardown. Execution, component initialization, or component release in the target or any such descendant MUST reject the top-level teardown before mutation, preserving every standard Domain state, parent reference, and strong ownership relationship. A repeated request after terminal Domain cleanup has entered `Uninitializing` or `Disposed` SHALL remain an idempotent no-op; active component replacement/release is not terminal Domain cleanup and MUST reject instead of silently returning. Once preflight succeeds, callback failures raised during actual cleanup SHALL retain the existing exhaustive aggregation behavior. Arbitrary custom `IDomain` implementations without framework-internal lifecycle state remain best-effort during parent cleanup.

#### Scenario: Descendant is executing
- **WHEN** an owned child or deeper standard descendant is synchronously executing an Event, Command, or Query and requests ancestor teardown
- **THEN** ancestor teardown is rejected before changing any Domain in the owned tree

#### Scenario: Descendant component is initializing or releasing
- **WHEN** an owned standard descendant is inside component binding, initialization, or release when ancestor teardown is requested
- **THEN** ancestor teardown is rejected and the ownership tree remains unchanged

#### Scenario: Descendant cleanup is still running
- **WHEN** an owned standard descendant has entered terminal cleanup and one of its cleanup callbacks requests ancestor teardown
- **THEN** ancestor teardown is rejected until that descendant reaches its disposed state, so the callback cannot continue against released ancestor resources

#### Scenario: Active component release requests Domain teardown
- **WHEN** a component cleanup callback runs during active replacement or rollback and calls its own Domain's `UnInitialize`
- **THEN** teardown is rejected rather than reported as a successful no-op, and the active replacement or rollback retains control of cleanup

#### Scenario: Cleanup callback fails after successful preflight
- **WHEN** the standard owned tree passes preflight and a later component or Domain cleanup callback throws
- **THEN** cleanup continues and aggregates failures under the existing terminal teardown contract
