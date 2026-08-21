## 1. One-Shot Lifecycle Binding

- [x] 1.1 Add regression tests for runtime first binding, same/different-Domain rebinding rejection, pre-bind access failure, terminal binding after cleanup, and repeatable Command/Query context injection.
- [x] 1.2 Add the public one-shot lifecycle binding contract, move `ISystem`/`IModel` to it, retain repeatable `IDomainConfigurable` for Command/Query, and update framework base classes and registration code.
- [x] 1.3 Update repository direct component implementations and add contract tests proving a direct custom `IDomain` and direct custom `ISystem`/`IModel` can interoperate without abstract base classes.

## 2. System-Owned Event Resources

- [x] 2.1 Add regression tests proving replaced Systems stop receiving local events, callers can cancel early, cleanup failures do not skip cancellation, and explicit Domain-scoped subscriptions survive unrelated replacement.
- [x] 2.2 Add owner-aware System event registration and per-entry unregister-resource tracking without inferring ownership from callback targets; keep direct Domain registration caller-managed.
- [x] 2.3 Make System replacement, failed initialization, rollback, and terminal teardown exhaustively cancel owned resources and aggregate cancellation failures with lifecycle cleanup failures.

## 3. Poisoned Initialization Transactions

- [x] 3.1 Add regression tests where an outer initializer catches a nested registration failure, attempts to continue, and otherwise returns successfully; assert the complete top-level registration still rolls back.
- [x] 3.2 Record nested registration failure in the shared initialization transaction, reject later enlistment after poison, and enforce a top-level commit barrier that preserves the original failure.
- [x] 3.3 Extend failure-path tests to cover nested components, Utilities, System-owned and Domain-scoped transaction subscriptions, cleanup failure aggregation, and continued usability of an already-active Domain after rollback.

## 4. Atomic Parent Relationship Changes

- [x] 4.1 Add regression tests for successful owned-child reparenting, lookup-only parent changes, and old-parent lifecycle rejection without partial parent/ownership mutation.
- [x] 4.2 Prepare the candidate weak reference, detach standard old-parent ownership through a validated internal path, and commit the new parent only after detachment succeeds; call third-party `RemoveChild` before committing framework state.

## 5. BindableProperty Reentrancy

- [x] 5.1 Add regression tests for sequential assignments, different-value reentrant rejection before mutation, comparer-equal reentrant no-op, listener-exception guard recovery, and unrelated-property notification.
- [x] 5.2 Add a `try/finally` notification guard to `BindableProperty<T>` while preserving existing synchronous multicast exception behavior and comparer semantics.

## 6. Contracts and Verification

- [x] 6.1 Update public XML comments, README/lifecycle documentation, and examples for System-vs-Domain event ownership, poisoned transactions, one-shot lifecycle binding, parent atomicity, BindableProperty reentrancy, and conventionally read-only Query behavior.
- [x] 6.2 Run focused Framework lifecycle and BindableProperty tests, then `dotnet test .\Test\Test.csproj` and `dotnet test .\SimpleFramework.sln`; resolve every regression without changing the approved contracts.
- [x] 6.3 Run `openspec validate stabilize-domain-lifecycle-contracts` and review the final diff for unintended `IDomain` capability removal, generated-file edits, or unrelated refactoring.

## 7. Review Follow-ups

- [x] 7.1 Add a regression test and poison the active transaction when a nested lifecycle registration fails during preflight validation.
- [x] 7.2 Add a regression test and make direct `RemoveChild` exception-atomic for standard parent/child relationships.
- [x] 7.3 Add a regression test and release System-owned resources when `BindDomain` fails before component initialization starts.
- [x] 7.4 Add regression tests and apply comparer-aware reentrancy protection to `SetValueWithoutNotify`.
- [x] 7.5 Run focused and full tests, strict OpenSpec validation, and final diff review.
- [x] 7.6 Align proposal, design, and lifecycle spec with System-only event ownership and binding/initializing/active owner phases.
- [x] 7.7 Enlist System-owned subscriptions in the active initialization transaction and cover rollback and commit behavior for an existing active System.
- [x] 7.8 Guard custom `BindDomain` execution as component initialization, add direct-component regression coverage, and run focused/full validation.
- [x] 7.9 Guard caller `IUnRegister` and `Dispose` paths without consuming rejected handles, document the BindDomain/Initialize phase uniformly, and run focused/full validation.
- [x] 7.10 Reject Domain teardown during synchronous Event/Command/Query execution, cover virtual hooks and exception recovery, and run focused/full validation.
- [x] 7.11 Recursively preflight standard owned Domain descendants before teardown and cover execution, component initialization, and component release phases.
- [x] 7.12 Reject existing System/Model replacement during synchronous execution while preserving new-key and Utility registration, then run focused/full validation.
- [x] 7.13 Reject ancestor teardown while an owned standard descendant cleanup callback is still running, then run focused/full validation.
- [x] 7.14 Reject ancestor System/Model replacement while an owned standard descendant is synchronously executing, and cover post-execution retry.
- [x] 7.15 Propagate guarded Domain/component initialization and release phases to standard ancestors so descendant failure cannot leave ancestor framework effects.
- [x] 7.16 Reject standard ownership relationship mutation during synchronous owned-tree execution so lifecycle guards cannot be bypassed by detachment.
- [x] 7.17 Reject Domain teardown during active component release while preserving idempotent terminal-cleanup reentrancy.
- [x] 7.18 Reject overlapping execution of the same framework Command/Query instance without preventing sequential cross-Domain reuse.
