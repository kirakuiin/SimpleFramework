# Round 3: Patterns

## Files Reviewed

- Production: `Patterns/Singleton.cs`, `Patterns/ServiceLocator.cs`, `Patterns/ObjectPool.cs`, `Patterns/MessageChannel.cs`, `Patterns/BlackBoard.cs`, `Patterns/StateMachine.cs`, `Patterns/PatternDefine.cs`.
- Contracts: `Patterns/README.md`, `AGENTS.md`, `docs/superpowers/specs/2026-07-10-full-codebase-review-design.md`, `docs/superpowers/plans/2026-07-10-full-codebase-review.md`.
- Tests: all six files under `Test/Patterns`.

## Contracts Checked

- Singleton construction, concurrent first access, initialization failure, recreation, and cleanup.
- Service exact/fallback lookup, replacement, missing lookup, removal, and null registration.
- Pool creation, reuse, return callbacks/reset, duplicate return, clear, prewarm, disposal, and callback failure bookkeeping.
- Message subscribe/unsubscribe mutation, nested publication, handler exceptions, disposal during publication, buffered replay, and disposed operations.
- Blackboard local and parent lookup, local null shadowing, notifications, lock scope, reentrant callbacks, snapshots, and concurrent access.
- State setup, ownership, initial activation, entry/update/exit ordering, hierarchy, AnyState precedence, invalid transitions, lifecycle reentrancy, inactive dispatch, and transition hot-path allocation.
- Public Chinese XML contracts, logging helpers, and modern syntax opportunities.

## Findings and Decisions

- **Important — fixed:** singleton candidates were published before successful initialization, so an initialization exception permanently cached an unusable instance.
- **Important — fixed:** returning one object to a pool twice made the same reference available for two checkouts.
- **Important — fixed:** self-unsubscription followed by nested publication mutated the handler list while an outer enumeration was active.
- **Important — fixed:** disposing a channel from a handler cleared the active enumeration and could invoke later handlers or throw.
- **Important — fixed:** buffered publication updated the cached value before the disposed guard threw.
- **Important — fixed:** state machines accepted null, unregistered, and foreign-owned states, allowing transitions into states that were never set up by that machine.
- **Important — fixed:** a transition dispatched from an exit callback could complete and then be silently overwritten by the outer transition.
- **Normal — fixed:** hierarchical exit ran parent before child instead of unwinding leaf to root.
- **Normal — fixed:** transition lookup allocated a filtered list on every dispatch despite needing only the first match.
- **Normal — fixed:** service registration accepted null and later returned it as a successfully located non-null service.
- **Normal — fixed:** the singleton concurrency test used a timing sleep; it now uses explicit synchronization events.
- **Documentation — fixed:** corrected inaccurate exception documentation and filled public XML gaps in the reviewed module.
- **Retained:** ServiceLocator remains intentionally lightweight and non-concurrent; replacement is last-write-wins and unregistering a missing service is a no-op.
- **Retained:** object pools accept objects not originally created by the pool, matching existing public usage; only duplicate references already in the pool are rejected.
- **Retained:** message handler exceptions propagate directly. Pending subscription mutations are committed when the outermost publication frame exits.
- **Retained:** Blackboard notifications already run outside its lock. Local reads may delegate to a parent while holding the child read lock, but no production path acquires those locks in reverse order; no lock change was justified.
- **Independent review — Important, fixed:** `SetActive` did not share the lifecycle reentrancy guard, allowing active/`CurrentState` pairs to become inconsistent from exit callbacks.
- **Independent review — Important, fixed:** child-state attachment mutated `ChildrenStateMachine` and `_parentStateRef` before null/ownership validation completed.
- **Independent review — Normal, verified:** object-pool callback rollback was implemented but lacked direct regression evidence for configured and listener callbacks.
- **Independent review — Documentation, fixed:** transition ownership exceptions were attached to `AddEventHandler`, including a nonexistent `toState` parameter reference.
- **Final review — Important, fixed:** `SetActive` changed lifecycle fields before callbacks succeeded, so uncaught exit/enter exceptions left inconsistent active/current-state pairs.
- **Final review — Important, fixed:** a buffered replay exception leaked the new subscription into pending delivery.
- **Failure-state review — Important, fixed:** restoring only root lifecycle fields after callback failure could not undo child exit/entry side effects; all lifecycle callback failures now close the affected machine to inactive/null.
- **Failure-state review — Important, fixed:** setup exceptions leaked temporary state ownership, list entries, initial-state selection, or transitions created while setup context was visible.
- **Failure-state review — Important, fixed:** a duplicate buffered subscription received an owning token, so replay rollback could cancel the earlier successful registration.
- **Failure-state review — Normal, fixed:** null handlers relied on a dictionary exception naming `key` instead of the public `handler` boundary.

