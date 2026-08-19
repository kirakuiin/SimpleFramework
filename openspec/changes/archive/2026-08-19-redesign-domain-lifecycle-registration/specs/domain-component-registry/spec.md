## ADDED Requirements

### Requirement: Domain registration is categorized and single-keyed
The Domain MUST maintain separate System, Model, and Utility categories. Each local registration SHALL create one entry with one primary key and one instance, and an instance MUST NOT occupy multiple local keys or categories. `Register*As<T>` SHALL use `typeof(T)` as the explicit primary key; non-`As` registration SHALL use its inferred generic type.

#### Scenario: Concrete Model registration
- **WHEN** `RegisterModel(new Player())` is called
- **THEN** one Model entry is created with `Player` as its primary key

#### Scenario: Explicit service registration
- **WHEN** `RegisterModelAs<IPlayer>(player)` is called
- **THEN** one Model entry is created with `IPlayer` as its primary key and no implicit aliases

#### Scenario: Same instance is registered twice locally
- **WHEN** an instance already has a local entry and registration is attempted under another key or category
- **THEN** registration throws `InvalidOperationException` without changing the existing entry

### Requirement: Component lookup uses exact then assignable resolution
Lookup MUST search only the requested category. It SHALL return an exact primary-key match first; otherwise it SHALL return the single distinct local instance whose runtime type is assignable to the requested type. If no local candidate exists, resolution SHALL continue at the parent Domain.

#### Scenario: Concrete registration is requested through an interface
- **WHEN** `Player` is registered as a Model under its concrete key and it is the only local Model implementing `IPlayer`
- **THEN** `GetModel<IPlayer>()` returns that `Player`

#### Scenario: Exact key disambiguates compatible instances
- **WHEN** multiple local Models implement `IPlayer` but one is registered with primary key `IPlayer`
- **THEN** `GetModel<IPlayer>()` returns the exact-key entry

#### Scenario: Runtime role does not leak across categories
- **WHEN** an object implementing multiple component interfaces is registered only as a Utility
- **THEN** System and Model lookup cannot resolve it

### Requirement: Ambiguous assignable lookup is explicit
If an exact key is absent and more than one distinct local entry in the requested category is assignable to the requested type, lookup MUST throw a public dedicated exception derived from `InvalidOperationException`. The exception SHALL expose `RequestedType` and a read-only list of `CandidateKeys`, and its message SHALL identify the category.

#### Scenario: Multiple Models implement one service interface
- **WHEN** two Model entries with different primary keys implement `IPlayer` and no exact `IPlayer` key exists
- **THEN** `GetModel<IPlayer>()` throws the dedicated ambiguity exception with `IPlayer` and both candidate keys

#### Scenario: TryGet encounters ambiguity
- **WHEN** `TryGetModel<IPlayer>` encounters multiple compatible local candidates
- **THEN** it throws the same ambiguity exception rather than returning false or choosing one candidate

### Requirement: Parent fallback preserves local precedence
Parent lookup MUST occur only when local resolution has neither an exact nor an assignable candidate. A local exact or unique assignable candidate SHALL shadow every parent candidate, and local ambiguity MUST throw before consulting the parent.

#### Scenario: Parent supplies a missing component
- **WHEN** the child has no compatible Model and the parent has one
- **THEN** child Model lookup returns the parent's instance

#### Scenario: Local assignable component shadows parent exact component
- **WHEN** the child has one assignable Model and the parent has an exact primary-key match
- **THEN** child lookup returns the local instance

#### Scenario: Local lookup is ambiguous
- **WHEN** the child has multiple assignable Models and the parent has an exact match
- **THEN** child lookup throws the local ambiguity exception without returning the parent's instance

### Requirement: System and Model instances have exclusive terminal ownership
Once initialization of a System or Model begins, that object MUST belong to exactly one Domain, one lifecycle category, and one primary key. Registration in another Domain, key, or lifecycle category MUST fail. After successful or failed cleanup, the object MUST remain permanently ineligible for lifecycle registration.

#### Scenario: Active component is registered in another Domain
- **WHEN** a System or Model already owned by one Domain is registered in another
- **THEN** the second registration throws `InvalidOperationException` without changing either Domain

#### Scenario: Dual-role lifecycle object is registered in both categories
- **WHEN** an object implementing both `ISystem` and `IModel` has begun initialization in one category
- **THEN** registration in the other lifecycle category is rejected

#### Scenario: Released component is reused
- **WHEN** a System or Model is registered after its cleanup succeeded or failed
- **THEN** registration throws `InvalidOperationException` before initialization runs

#### Scenario: Candidate was never initialized
- **WHEN** replacement cannot start because old-component cleanup failed
- **THEN** the untouched new candidate remains eligible for a later registration

### Requirement: Utility registration is caller-managed
Utility entries MUST participate in categorized lookup and transaction removal but MUST NOT receive System or Model initialization or cleanup callbacks. A Utility instance MAY be registered in multiple Domains because the caller owns its lifetime.

#### Scenario: Shared Utility instance
- **WHEN** the same Utility instance is registered in two Domains
- **THEN** each Domain resolves its local entry and disposing either Domain does not invoke a lifecycle callback on the Utility

### Requirement: Active replacement releases before publishing
Replacing an existing primary key in an active Domain MUST unpublish and clean the old lifecycle component completely before publishing or initializing the new component. The old component MUST never be restored. The key SHALL remain empty if either old cleanup or new initialization fails.

#### Scenario: Successful replacement
- **WHEN** old cleanup and new initialization both succeed
- **THEN** lookup observes the old instance before replacement and only the new instance after replacement, never both

#### Scenario: Old cleanup fails
- **WHEN** old cleanup throws during replacement
- **THEN** the Domain stays active, the key is empty, new initialization does not run, and the cleanup exception propagates

#### Scenario: New initialization fails
- **WHEN** old cleanup succeeds but new initialization throws
- **THEN** the new registration transaction is rolled back, the key remains empty, and the old instance is not restored

### Requirement: Replacement is forbidden inside component initialization
While a component initialization transaction is active, registration under any existing primary key MUST fail before releasing the current entry. Only absent, non-initializing keys may be added to the transaction.

#### Scenario: Nested initializer targets an active key
- **WHEN** a component initializer attempts to replace an existing System, Model, or Utility key
- **THEN** the Domain throws `InvalidOperationException` and leaves the existing entry unchanged

### Requirement: Public Container behavior is unchanged
`FrameworkImpl.Container` MUST retain its public exact-key registration and lookup behavior. Assignable resolution, category separation, ownership, and lifecycle semantics SHALL remain internal to Domain component management.

#### Scenario: Container stores concrete type and interface is requested
- **WHEN** a `Container` stores an instance only under its concrete type and a caller requests an implemented interface
- **THEN** `Container` returns no value for the interface key

### Requirement: Registration and lookup keep component marker constraints
System APIs MUST continue to require `ISystem`, Model APIs MUST continue to require `IModel`, and Utility APIs MUST continue to require `IUtility`. A domain service interface used with Model registration or lookup, such as `IPlayer`, MUST therefore inherit `IModel`.

#### Scenario: Service interface participates in Model lookup
- **WHEN** `IPlayer : IModel` and a compatible Player Model is registered
- **THEN** the existing generic Model APIs can resolve `IPlayer` without a new untyped registration API
