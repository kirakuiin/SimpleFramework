## Purpose

Define categorized single-key registration, exclusive lifecycle ownership, delayed publication, and exact, assignable, and parent component resolution for Domain v2.
## Requirements
### Requirement: Domain registration is categorized and single-keyed
The Domain MUST maintain separate System, Model, and Utility categories. Each local registration SHALL create one entry with one primary key and one instance. Non-generic Register MUST use the instance runtime type as the primary key. Explicit generic Register MUST use the supplied contract type and validate assignability before changing state. An instance MUST NOT occupy multiple local keys or categories, a primary key MUST NOT be registered twice in one category, and a runtime type that belongs to multiple component categories MUST be rejected.

#### Scenario: Concrete Model registration ignores variable static type
- **WHEN** RegisterModel is called without an explicit contract for a PlayerModel instance
- **THEN** the primary key is PlayerModel regardless of the variable's declared interface type

#### Scenario: Explicit service contract is registered
- **WHEN** `RegisterModel<IPlayerModel>(player)` is called and player implements IPlayerModel and IModelLifecycle
- **THEN** one Model entry is created with IPlayerModel as its primary key and no implicit aliases

#### Scenario: Explicit contract is incompatible
- **WHEN** explicit generic registration names a contract not implemented by the instance
- **THEN** it throws `ArgumentException` before the instance becomes visible to lookup

#### Scenario: Primary key is registered twice
- **WHEN** Configure registers a second Model under a primary key that already exists in the Domain
- **THEN** registration throws `InvalidOperationException` and the first entry remains unchanged

#### Scenario: Same instance is registered twice
- **WHEN** Configure registers one instance under two different keys or categories
- **THEN** the second registration throws `InvalidOperationException`

### Requirement: Component lookup uses exact then assignable resolution
Lookup MUST search only the requested category. It SHALL return a current-Domain exact primary-key match first; otherwise it SHALL return the single distinct current-Domain instance assignable to the requested type. Only if no local candidate exists SHALL lookup continue at the parent Domain. Exact-key lookup MUST use a dictionary; v2 SHALL NOT require an assignable-result cache.

#### Scenario: Interface resolves concrete registration
- **WHEN** PlayerModel is registered under its concrete key and is the only local Model implementing IPlayerModel
- **THEN** GetModel<IPlayerModel> returns that same PlayerModel instance

#### Scenario: Explicit key avoids fallback scan result
- **WHEN** IPlayerModel is registered as the exact key
- **THEN** GetModel<IPlayerModel> resolves the dictionary entry before assignable scanning or parent fallback

### Requirement: Ambiguous assignable lookup is explicit
If an exact key is absent and more than one distinct local entry in the requested category is assignable to the requested type, Get and TryGet MUST throw a clear `InvalidOperationException`. The message MUST identify the requested category, requested type, Domain, and candidate keys and runtime types.

#### Scenario: Multiple Models implement one contract
- **WHEN** two local Model entries implement IPlayerModel and no exact IPlayerModel key exists
- **THEN** GetModel<IPlayerModel> throws an ambiguity InvalidOperationException containing both candidates

#### Scenario: TryGet encounters ambiguity
- **WHEN** TryGetModel<IPlayerModel> encounters the same ambiguity
- **THEN** it throws rather than returning false or choosing a candidate

### Requirement: Parent fallback preserves local precedence
Parent lookup MUST occur only when local resolution has neither an exact nor an assignable candidate. A local exact or unique assignable candidate SHALL shadow every parent candidate, and local ambiguity MUST throw before consulting the parent. Parent fallback SHALL be available only after Domain attachment.

#### Scenario: Parent supplies a missing component after attachment
- **WHEN** an attached child has no compatible local Model and the parent has one
- **THEN** child Model lookup returns the parent instance

#### Scenario: Local assignable component shadows parent exact component
- **WHEN** the child has one assignable Model and the parent has an exact primary-key match
- **THEN** child lookup returns the local instance

#### Scenario: Independent initialization has no parent fallback
- **WHEN** an unattached child initializes before AddChild
- **THEN** lookup cannot resolve components from its future parent

### Requirement: Utility registration is caller-managed
Utility entries MUST participate in categorized lookup but MUST receive no framework initialization or cleanup callback. A pure Utility instance MAY be registered in multiple Domains because the caller owns its lifetime. An object whose runtime type also belongs to Model or System lifecycle categories MUST be rejected as a Utility to prevent conflicting ownership.

#### Scenario: Pure Utility is shared
- **WHEN** the same pure Utility instance is registered in two Domains
- **THEN** both Domains resolve it and disposing either Domain only clears its reference

#### Scenario: Lifecycle object is presented as Utility
- **WHEN** an instance implements IUtility and IModelLifecycle or ISystemLifecycle
- **THEN** Utility registration throws before publishing the instance

### Requirement: Registration and lookup keep component marker constraints
System contracts used for registration or lookup MUST inherit ISystem, Model contracts MUST inherit IModel, and Utility contracts MUST inherit IUtility. Lifecycle eligibility MUST be expressed separately by ISystemLifecycle or IModelLifecycle on the concrete instance. The lifecycle interfaces SHALL carry the role's capability rule interfaces, while business contracts that inherit only the category markers MUST NOT expose lifecycle methods or capability extensions to their consumers.

#### Scenario: Business Model contract participates in lookup
- **WHEN** `IPlayerModel : IModel` and PlayerModel implements both IPlayerModel and IModelLifecycle
- **THEN** registration and lookup can use IPlayerModel without exposing Initialize, Release, GetUtility, or SendEvent through the business contract

### Requirement: Get and TryGet have distinct missing behavior
Every Get operation MUST throw `KeyNotFoundException` when the complete local and parent lookup chain has no candidate. TryGetModel, TryGetSystem, and TryGetUtility SHALL exist only on `IDomain`; they MUST return false and assign null only for the missing case. Null arguments, ambiguity, and disposed access MUST retain their own exceptions. Base classes, capability rule extensions, CommandContext, and QueryContext MUST expose only Get operations.

#### Scenario: Required component is missing
- **WHEN** GetUtility<IOptionalUtility> finds no compatible Utility in the lookup chain
- **THEN** it throws `KeyNotFoundException` with category, type, and Domain information

#### Scenario: Optional component is missing
- **WHEN** `IDomain.TryGetUtility<IOptionalUtility>` finds no compatible Utility
- **THEN** it returns false and sets the output to null

### Requirement: Registration is limited to Configure and immediately visible
Register methods MUST be callable only while the framework is running Configure; any other call MUST throw `InvalidOperationException`. A registered component SHALL become visible to lookup as soon as Register returns, before Model or System initialization runs. Lookup during initialization MAY therefore return a component whose Initialize has not run yet.

#### Scenario: Registration outside Configure
- **WHEN** code calls RegisterModel on an Active Domain
- **THEN** it throws `InvalidOperationException` and the Domain is unchanged

#### Scenario: System obtains a later-registered System during initialization
- **WHEN** System A registered before System B calls GetSystem<B> from its OnInitialize
- **THEN** lookup returns the registered B instance even though B has not been initialized yet

### Requirement: Framework base classes reject repeated initialization
`AbstractModel` and `AbstractSystem` MUST throw `InvalidOperationException` when Initialize is called while they are already bound to a Domain, and MUST clear the binding after Release. Direct lifecycle implementations SHALL be responsible for their own reuse rules.

#### Scenario: Base-class Model is registered in a second Domain
- **WHEN** an AbstractModel instance that is still active in one Domain is registered in another Domain's Configure
- **THEN** creation of the second Domain fails with `InvalidOperationException` and the first Domain is unaffected