## Changes

- Appended Repairs A–N to the approved implementation plan before each production behavior edit.
- Publish singleton instances only after successful initialization and reset initialization status after callback failure.
- Track pooled references by identity and roll back membership if return callbacks fail.
- Make message dispatch depth-aware, apply mutations at stable boundaries, stop safely on disposal, and guard buffered state before mutation.
- Enforce state ownership, use caller-specific argument/state exceptions, reject lifecycle reentrancy, unwind child states first, and remove the LINQ allocation from dispatch.
- Reject null services at registration.
- Added 11 tests, increasing the Patterns fixture count from 84 to 95.
- Added 6 independent-review tests, increasing the Patterns fixture count from 95 to 101.
- Applied one lifecycle guard consistently to `Dispatch` and `SetActive`, with rejection before equality checks or state mutation and `try/finally` reset around entry/exit callbacks.
- Validated child null/ownership before creating a hierarchy or assigning a parent, while retaining idempotent addition to the same child state machine.
- Proved pool rollback by temporarily isolating the missing membership removal: both retry tests failed, then passed after restoring the production repair; no net pool production edit was required.
- Added 5 final-wave tests, increasing the Patterns fixture count from 101 to 106.
- Made `SetActive` transactional across callback exceptions: deactivation commits fields after exit succeeds, activation restores prior fields when entry fails, and the original exception propagates.
- Roll back buffered subscriptions when immediate replay fails, including subscriptions created during an active publication frame.
- Added 6 failure-state tests, increasing the Patterns fixture count from 106 to 112.
- Replaced compensating lifecycle rollback with coherent fail-closed inactive/null semantics for activation, deactivation, transition exit, target entry, and nested child failure; explicit later activation provides recovery.
- Roll back failed setup ownership, state-list membership, initial selection, and transitions referencing the failed state while preserving setup-time owner access.
- Return no-op tokens for duplicate effective subscriptions and owning tokens only when a call creates or restores registration; reject null handlers as `handler`.

## Verification

- Brief filter `FullyQualifiedName~SimpleFramework.Test.Patterns`: selected 0 tests because the actual namespace is `Test.Patterns`.
- Corrected focused baseline `FullyQualifiedName~Test.Patterns`: 84 passed, 0 failed, 0 skipped.
- Corrected focused final: 95 passed, 0 failed, 0 skipped.
- Full `Test/Test.csproj`: 690 passed, 0 failed, 0 skipped.
- Release solution build: succeeded with 0 warnings and 0 errors.
- Source-quality fixture: 1 passed, 0 failed, 0 skipped.
- `git diff --check`: exit 0; only Git line-ending conversion notices were emitted.
- Independent-review focused RED: 4 state/hierarchy failures and 2 passing pool characterizations across 6 selected tests.
- Pool isolated regression: 2 failed with rollback removed; restored GREEN: 2 passed.
- Independent-review state/hierarchy GREEN: 4 passed, 0 failed, 0 skipped.
- Corrected focused final after review: 101 passed, 0 failed, 0 skipped.
- Full `Test/Test.csproj` after review: 696 passed, 0 failed, 0 skipped.
- Release solution build after review: succeeded with 0 warnings and 0 errors.
- Source-quality fixture after review: 1 passed, 0 failed, 0 skipped.
- `git diff --check` after review: exit 0; only Git line-ending conversion notices were emitted.
- Final-wave RED: 4 selected tests failed, covering direct exit failure, uncaught lifecycle reentrancy, initial entry failure, and buffered replay leakage.
- Final-wave affected GREEN: 5 passed, including buffered replay failure during active publication.
- Final-wave Patterns: 106 passed, 0 failed, 0 skipped.
- Final-wave full `Test/Test.csproj`: 701 passed, 0 failed, 0 skipped.
- Final-wave Release solution build: succeeded with 0 warnings and 0 errors.
- Final-wave source-quality fixture: 1 passed, 0 failed, 0 skipped.
- Final-wave `git diff --check`: exit 0; only Git line-ending conversion notices were emitted.
- Failure-state RED: exact filter failed 8/8; the setup transition-leak strengthening then failed independently before rollback cleanup was completed.
- Failure-state affected GREEN: 8 passed, plus 2 setup rollback tests passed after transition cleanup.
- Failure-state Patterns: 112 passed, 0 failed, 0 skipped.
- Failure-state full `Test/Test.csproj`: 707 passed, 0 failed, 0 skipped.
- Failure-state Release solution build: succeeded with 0 warnings and 0 errors.
- Failure-state source-quality fixture: 1 passed, 0 failed, 0 skipped.
- Failure-state `git diff --check`: exit 0; only Git line-ending conversion notices were emitted.

