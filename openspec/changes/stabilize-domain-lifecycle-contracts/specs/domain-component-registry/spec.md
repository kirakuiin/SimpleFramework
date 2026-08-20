## ADDED Requirements

### Requirement: Lifecycle components bind to a Domain exactly once
System and Model components MUST use a public one-shot Domain-binding contract distinct from repeatable Command and Query execution-context injection. A lifecycle component SHALL reject null binding and every binding attempt after the first, including the same Domain. Binding SHALL remain terminal after initialization failure or cleanup, and reading the framework base component's `Domain` before binding MUST throw a clear `InvalidOperationException`.

#### Scenario: Runtime component registration binds once
- **WHEN** an unbound System or Model is registered while a Domain is initializing or active
- **THEN** the Domain binds the component once before its initialization callback and future dependency lookup uses that Domain

#### Scenario: Registered component is rebound externally
- **WHEN** code attempts to bind an already bound System or Model to the same or another Domain
- **THEN** binding throws before changing the component's Domain reference or lifecycle ownership

#### Scenario: Released component is rebound
- **WHEN** code attempts to bind a System or Model after successful or failed cleanup
- **THEN** binding throws and the terminal component remains associated with its original Domain

#### Scenario: Domain is read before binding
- **WHEN** code reads `Domain` from an unbound framework System or Model base instance
- **THEN** the access throws a clear `InvalidOperationException` rather than returning an implicit null

### Requirement: Direct custom Domain and component implementations remain supported
The one-shot lifecycle binding contract MUST remain public. A caller MUST be able to implement `IDomain`, `ISystem`, or `IModel` directly without inheriting `AbstractDomain`, `AbstractSystem`, or `AbstractModel`, and a compliant custom Domain SHALL be able to perform the first lifecycle binding through public contracts. Direct custom component implementations MUST honor the same one-shot binding semantics.

#### Scenario: Custom Domain registers a framework Model
- **WHEN** an external `IDomain` implementation registers an unbound framework Model
- **THEN** it can invoke the public binding contract before initializing and managing that Model

#### Scenario: Framework Domain registers a direct custom System
- **WHEN** `AbstractDomain` registers an external System implementation that does not inherit `AbstractSystem`
- **THEN** registration uses the public binding contract and does not require internal framework access

#### Scenario: Command and Query objects are reused
- **WHEN** a Command or Query object executes through different Domains at different times
- **THEN** repeatable execution-context injection remains available and is not subject to lifecycle-component one-shot binding
