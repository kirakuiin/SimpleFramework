# Round 6: Cross-Cutting API, Documentation, Syntax, and Test Simplification

## Scope and Baseline

- Required starting HEAD: `cc226dc80717429ca359efdd58e7d9ddc4a812cf`; the worktree was clean.
- Reviewed all 75 tracked production C# files (17,291 physical lines), all 40 tracked files under `Test/` (17,624 physical lines), all 10 tracked project files, root/module README and USAGE files, and `docs/domain-lifecycle.md`.
- Baseline command `dotnet test .\SimpleFramework.sln --no-restore --configuration Debug` passed 751 tests with 0 failures and 0 skips.
- LOC command/definition: `git ls-files '*.cs'`, excluding `Test/` for production and selecting `Test/` for tests, then sum `(Get-Content file).Count`; generated `bin/` and `obj/` files are not tracked and are excluded.

## Required Inventories and Exhaustive Decisions

The two exact Step 1 commands were run from the repository root. The public-declaration inventory returned 1,061 textual matches. Every match was reviewed in its containing type, including public members nested in non-public implementation types. The complete path:line/declaration/disposition/reason-code evidence is in [the public inventory](./2026-07-10-round-6-public-inventory.md); the following table is its module summary:

| Group | Matches | Decision |
| --- | ---: | --- |
| Core root | 98 | `Get`/`TryGet`/`Require`, component registration, command/query/event, bindable, lifecycle, nullability, and exception contracts remain coherent. Legacy `UnRegister`/`RegisterAble` spellings are retained because a repository-wide rename offers no correctness gain and would create compatibility churn. |
| FrameworkImpl | 46 | Container lookup/removal and event registration ownership agree with Core. Public implementation helpers are documented and do not expose mutable collections. |
| Collections | 44 | Dictionary-compatible members intentionally expose `ICollection` views as required by the implemented dictionary contracts; comparer behavior and missing-key semantics are consistent. |
| Maths | 66 | Value types, operators, factories, validation exceptions, and finite-input contracts remain consistent; no cross-module naming conflict was found. |
| Utility | 85 | Synchronous disposal, logging, file/serialization nullability, task cancellation, and extension parameter validation match their local contracts. |
| Toolkit | 13 | INI load/get/save/dispose members and default-value semantics remain consistent; only clear range/target-typed syntax was simplified. |
| Patterns | 115 | `Get` in the intentionally minimal `ServiceLocator`, subscription `IDisposable` ownership, blackboard snapshots, pool disposal, and state-machine lifecycle contracts remain appropriate to the module. |
| ECS | 159 | Entity/world lookup, command-buffer `Resolve`/`TryResolve`, query read-only exposure, component refs, and system lifecycle/order remain coherent. |
| Net | 422 | All public async signatures, result/exception boundaries, events, snapshots, disposal, and token placement were compared. One-shot discovery scans were the only public long-running operation whose existing internal cancellation path was not exposed; repaired below. |
| GDExt | 13 | Godot-only pool, constants, and node-exit subscription extension remain small and consistent; missing Chinese public XML was completed. |

The risk inventory returned 118 matches: 34 production and 84 test matches. There were zero `TODO`, `FIXME`, or `NotImplementedException` matches and exactly one pragma match.

- Core (7): the five abstract command/query/model/system `Domain` initializers represent framework injection before use; `BindableProperty<T>` and unconstrained generic defaults legitimately allow `default(T)`; `AbstractDomain`'s `previous!` follows a proven non-null lifecycle branch. Retained.
- ECS (6): `Activator.CreateInstance(...)!` is immediately cast for a constructed concrete column type; the five `World` suppressions represent cleared slot sentinels or false-path `out` defaults. Making hot internal slots nullable would add checks without changing the public non-null contract. Retained.
- Net (14): Discovery false-path packet outputs (5), registry false-path descriptor output (1), messenger reflection/state/data success paths (6), and transport `TryGetValue` false-path assignments (2) are localized flow-analysis assertions. Each caller checks the corresponding result/state; retained.
- Patterns (4): blackboard stores intentional null values while its event arguments are present on the notified branches (3); buffered channels support unconstrained `T` before the first message and guard access with `HasBufferedMessage` (1). Retained.
- Toolkit (1): `IniConfigTool.Get<T>` intentionally permits `default(T)` as its optional fallback. Retained.
- Utility (2): `[MaybeNull] out T` file-load failure paths assign `default(T)` as documented. Retained.
- Tests (84): per-file counts were reviewed across all 25 matching test files. They consist of deliberate null inputs for argument-boundary tests, NUnit setup fields initialized in `SetUp`, reflection results asserted before use, nullable event payload assertions, and generic test fixtures. The sole pragma is `CS8602` in `UnitTestStateMachine.cs`, narrowly justified because NUnit runtime assertions do not narrow compiler flow. No broad warning disable or production pragma exists; all were retained.

## Cross-Module Findings and Changes

