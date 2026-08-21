## Context

`AbstractDomain` already owns a categorized component registry, a local `EventBus`, a top-level initialization transaction, and lifecycle state guards. The missing invariant is that these mechanisms do not share a single ownership model: successful System subscriptions are detached from their System entry, nested registration failures only roll back when they escape the outermost initializer, and `IDomainConfigurable` conflates persistent lifecycle ownership with repeatable Command/Query execution context.

The framework remains single-threaded and lightweight. `IDomain`, `ISystem`, and `IModel` are public extension points; consumers may implement them directly instead of inheriting the abstract defaults. Source compatibility for direct implementations is not required, but that extensibility capability must remain.

## Goals / Non-Goals

**Goals:**

- Give local subscriptions created through a System's event-registration capability the same terminal ownership as that System. Models remain unable to register events through a component capability.
- Make every top-level lifecycle registration all-or-nothing for framework-local effects.
- Represent lifecycle Domain ownership as a one-shot public contract, separate from repeatable execution-context injection.
- Make standard `AbstractDomain` parent changes exception-atomic.
- Give `BindableProperty<T>` deterministic behavior when a listener attempts a reentrant write.
- Preserve direct custom implementations of the public Domain and component interfaces.

**Non-Goals:**

- Splitting `IDomain` into narrow capability interfaces or making Query runtime-enforced read-only.
- Migrating an active System or Model between Domains.
- Rolling back global events, Utility side effects, I/O, or other external work.
- Making Domain or BindableProperty thread-safe.
- Reorganizing public namespaces or refactoring `AbstractDomain` solely to reduce line count.

## Decisions

### System subscriptions use explicit ownership paths

`DomainComponentEntry` will own a collection of idempotent `IUnRegister` resources. The System extension path (`this.RegisterEvent`) will use an owner-aware registration hook and attach the returned local-event token to the currently registered System entry. `AbstractDomain` will validate that the supplied owner is a locally published System before recording the token. A System is published during `BindDomain`, initialization, and its active lifetime, then unpublished before cleanup; these are exactly the phases in which owner-aware registration is accepted.

If a System-owned subscription is created while a component initialization transaction is active, the same idempotent token will also be enlisted in that transaction. A successful commit discards only the temporary transaction record and leaves System ownership intact. Rollback cancels the token even when the owning System predates the transaction and therefore is not released; later System cleanup may safely attempt the same cancellation again.

The existing direct `domain.RegisterEvent` path remains Domain-scoped and caller-managed. It still participates in initialization rollback when called inside a registration transaction, and every returned token may be cancelled early by the caller. An owner-aware public capability will allow a custom Domain implementation to provide the same System-scoped behavior without inheriting `AbstractDomain`; custom implementations that do not opt into it retain their own `RegisterEvent` semantics.

`AbstractDomain` will return a guarded caller token while retaining the raw EventBus token in component ownership and initialization transactions. The caller token holds only a weak Domain reference, validates the public event-unregistration phase before consuming itself, and then delegates to the raw idempotent token. Framework rollback and cleanup use the raw token directly so they remain exhaustive during guarded lifecycle phases. Wrapping the guard in `CustomUnRegister` is insufficient because that type consumes itself before invoking its callback, which would make a rejected cancellation impossible to retry.

On replacement or teardown, owned subscriptions will be cancelled even if component cleanup throws. Cancellation failures will be aggregated with component cleanup failures. The framework will not infer ownership from `delegate.Target`, because lambdas, closures, and static callbacks make that inference unreliable.

Alternative considered: require every component author to store and unregister tokens manually. Rejected because it preserves the current default footgun and makes successful initialization less safe than failed initialization rollback.

### Nested failure poisons the top-level registration transaction

`InitializationTransaction` will record the first nested registration failure with preserved exception identity. The complete nested `RegisterSystem` or `RegisterModel` call, including duplicate-instance and replacement preflight validation, participates in the shared failure boundary and marks the transaction failed before rethrowing. Catching that exception in an outer initializer does not clear the failed state.

Once poisoned, later framework-local registrations and local subscription enlistment in the same initialization chain will be rejected. At the outer commit barrier, a poisoned transaction will throw the recorded failure and execute the existing exhaustive rollback over all enlisted lifecycle entries, Utilities, and subscriptions. Cleanup failures will be aggregated with the original failure.

