## 1. Remove the legacy contract and establish the v2 surface

- [x] 1.1 Inventory every repository-internal caller of the current Domain, component lifecycle, CQ, event, singleton, `IController`, Trait, `Require*`, `Register*As`, `SetParent`, replacement, and global-event APIs so the atomic rewrite has an explicit compile-fix list.
- [x] 1.2 Delete the existing `Test/Framework` Domain-framework tests, fixtures, helper Domains, and legacy behavior assertions instead of adapting them in place; create an empty v2 test organization with no old helper types.
- [x] 1.3 Replace the public component markers and lifecycle contracts with `IModel`, `ISystem`, `IUtility`, `IModelLifecycle`, and `ISystemLifecycle`, including complete Chinese XML documentation.
- [x] 1.4 Replace the mutable-Domain Command/Query contracts with synchronous `ICommand`, `ICommand<TResult>`, and `IQuery<TResult>` signatures that receive concrete stack Context values.
- [x] 1.5 Define the narrow v2 `IDomain` consumption surface and remove legacy compatibility members, capability Traits, `IController`, and arbitrary custom-`IDomain` support from the core contract.

## 2. Implement minimal state and Context foundations

- [x] 2.1 Implement internal `Starting`, `Active`, `Disposing`, and `Disposed` Domain state with centralized state exception helpers and idempotent Dispose entry.
- [x] 2.2 Implement shared `DomainTreeState` with `ExecutionDepth` and `IsTransitioning`, including exception-safe enter/exit helpers for synchronous execution and lifecycle transitions.
- [x] 2.3 Implement Model/System Context objects with `Initializing`, `Ready`, and `Invalid` behavior and the exact category capabilities defined by the specs.
- [x] 2.4 Implement `readonly ref struct` CommandContext and QueryContext with internal construction, declared capability separation, and no heap lease or pooling mechanism.
- [x] 2.5 Implement optional AbstractModel and AbstractSystem bases that manage their framework Context safely while preserving direct lifecycle-interface implementation as a supported path.

## 3. Rebuild categorized registration and lookup

- [x] 3.1 Implement separate Model, System, and Utility registries with runtime-type default keys, explicit generic contract keys, pre-mutation assignability validation, and exact-key dictionaries.
- [x] 3.2 Implement category-conflict rejection, same-Domain duplicate-instance rejection, and global one-shot lifecycle ownership that releases untouched Configure reservations after pre-initialization failure.
- [x] 3.3 Implement exact, unique-assignable, and parent-fallback resolution without an assignable-result cache, including local precedence and post-attachment-only parent lookup.
- [x] 3.4 Implement missing and ambiguity behavior for all Get/TryGet variants with category, Domain, requested type, candidate key, and runtime type details.
- [x] 3.5 Implement delayed Model/System publication, Utility availability before Model initialization, and cancellation of publication before lifecycle Release.
- [x] 3.6 Implement Active new-key registration as a single-candidate atomic operation and reject existing keys, nested registration, replacement, removal, and batch rollback semantics.

## 4. Rebuild creation, startup, singleton, and failure cleanup

- [x] 4.1 Implement factory-based `CreateDomain` orchestration for private parameterless or parameterized constructors without reflection or `new()` constraints.
- [x] 4.2 Implement Configure collection followed by Model registration-order initialization and System registration-order initialization, with phase-specific Context restrictions.
- [x] 4.3 Transition to Active and clear the creation transition before OnActivated; reject returning a candidate that OnActivated disposed or otherwise left non-Active.
- [x] 4.4 Implement creation-failure cleanup that consumes every started lifecycle instance, releases untouched reservations, clears framework resources, preserves external-side-effect boundaries, and orders aggregate failures correctly.
- [x] 4.5 Implement strict `AbstractSingletonDomain<T>` Empty/Creating/Published state, reentrancy rejection, delayed publication, failure retry, direct-Dispose cache clearing, GetInstance, and no-create DestroyInstance.
- [x] 4.6 Implement OnDeactivating and component Release with all Context capability disabled before callbacks and no release-time dependency lookup.

## 5. Rebuild dynamic Domain tree management

- [x] 5.1 Implement AddChild validation for Active idle roots, strong bidirectional ownership, duplicate/self/cycle rejection, and O(subtree) TreeState adoption without lifecycle callbacks.
- [x] 5.2 Implement direct-child RemoveChild with bidirectional unlink, Active subtree preservation, new TreeState assignment, and no release or detach callback.
- [x] 5.3 Implement explicit subtree moves as RemoveChild then AddChild and ensure future lookup uses the new parent without refreshing cached component references.
- [x] 5.4 Implement reverse-attachment postorder subtree Dispose with exhaustive child cleanup and `finally`-guaranteed structural unlink.
- [x] 5.5 Implement DisposeSelfOnly with temporarily transition-locked detached child roots, current-Domain-only cleanup, and deterministic child usability after success or failure.
- [x] 5.6 Remove SetParent, dual lookup-only relationships, weak-parent behavior, CreateChild, public Children, and attachment/detachment callbacks from the v2 implementation.

