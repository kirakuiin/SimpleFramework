## Why

The current Domain lifecycle is implicit, which permits reentrant singleton creation, teardown during initialization, component reuse across owners, and callbacks that repopulate a Domain while it is being disposed. Type registration also relies on exact keys and multi-key bookkeeping, making interface-based lookup and lifecycle ownership harder to reason about than necessary.

## What Changes

- **BREAKING** Replace the implicit Domain lifecycle with a private, one-shot state machine covering creation, initialization, active use, teardown, and terminal disposal.
- Make initialization and teardown failure-safe: reject reentrant or invalid operations, roll back framework-managed initialization effects, continue cleanup after failures, and aggregate teardown errors.
- **BREAKING** Introduce a categorized internal component registry with one primary key per instance, exact-key precedence, unique assignable-type fallback, deterministic ambiguity errors, and parent fallback only when no local match exists.
- **BREAKING** Enforce exclusive lifecycle ownership for Systems and Models: one Domain and one category/key at a time, with no reuse after teardown or failed activation.
- Preserve exact-key semantics for the public `Container`, keep Utilities caller-managed, and keep Domain execution single-threaded.
- Define deterministic replacement and teardown ordering, including unpublishing components before their cleanup callbacks.
- Ship the behavior as the v1.1 cutover with a migration guide and no compatibility switch.

## Capabilities

### New Capabilities

- `domain-lifecycle`: Explicit Domain and component lifecycle states, transactional initialization, terminal disposal, operation guards, and deterministic cleanup.
- `domain-component-registry`: Categorized single-key registration, assignable lookup, ambiguity reporting, parent resolution, replacement, and ownership rules.

### Modified Capabilities

None.

## Impact

- Affects the core Domain APIs and implementations, System/Model lifecycle handling, local event registration, command/query execution guards, and parent/child relationships.
- Changes registration and lookup behavior for consumers using concrete types, interfaces, `RegisterSystemAs`/`RegisterModelAs`, replacement, or component instance reuse.
- Requires focused regression tests plus updates to public XML documentation, README/API examples, and a v1.1 migration guide.
- Does not change public `Container` lookup semantics, introduce thread safety, or attempt to roll back arbitrary external side effects.
