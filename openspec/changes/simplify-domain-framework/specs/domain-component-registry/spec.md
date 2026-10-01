## MODIFIED Requirements

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

## ADDED Requirements

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

## REMOVED Requirements

### Requirement: System and Model instances have exclusive terminal ownership
**Reason**: The global ownership table guards a rare misuse at the cost of hidden global state and lookup overhead.
**Migration**: Duplicate registration in one Domain is still rejected by the registry, and `AbstractModel`/`AbstractSystem` reject repeated initialization. Direct implementations manage their own reuse.

### Requirement: Lifecycle components publish only after successful initialization
**Reason**: Two-phase candidate publication made System-to-System lookup during initialization impossible.
**Migration**: Components are visible as soon as they are registered. Cache references in OnInitialize but do not depend on another System having finished initialization.

### Requirement: Active registration adds only a new key atomically
**Reason**: Dynamic registration is removed; component sets are fixed by Configure.
**Migration**: Model dynamic feature sets as child Domains created and attached at runtime.

### Requirement: Dynamic registration may change assignable resolution
**Reason**: Without dynamic registration the registry no longer changes after Configure.
**Migration**: None required.