Alternative considered: nested savepoints. Rejected because optional recovery can be handled inside a component before its initializer throws, while savepoints require nested journals, partial rollback, and more complicated exception aggregation.

### Lifecycle binding and execution injection use separate contracts

System and Model will use a new public one-shot `IDomainBindable.BindDomain(IDomain)` contract. Command and Query will continue to use repeatable `IDomainConfigurable.SetDomain(IDomain)` injection. `ISystem` and `IModel` will require the binding contract rather than the configurable contract.

`AbstractSystem` and `AbstractModel` will explicitly implement binding, reject null, reject every second call including the same Domain, and throw when `Domain` is read before binding. Binding remains set after initialization failure or release because lifecycle instances are terminal and cannot be reused.

Framework `AbstractCommand` and `AbstractQuery` bases keep repeatable sequential context injection but reject `SetDomain` and `Execute` reentrancy while the same instance is already executing. This prevents nested cross-Domain reuse from overwriting the outer invocation's mutable `Domain` context. Different Command/Query instances may still nest normally, and direct custom implementations remain responsible for equivalent context safety.

The contract remains public so a custom `IDomain` can bind a component and custom `ISystem`/`IModel` implementations can participate without framework base classes. Custom implementations are responsible for honoring the same one-shot rule, just as custom Domain implementations are responsible for all other `IDomain` invariants.

Alternative considered: an internal binding method. Rejected because it would make the public component interfaces unusable from a direct external `IDomain` implementation.

`BindDomain` is externally implemented lifecycle code, so `AbstractDomain` will enter the component-initialization guard before invoking it and leave the guard only after `Initialize` returns. This gives both callbacks the same restrictions without changing the binding failure boundary: cleanup is invoked only after initialization actually starts, while resources created during binding are still released through the published component entry. Queries, nested new-key registration, and owner-aware System subscriptions remain allowed by their existing guard rules.

### Parent changes detach before commit

`SetParent` will validate the candidate and prepare its weak reference before mutating relationships. When the old parent is an `AbstractDomain` owner, an internal detach path will validate the old parent's phase and remove its strong child reference without recursive public calls. For another `IDomain` implementation, `RemoveChild` will run before the new parent reference is committed.

If old-parent removal fails, the standard child retains its old parent reference. The framework cannot make arbitrary third-party `RemoveChild` implementations internally atomic, but it will no longer pre-commit its own new reference before invoking them.

### BindableProperty rejects different-value notification reentrancy

Every public `BindableProperty<T>` write path will compare the requested value first. An equal reentrant assignment remains a no-op. A different value requested through either `Value` or `SetValueWithoutNotify` while listeners are being notified will throw `InvalidOperationException` before calling `SetValue`. Notification state will be reset in `finally`, including when any listener throws.

Sequential writes after a setter completes and writes to other properties remain valid. Listener exceptions continue to stop the current multicast dispatch; per-listener exception isolation is outside this change.

Alternative considered: queue reentrant writes. Rejected because a correct queue must defer the write itself, then define comparison, coalescing, and exception behavior for pending values.

### Query remains conventionally read-only

Public documentation will replace claims that Query “guarantees” no mutation with wording that Query represents a conventionally read-only operation. `IDomainAccessible.Domain` and mutable Model references remain available. Enforced read-only views would require a separate architectural change.

### Runtime execution defers terminal ownership changes by rejection

`AbstractDomain` will maintain one synchronous execution-depth counter around both local-event overloads, both Command overloads, and Query execution. Each public entry validates its existing phase rule, increments before invoking EventBus or the overridable execution hook, and decrements in `finally`. `UnInitialize` rejects while the counter is nonzero, so teardown cannot release components, relationships, or the EventBus while callback snapshots or Command/Query code are still running. Nested synchronous execution remains valid because the counter is depth-based rather than a boolean.

Alternative considered: queue a teardown request until the outermost execution returns. Rejected because it requires new rules for repeated requests, callback exceptions, nested dispatch, and teardown error propagation. Callers can explicitly schedule or invoke teardown after the public execution method returns.

The same execution counter will reject replacement only when an existing System or Model lifecycle key would be released. Before replacement, a standard `AbstractDomain` will recursively inspect its owned standard descendants as well as itself, because descendant Command/Query code can resolve and retain ancestor components through parent lookup. New-key lifecycle registration and Utility replacement remain available. This avoids released callbacks from EventBus snapshots and prevents Command/Query code from releasing a component that is still on its call stack without adding listener-state tracking or deferred replacement queues.

