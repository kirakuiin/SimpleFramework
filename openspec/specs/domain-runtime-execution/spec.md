## Purpose

Define synchronous Command and Query contexts, component capability boundaries, tree-scoped execution guards, and Domain-local event subscription lifetimes.
## Requirements
### Requirement: Commands and queries receive non-escaping stack Context
`ICommand`, `ICommand<TResult>`, and `IQuery<TResult>` MUST execute with `readonly ref struct` CommandContext or QueryContext values. The Context types MUST NOT be boxable, heap-storable, captured by asynchronous closures, or usable across `await`. Each Context SHALL wrap an `IDomain` and SHALL expose a public constructor so tests can execute a Command or Query directly against any `IDomain`.

#### Scenario: Command executes synchronously
- **WHEN** a Domain sends an ICommand
- **THEN** the framework passes a stack CommandContext, executes the command synchronously, and returns only after Execute completes

#### Scenario: Command implementation tries to retain Context
- **WHEN** an implementation tries to store CommandContext in a normal object field or capture it for asynchronous use
- **THEN** the C# type system rejects that code

#### Scenario: Test executes a Command directly
- **WHEN** a test constructs `new CommandContext(domain)` and passes it to a Command's Execute
- **THEN** the Command runs against that Domain without calling SendCommand

### Requirement: Command and query capabilities remain distinct
CommandContext MUST expose GetModel, GetSystem, GetUtility, SendEvent, SendCommand, and SendQuery. QueryContext MUST expose only GetModel, GetSystem, and SendQuery. Neither Context SHALL expose TryGet operations. Query expresses read intent but SHALL NOT guarantee that returned objects are immutable or side-effect free.

#### Scenario: Query tries to send event
- **WHEN** query code is written against QueryContext
- **THEN** no SendEvent, SendCommand, or GetUtility API is available

#### Scenario: Query receives mutable model
- **WHEN** QueryContext resolves a Model whose business API contains mutation
- **THEN** the framework does not claim to enforce deep read-only behavior

### Requirement: Domain events are local and ordered
Event registration and sending through a Domain MUST affect only that Domain. Parent fallback MUST NOT apply to events and the framework MUST NOT provide EventBus.Global. Handlers SHALL execute synchronously in registration order.

#### Scenario: Child sends event also registered by parent
- **WHEN** an attached child sends an event whose type also has handlers on its parent
- **THEN** only handlers registered on the child Domain execute

#### Scenario: Explicit shared bus is used
- **WHEN** an application needs cross-Domain events
- **THEN** it must explicitly own and pass a shared Utility or service outside Domain event propagation guarantees

### Requirement: Runtime follows a single-thread cooperation contract
The framework MUST NOT add locking, thread ownership capture, or automatic main-thread dispatch. Callers SHALL serialize Domain and connected-tree operations on one owning thread and marshal background results back before using Domain or Context APIs.

#### Scenario: Background operation completes
- **WHEN** background work produces a result needed by a Domain
- **THEN** application code returns that result to the owning thread before sending a synchronous Command or Event

### Requirement: Capability rule interfaces define component capabilities
The framework MUST provide `IBelongToDomain` with `GetDomain()` and capability rule interfaces `ICanGetModel`, `ICanGetSystem`, `ICanGetUtility`, `ICanSendCommand`, `ICanSendQuery`, `ICanSendEvent`, and `ICanRegisterEvent`, each with extension methods that forward to `GetDomain()`. `IModelLifecycle` SHALL include only `ICanGetUtility` and `ICanSendEvent`. `ISystemLifecycle` SHALL include `ICanGetModel`, `ICanGetSystem`, `ICanGetUtility`, `ICanSendEvent`, and `ICanRegisterEvent`. `AbstractModel` and `AbstractSystem` SHALL expose the same capabilities as protected instance methods so subclasses call them without a `this.` prefix.

#### Scenario: Model code tries to get a System
- **WHEN** code inside an AbstractModel subclass calls GetSystem
- **THEN** the C# compiler rejects it because Models have no System capability

#### Scenario: System reads a Model
- **WHEN** code inside an AbstractSystem subclass calls `GetModel<IPlayerModel>()`
- **THEN** it resolves through the System's Domain using normal lookup rules

#### Scenario: Released component uses a capability
- **WHEN** an AbstractSystem calls a capability after its Release completed
- **THEN** it throws `InvalidOperationException` because the Domain binding was cleared

### Requirement: Controller is a pure capability interface
The framework MUST provide `IController` combining `ICanGetModel`, `ICanGetSystem`, `ICanGetUtility`, `ICanSendCommand`, `ICanSendQuery`, and `ICanRegisterEvent`. Implementers SHALL supply only `GetDomain()`; the framework MUST NOT require a base class, so engine objects such as Godot nodes can implement it.

#### Scenario: View sends a command
- **WHEN** a class implementing IController calls `this.SendCommand(new AttackCommand())`
- **THEN** the command executes on the Domain returned by its GetDomain

### Requirement: Runtime execution is synchronous and reentrant
Command, Query, and Event dispatch SHALL be synchronous and MAY be nested. Tree mutation, child attachment, and Domain disposal MUST be allowed from inside Command, Query, and Event handlers. After a Domain is disposed inside such a handler, any later use of that Domain or its Contexts MUST throw `ObjectDisposedException`. Async Command/Query semantics MUST NOT be provided.

#### Scenario: Command attaches a child Domain
- **WHEN** a Command creates a BattleDomain and calls AddChild on its own Domain
- **THEN** the child is attached and the Command completes normally

#### Scenario: Event handler disposes a Domain
- **WHEN** an event handler disposes a child Domain
- **THEN** disposal completes and SendEvent returns normally

#### Scenario: Command uses its Domain after disposing it
- **WHEN** a Command disposes its own Domain and then calls GetModel through its Context
- **THEN** the call throws `ObjectDisposedException`

### Requirement: Event dispatch uses snapshots and skips inactive subscriptions
Each event type MUST store an immutable current subscription array. Registration and unregistration SHALL replace that array; sending SHALL capture one array reference and iterate it without allocating a snapshot. A handler registered during dispatch MUST NOT run in that dispatch. A subscription that becomes inactive during dispatch, because it was unregistered or its Domain began disposal, MUST be skipped for the remainder of that dispatch.

#### Scenario: Handler is registered during send
- **WHEN** an event handler registers another handler of the same event type
- **THEN** the new handler does not run in the current send and is available on the next send

#### Scenario: Handler unregisters a later handler during send
- **WHEN** handler A unregisters handler B during a dispatch in which B has not run yet
- **THEN** B does not run in that dispatch

#### Scenario: Handler disposes the Domain during send
- **WHEN** handler A disposes the Domain whose event is being dispatched
- **THEN** no later handler of that Domain runs in that dispatch and SendEvent returns normally

### Requirement: Event failures stop dispatch and tokens are idempotent
The first event handler exception MUST stop later handlers and propagate unchanged. Every unregistration token MUST be idempotent and MUST become a harmless no-op after Domain disposal.

#### Scenario: First handler throws
- **WHEN** an earlier handler throws during synchronous event dispatch
- **THEN** later handlers do not run and the same exception propagates

#### Scenario: Token is used after Domain disposal
- **WHEN** an external caller unregisters a token after its Domain was disposed
- **THEN** the call has no effect and does not throw

