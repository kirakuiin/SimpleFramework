## MODIFIED Requirements

### Requirement: Domain lifecycle is explicit and one-shot
Each Domain instance MUST follow the private lifecycle `Starting -> Active -> Disposing -> Disposed`. Creation failure SHALL perform cleanup and leave the instance Disposed. A Disposed instance MUST NOT return to an earlier state. `Create` and singleton `Instance` MUST return only an Active instance. Dispose MUST be idempotent, including reentrant calls made while the same Domain is Disposing.

#### Scenario: Successful creation
- **WHEN** a newly constructed Domain completes Configure, component initialization, and OnActivated
- **THEN** Create returns that Active instance exactly once

#### Scenario: Creation fails
- **WHEN** Configure, component initialization, or OnActivated throws
- **THEN** the framework releases every successfully initialized component in reverse order, does not release the component whose Initialize failed, invokes OnDeactivating only if the candidate had become Active, leaves the candidate Disposed, and does not return or publish it

#### Scenario: Factory returns an existing Domain
- **WHEN** a CreateDomain factory returns a Domain instance that has already been started
- **THEN** CreateDomain throws `InvalidOperationException` without running cleanup on that instance

#### Scenario: Configure restarts its own Domain
- **WHEN** Configure passes the Domain that is currently starting to CreateDomain again
- **THEN** the inner call throws `InvalidOperationException` instead of recursing, the outer startup fails with that exception, and the Domain ends Disposed

#### Scenario: Disposed operation is attempted
- **WHEN** any public operation other than Dispose is called on a Disposed Domain
- **THEN** it throws `ObjectDisposedException`

#### Scenario: Release callback disposes its own Domain
- **WHEN** a component Release callback calls Dispose on the Domain that is releasing it
- **THEN** the call returns without effect and the original disposal continues

### Requirement: Singleton creation rejects initialization reentrancy
Each `AbstractSingletonDomain<T>` MUST reject access to `Instance` or `DestroyInstance` while T is being created by throwing a clear `InvalidOperationException`; `GetInstance` MUST return null until a created instance is published. Failure SHALL leave no published instance, and only a fully Active candidate SHALL be published.

#### Scenario: Startup accesses Instance of the same type
- **WHEN** Configure, component Initialize, or OnActivated for T accesses T.Instance
- **THEN** access throws `InvalidOperationException` without recursion or partial publication

#### Scenario: Failed singleton is retried
- **WHEN** singleton creation fails and a later Instance access occurs after the cause is corrected
- **THEN** the framework creates a fresh candidate rather than reusing the failed instance

#### Scenario: Published singleton is directly disposed
- **WHEN** the published singleton instance is disposed
- **THEN** its static reference is cleared and a future Instance access may create a fresh instance

### Requirement: Operations obey the lifecycle phase
Registration MUST be allowed only inside Configure. Lookup, Command, Query, SendEvent, and RegisterEvent SHALL be allowed while the Domain is Starting or Active. While Disposing, lookup, Command, Query, and SendEvent SHALL remain allowed so release callbacks can persist state, but RegisterEvent MUST throw `InvalidOperationException` and event subscriptions are already inactive. AddChild, RemoveChild, and Dispose SHALL require an Active Domain, except that Dispose on a Disposing or Disposed Domain is a no-op. Disposed Domains MUST reject every operation except Dispose with `ObjectDisposedException`.

#### Scenario: Model reads Utility during initialization
- **WHEN** a Model's OnInitialize resolves a Utility registered in the same Configure
- **THEN** lookup returns that Utility

#### Scenario: System persists data during release
- **WHEN** a System's OnRelease reads a Model and calls a Utility to save it
- **THEN** both lookups succeed because Models are released after Systems and the registry is cleared last

#### Scenario: Release callback subscribes to an event
- **WHEN** OnDeactivating or a component Release callback calls RegisterEvent
- **THEN** it throws `InvalidOperationException`

### Requirement: Full teardown has defined dependency order
Each Domain MUST enter Disposing before releasing its children, so that reentrant Dispose calls and tree mutations made from descendant release callbacks are a no-op or rejected respectively. Entering Disposing MUST also deactivate all of its local event subscriptions before any child is released. It MUST then release children in reverse-attachment postorder, invoke OnDeactivating only if it had become Active, release Systems in reverse initialization order, release Models in reverse initialization order, clear its registry without disposing Utilities, unlink parent and child relationships, and enter Disposed.

#### Scenario: Components and children are released in order
- **WHEN** children, Systems, and Models were attached or initialized in a known sequence
- **THEN** later children finish before earlier siblings, all children finish before their parent, Systems finish before Models, and each category uses reverse initialization order

#### Scenario: Child release callback disposes its parent
- **WHEN** a child's OnDeactivating or component Release calls Dispose on its parent while the parent is disposing that child
- **THEN** the call is a no-op, OnDeactivating of the parent runs exactly once, and the child finishes before the parent

#### Scenario: Child release callback uses its disposing parent's events
- **WHEN** a child's release callback sends an event to, or subscribes to, its parent while the parent is disposing that child
- **THEN** the sent event reaches no handler and the subscription throws `InvalidOperationException`

#### Scenario: OnActivated throws
- **WHEN** OnActivated throws after the Domain entered Active
- **THEN** OnDeactivating is invoked during the failure cleanup and Create rethrows the original exception

#### Scenario: Event is sent during release
- **WHEN** a component Release callback sends an event of a type that other Systems in the same Domain subscribed to
- **THEN** no handler of the disposing Domain is invoked

### Requirement: Teardown is exhaustive and terminal despite failures
The Domain MUST attempt every targeted child, OnDeactivating, lifecycle Release, registry clear, and relationship unlink even when earlier work fails. Structural and terminal state updates MUST run in `finally`. One failure SHALL propagate with its original stack; multiple failures SHALL be flattened in occurrence order into AggregateException.

#### Scenario: Multiple cleanup stages fail
- **WHEN** child, OnDeactivating, or component Release stages throw
- **THEN** all remaining cleanup stages run, every targeted Domain becomes Disposed and unlinked, and the caller receives every failure in deterministic order

#### Scenario: Creation and cleanup both fail
- **WHEN** a creation hook throws and one or more failure-cleanup steps also throw
- **THEN** AggregateException contains the original creation failure first and cleanup failures afterward

### Requirement: Startup collects before initialization
Starting MUST invoke Configure exactly once, then initialize all Models in registration order, then initialize all Systems in registration order, then enter Active and invoke OnActivated. Every registered component SHALL be resolvable from the moment it is registered.

#### Scenario: System is registered before Model in Configure
- **WHEN** Configure registers a System before a Model
- **THEN** every Model still initializes before any System

#### Scenario: OnActivated uses Active capability
- **WHEN** OnActivated executes a Command or Query, sends an Event, or changes the tree
- **THEN** the operation follows normal Active rules

#### Scenario: OnActivated disposes its Domain
- **WHEN** OnActivated leaves the candidate in a state other than Active
- **THEN** Create throws and never returns or publishes that candidate

## REMOVED Requirements

### Requirement: Initialization permits only recoverable Domain operations
**Reason**: Per-phase Context restrictions blocked common initialization code such as System-to-System lookup and added a three-state Context machine.
**Migration**: Initialization code may use every capability of its role. Do not depend on another System having finished initialization.