## 6. Rebuild CQ and local events for the hot path

- [x] 6.1 Implement synchronous Command and Query dispatch with stack Context construction, tree-scoped nested ExecutionDepth, result propagation, and `finally` restoration on every failure path.
- [x] 6.2 Implement Domain-local EventBus storage as copy-on-write handler arrays so registration/unregistration changes future arrays and sending allocates no snapshot.
- [x] 6.3 Implement ordered synchronous event dispatch, current-array snapshot semantics, fail-fast exception propagation, and `finally` restoration of ExecutionDepth.
- [x] 6.4 Implement idempotent event tokens, automatic System-owned subscription cancellation before System Release, harmless external token use after Domain disposal, and removal of EventBus.Global.
- [x] 6.5 Add only compositional convenience extensions such as parameterless `SendCommand<T>` and `SendEvent<T>`; keep engine-specific lifetime binding in GDExt and do not recreate capability forwarding Traits.

## 7. Write the v2 test suite from zero

- [x] 7.1 Write new creation and startup tests covering private/parameterized factories, Configure restrictions, category initialization order, delayed publication, Active OnActivated behavior, and failure cleanup.
- [x] 7.2 Write new registry tests covering runtime and explicit keys, interface/concrete same-instance resolution, local precedence, parent fallback, missing, ambiguity, duplicate, category conflict, ownership, and Active registration.
- [x] 7.3 Write new lifecycle and Context tests covering direct implementations, abstract bases, Initializing/Ready/Invalid permissions, partial initialization Release, one-shot consumption, release-time capability rejection, and Utility non-ownership.
- [x] 7.4 Write new tree tests covering independent initialization, AddChild/RemoveChild, cycle and busy-tree rejection, move semantics, TreeState replacement, Dispose, DisposeSelfOnly, reverse postorder, reentrancy, and structural consistency after failures.
- [x] 7.5 Write new CQ tests covering stack Context capabilities, synchronous nesting, sequential cross-Domain command reuse, query intent limits, exception propagation, execution guards, and compile-time non-escape expectations where testable.
- [x] 7.6 Write new event tests covering locality, registration order, copy-on-write changes during send, fail-fast behavior, System ownership, token idempotence, disposal, and zero-snapshot-allocation characterization.
- [x] 7.7 Write new strict-singleton and exhaustive-cleanup tests covering creation reentrancy, GetInstance/DestroyInstance rules, retry, direct Dispose clearing, child/component/hook failures, flattened aggregate ordering, and terminal state.
- [x] 7.8 Add focused performance characterization for exact-key lookup, assignable fallback, event send allocation, CQ Context allocation, and deep-tree transition guards without introducing speculative lookup caches.

## 8. Adapt repository callers and document the new API

- [x] 8.1 Update Toolkit, GDExt, root helpers, examples, and every repository-internal framework caller to the v2 contracts without adding compatibility shims.
- [x] 8.2 Keep unrelated Collections, ECS, Maths, Net, Patterns, and Utility behavior unchanged except for required compile adaptations to the new core API.
- [x] 8.3 Add complete Chinese XML comments for every public type and member, including key selection, lookup precedence, phase permissions, ownership, release order, exception behavior, single-thread use, and synchronous-only CQ.
- [x] 8.4 Add concise v2 usage examples for ordinary and parameterized Domain creation, strict singleton access, Configure registration, interface/concrete lookup, dynamic registration, child attachment, CQ, events, and both disposal modes.
- [x] 8.5 Verify source and tests contain no legacy `Require*`, `Register*As`, `SetParent`, replacement/removal, global-event, `IController`, capability-Trait, public-state, public-Children, async-CQ, or old test-helper contract remnants.

## 9. Verify the atomic rewrite

- [x] 9.1 Run `dotnet restore .\SimpleFramework.sln` and resolve only issues caused by the v2 rewrite.
- [x] 9.2 Run `dotnet build .\SimpleFramework.sln` with nullable analysis enabled and eliminate all new warnings or contract mismatches.
- [x] 9.3 Run `dotnet test .\Test\Test.csproj` and confirm the entirely new Framework suite plus unaffected module tests pass.
- [x] 9.4 Run `dotnet test .\SimpleFramework.sln` and record any environment-dependent GDExt restore/build limitations separately from core correctness.
- [x] 9.5 Review the final public API surface, XML documentation, removal scan, allocation characterization, and OpenSpec scenarios against the implementation before declaring the change apply-complete.
