## MODIFIED Requirements

### Requirement: Domain attachment uses independently active instances
A child Domain MUST complete its own creation and become Active before `AddChild` can attach it. Child initialization MUST resolve only local components because parent fallback SHALL begin only after attachment succeeds. `AddChild` MUST NOT create, initialize, reactivate, or invoke attachment callbacks on the child.

#### Scenario: Active child is attached
- **WHEN** an independently created Active child is added to an Active parent
- **THEN** the existing child instance becomes attached without repeating any creation or component initialization hook

#### Scenario: Child initialization requests a future parent dependency
- **WHEN** a child component requests a dependency that exists only in a Domain to which the child has not yet been attached
- **THEN** initialization observes the dependency as missing and the framework does not establish a temporary parent relationship

### Requirement: One strong parent-child relation defines the tree
Each attached child MUST have exactly one strong `Parent` reference and each parent MUST strongly retain its direct children internally. `AddChild` MUST reject null, self-attachment, an already attached child, cycles, and non-Active participants.

#### Scenario: Cycle is rejected
- **WHEN** attachment would make a Domain an ancestor of itself
- **THEN** `AddChild` throws `InvalidOperationException` before changing either tree

#### Scenario: Parent retains child
- **WHEN** the caller releases its own child reference after successful attachment
- **THEN** the parent continues to retain the child until removal or disposal

#### Scenario: Derived Domains compare equal by business value
- **WHEN** distinct Domain instances compare equal through overridden Equals
- **THEN** attachment checks, removal, and disposal unlinking MUST use reference identity

### Requirement: Removal detaches without releasing
`RemoveChild` MUST accept only a direct child. It SHALL remove the strong relationship, set the removed root's Parent to null, and leave every node in that subtree Active. It MUST NOT release components or invoke attachment callbacks.

#### Scenario: Direct child is removed
- **WHEN** an Active parent removes one of its direct Active children
- **THEN** the child subtree becomes an independent Active tree and all its local components remain available

#### Scenario: Non-child removal is rejected
- **WHEN** a Domain is asked to remove a node that is not its direct child
- **THEN** it throws `InvalidOperationException` without changing either tree

### Requirement: Disposal strategy has explicit child ownership semantics
`Dispose()` MUST dispose the currently attached subtree in reverse-attachment postorder before disposing the current Domain. Disposing an attached child directly MUST also remove it from its parent. A child removed before a later parent Dispose MUST remain unaffected.

#### Scenario: Parent disposes attached subtree
- **WHEN** a parent with several attached descendants is disposed
- **THEN** later-attached siblings finish first, every descendant finishes before its parent, and all targeted nodes become Disposed

#### Scenario: Attached child is disposed directly
- **WHEN** an attached child is disposed while its parent stays Active
- **THEN** the child becomes Disposed and the parent no longer lists it as a child

### Requirement: Tree consistency survives cleanup failures
Disposal MUST continue after child or component failures and MUST use `finally` paths to unlink every Disposed node from both directions.

#### Scenario: One child cleanup fails
- **WHEN** cleanup of one child throws while siblings and a parent remain
- **THEN** all remaining targeted nodes are still cleaned, all disposed relationships are removed, and the caller receives the recorded failure after structural cleanup

## ADDED Requirements

### Requirement: Tree changes are explicit and allowed during execution
Moving a subtree MUST be expressed as `oldParent.RemoveChild(child)` followed by `newParent.AddChild(child)`. AddChild and RemoveChild SHALL be allowed from any code path, including Command, Query, and Event handlers, as long as both participants are Active. Failed preconditions MUST leave both trees unchanged.

#### Scenario: Command switches scenes
- **WHEN** a Command removes and disposes a MenuDomain child and attaches a newly created GameDomain child
- **THEN** both tree operations succeed and the Command completes normally

#### Scenario: Structural validation fails
- **WHEN** any precondition for AddChild or RemoveChild fails
- **THEN** parent links and internal child retention remain unchanged

## REMOVED Requirements

### Requirement: Tree changes are explicit and transition-safe
**Reason**: Execution and transition locks are removed so tree changes work from Commands and event handlers.
**Migration**: See "Tree changes are explicit and allowed during execution".
