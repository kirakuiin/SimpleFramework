## MODIFIED Requirements

### Requirement: Domain attachment uses independently active instances
A child Domain MUST complete its own creation and become Active before `AddChild` can attach it. `AddChild` MUST NOT create, initialize, reactivate, or invoke attachment callbacks on the child. Alternatively, `CreateDomain` SHALL accept an optional Active parent: the child MUST then resolve components through that parent during its own initialization, and SHALL be attached to the parent only after its components initialized successfully, as if `AddChild` were called before `OnActivated`. A Domain created without a parent MUST resolve only local components during initialization. A parent that is not Active MUST be rejected; if the parent stops being Active during the child's initialization, the child's startup MUST fail. A child whose startup fails MUST leave the parent's child list unchanged. When lookup falls back to an ancestor that is already Disposed (a child whose parent was disposed during the child's startup, or during the child's own release callbacks), Get and TryGet MUST throw `ObjectDisposedException` stating that the parent is disposed, instead of reporting the component as missing.

#### Scenario: Active child is attached
- **WHEN** an independently created Active child is added to an Active parent
- **THEN** the existing child instance becomes attached without repeating any creation or component initialization hook

#### Scenario: Child initialization requests a future parent dependency
- **WHEN** a child created without a parent requests, during initialization, a dependency that exists only in a Domain to which it will later be attached
- **THEN** initialization observes the dependency as missing and the framework does not establish a temporary parent relationship

#### Scenario: Child created with a parent uses parent components during initialization
- **WHEN** `CreateDomain` is called with an Active parent that has registered an `IEventHub`, and a child System subscribes through `SubscribeEvent` in OnInitialize
- **THEN** the subscription succeeds and the created child is attached to that parent

#### Scenario: Parent is still starting
- **WHEN** a Domain's Configure creates a child passing itself as the parent
- **THEN** child creation throws `InvalidOperationException`

#### Scenario: Lookup reaches a disposed parent
- **WHEN** a child's component, during the child's startup, disposes the parent and then requests a component that only the parent provided
- **THEN** lookup throws `ObjectDisposedException` stating that the parent Domain is disposed

#### Scenario: Child startup fails
- **WHEN** a child created with a parent fails during Configure or component initialization
- **THEN** the child ends Disposed and the parent's child list and components are unchanged

### Requirement: Tree changes are explicit and allowed during execution
Moving a subtree MUST be expressed as `oldParent.RemoveChild(child)` followed by `newParent.AddChild(child)`. AddChild and RemoveChild SHALL be allowed from any code path, including Command, Query, and Event handlers, as long as both participants are Active. Failed preconditions MUST leave both trees unchanged.

#### Scenario: Command switches scenes
- **WHEN** a Command removes and disposes a MenuDomain child and attaches a newly created GameDomain child
- **THEN** both tree operations succeed and the Command completes normally

#### Scenario: Structural validation fails
- **WHEN** any precondition for AddChild or RemoveChild fails
- **THEN** parent links and internal child retention remain unchanged

#### Scenario: Disposing child asks its parent to remove it
- **WHEN** a child that is being disposed calls `RemoveChild` on its Active parent with itself as the argument
- **THEN** `InvalidOperationException` is thrown, the child stays attached until its own disposal detaches it, and its release callbacks can still resolve parent components
