## Context

`AbstractDomain` currently coordinates lifecycle, component storage, hierarchy, events, and command/query dispatch using a `Container`, a set of lifecycle keys, and several booleans. Those pieces do not form a complete state machine: singleton creation can reenter, public teardown can run during initialization, one component can acquire multiple lifecycle owners, and teardown callbacks can mutate collections after they have been cleared.

Registration is exact-key only. Supporting both concrete and interface access therefore encourages registering one instance under multiple keys, which in turn complicates replacement, activation, rollback, and cleanup. This change is an intentional v1.1 behavioral cutover for controlled consumers, so correctness and a small coherent core take priority over preserving those accidental semantics.

## Goals / Non-Goals

**Goals:**

- Make every Domain transition explicit, one-shot, and terminal per instance.
- Centralize lifecycle operation guards instead of distributing more boolean checks across public methods.
- Give each component one category and one primary key while still supporting interface/base-type lookup.
- Make initialization rollback and teardown deterministic, including nested registration and local event subscriptions.
- Enforce exclusive ownership and permanent non-reuse for lifecycle-managed component instances.
- Keep the implementation readable and localized to the core Domain, its internal registry, ownership tracking, and focused tests/docs.

**Non-Goals:**

- Changing the public `Container` or its exact-key semantics.
- Removing the `ISystem`/`IModel` constraints from registration or lookup APIs; service interfaces such as `IPlayer` still inherit `IModel`.
- Adding thread safety, a resolution cache, implicit aliases, or multi-key registration.
- Rolling back arbitrary Utility calls, global events, I/O, or other external side effects performed by user code.
- Adding a `DomainStopping` event or a compatibility switch for the old behavior.

## Decisions

### 1. Model Domain lifecycle with one private state

`AbstractDomain` will replace lifecycle booleans with a private `DomainState` value: `Created`, `Initializing`, `Active`, `Uninitializing`, and `Disposed`. A small set of centralized guard methods will define which API families are legal in each state. The state remains an implementation detail; no public `State` or `IsDisposed` API is added.

`Initialize` performs `Created -> Initializing -> Active`. An initialization failure invokes an internal cleanup path, not the public `UnInitialize` entry point, and always ends in `Disposed`. `UnInitialize` is valid from `Created` or `Active`, is a no-op while already `Uninitializing` or `Disposed`, and throws when called from `Initializing`. This prevents a callback from clearing a Domain underneath the still-running initializer.

After disposal, read-only component lookup, `Parent`, and `ToString` remain callable; the registry is empty and the parent is cleared. Registration, relationship mutation, event APIs, commands, and queries reject the operation. This retains useful observation without permitting resurrection.

Alternative considered: expose the state publicly. It was rejected because callers should rely on operation contracts rather than coordinate against framework internals.

### 2. Guard singleton creation without exposing a partial Domain

`AbstractDomain<T>` will assign `_domain` only after `Initialize` succeeds. A per-closed-generic creation-depth guard will make `Instance` throw a clear `InvalidOperationException` whenever an instance of the same `T` is currently initializing, including initialization started through `Create()`. The guard is reset in `finally`, so failed creation leaves no cached instance and a later attempt can create a fresh Domain.

Disposing the cached singleton clears only the static reference; the disposed object itself remains terminal. A later `Instance` access may therefore create a new Domain instance without reactivating the old one.

Alternative considered: cache the new object before initialization so reentrant access returns it. It was rejected because consumers would observe a partially initialized Domain.

### 3. Replace Domain's `Container` bookkeeping with one internal categorized registry

Add an internal `DomainComponentRegistry` owned by each Domain. It stores entries with `Category` (`System`, `Model`, or `Utility`), `PrimaryKey`, `Instance`, and activation metadata. Each category has an independent exact-key index; an instance can have only one local entry. `RegisterSystemAs<T>`, `RegisterModelAs<T>`, and `RegisterUtilityAs<T>` select `typeof(T)` as the primary key. Their non-`As` counterparts continue to use generic inference, normally making the concrete type the key.

The registry owns lookup, identity checks, publication/unpublication, and deterministic activation lists. `AbstractDomain` owns state transitions and invokes component callbacks. This split keeps storage rules independent from orchestration without creating a general dependency-injection abstraction.

The existing public `FrameworkImpl.Container` remains unchanged and is no longer used to infer Domain lifecycle ownership.

Alternative considered: pre-register every implemented interface as an alias. It was rejected because aliases recreate multi-key replacement and ownership bookkeeping and make collisions depend on implementation details.

### 4. Resolve exact keys first, then one assignable local instance

Within the requested category, lookup first checks the exact primary key. If absent, it scans entries whose runtime instance is assignable to the requested type and deduplicates by reference. Zero candidates triggers parent fallback, one returns that instance, and more than one throws a public `AmbiguousComponentException : InvalidOperationException`.

The exception exposes `RequestedType` and a read-only `CandidateKeys`; its message includes the component category. Exact registration therefore remains an explicit way to disambiguate a service, while `RegisterModel(new Player())` can be resolved by `GetModel<IPlayer>()` when it is the only compatible Model.

Local ambiguity is an error and never falls through to the parent. A local exact or unique assignable match shadows the parent. Scanning is intentional for v1.1; caching can be considered only after profiling.

### 5. Track lifecycle ownership by object identity

An internal ownership tracker backed by `ConditionalWeakTable<object, LifecycleRecord>` records lifecycle-managed instances without retaining dead components globally. Each Domain registry owns an opaque token; a record stores that token, selected System/Model category and key, and a lifecycle status such as initializing, active, releasing, or released.

