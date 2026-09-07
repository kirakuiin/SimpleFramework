## Purpose

Define the one-shot Domain v2 lifecycle, factory and singleton creation, phase guards, failure propagation, and deterministic terminal teardown.

## Requirements

### Requirement: Domain lifecycle is explicit and one-shot
Each Domain instance MUST follow the private lifecycle `Starting -> Active -> Disposing -> Disposed`. Creation failure SHALL perform cleanup and leave the instance Disposed. A Disposed instance MUST NOT return to an earlier state. `Create` and singleton `Instance` MUST return only an Active instance.

#### Scenario: Successful creation
- **WHEN** a newly constructed Domain completes Configure, component initialization, and OnActivated
- **THEN** Create returns that Active instance exactly once

#### Scenario: Creation fails
- **WHEN** Configure, component initialization, or OnActivated throws
- **THEN** the framework cleans all framework-owned resources, leaves the candidate Disposed, and does not return or publish it

#### Scenario: Disposed operation is attempted
- **WHEN** any public operation other than idempotent Dispose is called on a Disposed Domain
- **THEN** it throws `ObjectDisposedException`

### Requirement: Singleton creation rejects initialization reentrancy
Each `AbstractSingletonDomain<T>` MUST maintain static `Empty`, `Creating`, and `Published` states. Accessing `Instance` or calling `DestroyInstance` while T is Creating MUST throw a clear `InvalidOperationException`; `GetInstance` MUST return null until publication. Failure SHALL return the singleton state to Empty, and only successful OnActivated SHALL publish the candidate.

#### Scenario: Startup accesses Instance of the same type
- **WHEN** Configure, component Initialize, or OnActivated for T accesses T.Instance
- **THEN** access throws `InvalidOperationException` without recursion or partial publication

#### Scenario: Failed singleton is retried
- **WHEN** singleton creation fails and a later Instance access occurs after the cause is corrected
- **THEN** the framework creates a fresh candidate rather than reusing the failed instance

#### Scenario: Published singleton is directly disposed
- **WHEN** the published singleton instance is disposed
- **THEN** its static reference returns to Empty and a future Instance access may create a fresh instance

### Requirement: Operations obey the lifecycle phase
Public Domain operations MUST centralize state, Context, ExecutionDepth, and transition checks. Starting SHALL permit only framework-orchestrated Configure registration and declared initialization Context capabilities. Active SHALL permit consumption and new-key dynamic registration while the tree is idle. Disposing and Disposed SHALL reject public operations except idempotent Dispose.

#### Scenario: Configure attempts lookup
- **WHEN** Configure calls Get, TryGet, CQ, Event, tree mutation, or Dispose
- **THEN** the operation throws `InvalidOperationException`

#### Scenario: Active Domain receives normal operation
- **WHEN** an Active Domain in an idle tree receives Get, CQ, Event, registration, or tree management allowed by its public surface
- **THEN** the operation executes under the relevant category and tree guards

#### Scenario: Operation occurs during transition
- **WHEN** public user code attempts registration, tree mutation, execution, or disposal while its tree is transitioning
- **THEN** the operation is rejected before user-visible state changes

### Requirement: Initialization permits only recoverable Domain operations
Configure MUST only collect categorized registrations. Model initialization MUST only use Utility lookup. System initialization MUST only use Model/Utility lookup and owned local event registration. Component initialization MUST reject nested registration, tree mutation, disposal, Command, Query, Event sending, and System lookup, including when dynamic registration occurs in an Active Domain.

#### Scenario: Model reads Utility during initialization
- **WHEN** a Model Initialize callback resolves a registered Utility
- **THEN** the framework returns that Utility if resolution succeeds

#### Scenario: Initializer performs nested registration
- **WHEN** a Model or System Initialize callback calls any Register method
- **THEN** the framework throws `InvalidOperationException` before registering the nested component

#### Scenario: Dynamic System sends during initialization
- **WHEN** a System added to an Active Domain sends a Command, Query, or Event before its Initialize returns
- **THEN** its Initializing Context rejects the operation