Relationship mutation will use the same owned-tree execution preflight. Otherwise executing code could first detach its Domain from the ownership tree and then bypass ancestor replacement or teardown checks while still retaining inherited components. Callers that need to reparent or remove a Domain after a Command/event/Query will perform that operation after the outermost synchronous send returns.

### Standard owned trees preflight teardown recursively

Before `CleanupCore` changes the parent state, `AbstractDomain.UnInitialize` will recursively inspect only owned descendants that are also `AbstractDomain`. The check reads their lifecycle state plus execution, component-initialization, and component-release depths and throws before mutation when any descendant is busy, including a descendant that has entered terminal cleanup but still owns running cleanup callbacks. Only an already-disposed descendant may be skipped. The public interfaces and custom Domain implementation model stay unchanged; arbitrary `IDomain` children remain best-effort because their internal readiness cannot be observed without adding another public capability.

Alternative considered: add a public teardown-readiness interface for custom Domains. Rejected for this change because it expands the public lifecycle protocol to solve a guarantee that the framework can enforce internally for its standard implementation.

Terminal cleanup reentrancy remains an idempotent no-op once the Domain state is `Uninitializing` or `Disposed`. A teardown request made during active component replacement or rollback release is different: the Domain has not entered terminal cleanup, so silently returning would falsely imply that the request completed. `UnInitialize` will reject that active release phase before mutation.

### Guarded component phases propagate to standard ancestors

An `AbstractDomain` operation that is forbidden during its own Domain/component initialization or component release will also inspect standard descendants it owns before executing. This prevents a descendant callback from escaping its transaction or cleanup guard by invoking the corresponding API on an ancestor whose local depth counter is zero. In particular, ancestor component/event registration, Command/event execution, Query execution during release, and relationship mutation will reject while an owned descendant is in the corresponding guarded phase. Event unregistration keeps its existing release-phase behavior, while initialization-phase unregistration remains rejected.

The check is a recursive read-only preflight and does not coordinate transactions across Domains. Lookup-only and arbitrary custom `IDomain` relationships remain the caller's responsibility because the ancestor has no ownership traversal or observable internal phase for them.

Alternative considered: enlist ancestor mutations into a descendant transaction. Rejected because that would require a distributed transaction across Domain registries; simple rejection preserves atomicity without cross-Domain journals or savepoints.

## Risks / Trade-offs

- [Direct custom component implementations can violate one-shot binding] → Document the public contract and test the framework base implementations plus a compliant direct implementation; custom interface implementations are inherently responsible for their contract.
- [System-owned cancellation can surface new cleanup exceptions] → Aggregate cancellation and lifecycle cleanup failures while continuing all cleanup work.
- [Previously tolerated reentrant binding code now throws] → Treat this as an intentional breaking safety change and document non-reentrant alternatives.
- [A caught optional nested-registration failure now aborts the outer registration] → Document that optional behavior must absorb failures before an initializer throws or be decided before registration begins.
- [Owner-aware event registration adds another public extension concept] → Keep direct Domain registration unchanged and isolate ownership routing in the existing System extension API.
- [Caller-held guarded tokens could retain a released Domain] → Hold the Domain weakly while retaining the raw EventBus cancellation token.
- [Synchronous callbacks can no longer tear down their Domain immediately] → Reject with a clear lifecycle exception and require teardown after the outermost send call returns.
- [Custom owned Domains cannot participate in internal recursive preflight] → Preserve their current best-effort cleanup contract and document the standard-implementation guarantee.

## Migration Plan

1. Add the public one-shot binding contract and update framework lifecycle interfaces and base classes.
2. Update direct repository implementations and tests to use `BindDomain`; external direct implementations must make the analogous source change.
3. Add System-owned subscription tracking and transaction poison behavior without changing Domain-scoped `RegisterEvent` usage.
4. Apply parent atomicity and BindableProperty reentrancy guards.
5. Update XML documentation and lifecycle documentation, then run focused and full solution tests.

Rollback is a source revert; there is no persisted data or external deployment migration.

## Open Questions

None. The behavior and compatibility boundaries were resolved during exploration.