- Important API consistency: `NetDiscovery` already linked backend scans to caller/disposal cancellation internally, but both public one-shot `ScanAsync<TMetadata>` overloads forced `CancellationToken.None`. The ordinary overload is now `(TimeSpan duration, CancellationToken token = default)` and the explicit-schema overload is `(uint metadataSchemaId, TimeSpan duration, CancellationToken token = default)`. Distinct first parameter types remove overload ambiguity while keeping cancellation optional and last; both forward to the existing linked path. The internal implementation was named `ScanCoreAsync` and the trivial public forwarding methods no longer create unnecessary async state machines.
- Documentation: root README described removed Net types (`ITransport`, `ProtocolHandler`, `ConnectionModel`, and related APIs), attributed tag ownership to ECS entities, and documented a nonexistent Godot multiplayer transport. It now describes the current `GameNet`, ECS handle/World model, command buffer, and actual GDExt surface; the nullable Core example now uses `RequireModel`.
- Documentation: `Patterns/README.md` was an implementation prompt rather than module documentation. It now documents all six current patterns and gives API-valid subscription and parent-blackboard examples. `docs/domain-lifecycle.md` now names the actual `InvalidOperationException` from `Require*`.
- Public XML: completed the GDExt extension/channel/pool-clear contracts, discovery backend methods, discovery advertisement sentinel, metadata attribute constructor, versioned discovery packet, scan cancellation/return/exception contracts, and Net message-context positional parameters. Both public scan overloads now name every actual `InvalidOperationException` family: missing schema declaration, declared/requested schema mismatch, metadata-type mapping conflict, and schema-ID mapping conflict.
- Equivalent syntax: `IniConfigTool` uses target-typed `new()` and ranges for section/key/value slicing; `FileUtil` uses correctly spaced target-typed construction. Broader collection-expression, primary-constructor, switch-expression, or expression-body churn was rejected where it would not be shorter and clearer.
- Project files: target framework, nullable/implicit-using policy, references, conditional Godot dependencies, and test packaging were internally consistent; no project or dependency edit was justified.
- Empty `Collections`, `Maths`, and `Utility` module README files make no stale API claims. ECS README/USAGE and Net README examples were checked against current signatures and remain valid.
- API shape: a real compile-and-call regression proves ordinary, explicit-schema, cancellation-token, and `(duration, default)` calls are unambiguous. The explicit-schema overload intentionally moved `metadataSchemaId` first; this repository is unpublished and has no external consumers, so source/binary compatibility with the intermediate duration-first schema signature is not a constraint.

## Evidence Reproduction

- Public total: run the exact public command and inspect `$LASTEXITCODE`; assigning its output to `$matches`, `$matches.Count` is 1,061. Normalize each matched path with `-replace '\\','/'`, map root files to Core and first-directory paths to their module (with `FrameworkImpl` separate), then `Group-Object`; this reproduces the table totals, whose sum is 1,061.
- Round 7 final reconciliation rebuilt every affected `GameNet`, `NetDiscovery`, `NetMessenger`, and `NetStats` ledger row from the exact current source location and declaration. The total remains 1,061 (Net 422), with 1,061 unique source locations and zero source/ledger differences; the typed-send and browser-refresh rows identify their final-review contract repairs.
- Risk total: assign the exact risk command output to `$matches`; `$matches.Count` is 118. Filtering paths with `Test/` yields 84 across 25 files; the complement yields 34. Grouping the production complement reproduces Core 7, ECS 6, Net 14, Patterns 4, Toolkit 1, Utility 2. The category bullets above dispose every match in each resulting group; zero TODO/FIXME/NotImplementedException and the single test pragma are direct subsets of the same output.
- Scope manifests: `(git ls-files '*.cs' | Where-Object { $_ -notlike 'Test/*' }).Count` is 75; `(git ls-files 'Test/*.cs').Count` is 40; `(git ls-files '*.csproj').Count` is 10. Documentation scope is exactly `README.md`, `Collections/README.md`, `ECS/README.md`, `ECS/USAGE.md`, `Maths/README.md`, `Net/README.md`, `Patterns/README.md`, `Utility/README.md`, and `docs/domain-lifecycle.md`. The public appendix reconciles all 1,061 source locations with zero set difference.
- LOC: for each tracked C# path, sum `(Get-Content $path).Count`, selecting or excluding `Test/` as stated in Scope and Baseline. For the baseline, enumerate the same paths from `git ls-tree -r --name-only cc226dc80717429ca359efdd58e7d9ddc4a812cf` and count `git show "cc226dc80717429ca359efdd58e7d9ddc4a812cf:$path"`; this reproduces 17,291 production and 17,624 test lines.

## TDD Evidence

Repair A was appended to tracked Task 6 before production editing.

- First attempted RED exposed a missing `System.Reflection` test import and was corrected before accepting evidence.
- Valid RED: `DiscoveryScan_PublicOverloadsExposeTrailingCancellationToken` failed 1/1. Reflection showed only `(TimeSpan)` and `(TimeSpan, uint)` public overloads, so both expected trailing-token signatures were absent.
- Exact GREEN: the same filter passed 1/1 after the minimal public forwarding change.
- Affected fixture: `NetDiscoveryStatsTests` passed 65/65, protecting existing disposal cancellation, scan timeout, backend serialization, and browser behavior.

