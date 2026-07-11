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

## Changes

- Appended Repairs A–J to the approved implementation plan before each production behavior edit.
- Publish singleton instances only after successful initialization and reset initialization status after callback failure.
- Track pooled references by identity and roll back membership if return callbacks fail.
- Make message dispatch depth-aware, apply mutations at stable boundaries, stop safely on disposal, and guard buffered state before mutation.
- Enforce state ownership, use caller-specific argument/state exceptions, reject lifecycle reentrancy, unwind child states first, and remove the LINQ allocation from dispatch.
- Reject null services at registration.
- Added 11 tests, increasing the Patterns fixture count from 84 to 95.

## Verification

- Brief filter `FullyQualifiedName~SimpleFramework.Test.Patterns`: selected 0 tests because the actual namespace is `Test.Patterns`.
- Corrected focused baseline `FullyQualifiedName~Test.Patterns`: 84 passed, 0 failed, 0 skipped.
- Corrected focused final: 95 passed, 0 failed, 0 skipped.
- Full `Test/Test.csproj`: 690 passed, 0 failed, 0 skipped.
- Release solution build: succeeded with 0 warnings and 0 errors.
- Source-quality fixture: 1 passed, 0 failed, 0 skipped.
- `git diff --check`: exit 0; only Git line-ending conversion notices were emitted.

## Independent Review

Not dispatched, as explicitly required by the Task 3 assignment. The round is prepared as one reviewable commit from base `e872be89b389234ef110ec2ab84417d088534cf2`.