Claiming rejects a component already claimed under another key, category, or Domain. Once initialization has started, cleanup—successful or failed—moves the record permanently to released, and the same instance cannot be registered again. Rejections that occur before initialization starts do not consume the new instance. Utility-only registration does not claim lifecycle ownership and has no callback lifecycle; the same Utility instance may be shared by multiple Domains. Within one registry, identity checks still prevent duplicate local entries or cross-category registration.

Alternative considered: store ownership only in `AbstractSystem` and `AbstractModel`. It was rejected because custom implementations of `ISystem` and `IModel` must obey the same contract.

### 6. Make registration a framework-local transaction

A top-level System/Model registration opens an initialization transaction; nested new-key registrations join it. The transaction records newly published entries and `IUnRegister` handles returned by local event subscriptions. A component is published before its callback so initialization-time lookup works, but its key and instance are marked as initializing.

During a component `Initialize` callback, lookups, queries, local event registration, and nested registrations for absent non-initializing keys are allowed. Commands, local event sending, relationship mutation, replacement of any existing key, registering the same instance again, and initialization cycles are rejected. The initializing key/instance stack provides direct cycle diagnostics.

If the outer callback chain succeeds, lifecycle entries become active and are appended to their category's successful activation order. If it fails, the failing component is unpublished and cleaned while its dependencies remain readable, then the transaction removes later framework-local additions in dependency-safe reverse order. System/Model callbacks run when initialization started; Utility entries are only removed; enlisted event handles are unregistered. Cleanup failures are aggregated with the original initialization error.

Domain `Init` uses the same enlistment mechanism for local event registrations, while a Domain initialization failure additionally invokes full terminal cleanup for children, components, remaining events, and derived Domain state. External side effects are deliberately outside this transaction boundary.

Alternative considered: allow replacement during nested initialization and restore old entries on failure. It was rejected because restoring already-released lifecycle objects violates one-shot ownership and makes rollback non-local.

### 7. Replace an active key by release-first semantics

At runtime in an `Active` Domain, replacing a primary key first unpublishes the old entry and permanently releases it. Only after successful old cleanup does the framework claim, publish, and initialize the new instance. The registry never exposes both instances at once.

If old cleanup throws, the exception propagates, the Domain remains `Active`, the key remains empty, and the untouched new instance remains reusable. If new initialization throws, its transaction is rolled back and the key remains empty; the released old instance is never restored or reinitialized.

### 8. Teardown is ordered, mutation-proof, and exhaustive

Full teardown keeps the Domain in `Uninitializing` for every child callback, component callback, framework clear, `OnDomainCleared`, and derived `UnInit` callback. Mutation and execution guards therefore also apply to derived cleanup hooks and prevent late repopulation.

Teardown processes owned children before local components, then Systems before Models. Within each lifecycle category it uses reverse successful activation order. Before invoking each component's `UnInitialize`, the entry is removed from the registry and marked releasing, so callbacks can read only components that are still live. Utility entries are cleared without lifecycle callbacks; local events and the parent link are cleared before terminal completion.

Every cleanup stage runs even if earlier stages fail. All failures from children, components, framework cleanup hooks, and `UnInit` are collected into one `AggregateException`; state transitions to `Disposed` in `finally`. A reentrant `UnInitialize` during teardown is a no-op. Component cleanup failure still marks that component permanently released.

Alternative considered: rely on dictionary enumeration and clear everything before callbacks. It was rejected because order would be unstable and callbacks could not safely observe remaining dependencies.

## Risks / Trade-offs

- [Assignable lookup can become ambiguous as new implementations are registered] -> Exact primary keys take precedence and the dedicated exception reports every candidate key.
- [A linear fallback scan adds lookup cost] -> Keep v1.1 simple and measurable; add caching only if real profiles justify invalidation complexity.
- [Strict operation guards can break initialization code that currently sends events or commands] -> Document the initialization matrix and migration path; queries and framework-local registrations remain available.
- [Permanent component non-reuse is stricter than current behavior] -> Fail at the registration boundary with ownership context and require callers to construct a fresh lifecycle component.
- [Framework-local rollback cannot undo arbitrary user side effects] -> State the boundary explicitly and require component initializers to compensate their own external effects.
- [Cleanup aggregation changes which exception reaches callers] -> Preserve every failure in deterministic order and document `AggregateException` behavior.

## Migration Plan

1. Add regression tests that capture the lifecycle, lookup, ambiguity, ownership, rollback, replacement, and cleanup contracts before replacing internals.
2. Introduce the internal registry, ownership tracker, ambiguity exception, and state guards; migrate `AbstractDomain` atomically rather than maintaining dual semantics.
3. Update consumer registrations so each instance has one primary key. Prefer concrete registration plus assignable interface lookup, or use `Register*As<TService>` when an exact service key must disambiguate candidates.
4. Replace reused System/Model objects with factories that create a fresh instance for each lifecycle.
5. Move initialization-time commands, event sends, and relationship changes to code that runs after Domain activation; retain only reversible local setup inside initialization.
6. Update XML documentation, README examples, and a v1.1 migration guide, then build and test with the pinned .NET 9 SDK/CI.

Rollback is package/source-level: controlled consumers return to v1.0.0 if a severe issue is found, then the framework ships a v1.1.x correction. There is no runtime compatibility switch.

## Open Questions

None. The behavior required for implementation is fixed by the accompanying capability specs.
