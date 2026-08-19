## 1. Lifecycle and Registry Foundations

- [x] 1.1 Add the private `DomainState` model and centralized operation guards to `AbstractDomain`, replacing lifecycle booleans without exposing new public state APIs.
- [x] 1.2 Add the public ambiguity exception with `RequestedType`, read-only `CandidateKeys`, category-aware diagnostics, nullable annotations, and Chinese XML documentation consistent with the core API.
- [x] 1.3 Implement the internal categorized `DomainComponentRegistry` with one primary key per local instance, exact lookup, unique assignable fallback, identity checks, parent-independent local resolution results, and deterministic activation metadata.
- [x] 1.4 Add weak identity-based lifecycle ownership tracking for Systems and Models, covering cross-key/category/Domain rejection and permanent released state without changing `IConstructable` implementations.

## 2. Domain Registration and Resolution

- [x] 2.1 Migrate System, Model, and Utility registration in `AbstractDomain` from `Container` plus lifecycle-key scanning to the categorized registry while preserving generic constraints and explicit `Register*As<T>` primary keys.
- [x] 2.2 Migrate `Get`, `TryGet`, `Require`, and `ToString` to categorized resolution with exact-key precedence, assignable ambiguity handling, local shadowing, and parent fallback only for a local miss.
- [x] 2.3 Implement active replacement as unpublish-and-release old before initializing new, leaving the key empty on either cleanup or initialization failure and never restoring the old instance.
- [x] 2.4 Keep Utility lifetime caller-managed and verify that the public `FrameworkImpl.Container` remains unchanged and exact-key only.

## 3. Initialization Transactions

- [x] 3.1 Add a Domain-owned initialization transaction that enlists nested new component entries and local event unregistration handles across the complete callback chain.
- [x] 3.2 Publish initializing lifecycle entries for dependency lookup, track initializing keys and instances, and reject cycles, duplicate instances, and all nested replacement attempts before existing entries are touched.
- [x] 3.3 Centralize the initialization operation matrix so lookups, queries, local event registration, and absent-key nested registration are allowed while commands, local event sending, and relationship mutation fail before user code runs.
- [x] 3.4 Implement dependency-safe rollback: clean every lifecycle component whose initialization started, remove Utilities without callbacks, unregister local event additions, and aggregate rollback failures with the original initialization exception.

## 4. Terminal Domain Teardown

- [x] 4.1 Rework Domain initialization and `UnInitialize` transitions so initialization-time teardown throws, initialization failure uses internal terminal cleanup, and reentrant or repeated teardown is idempotent.
- [x] 4.2 Implement full teardown order as children first, Systems before Models, and reverse successful activation within each category, unpublishing every component before its cleanup callback.
- [x] 4.3 Keep the Domain uninitializing through framework cleanup and derived `UnInit`, reject mutation/execution throughout that phase, continue all cleanup stages after errors, aggregate failures, and set `Disposed` in `finally`.
- [x] 4.4 Guard `AbstractDomain<T>.Instance` against same-type initialization reentrancy, cache only successfully initialized instances, reset creation guards on failure, and clear only the cached singleton reference on disposal.

## 5. Contract and Regression Tests

- [x] 5.1 Add focused lifecycle tests for singleton reentrancy, initialization-time teardown, terminal disposed operations, repeated teardown, derived `UnInit` repopulation attempts, and fresh singleton creation after disposal.
- [x] 5.2 Add registry tests for concrete-to-interface lookup, explicit exact-key disambiguation, structured ambiguity exceptions, category isolation, marker-interface constraints, and unchanged public `Container` behavior.
- [x] 5.3 Add hierarchy and ownership tests for parent fallback/local precedence/local ambiguity, cross-key/category/Domain rejection, post-cleanup non-reuse, and shared caller-managed Utilities.
- [x] 5.4 Add transaction tests for successful nested registration, failed outer initialization rollback of Systems/Models/Utilities/local events, cleanup-error aggregation, forbidden commands/events/relationships, nested replacement, and initialization cycles.
- [x] 5.5 Add replacement and teardown tests for empty-key failure semantics, untouched replacement candidate reuse, children/System/Model/reverse-activation order, cleanup-time visibility, exhaustive aggregation, and Utility runtime-role traps.
- [x] 5.6 Audit existing framework tests and examples that rely on multi-key aliases, component reuse, or previous replacement behavior; migrate them to the v1.1 contract without weakening unrelated coverage.

## 6. Documentation and Verification

- [x] 6.1 Update `IDomain` and related Chinese XML documentation plus README examples to explain one-key registration, assignable resolution, ambiguity, phase restrictions, Utility ownership, and terminal lifecycle behavior.
- [x] 6.2 Add a v1.1 migration guide covering intentional breaking changes, interface registration choices, fresh component factories, initialization side-effect boundaries, and source/package rollback to v1.0.0.
- [x] 6.3 Run targeted framework regression tests, then `dotnet test .\Test\Test.csproj` with the pinned .NET 9 SDK and resolve all failures.
- [x] 6.4 Run `dotnet build .\SimpleFramework.sln --configuration Release` with the pinned .NET 9 SDK or CI and finish with zero warnings and zero errors.
