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

## Independent Review

Independent review of commit `4c9033a9499fb83160fb515dceaee88d68404396` reported four findings. All were resolved: lifecycle reentrancy and pre-mutation child validation received failing regression tests and production fixes; pool rollback received isolated fail/pass regression proof; XML ownership contracts were moved to the correct method. No further reviewer was dispatched, as explicitly required.
