## MODIFIED Requirements

### Requirement: Domain registration is categorized and single-keyed
The Domain MUST maintain separate System, Model, and Utility categories. Each local registration SHALL create one entry with one primary key and one instance. Non-generic Register MUST use the instance runtime type as the primary key. Explicit generic Register MUST use the supplied contract type and validate assignability before changing state. An instance MUST NOT occupy multiple local keys or categories, and a runtime type that belongs to multiple component categories MUST be rejected.

#### Scenario: Concrete Model registration ignores variable static type
- **WHEN** RegisterModel is called without an explicit contract for a PlayerModel instance
- **THEN** the primary key is PlayerModel regardless of the variable's declared interface type

#### Scenario: Explicit service contract is registered
- **WHEN** `RegisterModel<IPlayerModel>(player)` is called and player implements IPlayerModel and IModelLifecycle
- **THEN** one Model entry is created with IPlayerModel as its primary key and no implicit aliases

#### Scenario: Explicit contract is incompatible
- **WHEN** explicit generic registration names a contract not implemented by the instance
- **THEN** it throws `ArgumentException` before reserving, initializing, or publishing the instance

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

### Requirement: System and Model instances have exclusive terminal ownership
Once initialization of a System or Model begins, that object MUST belong to exactly one Domain, one lifecycle category, and one primary key. Registration in another Domain, key, or category MUST fail. Successful or failed Release leaves the object permanently ineligible. A candidate reserved during Configure but never passed to Initialize because Configure failed SHALL become eligible again.

#### Scenario: Active component is registered elsewhere
- **WHEN** a lifecycle component already owned by one Domain is registered in another
- **THEN** the second registration throws `InvalidOperationException` without changing either Domain

#### Scenario: Failed initialization instance is reused
- **WHEN** Initialize began, failed, and failure cleanup attempted Release
- **THEN** later lifecycle registration of that same object is rejected

#### Scenario: Configure-only reservation is released
- **WHEN** Configure fails before a collected lifecycle candidate begins Initialize
- **THEN** that untouched candidate may be registered in a later Domain creation attempt

### Requirement: Utility registration is caller-managed
Utility entries MUST participate in categorized lookup but MUST receive no framework initialization or cleanup callback. A pure Utility instance MAY be registered in multiple Domains because the caller owns its lifetime. An object whose runtime type also belongs to Model or System lifecycle categories MUST be rejected as a Utility to prevent conflicting ownership.

#### Scenario: Pure Utility is shared
- **WHEN** the same pure Utility instance is registered in two Domains
- **THEN** both Domains resolve it and disposing either Domain only clears its reference

#### Scenario: Lifecycle object is presented as Utility
- **WHEN** an instance implements IUtility and IModelLifecycle or ISystemLifecycle
- **THEN** Utility registration throws before publishing the instance

### Requirement: Registration and lookup keep component marker constraints
System contracts used for registration or lookup MUST inherit ISystem, Model contracts MUST inherit IModel, and Utility contracts MUST inherit IUtility. Lifecycle eligibility MUST be expressed separately by ISystemLifecycle or IModelLifecycle on the concrete instance.

#### Scenario: Business Model contract participates in lookup
- **WHEN** `IPlayerModel : IModel` and PlayerModel implements both IPlayerModel and IModelLifecycle
- **THEN** registration and lookup can use IPlayerModel without exposing Initialize or Release through the business contract

## ADDED Requirements

### Requirement: Get and TryGet have distinct missing behavior
Get MUST throw `KeyNotFoundException` when the complete local and parent lookup chain has no candidate. TryGet MUST return false and assign null only for that missing case. Null arguments, ambiguity, invalid phase, and disposed access MUST retain their own exceptions.

#### Scenario: Required component is missing
- **WHEN** GetUtility<IOptionalUtility> finds no compatible Utility in the lookup chain
- **THEN** it throws `KeyNotFoundException` with category, type, and Domain information

#### Scenario: Optional component is missing
- **WHEN** TryGetUtility<IOptionalUtility> finds no compatible Utility
- **THEN** it returns false and sets the output to null

### Requirement: Lifecycle components publish only after successful initialization
Model and System entries MUST remain private candidates while Initialize runs and SHALL become visible only after success. Release MUST cancel publication before invoking the component callback. Utilities collected during Configure SHALL become available before Model initialization.

#### Scenario: Model initialization requests itself
- **WHEN** a Model Initialize callback requests the key currently being initialized
- **THEN** lookup does not observe that unpublished candidate

#### Scenario: Initialization succeeds
- **WHEN** lifecycle Initialize returns successfully
- **THEN** the entry is published exactly once and later lookup returns it

#### Scenario: Release callback performs external lookup
- **WHEN** a component has been selected for Release
- **THEN** it is already absent from its Domain Registry and its saved Context has no capability

### Requirement: Active registration adds only a new key atomically
An Active Domain in an idle tree MAY register one component under an absent key. Lifecycle initialization MUST finish before publication. Failure SHALL clean the candidate and preserve all pre-existing registration bindings and lifecycle ownership. This guarantee covers the candidate and its owned subscriptions; it does not roll back business-state changes or subscriptions created through other objects. Existing keys, replacement, removal, and batch rollback MUST NOT be supported.

#### Scenario: Active new Model succeeds
- **WHEN** an Active idle Domain registers a lifecycle Model under an absent key and Initialize succeeds
- **THEN** the Model becomes immediately available after Register returns

#### Scenario: Active new System fails
- **WHEN** an Active registration candidate throws during Initialize
- **THEN** the framework attempts its Release, permanently consumes that candidate, leaves the key absent, and preserves all older entries

#### Scenario: Active key already exists
- **WHEN** Register targets any existing primary key
- **THEN** it throws `InvalidOperationException` without releasing or replacing the current instance

#### Scenario: Failed candidate indirectly creates another System's subscription
- **WHEN** a dynamic candidate's Initialize calls an existing System's business method that registers through that System's Context, and the candidate then fails
- **THEN** the subscription remains owned by the existing System and is not automatically canceled by candidate cleanup; a caller that needs candidate-scoped cancellation must retain the token and cancel it during candidate Release

### Requirement: Dynamic registration may change assignable resolution
Adding a new entry MAY change a previously unique assignable request into an ambiguity. The framework MUST apply current Registry contents on each non-exact lookup and MUST NOT preserve an earlier successful answer unless a future versioned cache proves equivalent.

#### Scenario: New candidate creates ambiguity
- **WHEN** GetModel<IServiceModel> initially resolves one assignable entry and a second compatible entry is later registered under another key
- **THEN** the next GetModel<IServiceModel> throws ambiguity unless an exact IServiceModel key exists

## REMOVED Requirements

### Requirement: Active replacement releases before publishing
**Reason**: v2 removes replacement and hot swap to eliminate destructive partial states and lifecycle rollback complexity.

**Migration**: Register only absent keys. Model multi-component runtime features as independently created child Domains.

### Requirement: Replacement is forbidden inside component initialization
**Reason**: v2 forbids all nested registration during component Initialize and removes replacement entirely.

**Migration**: Declare initial components in Configure or register a new standalone component after the Domain is Active.
