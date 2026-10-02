## ADDED Requirements

### Requirement: Event hub is a caller-owned shared Utility
The framework MUST provide `IEventHub : IUtility, IDisposable` with `Publish<T>(T message)` and `Subscribe<T>(Action<T> handler)`, and a default implementation `EventHub`. A hub SHALL be owned by the caller: Domains MUST NOT dispose it, and the same instance MAY be registered in multiple Domains. Its scope SHALL follow normal Utility lookup, so a hub registered in a root Domain is shared by attached descendants and a hub registered in a child shadows the parent's hub.

#### Scenario: Hub shared through the tree
- **WHEN** a hub is registered in a root Domain and a System in an attached child publishes an event
- **THEN** subscribers in the root and in every other attached Domain that resolves the same hub receive it

#### Scenario: Child hub shadows parent hub
- **WHEN** both a parent and its attached child register a hub
- **THEN** components of the child publish to and subscribe on the child's hub only

#### Scenario: Domain disposal does not dispose the hub
- **WHEN** a Domain that registered a hub is disposed
- **THEN** the hub remains usable by its other holders

### Requirement: Hub dispatch matches Domain event semantics
Publishing MUST dispatch synchronously in registration order using a copy-on-write subscription array. A handler registered during dispatch MUST NOT run in that dispatch, a subscription canceled during dispatch MUST be skipped for the rest of that dispatch, and the first handler exception MUST stop later handlers and propagate unchanged. Unregistration tokens MUST be idempotent. After `Dispose`, every subscription MUST be inactive, `Dispose` MUST be idempotent, and `Publish` and `Subscribe` MUST throw `ObjectDisposedException`.

#### Scenario: Handler unsubscribes a later handler during publish
- **WHEN** handler A cancels handler B's subscription during a publish in which B has not run yet
- **THEN** B does not run in that publish

#### Scenario: Hub is disposed
- **WHEN** the hub owner disposes the hub and then publishes
- **THEN** `ObjectDisposedException` is thrown and no handler runs

### Requirement: Publish and subscribe follow the capability matrix
`PublishEvent` MUST be available exactly where local `SendEvent` is: as extension methods on `ICanSendEvent`, as protected methods on `AbstractModel` and `AbstractSystem`, and on `CommandContext`. `SubscribeEvent` MUST be available exactly where local `RegisterEvent` is: as an extension method on `ICanRegisterEvent` and as a protected method on `AbstractSystem`. `QueryContext` MUST NOT expose either. Both SHALL resolve `IEventHub` through the caller's Domain using normal Utility lookup; when no hub is found they MUST throw `KeyNotFoundException` whose message states that an `IEventHub` must be registered.

#### Scenario: Controller subscribes and a Command publishes
- **WHEN** an IController subscribes with `this.SubscribeEvent<GameOver>(...)` and a Command calls `context.PublishEvent(new GameOver())` in a Domain that resolves the same hub
- **THEN** the controller's handler runs once

#### Scenario: Hub is not registered
- **WHEN** a System calls `PublishEvent` and no hub is resolvable from its Domain
- **THEN** `KeyNotFoundException` is thrown with a message telling the user to register `IEventHub`

#### Scenario: Query attempts to publish
- **WHEN** query code is written against QueryContext
- **THEN** no PublishEvent or SubscribeEvent API is available

### Requirement: System-owned subscriptions are canceled on release
Subscriptions created through `AbstractSystem.SubscribeEvent`, and any `IUnRegister` passed to `UnRegisterOnRelease(this IUnRegister, AbstractSystem)`, MUST be canceled when that System is released, before its `OnRelease` runs. Canceling the token returned by `SubscribeEvent` or `UnRegisterOnRelease` early SHALL also stop tracking it; canceling the original token passed to `UnRegisterOnRelease` SHALL stop the handler, but its tracking entry MAY remain until release, so callers that cancel early MUST use the returned token. Calling `SubscribeEvent` or `UnRegisterOnRelease` for a System that is not currently bound to a Domain, or that is executing its release, MUST throw `InvalidOperationException` without leaving a subscription behind. If canceling one token fails, the remaining tokens MUST still be canceled and `OnRelease` MUST still run; all failures SHALL be reported together. When initialization fails, its owned subscriptions MUST be canceled and the original initialization exception MUST be reported first. Extension-method subscriptions made by other callers, such as Controllers, SHALL remain caller-managed.

#### Scenario: Child Domain is disposed while hub lives on
- **WHEN** a System in a child Domain subscribed through `SubscribeEvent` and the child is disposed while the hub stays registered in the root
- **THEN** later publishes on the hub no longer invoke that System's handler

#### Scenario: Parent BindableProperty subscription follows the System
- **WHEN** a child System subscribes to a parent Model's BindableProperty and calls `UnRegisterOnRelease(this)` on the token, and the child Domain is disposed
- **THEN** later changes to the property no longer invoke the handler

#### Scenario: Subscribing during release
- **WHEN** a System calls `SubscribeEvent` or `UnRegisterOnRelease` from its OnRelease
- **THEN** `InvalidOperationException` is thrown and no handler remains subscribed after release

#### Scenario: A token fails to cancel
- **WHEN** one owned token throws while being canceled and OnRelease also throws
- **THEN** every other owned subscription is still canceled and both failures are reported in order

#### Scenario: Early cancellation
- **WHEN** a System cancels a token returned by `SubscribeEvent` before it is released
- **THEN** the handler stops receiving events and release does not fail