## Independent Review

Independent review of commit `4c9033a9499fb83160fb515dceaee88d68404396` reported four findings. All were resolved: lifecycle reentrancy and pre-mutation child validation received failing regression tests and production fixes; pool rollback received isolated fail/pass regression proof; XML ownership contracts were moved to the correct method. No further reviewer was dispatched, as explicitly required.

The final follow-up from base `9ee91aa422b9367a18914ed34a725000d645a66c` identified exception atomicity and buffered replay rollback. Both received failing regression tests and fixes; exception identity, final lifecycle fields, guard reset, and pending-subscription cleanup were self-reviewed. No further reviewer was dispatched.

The failure-state follow-up from base `9aae063465f7dd1cb0f285fd2cc1ade8f4819968` corrected the lifecycle policy to fail closed because child callback side effects cannot be safely compensated. It also closed setup ownership/transition leaks and duplicate-subscription token ownership. All findings have failing regression evidence and passing recovery paths; no further reviewer was dispatched.

## Ownership Closure Follow-up

### Findings and Repairs

- Subscription tokens previously identified only a handler. After an explicit unsubscribe and resubscribe of the same delegate, disposal of the stale token cancelled the newer registration. Active and pending registrations now carry monotonically increasing identifiers, and token disposal matches both handler and identifier.
- Subscription finalization previously changed channel state, so merely dropping the returned token silently unsubscribed the handler. Tokens now have explicit-only lifetime, and their concrete implementation is private so callers cannot manufacture cancellation authority.
- Setup could activate or dispatch through its temporarily published owner and could retain a child hierarchy after failure. A setup-depth guard now rejects both lifecycle operations before mutation, while transactional snapshots preserve successful transition/handler configuration and restore owner, list, initial state, transitions, handlers, and hierarchy after failure.
- `Unsubscribe(null)` exposed the dictionary parameter name `key`, and a null object-pool factory threw `ArgumentException`. Both public boundaries now throw `ArgumentNullException` with their documented parameter names.

### TDD Evidence

- Exact closure RED: 6 failed, 0 passed. Stale-token and collected-token publication both observed zero calls instead of one; the concrete token was exported; null unsubscribe reported `key`; the pool threw `ArgumentException`; and setup activation/dispatch both succeeded and performed transitions.
- Exact closure GREEN: 6 passed, 0 failed, 0 skipped.
- Full Patterns: 118 passed, 0 failed, 0 skipped, including pending mutation, nested publication, buffered replay rollback, hierarchical lifecycle failure, and setup retry coverage.

### Verification

- Full `Test/Test.csproj`: 713 passed, 0 failed, 0 skipped.
- Release solution build: 0 warnings, 0 errors.
- Source-quality fixture: 1 passed, 0 failed, 0 skipped.
- No additional reviewer was dispatched, as explicitly required for this closure.

## Hierarchy Closure Follow-up

### Findings and Repairs

- Setup isolation was local to the machine running `Setup`, so a root setup callback could activate a pre-existing child machine. Child machines now retain an owner-state link and reject lifecycle mutation whenever any ancestor machine is in setup.
- Setup snapshots stopped at the immediate hierarchy and omitted lifecycle/configuration fields. Snapshots now recurse through every pre-existing descendant and restore state lists, ownership/parent links, initial/current state, transitions, handlers, active/change/setup fields, and `TriggerUpdateWhenStateChange`; newly-created descendant hierarchy is detached on failure while successful setup configuration is retained.
- Lifecycle failure catches only failed closed the machine handling the exception. A callback-free recursive helper now closes the complete reachable hierarchy after activation, transition, or update exceptions without invoking additional lifecycle callbacks, while preserving the original exception identity and explicit recovery path.
- Base and buffered subscription XML now state that duplicate effective subscriptions may return a non-owning no-op handle.

### TDD Evidence

- Exact hierarchy RED: 2 failed, 0 passed. Descendant activation during ancestor setup returned no guard exception, and target-update failure left both entered descendant machines active.
- Exact hierarchy GREEN: 2 passed, 0 failed, 0 skipped.
- StateMachine fixture: 44 passed, 0 failed, 0 skipped.
- Full Patterns: 120 passed, 0 failed, 0 skipped.

### Verification

- Full `Test/Test.csproj`: 715 passed, 0 failed, 0 skipped.
- Release solution build: 0 warnings, 0 errors.
- Source-quality fixture: 1 passed, 0 failed, 0 skipped.
- `git diff --check`: exit 0; only Git line-ending conversion notices were emitted.
- Recursive restore was self-reviewed for pre-existing and newly-created descendants, lifecycle/configuration fields, successful-setup retention, exception identity, guard reset, and explicit recovery. No additional reviewer was dispatched, as explicitly required.
