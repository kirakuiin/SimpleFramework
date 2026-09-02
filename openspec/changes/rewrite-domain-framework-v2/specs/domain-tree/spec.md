## ADDED Requirements

### Requirement: Domain attachment uses independently active instances
A child Domain MUST complete its own creation and become Active before `AddChild` can attach it. Child initialization MUST resolve only local components because parent fallback SHALL begin only after attachment succeeds. `AddChild` MUST NOT create, initialize, reactivate, or invoke attachment callbacks on the child.

#### Scenario: Active child is attached
- **WHEN** an independently created Active child is added to an Active parent while both trees are idle
- **THEN** the existing child instance becomes attached without repeating any creation or component initialization hook

#### Scenario: Child initialization requests a future parent dependency
- **WHEN** a child component requests a dependency that exists only in a Domain to which the child has not yet been attached
- **THEN** initialization observes the dependency as missing and the framework does not establish a temporary parent relationship

### Requirement: One strong parent-child relation defines the tree
Each attached child MUST have exactly one strong `Parent` reference and each parent MUST strongly retain its direct children internally. `AddChild` MUST reject null, self-attachment, an already attached child, duplicate attachment, cycles, non-Active participants, and trees that are executing or transitioning.

#### Scenario: Cycle is rejected
- **WHEN** attachment would make a Domain an ancestor of itself
- **THEN** `AddChild` throws `InvalidOperationException` before changing either tree

#### Scenario: Parent retains child
- **WHEN** the caller releases its own child reference after successful attachment
- **THEN** the parent continues to retain the child until removal or disposal

### Requirement: Attached lookup falls back through parents
After successful attachment, categorized component lookup MUST use the current parent chain when the local Domain has no exact, assignable, or ambiguous result. Removing or moving a subtree SHALL affect future lookup only and MUST NOT refresh dependency references already cached by components.

#### Scenario: Attached child resolves parent component
- **WHEN** an attached child lacks a compatible local Model and its parent contains one
- **THEN** child Model lookup returns the parent instance

#### Scenario: Move changes future resolution
- **WHEN** a subtree is removed from one parent and attached to another
- **THEN** later uncached lookup follows the new parent while previously cached references remain the component author's responsibility

### Requirement: Removal detaches without releasing
`RemoveChild` MUST accept only a direct child. It SHALL remove the strong relationship, set the removed root's Parent to null, assign a new shared TreeState to the removed subtree, and leave every node in that subtree Active. It MUST NOT release components or invoke attachment callbacks.

#### Scenario: Direct child is removed
- **WHEN** an idle Active parent removes one of its direct Active children
- **THEN** the child subtree becomes an independent Active tree and all its local components remain available

#### Scenario: Non-child removal is rejected
- **WHEN** a Domain is asked to remove a node that is not its direct child
- **THEN** it throws `InvalidOperationException` without changing either tree

### Requirement: Tree changes are explicit and transition-safe
Moving a subtree MUST be expressed as `oldParent.RemoveChild(child)` followed by `newParent.AddChild(child)`. Add, remove, move, dynamic registration, and disposal MUST fail while either affected tree is executing or transitioning. Structural mutation SHALL update both directions and subtree TreeState references without invoking user callbacks.

#### Scenario: Command attempts tree mutation
- **WHEN** a Command, Query, or Event callback calls AddChild or RemoveChild while tree ExecutionDepth is nonzero
- **THEN** the operation throws `InvalidOperationException` before changing the tree

#### Scenario: Structural validation fails
- **WHEN** any precondition for AddChild or RemoveChild fails
- **THEN** parent links, internal child retention, and TreeState references remain unchanged

### Requirement: Disposal strategy has explicit child ownership semantics
`Dispose()` MUST dispose the currently attached subtree in reverse-attachment postorder before disposing the current Domain. `DisposeSelfOnly()` MUST detach direct child subtrees, preserve them as Active roots, keep them transition-locked until the call finishes, and dispose only the current Domain. A child detached before a later parent Dispose MUST remain unaffected.

#### Scenario: Parent disposes attached subtree
- **WHEN** a parent with several attached descendants is disposed normally
- **THEN** later-attached siblings finish first, every descendant finishes before its parent, and all targeted nodes become Disposed

#### Scenario: Parent disposes itself only
- **WHEN** `DisposeSelfOnly` is called on a parent with direct children
- **THEN** the parent becomes Disposed and each former child subtree is Active with Parent null after the call completes

#### Scenario: Parent cleanup fails after self-only detach
- **WHEN** parent cleanup throws during `DisposeSelfOnly`
- **THEN** the parent still becomes Disposed, detached child subtrees still become usable Active roots after the call, and the cleanup failure is propagated

### Requirement: Tree consistency survives cleanup failures
Disposal MUST continue after child or component failures and MUST use `finally` paths to unlink every Disposed node from both directions. During cleanup, newly detached preserved subtrees MUST remain transition-locked so release callbacks cannot reattach or mutate them reentrantly.

#### Scenario: One child cleanup fails
- **WHEN** cleanup of one child throws while siblings and a parent remain
- **THEN** all remaining targeted nodes are still cleaned, all disposed relationships are removed, and the caller receives the recorded failure after structural cleanup

#### Scenario: Release callback attempts reattachment
- **WHEN** a release callback tries to attach a temporarily detached subtree before `DisposeSelfOnly` finishes
- **THEN** the framework rejects the operation because that subtree is still transitioning