### Requirement: Full teardown has defined dependency order
Normal subtree teardown MUST release children in reverse-attachment postorder. Each Domain MUST then enter Disposing, invalidate Context capabilities, invoke OnDeactivating, release Systems in reverse successful activation order, release Models in reverse successful activation order, clear local events, clear Utility references without disposing Utilities, unlink relationships, and enter Disposed. Each System MUST be unpublished and have owned event tokens canceled before Release; each Model MUST be unpublished before Release.

#### Scenario: Components and children are released in order
- **WHEN** children, Systems, and Models were attached or activated in a known sequence
- **THEN** later children finish before earlier siblings, all children finish before their parent, Systems finish before Models, and each category uses reverse successful activation order

#### Scenario: Release tries to use Context
- **WHEN** OnDeactivating or a component Release callback invokes a saved Context
- **THEN** the call fails because release-time Context capabilities no longer exist

### Requirement: Teardown is exhaustive and terminal despite failures
The Domain MUST attempt every targeted child, lifecycle component, event clear, Utility clear, relationship unlink, and derived cleanup hook even when earlier work fails. Structural and terminal state updates MUST run in `finally`. One failure SHALL propagate with its original stack; multiple failures SHALL be flattened in occurrence order into AggregateException. A component whose Release fails MUST remain permanently consumed.

#### Scenario: Multiple cleanup stages fail
- **WHEN** child, OnDeactivating, component, or final cleanup stages throw
- **THEN** all remaining cleanup stages run, every targeted Domain becomes Disposed and unlinked, and the caller receives every failure in deterministic order

#### Scenario: Creation and cleanup both fail
- **WHEN** a creation hook throws and one or more failure-cleanup steps also throw
- **THEN** AggregateException contains the original creation failure first and cleanup failures afterward

#### Scenario: A lifecycle callback throws an empty aggregate
- **WHEN** Configure, Initialize, OnActivated, or a cleanup callback throws an AggregateException with no inner exceptions
- **THEN** the exception itself MUST remain a failure, including inside nested aggregates; cleanup continues and the original empty aggregate is propagated alone or retained in the ordered failure list

### Requirement: Framework cleanup excludes external side effects
Creation failure MUST clean framework-owned registry entries, component lifecycle resources, owned local event subscriptions, tree relations, and singleton candidate state. It MUST NOT claim to roll back Utility calls, already executed event handlers, I/O, or other external business side effects.

#### Scenario: OnActivated performs external work and then fails
- **WHEN** OnActivated sends an event or performs I/O before throwing
- **THEN** the Domain is disposed and Create fails, but the framework does not claim to reverse the completed external effect

### Requirement: Domain creation uses explicit factories
Concrete non-singleton Domains MUST use non-public constructors and concrete static Create methods that call a protected factory-based `CreateDomain`. Creation MUST NOT rely on a public `new()` constraint or reflection, and it MUST support constructor parameters.

#### Scenario: Parameterized Domain is created
- **WHEN** a concrete Domain Create method passes a parameter-capturing factory to CreateDomain
- **THEN** constructor parameters are available before Configure and only a fully Active Domain is returned

#### Scenario: Caller attempts direct construction
- **WHEN** external code tries to invoke the concrete Domain constructor
- **THEN** normal C# accessibility prevents obtaining an unstarted Domain

### Requirement: Startup collects before initialization
Starting MUST invoke Configure exactly once, make collected Utilities available, initialize all Models in registration order, then initialize all Systems in registration order. Lifecycle components MUST publish only after their own Initialize succeeds. OnActivated MUST run after state becomes Active and the creation transition lock is cleared.

#### Scenario: System is registered before Model in Configure
- **WHEN** Configure registers a System before a Model
- **THEN** every Model still initializes before any System

#### Scenario: OnActivated uses Active capability
- **WHEN** OnActivated registers a new component, executes CQ, sends an Event, or changes the tree
- **THEN** the operation follows normal Active rules because creation transition has already ended

#### Scenario: OnActivated disposes its Domain
- **WHEN** OnActivated leaves the candidate in a state other than Active
- **THEN** Create throws and never returns or publishes that candidate
