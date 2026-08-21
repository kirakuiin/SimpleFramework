## Why

Domain lifecycle registration currently leaves several ownership gaps: replaced components can retain local-event subscriptions, caught nested initialization failures can commit framework-local side effects, lifecycle components can be rebound to another Domain, and parent changes can expose partially committed relationships. `BindableProperty<T>` also permits synchronous reentrant writes that deliver stale notifications. These behaviors must be made explicit and fail-safe before treating the v1.1 lifecycle contract as stable.

## What Changes

- Make local-event subscriptions created through a System's event-registration capability System-owned by default, while retaining an `IUnRegister` handle for early manual cancellation and an explicit Domain-scoped registration path for caller-managed subscriptions. Models do not gain an event-registration capability in this change.
- Poison the entire top-level component-registration transaction when any nested lifecycle registration fails, even if user initialization code catches the nested exception; roll back every enlisted component, Utility, and local subscription.
- **BREAKING** Separate one-shot System/Model Domain binding from repeatable Command/Query context injection. Keep the binding contract public so custom `IDomain`, `ISystem`, and `IModel` implementations remain supported without inheriting framework abstract classes.
- Make `SetParent` commit a new parent only after the old ownership relationship is removed successfully.
- Reject terminal Domain teardown while synchronous local-event, Command, or Query execution is still on the call stack.
- Preflight standard owned Domain descendants before parent teardown, and reject lifecycle-component replacement during synchronous execution.
- Apply guarded component initialization/release phases to standard ancestor Domains so descendant callbacks cannot commit cross-Domain framework effects.
- **BREAKING** Reject writes of a different `BindableProperty<T>` value while its listeners are being notified.
- Clarify that Query read-only behavior is a usage convention rather than a capability boundary or runtime guarantee.
- Keep `IDomain` as the complete public facade; interface segregation and namespace reorganization remain outside this change.

## Capabilities

### New Capabilities

- `bindable-property-notification`: Defines synchronous notification ordering and rejection of reentrant writes.

### Modified Capabilities

- `domain-lifecycle`: Strengthens transaction failure propagation, System-owned subscription cleanup, and atomic parent changes.
- `domain-component-registry`: Defines one-shot lifecycle-component Domain binding while preserving public custom implementation points.

## Impact

- Core behavior and API: `AbstractDomain.cs`, `Framework.cs`, `FrameworkExtension.cs`, `AbstractSystem.cs`, `AbstractModel.cs`, `AbstractCommand.cs`, `AbstractQuery.cs`, `BindableProperty.cs`, and internal component/transaction records.
- Tests: Domain lifecycle, replacement, nested-registration failure, custom implementation compatibility, parent relationship failure, event ownership, and bindable reentrancy coverage.
- Documentation: lifecycle documentation and Query wording in public XML/README material.
- No new external dependencies. Existing direct `ISystem`/`IModel` implementations must adopt the public one-shot binding contract; source compatibility is not required.