Repair B was appended after takeover identified the intermediate overload shape as an API concern.

- Valid RED: `DiscoveryScan_PublicApiIsUnambiguousAndForwardsCancellation` failed compilation with CS1503 because the desired schema-first calls did not match the duration-first schema overload. The same compiler run showed `(duration, default)` already selected the exact two-parameter cancellation overload rather than producing CS0121.
- GREEN: the exact filter passed 1/1 after the minimal schema parameter reorder and internal browser-call update. The test executes ordinary and explicit-schema zero-duration scans, then proves both overloads propagate a pre-cancelled token as `OperationCanceledException`.

## Test Simplification Review

All 40 tracked test files were reviewed by fixture, setup, operation, and externally asserted contract. The complete per-file contract families, candidate comparisons and decisions are in [the test inventory](./2026-07-10-round-6-test-inventory.md); its tracked-path reconciliation has zero set difference. No test was deleted: no pair had identical setup/operation/observable contract while also lacking boundary, error, ordering, concurrency, or regression value.

- Framework registration tests that look symmetric protect distinct System/Model/Utility lifecycle ownership and release ordering.
- ECS `Update`/delta-time, real/buffered entity, and stale/foreign handle pairs protect different overload and identity contracts.
- Patterns lifecycle tests target distinct setup/enter/update/exit failure phases; message-channel tests distinguish active/pending/buffered subscription ownership.
- Net dispose/error/cancellation tests often repeat setup but protect different public entry points, state transitions, races, or transport failure phases.
- Utility string/byte serialization and file JSON/binary tests protect distinct allocation and I/O paths.

No helper extraction made intent clearer than the local setup. Final test count increases only for the new public API contract test.

## Retained Cross-Cutting Decisions

- `Get`/`TryGet`/`Require` is used where absence is a normal query distinction. `ServiceLocator.Get` and other single-shape APIs retain their documented `KeyNotFoundException`/validation behavior rather than gaining unused variants.
- Cancellation tokens remain optional and last. Final-review gated transport tests demonstrated blocking waits in typed sends, so `GameNet` and `NetMessenger` typed send/broadcast APIs now expose channel selection followed by an optional token while retaining structured `NetSendResult` cancellation semantics.
- CPU/local ownership types use `IDisposable`; transports, discovery, browser, and `GameNet` use `IAsyncDisposable` because cleanup awaits active work. No sync-over-async bridge was introduced.
- Core returns `IUnRegister`, Patterns returns standard `IDisposable`, and Net exposes .NET events. These shapes match their ownership models; unifying names alone would be disruptive.
- Read-only query/registry/peer/diagnostic surfaces use cached wrappers or snapshots. Mutable collection views exist only on dictionary-compatible collection types where they are part of the implemented interface contract.
- No network-security expansion, architecture rewrite, project move, new dependency, or unmeasured hot-path redesign was introduced.

## Final Counts and Verification

The same physical-line command was used for baseline and final comparison; test count comes from the VSTest summary, not textual `[Test]` counting.

- Final production: 75 files and 17,373 physical lines, +82 lines. The increase is Chinese XML for previously undocumented public surfaces; the executable syntax/refactor changes are locally neutral or shorter.
- Final tests: 40 files and 17,649 physical lines, +25 lines; 752 tests, +1 API regression. No test was deleted.
- Round 7 final state: the same 40 tracked test files now contain 763 VSTest tests. The 11-test increase is limited to deterministic Net cancellation, public typed-send/channel, and inbound-kind performance/correctness regressions; the existing late-flow test was synchronized without changing the count.
- `dotnet test .\SimpleFramework.sln --no-restore --configuration Debug`: 752 passed, 0 failed, 0 skipped.
- `dotnet build .\SimpleFramework.sln --no-restore --configuration Release`: succeeded, 0 warnings, 0 errors, including GDExt.
- `dotnet msbuild .\GDExt\GDExt.csproj -getProperty:DefineConstants -p:Configuration=Release`: evaluated `GODOT;RELEASE;NET;NET8_0;NETCOREAPP`. Contrary to the review concern, `GDExt.csproj` defines `GODOT` unconditionally, so the normal Release GDExt compilation includes the guarded branch.
- `dotnet build .\GDExt\GDExt.csproj --no-restore --configuration Release`: succeeded with 0 warnings and 0 errors, providing focused compilation evidence for the `#if GODOT` edits without adding a dependency.
- Appendix reconciliation commands: public source/ledger 1,061/1,061, 1,061 unique locations, zero set difference and zero per-file ordering errors; test tracked/ledger 40/40, 40 unique paths and zero set difference.
- `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~TestSourceTextQuality"`: 1 passed, 0 failed.
- `git diff --check`: exit 0; only line-ending conversion notices were emitted.

Complete diff self-review confirmed each token remains optional and last, ordinary and schema-first calls compile without ambiguity, cancellation reaches the existing linked core, XML describes actual behavior, README examples use real constructors/methods, and no unrelated file entered scope. No known API concern remains from the intermediate overload shape. No independent reviewer was dispatched, as explicitly required.
