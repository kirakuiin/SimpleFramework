# SimpleFramework Full Codebase Review Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete seven evidence-backed review and repair rounds so the whole solution is readable, robust, stable, performant where practical, consistently documented in Chinese, and ready for release.

**Architecture:** Treat each module group as an independently reviewable unit. Every round produces a concrete review record, uses test-first repair for behavior or API changes, verifies the full solution, commits once, and receives an independent Git-range review before the next round starts.

**Tech Stack:** C# / .NET 8, SDK 9.0 with `latestMinor` roll-forward, NUnit 3, MSBuild, Git, PowerShell.

## Global Constraints

- Work only on `codex/full-codebase-review`.
- Preserve the solution's project boundaries and lightweight architecture; do not perform a wholesale rewrite.
- Public APIs may change because the library has no external users, but every change must improve correctness or usability.
- Maintain accurate Chinese XML documentation for every public type and member touched.
- Use modern C# syntax whenever it makes the `net8.0` code shorter and clearer; reject syntax that makes intent harder to understand.
- Treat readability, usability, robustness, stability, and practical performance as release requirements.
- For Net, review correctness, lifecycle, concurrency, allocation, computation, and lock behavior; network security is out of scope.
- Do not add unrelated runtime dependencies.
- Behavior fixes, refactors, and API changes require a failing regression test before production code.
- Equivalent syntax-only and documentation-only edits require compiler, source-quality, and existing test protection.
- A round is complete only after its relevant tests, full Debug tests, Release build, Git checks, and independent review pass.
- Fix Critical and Important reviewer findings before entering the next round; accept Minor findings only when they improve clarity or reduce code without obscuring behavior.
- If an audit discovers a production defect, append a concrete TDD repair subtask to this plan before editing production code. The amendment must name exact files, test method, expected failure, exact implementation contract, verification command, and expected result; this rule prevents speculative fixes and placeholder steps.

## Review Record Format

Each round creates one Markdown file under `docs/superpowers/reviews/`. Its H1 is the exact task title (`Round 1: Core Framework and Project Contracts` through `Round 7: Independent Final Audit and Release Verification`), followed by these exact sections and only concrete content gathered during that round:

```markdown
## Files Reviewed

## Contracts Checked

## Findings and Decisions

## Changes

## Verification

## Independent Review
```

Record every inspected production file, every discovered issue with severity, the repair or reason for retaining current behavior, fresh command results, the reviewed Git range, and disposition of reviewer feedback. Do not use empty placeholders; write `No production change required` when an exhaustive check finds no actionable issue.

---

### Task 1: Core Framework and Project Contracts

**Files:**
- Review: `Directory.Build.props`, `global.json`, `SimpleFramework.csproj`
- Review: `Framework.cs`, `FrameworkExtension.cs`, `AbstractCommand.cs`, `AbstractQuery.cs`, `AbstractModel.cs`, `AbstractSystem.cs`, `AbstractDomain.cs`, `BindableProperty.cs`
- Review: `FrameworkImpl/Container.cs`, `FrameworkImpl/Event.cs`, `FrameworkImpl/FrameworkDefine.cs`, `FrameworkImpl/Traits.cs`
- Review tests: `Test/Framework/UnitTestFrame.cs`, `Test/Extensions/UnitTestExtension.cs`, `Test/Documentation/UnitTestSourceTextQuality.cs`
- Create: `docs/superpowers/reviews/2026-07-10-round-1-core.md`

**Interfaces:**
- Consumes: lifecycle and lookup contracts in `AGENTS.md`, `docs/domain-lifecycle.md`, and the approved design spec.
- Produces: verified Domain/component/event/command/query/bindable contracts, a round record, and one reviewable commit.

- [ ] **Step 1: Read every listed file and map public contracts**

Check initialization and release ordering, ownership versus lookup inheritance, component registration aliases, replacement lifecycle, local versus global events, command/query dispatch, nullable returns, comparer scope, exception behavior, and XML documentation accuracy. Record concrete observations directly in the round record.

- [ ] **Step 2: Run the focused baseline**

Run:

```powershell
dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~SimpleFramework.Test.Framework|FullyQualifiedName~SimpleFramework.Test.Extensions|FullyQualifiedName~SimpleFramework.Test.Documentation"
```

Expected: all selected tests pass with zero failures before repairs.

- [ ] **Step 3: Audit every contract against tests and implementation**

Trace at least one normal, duplicate, replacement, missing-value, parent lookup, and release path for each applicable abstraction. Search for null suppression, unchecked casts, duplicate lifecycle calls, mutable global state, stale registrations, public members without Chinese XML docs, and old syntax that can be simplified without changing behavior.

- [ ] **Step 4: Repair each concrete finding through a plan amendment and TDD**

For every behavior or API issue, first append its exact repair subtask beneath this task, then follow RED → GREEN → REFACTOR. Documentation or equivalent syntax findings may be edited directly after the audit record states why the change is behavior-neutral.

- [ ] **Step 5: Verify the complete round**

Run:

```powershell
dotnet test .\Test\Test.csproj --no-restore
dotnet build .\SimpleFramework.sln --no-restore --configuration Release
git diff --check
```

Expected: 627 baseline tests plus new tests pass, Release build has zero warnings/errors, and Git reports no whitespace errors.

- [ ] **Step 6: Commit round 1**

```powershell
git add -- Directory.Build.props global.json SimpleFramework.csproj *.cs FrameworkImpl Test/Framework Test/Extensions Test/Documentation docs/superpowers/plans/2026-07-10-full-codebase-review.md docs/superpowers/reviews/2026-07-10-round-1-core.md
git commit -m "review: harden core framework contracts"
```

- [ ] **Step 7: Request independent review**

Use `superpowers:requesting-code-review` with the pre-round and post-round SHAs. Resolve all Critical and Important findings, rerun Step 5, commit any review corrections as `review: address core review feedback`, and record the disposition in the round record.

#### Task 1 Repair A: Use an intentional missing-component exception

**Files:** `AbstractDomain.cs`, `Framework.cs`, `FrameworkExtension.cs`, `Test/Framework/UnitTestFrame.cs`

- [ ] Change `TestTryGetAndRequireModel`, `TestTryGetAndRequireUtility`, and `TestTryGetAndRequireSystem` to expect `InvalidOperationException` while retaining the missing type and domain assertions. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Framework.TestFramework.TestTryGetAndRequire"`; expect all three tests to fail because the implementation still throws `NullReferenceException`.
- [ ] Make `RequireModel`, `RequireUtility`, and `RequireSystem` throw `InvalidOperationException` with the same diagnostic type/domain details, and update every touched Chinese XML `<exception>` contract. Rerun the same command; expect all three tests to pass.

#### Task 1 Repair B: Reject null container registrations immediately

**Files:** `FrameworkImpl/Container.cs`, `Test/Framework/UnitTestFrame.cs`

- [ ] Add `TestContainerRegisterRejectsNull`, calling `Container.Register<IUtility>(null!)` and expecting `ArgumentNullException` naming `instance`. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Framework.TestFramework.TestContainerRegisterRejectsNull"`; expect failure because the Release-safe null guard is absent.
- [ ] Add `ArgumentNullException.ThrowIfNull(instance)` before changing container state and document the exception in Chinese XML. Rerun the focused command; expect the test to pass.

#### Task 1 Repair C: Express missing events without null suppression

**Files:** `FrameworkImpl/Event.cs`, `Test/Framework/UnitTestFrame.cs`

- [ ] Add `TestEventContainerMissingEventReturnsNull`, asserting a new `EventContainer` returns null for `GetEvent<Event<EventA>>()`. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Framework.TestFramework.TestEventContainerMissingEventReturnsNull"`; expect the runtime assertion to pass, demonstrating the public non-null signature disagrees with behavior.
- [ ] Change `GetEvent<T>` to return `T?`, remove the unchecked cast/null suppression through type-pattern matching, and update the Chinese XML return contract. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Framework.TestFramework.TestEventContainerMissingEventReturnsNull|FullyQualifiedName~Test.Framework.TestFramework.TestEventBus"`; expect all selected tests to pass with nullable analysis clean.

#### Task 1 Repair D: Prevent reentrant domain release from duplicating lifecycle calls

**Files:** `AbstractDomain.cs`, `Framework.cs`, `Test/Framework/UnitTestFrame.cs`

- [ ] Add `TestReentrantUninitializeReleasesComponentOnce` plus a guarded `ReentrantUninitializeModel` whose first `OnUninitialize` calls `Domain.UnInitialize()` and whose counter exposes duplicate release. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Framework.TestFramework.TestReentrantUninitializeReleasesComponentOnce"`; expect failure with an uninitialize count of 2 instead of 1.
- [ ] Treat a reentrant `UnInitialize` call as an idempotent no-op while the outer release owns cleanup, and document that contract on `IDomain.UnInitialize` and `AbstractDomain<T>.UnInitialize`. Rerun the focused command; expect one release and a passing test.

#### Task 1 Repair E: Read a bindable property's old value only once

**Files:** `BindableProperty.cs`, `Test/Framework/UnitTestFrame.cs`

- [ ] Add `TestBindableSetterReadsOldValueOnce` plus a `CountingBindableProperty` subclass that counts `GetValue` calls; assert one old-value read and one post-write read when notifying. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Framework.TestFramework.TestBindableSetterReadsOldValueOnce"`; expect failure because the setter reads the virtual getter three times.
- [ ] Capture the old value once before comparison, retain the post-write read used for the delivered current value, and add accurate Chinese XML documentation to the touched public/protected members. Rerun the focused command; expect two reads and a passing test.

#### Task 1 Repair F: Keep lifecycle replacement state atomic when cleanup fails

**Files:** `AbstractDomain.cs`, `Test/Framework/UnitTestFrame.cs`

- [ ] Add `TestRegisterModelReplacementCleanupFailureKeepsPreviousRegistration` plus a controllable lifecycle model whose `OnUninitialize` throws. Assert the cleanup exception remains directly diagnosable, the previous model remains registered, and the replacement is not initialized. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Framework.TestFramework.TestRegisterModelReplacementCleanupFailureKeepsPreviousRegistration"`; expect failure because the container currently publishes the replacement before releasing the previous model.
- [ ] Release a previous last lifecycle reference before replacing its container entry; determine last-reference status while excluding the key being replaced, preserve alias lifecycle behavior, and publish/initialize the replacement only after successful cleanup. Document the direct cleanup-exception contract. Rerun the focused command plus all `TestRegisterModel`, `TestRegisterSystem`, `TestDualRole`, and `TestUtility` lifecycle tests; expect all selected tests to pass.

#### Task 1 Repair G: Remove components whose initialization fails

**Files:** `AbstractDomain.cs`, `Test/Framework/UnitTestFrame.cs`

- [ ] Add `TestRegisterModelInitializationFailureDoesNotPublishComponent` plus a model that throws from `OnInitialize`; assert the original exception is propagated and the failed component is absent from lookup. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Framework.TestFramework.TestRegisterModelInitializationFailureDoesNotPublishComponent"`; expect failure because registration currently publishes the model before initialization succeeds.
- [ ] Roll back the new container/lifecycle-key entry when initialization fails, restoring a prior still-valid non-released entry when applicable, and document the direct initialization-exception contract. Rerun the focused command and all core lifecycle tests; expect the failed component to remain absent and all selected tests to pass.

#### Task 1 Repair H: Reject nested registration during replacement cleanup

**Files:** `AbstractDomain.cs`, `Test/Framework/UnitTestFrame.cs`

- [ ] Add `TestRegisterDuringReplacementCleanupDoesNotPublishNestedComponent` plus a model whose replacement cleanup attempts one nested registration. Assert `InvalidOperationException`, the old registration remains visible, and neither requested replacement initializes. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Framework.TestFramework.TestRegisterDuringReplacementCleanupDoesNotPublishNestedComponent"`; expect failure because nested registration currently publishes and initializes a component during outer cleanup.
- [ ] Guard last-reference replacement cleanup with a dedicated lifecycle-cleanup state and reject component registration while it is active, resetting the guard in `finally`; retain the outer old registration when the callback propagates the guard exception. Rerun the focused command and all core lifecycle tests; expect the nested and outer replacements to remain uninitialized and all tests to pass.

#### Task 1 Repair I: Consume custom unregister callbacks before invocation

**Files:** `FrameworkImpl/Event.cs`, `Test/Framework/UnitTestFrame.cs`

- [ ] Add `TestCustomUnregisterThrowingCallbackRunsOnce`, using a callback that increments a counter and throws `InvalidOperationException`; assert the first `UnRegister` propagates the original exception and a subsequent `Dispose` does not invoke the callback again. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Framework.TestFramework.TestCustomUnregisterThrowingCallbackRunsOnce"`; expect failure because the counter becomes 2 when the callback throws before the field is cleared.
- [ ] Capture the callback, clear the stored field before invocation, and invoke the captured callback without catching it so the original exception is preserved. Document in Chinese XML that the callback is consumed at most once even when it throws and that its exception propagates. Rerun the focused command; expect the first call to throw, the second call to complete, the counter to remain 1, and the test to pass.

### Task 2: Collections, Utility, Toolkit, and Maths

**Files:**
- Review: `Collections/Counter.cs`, `Collections/DefaultDict.cs`
- Review: `Utility/Disposable.cs`, `Utility/FileUtil.cs`, `Utility/Logging.cs`, `Utility/MiscUtil.cs`, `Utility/SerializeUtil.cs`, `Utility/TaskUtil.cs`, `Utility/TimeUtil.cs`
- Review: `Utility/Extensions/EnumeratorExtension.cs`, `Utility/Extensions/ListExtension.cs`, `Utility/Extensions/RandomExtension.cs`, `Utility/Extensions/StringExtension.cs`
- Review: `Toolkit/ConfigTool.cs`, `Toolkit/ToolkitDefine.cs`
- Review: `Maths/Common.cs`, `Maths/Matrix.cs`, `Maths/HexagonGrid.cs`
- Review tests: `Test/Collections`, `Test/Utility`, `Test/Toolkit`, `Test/Maths`
- Create: `docs/superpowers/reviews/2026-07-10-round-2-foundations.md`

**Interfaces:**
- Consumes: project-wide public API, documentation, TDD, and style constraints.
- Produces: verified collection, utility, configuration, and math contracts plus one reviewable commit.

- [ ] **Step 1: Run the focused baseline**

```powershell
dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~SimpleFramework.Test.Collections|FullyQualifiedName~SimpleFramework.Test.Utility|FullyQualifiedName~SimpleFramework.Test.Toolkit|FullyQualifiedName~SimpleFramework.Test.Maths"
```

Expected: all selected tests pass before repairs.

- [ ] **Step 2: Review all listed source and test files**

Check empty collections, missing keys, comparer preservation, enumeration invalidation, disposal idempotence, file-not-found and malformed data behavior, serializer options, cancellation, time boundaries, INI round trips, matrix dimensions, coordinate conversion, invalid hex ranges, allocation in repeated operations, public naming, XML docs, and opportunities for clearer collection expressions, pattern matching, ranges, target-typed construction, and expression bodies.

- [ ] **Step 3: Repair concrete findings with appended TDD subtasks**

Append exact subtasks before behavior changes. Prefer direct APIs and deterministic tests; do not add abstraction layers solely to facilitate testing.

- [ ] **Step 4: Create the round record and verify**

```powershell
dotnet test .\Test\Test.csproj --no-restore
dotnet build .\SimpleFramework.sln --no-restore --configuration Release
git diff --check
```

Expected: all tests pass, Release has zero warnings/errors, and no whitespace errors exist.

- [ ] **Step 5: Commit and independently review round 2**

```powershell
git add -- Collections Utility Toolkit Maths Test/Collections Test/Utility Test/Toolkit Test/Maths docs/superpowers/plans/2026-07-10-full-codebase-review.md docs/superpowers/reviews/2026-07-10-round-2-foundations.md
git commit -m "review: harden foundation modules"
```

Request review for the exact Git range, resolve Critical and Important feedback, rerun Step 4, and record the disposition.

#### Task 2 Repair A: Preserve complete Counter subtraction semantics

**Files:** `Collections/Counter.cs`, `Test/Collections/UnitTestCounter.cs`

- [ ] Add `TestSubtractIncludesRightOnlyKeys`, subtracting a counter that contains a key absent from the left operand and asserting that the result contains the negated right-hand count. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Collections.TestCounter.TestSubtractIncludesRightOnlyKeys"`; expect failure because binary subtraction currently enumerates only left-hand keys.
- [ ] Build the result from the union of both key sets and subtract missing values as zero. Rerun the same command; expect the right-only key to be present with its negative count and the test to pass.

#### Task 2 Repair B: Preserve caller-supplied key comparers

**Files:** `Collections/DefaultDict.cs`, `Collections/Counter.cs`, `Test/Collections/UnitTestDefaultDict.cs`, `Test/Collections/UnitTestCounter.cs`

- [ ] Add `TestUsesSuppliedComparer` and `TestCopyAndOperatorsPreserveComparer`; use reflection only for the initial RED so the tests compile before the comparer-aware constructors exist, then assert case-insensitive lookup, copy, addition, and subtraction keep one logical key. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Collections.TestDefaultDict.TestUsesSuppliedComparer|FullyQualifiedName~Test.Collections.TestCounter.TestCopyAndOperatorsPreserveComparer"`; expect both tests to fail because neither type accepts or exposes a comparer.
- [ ] Let `DefaultDict` accept an optional comparer and expose the effective comparer; let every `Counter` constructor and derived operator preserve the originating counter's comparer. Refactor the green tests to call the public APIs directly, rerun the same command, and expect both tests to pass without duplicate case-variant keys.

#### Task 2 Repair C: Reject a missing DefaultDict factory immediately

**Files:** `Collections/DefaultDict.cs`, `Test/Collections/UnitTestDefaultDict.cs`

- [ ] Add `TestConstructorRejectsNullFactory`, constructing the dictionary with a null callback and expecting `ArgumentNullException` naming `initCallback`. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Collections.TestDefaultDict.TestConstructorRejectsNullFactory"`; expect failure because construction currently succeeds and defers a null-reference failure until a missing-key read.
- [ ] Guard the constructor before storing the callback and document the exception in Chinese XML. Rerun the same command; expect the test to pass.

#### Task 2 Repair D: Complete grouped disposal deterministically

**Files:** `Utility/Disposable.cs`, `Test/Utility/UnitTestDisposable.cs`

- [ ] Add `TestDisposableGroupDisposesAllChildrenWhenOneThrows`, registering a throwing child before a tracking child and expecting an `AggregateException` containing the original failure while both children are called once. Add `TestDisposableGroupDisposesItemsAddedAfterDisposal`, asserting an item added after group disposal is immediately disposed. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Utility.TestDisposable.TestDisposableGroupDisposesAllChildrenWhenOneThrows|FullyQualifiedName~Test.Utility.TestDisposable.TestDisposableGroupDisposesItemsAddedAfterDisposal"`; expect failures because the first exception aborts cleanup and post-disposal additions are retained without disposal.
- [ ] Consume the group's children once, attempt every disposal in insertion order, aggregate failures deterministically, ignore null entries, and immediately dispose non-null items added after the group is closed. Rerun the same command; expect both tests to pass and repeated group disposal to remain a no-op.

#### Task 2 Repair E: Express nullable JSON results and remove the UTF-8 round-trip allocation

**Files:** `Utility/SerializeUtil.cs`, `Utility/FileUtil.cs`, `Test/Utility/UnitTestSerializeUtil.cs`

- [ ] Add `TestDeserializeDeclaresNullableResult`, using `NullabilityInfoContext` to require nullable return metadata on generic and runtime-type deserialize overloads, and `TestSerializeBytesAvoidsIntermediateStringAllocation`, comparing warmed repeated allocations for a large ASCII payload against string serialization. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Utility.TestSerializeUtil.TestDeserializeDeclaresNullableResult|FullyQualifiedName~Test.Utility.TestSerializeUtil.TestSerializeBytesAvoidsIntermediateStringAllocation"`; expect metadata and allocation assertions to fail because deserialize suppresses null and byte serialization creates a JSON string before UTF-8 encoding.
- [ ] Return nullable results without suppression, cache the default serializer options, call `JsonSerializer.SerializeToUtf8Bytes` and byte-span deserialize APIs directly, and make `FileUtil` treat a JSON `null` payload as a successful nullable result. Rerun the same command plus all serialization/file tests; expect nullable metadata, allocation, and round trips to pass.

#### Task 2 Repair F: Allow WaitUntil cancellation

**Files:** `Utility/TaskUtil.cs`, `Test/Utility/UnitTestTaskUtil.cs`

- [ ] Add `TestWaitUntilHonorsCancellation`, using reflection for the initial RED to require the direct four-parameter overload and invoking it with a pre-cancelled token. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Utility.TestTaskUtil.TestWaitUntilHonorsCancellation"`; expect failure because no token-aware API exists.
- [ ] Add an optional trailing `CancellationToken`, check cancellation before polling, and pass it into `Task.Delay`; document cancellation propagation in Chinese XML. Refactor the green test to call the API directly and rerun the same command; expect `OperationCanceledException` and a passing test.

#### Task 2 Repair G: Reject invalid utility boundary inputs

**Files:** `Utility/TimeUtil.cs`, `Utility/Extensions/RandomExtension.cs`, `Utility/Extensions/StringExtension.cs`, `Test/Utility/UnitTestTimeUtil.cs`, `Test/Extensions/UnitTestExtension.cs`

- [ ] Add `TestToMsThrowsOnOverflow`, `TestChoiceRejectsEmptyList`, `TestSampleRejectsNegativeCount`, and `TestRepeatRejectsNegativeCount`, asserting `OverflowException` or an argument exception naming the invalid parameter. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Utility.TestTimeUtil.TestToMsThrowsOnOverflow|FullyQualifiedName~Test.Extensions.TestRandom.TestChoiceRejectsEmptyList|FullyQualifiedName~Test.Extensions.TestRandom.TestSampleRejectsNegativeCount|FullyQualifiedName~Test.Extensions.TestString.TestRepeatRejectsNegativeCount"`; expect all four to fail because overflow wraps, empty choice fails through an index, and negative counts silently produce empty results.
- [ ] Use checked seconds-to-milliseconds arithmetic and direct parameter validation for empty/negative inputs, with exact Chinese XML exception contracts. Rerun the same command; expect all four tests to pass.

#### Task 2 Repair H: Persist typed INI values independently of ambient culture

**Files:** `Toolkit/ConfigTool.cs`, `Test/Toolkit/UnitTestIniConfigTool.cs`

- [ ] Add `TestTypedValuesRoundTripAcrossCultures`, setting a double and `DateTime` under `fr-FR`, saving, then loading and reading under `en-US`, while asserting an explicitly supplied raw string containing a comma is unchanged. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Toolkit.TestIniConfigTool.TestTypedValuesRoundTripAcrossCultures"`; expect failure because typed values currently use ambient-culture `ToString` and `Convert` behavior.
- [ ] Format non-string `IFormattable` values with invariant culture (using round-trip format for `DateTime`) and convert typed reads with invariant culture, while returning raw string values unchanged. Rerun the same command; expect exact numeric/date values and raw text to round-trip across cultures.

#### Task 2 Repair I: Reject non-finite matrix inversion

**Files:** `Maths/Matrix.cs`, `Test/Maths/UnitTestMatrix2D.cs`

- [ ] Add `TestNonFiniteMatrixThrows`, constructing matrices whose determinants are `NaN` or infinity and expecting `InvalidOperationException`. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Maths.TestMatrix2D.TestNonFiniteMatrixThrows"`; expect failure because the epsilon comparison lets non-finite determinants through and returns non-finite inverse values.
- [ ] Treat a non-finite determinant as non-invertible and document the exception contract. Rerun the same command and the existing inverse tests; expect all selected tests to pass.

#### Task 2 Repair J: Validate hex coordinates and checked arithmetic

**Files:** `Maths/HexagonGrid.cs`, `Test/Maths/UnitTestHexagonGrid.cs`

- [ ] Add `TestHexRejectsOverflowInvalidCoordinates`, `TestHexArithmeticThrowsOnOverflow`, and `TestFractionalHexRejectsNonFiniteCoordinates`. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Maths.TestHexagonGrid.TestHexRejectsOverflowInvalidCoordinates|FullyQualifiedName~Test.Maths.TestHexagonGrid.TestHexArithmeticThrowsOnOverflow|FullyQualifiedName~Test.Maths.TestHexagonGrid.TestFractionalHexRejectsNonFiniteCoordinates"`; expect failures because integer-sum overflow can satisfy the invariant, coordinate operators wrap, and NaN/infinity bypass the fractional sum check.
- [ ] Validate integer sums in widened arithmetic, perform integer coordinate arithmetic in checked context, reject each non-finite fractional coordinate with `ArgumentOutOfRangeException`, and retain `ArgumentException` for finite coordinates whose sum is invalid. Rerun the same command and all hex arithmetic/rounding tests; expect all selected tests to pass.

#### Task 2 Repair K: Make hex presets immutable and reject unusable layout inputs

**Files:** `Maths/HexagonGrid.cs`, `Test/Maths/UnitTestHexagonGrid.cs`

- [ ] Add `TestOrientationPresetsAreReadOnlyProperties`, `TestLayoutRejectsZeroOrNonFiniteSize`, and `TestDirectionMethodsNameInvalidDirection`; require preset properties rather than mutable fields, reject a zero/non-finite scale axis, and require invalid direction failures to name `direction`. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Maths.TestHexagonGrid.TestOrientationPresetsAreReadOnlyProperties|FullyQualifiedName~Test.Maths.TestHexagonGrid.TestLayoutRejectsZeroOrNonFiniteSize|FullyQualifiedName~Test.Maths.TestHexagonGrid.TestDirectionMethodsNameInvalidDirection"`; expect failures because presets are writable fields, invalid scales create non-finite inverse transforms, and list indexing reports `index`.
- [ ] Make `HexOrientation` readonly with get-only static presets, validate finite non-zero layout size axes while allowing finite negative mirroring scales, and validate direction values before lookup. Rerun the same command plus all layout/corner tests; expect all selected tests to pass.

#### Task 2 Repair L: Reject a non-finite hex layout origin

**Files:** `Maths/HexagonGrid.cs`, `Test/Maths/UnitTestHexagonGrid.cs`

- [ ] Add `TestLayoutRejectsNonFiniteOrigin`, constructing a layout with a NaN or infinite origin coordinate and expecting `ArgumentOutOfRangeException` naming `origin`. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Maths.TestHexagonGrid.TestLayoutRejectsNonFiniteOrigin"`; expect failure because non-finite origin values are currently stored and later poison every coordinate conversion.
- [ ] Validate both origin coordinates as finite while retaining every finite origin value. Rerun the same command and the existing layout round-trip tests; expect all selected tests to pass.

#### Task 2 Repair M: Make comparer semantics consistent across dictionary pairs and Counter operations

**Files:** `Collections/DefaultDict.cs`, `Collections/Counter.cs`, `Test/Collections/UnitTestDefaultDict.cs`, `Test/Collections/UnitTestCounter.cs`

- [ ] Add `TestPairContainsUsesConfiguredComparer`, `TestCrossComparerArithmeticThrows`, `TestCrossComparerRelationsThrow`, and `TestComparerCompatibilityControlsEqualityAndHashing`; retain same-comparer aggregation assertions. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~TestPairContainsUsesConfiguredComparer|FullyQualifiedName~TestCrossComparerArithmeticThrows|FullyQualifiedName~TestCrossComparerRelationsThrow|FullyQualifiedName~TestComparerCompatibilityControlsEqualityAndHashing"`; expect pair containment to ignore the configured comparer and cross-comparer operations to return inconsistent values instead of a documented failure.
- [ ] Delegate pair containment through `ICollection<KeyValuePair<TKey,TValue>>`, define comparer compatibility through comparer equality, reject binary arithmetic and every relational direction with `ArgumentException` naming the incompatible operand, keep incompatible equality false, and document the rule. Rerun the same command plus all Counter/DefaultDict tests; expect comparer-aware containment, symmetric failures, same-comparer aggregation, and equality/hash invariants to pass.

#### Task 2 Repair N: Complete logger cleanup and publish nullable DisposableGroup additions

**Files:** `Utility/Disposable.cs`, `Utility/Logging.cs`, `Test/Utility/UnitTestDisposable.cs`, `Test/Utility/UnitTestLogging.cs`

- [ ] Add `TestDisposableGroupAddDeclaresNullableParameter` and `TestClearHandlersDisposesEveryHandlerAndAggregatesFailures`, using two throwing handlers with a later tracker and asserting failure order. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~TestDisposableGroupAddDeclaresNullableParameter|FullyQualifiedName~TestClearHandlersDisposesEveryHandlerAndAggregatesFailures"`; expect nullable metadata to be non-null and logger cleanup to stop on the first failure.
- [ ] Change `DisposableGroup.Add` to `IDisposable?`; in `Logger.ClearHandlers`, snapshot and clear under the handler lock, dispose the snapshot without holding the shared collection lock, attempt every handler in order, and throw one `AggregateException` afterward. Update Chinese XML failure contracts and rerun the focused command plus all disposal/logging tests; expect complete cleanup and deterministic failures.

#### Task 2 Repair O: Validate WaitUntil boundaries and cap polling delay

**Files:** `Utility/TaskUtil.cs`, `Test/Utility/UnitTestTaskUtil.cs`

- [ ] Add `TestWaitUntilRejectsInvalidArguments`, `TestWaitUntilCapsDelayToRemainingTimeout`, and `TestWaitUntilCancellationInterruptsActiveDelay`; cover null predicate, negative timeout, interval zero/-1, an `int.MaxValue` interval with a short timeout, and cancellation after the predicate signals entry. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~TestWaitUntilRejectsInvalidArguments|FullyQualifiedName~TestWaitUntilCapsDelayToRemainingTimeout|FullyQualifiedName~TestWaitUntilCancellationInterruptsActiveDelay"`; expect missing parameter validation, oversleep, and an uninterruptible active delay without the token path.
- [ ] Validate arguments before polling, compute each delay as the smaller of interval and positive remaining timeout, and retain token propagation through the active delay. Document parameter and cancellation exceptions; rerun the focused command and all TaskUtil tests, expecting bounded normal timeout and prompt cancellation.

#### Task 2 Repair P: Round-trip DateTimeOffset values invariantly

**Files:** `Toolkit/ConfigTool.cs`, `Test/Toolkit/UnitTestIniConfigTool.cs`

- [ ] Add `TestDateTimeOffsetRoundTripsAcrossCultures`, writing under `fr-FR` and reading under `en-US` while asserting ticks and offset. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~TestDateTimeOffsetRoundTripsAcrossCultures"`; expect the read to return its default because `Convert.ChangeType` cannot create `DateTimeOffset`.
- [ ] Parse `DateTimeOffset` with invariant culture and round-trip styles, and document the actually supported typed conversions without claiming arbitrary conversion. Rerun the focused command and all Toolkit tests; expect exact ticks/offset and existing raw-string behavior to pass.

#### Task 2 Repair Q: Reject non-finite derived matrix and layout transforms

**Files:** `Maths/Matrix.cs`, `Maths/HexagonGrid.cs`, `Test/Maths/UnitTestMatrix2D.cs`, `Test/Maths/UnitTestHexagonGrid.cs`

- [ ] Add `TestInverseRejectsNonFiniteCandidate`, `TestOrientationRejectsNonFiniteAngle`, `TestLayoutRejectsDefaultOrNonFiniteOrientation`, `TestLayoutRejectsNonRepresentableReciprocal`, and `TestNegativeScaleMirrorsAndRoundTrips`; cover extreme finite matrices, `double.Epsilon` size, default orientation, non-finite angle, and finite negative reflection. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~TestInverseRejectsNonFiniteCandidate|FullyQualifiedName~TestOrientationRejectsNonFiniteAngle|FullyQualifiedName~TestLayoutRejectsDefaultOrNonFiniteOrientation|FullyQualifiedName~TestLayoutRejectsNonRepresentableReciprocal|FullyQualifiedName~TestNegativeScaleMirrorsAndRoundTrips"`; expect non-finite derived values to be accepted while the explicit negative-scale coverage already passes.
- [ ] Validate inverse candidate components, finite orientation matrices/inverse/start angle at the appropriate constructor/layout boundaries, and finite reciprocals/derived transforms, while retaining negative mirroring. Document exact exceptions and rerun the focused command plus all matrix/hex tests; expect all selected tests to pass.

#### Task 2 Repair R: Align XML contracts and FileUtil serialization failure behavior

**Files:** `Utility/FileUtil.cs`, `Utility/SerializeUtil.cs`, `Utility/Logging.cs`, `Utility/MiscUtil.cs`, `Utility/TimeUtil.cs`, `Utility/Extensions/RandomExtension.cs`, `Test/Utility/UnitTestFileUtil.cs`, `Test/Documentation/UnitTestSourceTextQuality.cs`

- [ ] Add `TestSaveAsJsonContainsSerializationFailures` using a cyclic object and asserting no exception/file, plus source-contract assertions for reviewed XML defects. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~TestSaveAsJsonContainsSerializationFailures|FullyQualifiedName~TestSourceTextQuality"`; expect cyclic serialization to escape because it occurs before the try block and source checks to expose inaccurate/empty tags.
- [ ] Move JSON serialization inside the SaveAsJson try block; correct Shuffle/Sample type-parameter and exception docs, MiscUtil/TimeUtil empty tags, JSON-null value-type behavior, and Logging parameter/failure contracts. Rerun the focused command, FileUtil/SerializeUtil tests, and source quality; expect behavior and docs to match with no compiler warnings.

#### Task 2 Repair S: Validate HexRound representability explicitly

**Files:** `Maths/HexagonGrid.cs`, `Test/Maths/UnitTestHexagonGrid.cs`

- [ ] Add `TestHexRoundRejectsUnrepresentableFiniteCoordinates`, passing extreme finite fractional coordinates that satisfy the cube sum and expecting a documented deterministic exception before integer conversion. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~TestHexRoundRejectsUnrepresentableFiniteCoordinates"`; expect conversion to produce implementation-defined integer results or a later unrelated failure.
- [ ] Validate finite rounded coordinates are within `int` range and the corrected cube coordinate remains representable before casting; throw `OverflowException` for an unrepresentable result and document it. Rerun the focused command plus all HexRound/line/layout tests; expect deterministic rejection and unchanged normal rounding.

#### Task 2 Repair T: Reject incompatible comparers before mutating Counter instances

**Files:** `Collections/Counter.cs`, `Test/Collections/UnitTestCounter.cs`

- [ ] Add `TestUpdateRejectsIncompatibleComparerBeforeMutation` and `TestSubtractRejectsIncompatibleComparerBeforeMutation`, using ordinal and ordinal-ignore-case counters whose first potential write would change existing state. Assert `ArgumentException` names `other` and the receiver remains exactly unchanged. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~TestUpdateRejectsIncompatibleComparerBeforeMutation|FullyQualifiedName~TestSubtractRejectsIncompatibleComparerBeforeMutation"`; expect both tests to fail because the mutating methods currently merge incompatible comparer domains.
- [ ] Call the existing comparer compatibility guard before either method begins iteration and document the Chinese XML exception contract. Rerun the focused command plus all Counter tests; expect incompatible calls to fail before mutation while compatible same-comparer updates and subtraction remain green.

#### Task 2 Repair U: Dispose removed logger handlers outside the shared lock

**Files:** `Utility/Logging.cs`, `Test/Utility/UnitTestLogging.cs`

- [ ] Add `TestRemoveHandlerDisposesOutsideLoggerLock`, whose disposal callback starts a coordinated task that must acquire the same logger lock and records whether it completes during the bounded callback window. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~TestRemoveHandlerDisposesOutsideLoggerLock"`; expect failure because `RemoveHandler` currently retains `_handlersLock` throughout the external callback.
- [ ] Detach the handler under `_handlersLock`, release the lock, and dispose only when removal succeeded. Preserve direct disposal-exception propagation and missing-handler no-op behavior, retain accurate Chinese XML documentation, then rerun the focused command plus all logging tests; expect the coordinated lock acquisition to complete without deadlock or timeout.

### Task 3: Patterns

**Files:**
- Review: `Patterns/Singleton.cs`, `Patterns/ServiceLocator.cs`, `Patterns/ObjectPool.cs`, `Patterns/MessageChannel.cs`, `Patterns/BlackBoard.cs`, `Patterns/StateMachine.cs`, `Patterns/PatternDefine.cs`
- Review tests: `Test/Patterns`
- Create: `docs/superpowers/reviews/2026-07-10-round-3-patterns.md`

**Interfaces:**
- Consumes: Collections and Utility behavior plus hierarchy/lifecycle conventions.
- Produces: verified pattern implementations, deterministic state transitions, and one reviewable commit.

- [ ] **Step 1: Run the Patterns baseline**

```powershell
dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~SimpleFramework.Test.Patterns"
```

Expected: all Patterns tests pass before repairs.

- [ ] **Step 2: Review implementation and tests exhaustively**

Trace singleton construction, service replacement and removal, pool duplicate returns and reset behavior, message subscription mutation during dispatch, Blackboard parent lookup and lock usage, state entry/exit ordering, hierarchical transitions, reentrancy, invalid transitions, exception paths, hot-loop allocations, XML docs, and modern syntax opportunities.

- [ ] **Step 3: Repair findings using appended TDD subtasks**

Use deterministic tests for reentrancy and concurrency; never use timing sleeps as proof of ordering. Keep lock scope and transition order explicit in both code and Chinese documentation.

- [ ] **Step 4: Record, verify, commit, and review round 3**

```powershell
dotnet test .\Test\Test.csproj --no-restore
dotnet build .\SimpleFramework.sln --no-restore --configuration Release
git diff --check
git add -- Patterns Test/Patterns docs/superpowers/plans/2026-07-10-full-codebase-review.md docs/superpowers/reviews/2026-07-10-round-3-patterns.md
git commit -m "review: harden reusable patterns"
```

Request review for the round range, resolve Critical and Important feedback, rerun verification, and record the disposition.

#### Task 3 Repair A: Do not cache a singleton whose initialization fails

**Files:** `Patterns/Singleton.cs`, `Test/Patterns/UnitTestSingleton.cs`

- [ ] Add `TestInitializationFailureDoesNotPublishInstance`, using a singleton whose first `Initialize` call throws and whose second succeeds; assert the first access throws, the second returns an initialized instance, and two instances were constructed. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Patterns.TestSingleton.TestInitializationFailureDoesNotPublishInstance"`; expect failure because the first, uninitialized instance remains cached.
- [ ] Initialize a local candidate before publishing it under the singleton lock, leaving `_instance` null when initialization throws. Rerun the focused command; expect the retry to construct and publish one initialized instance.

#### Task 3 Repair B: Reject duplicate object-pool returns

**Files:** `Patterns/ObjectPool.cs`, `Test/Patterns/UnitTestObjectPool.cs`

- [ ] Add `TestDuplicateReturnIsRejectedWithoutDuplicatingObject`, returning one object twice, expecting `InvalidOperationException`, then asserting the pool contains and lends that object only once. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Patterns.TestObjectPool.TestDuplicateReturnIsRejectedWithoutDuplicatingObject"`; expect failure because the duplicate return succeeds and increments the count to two.
- [ ] Track pooled references with a reference-identity `HashSet<T>`, reject an already-pooled reference before callbacks, roll membership back if return callbacks fail, and remove membership on get/clear. Rerun the focused command; expect one rejected duplicate and one reusable object.

#### Task 3 Repair C: Make message mutation safe across reentrant publication and exceptions

**Files:** `Patterns/MessageChannel.cs`, `Test/Patterns/UnitTestMessageChannel.cs`

- [ ] Add `TestHandlerCanUnsubscribeBeforeReentrantPublish`, where the first handler disposes itself and publishes recursively while a second handler records both messages; assert no exception, the removed handler sees only the outer message, and the remaining handler sees inner then outer. Add `TestSubscriptionMutationIsAppliedWhenHandlerThrows`, where a handler disposes itself then throws and is absent on the next publish. Run both through `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Patterns.TestMessageChannel.TestHandlerCanUnsubscribeBeforeReentrantPublish|FullyQualifiedName~Test.Patterns.TestMessageChannel.TestSubscriptionMutationIsAppliedWhenHandlerThrows"`; expect the reentrant test to fail from active-list mutation, while the exception-path characterization already passes because the next publication flushes pending removal.
- [ ] Track publication depth, never apply pending mutations while any publication frame is iterating, skip handlers pending removal, and apply pending changes in the outermost `finally`. Rerun the focused command; expect deterministic ordering and exception-safe cleanup.

#### Task 3 Repair D: Preserve buffered-channel state after disposal

**Files:** `Patterns/MessageChannel.cs`, `Test/Patterns/UnitTestMessageChannel.cs`

- [ ] Add `TestDisposedBufferedChannelRejectsPublishWithoutChangingBuffer`, publish an initial value, dispose, attempt a second publish, and assert `ObjectDisposedException` plus the original buffered value. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Patterns.TestMessageChannel.TestDisposedBufferedChannelRejectsPublishWithoutChangingBuffer"`; expect failure because the buffer changes before the base disposed guard runs.
- [ ] Expose the disposed guard to derived channels and invoke it before updating buffered state. Rerun the focused command; expect the exception without mutation.

#### Task 3 Repair E: Reject states and transitions owned by another machine

**Files:** `Patterns/StateMachine.cs`, `Test/Patterns/UnitTestStateMachine.cs`

- [ ] Change `TestAddNullState` to expect `ArgumentNullException`; add `TestInitialStateMustBelongToMachine` and `TestTransitionStatesMustBelongToMachine`, asserting assignments/transitions involving unregistered or foreign-owned states throw `InvalidOperationException`. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Patterns.TestStateMachine.TestAddNullState|FullyQualifiedName~Test.Patterns.TestStateMachine.TestInitialStateMustBelongToMachine|FullyQualifiedName~Test.Patterns.TestStateMachine.TestTransitionStatesMustBelongToMachine"`; expect failures because null is dereferenced and ownership is unchecked.
- [ ] Guard null state input, prevent a state from joining multiple machines, validate non-null initial/source states and target states belong to the receiving machine, and document each exception contract. Rerun the focused command; expect all invalid operations to be rejected before state changes.

#### Task 3 Repair F: Exit hierarchical states from leaf to root

**Files:** `Patterns/StateMachine.cs`, `Test/Patterns/UnitTestStateMachine.cs`

- [ ] Add `TestNestedStatesExitChildBeforeParent`, recording callbacks while deactivating a two-level active hierarchy and asserting `child-exit` precedes `parent-exit`. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Patterns.TestStateMachine.TestNestedStatesExitChildBeforeParent"`; expect the current parent-first order.
- [ ] Deactivate the child state machine before invoking the parent state's exit callback, while retaining parent-first entry. Rerun the focused command; expect leaf-to-root exit order.

#### Task 3 Repair G: Remove per-dispatch transition-list allocation

**Files:** `Patterns/StateMachine.cs`

- [ ] After Repairs E and F are green, replace the `Where(...).ToList()` transition search with a direct ordered loop that selects the same first matching transition. This is behavior-neutral and is protected by the existing transition, AnyState, duplicate-transition, and workflow tests.
- [ ] Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Patterns.TestStateMachine"`; expect all state-machine tests to pass with unchanged first-match semantics.

#### Task 3 Repair H: Reject null service registrations at the public boundary

**Files:** `Patterns/ServiceLocator.cs`, `Test/Patterns/UnitTestServiceLocator.cs`

- [ ] Add `TestRegisterRejectsNullService`, passing `null!` to `Register<TestService>` and expecting `ArgumentNullException` naming `service`; then assert lookup still reports the service as missing. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Patterns.TestServiceLocator.TestRegisterRejectsNullService"`; expect failure because the null value is stored and returned as though it were a service.
- [ ] Add a release-safe null guard before mutating the service dictionary and document the exception contract. Rerun the focused command; expect immediate rejection and unchanged locator state.

#### Task 3 Repair I: Allow a message handler to dispose its channel safely

**Files:** `Patterns/MessageChannel.cs`, `Test/Patterns/UnitTestMessageChannel.cs`

- [ ] Add `TestHandlerCanDisposeChannelDuringPublish`, with a first handler that disposes the channel and a second handler that records delivery; assert publication does not throw, the second handler is not called, and the channel is disposed. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Patterns.TestMessageChannel.TestHandlerCanDisposeChannelDuringPublish"`; expect failure because disposal clears the list currently being enumerated.
- [ ] Mark the channel disposed immediately but defer clearing the active handler list until the outermost publication frame exits; stop delivery once disposal is observed. Rerun the focused command; expect safe termination with no later handler invoked.

#### Task 3 Repair J: Reject transitions reentered from state lifecycle callbacks

**Files:** `Patterns/StateMachine.cs`, `Test/Patterns/UnitTestStateMachine.cs`

- [ ] Add `TestExitCallbackCannotReenterTransition`, where an exit callback catches the exception from dispatching a second transition and records it; assert the outer transition reaches its intended target and the nested target is never entered. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Patterns.TestStateMachine.TestExitCallbackCannotReenterTransition"`; expect failure because the nested transition succeeds before being silently overwritten.
- [ ] Track the state-change lifecycle scope and make `Dispatch` throw `InvalidOperationException` while entry or exit callbacks are running, resetting the guard in `finally`; document the reentrancy contract. Rerun the focused command; expect the nested transition to be rejected without corrupting the outer transition.

#### Task 3 Review Repair K: Reject activation changes reentered from lifecycle callbacks

**Files:** `Patterns/StateMachine.cs`, `Test/Patterns/UnitTestStateMachine.cs`

- [ ] Add `TestTransitionExitCannotDeactivateMachine` and `TestDeactivationExitCannotReactivateMachine`. In each exit callback, capture the `InvalidOperationException` from reentrant `SetActive`; assert the outer operation completes atomically, with one exit callback and a consistent `IsActive`/`CurrentState` pair. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Patterns.TestStateMachine.TestTransitionExitCannotDeactivateMachine|FullyQualifiedName~Test.Patterns.TestStateMachine.TestDeactivationExitCannotReactivateMachine"`; expect failures because `SetActive` currently mutates lifecycle state without consulting the transition guard.
- [ ] Check the lifecycle guard at the start of `SetActive`, before equality checks or mutation, and wrap both activation and deactivation lifecycle callbacks in the same `try/finally` guard used by transitions. Rerun the focused command; expect reentrant calls to fail before mutation while outer operations finish in a consistent state.

#### Task 3 Review Repair L: Validate child states before hierarchy mutation

**Files:** `Patterns/StateMachine.cs`, `Test/Patterns/UnitTestStateMachine.cs`

- [ ] Add `TestAddNullChildDoesNotCreateHierarchy`, expecting `ArgumentNullException` naming `subState` and no child state machine, and `TestAddForeignChildPreservesOriginalHierarchy`, attempting to attach an already-owned nested child elsewhere and asserting its owner, depth, original-parent event propagation, and target parent remain unchanged. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Patterns.TestStateMachine.TestAddNullChildDoesNotCreateHierarchy|FullyQualifiedName~Test.Patterns.TestStateMachine.TestAddForeignChildPreservesOriginalHierarchy"`; expect failures because the method creates hierarchy state or rewrites the parent reference before validation.
- [ ] Validate null and ownership before creating `ChildrenStateMachine` or assigning `_parentStateRef`; then attach the child only after `StateMachine.AddState` succeeds. Document parameters and exceptions in Chinese. Rerun the focused command; expect both failures to leave hierarchy and propagation unchanged.

#### Task 3 Review Repair M: Prove object-pool return rollback on callback failure

**Files:** `Patterns/ObjectPool.cs`, `Test/Patterns/UnitTestObjectPool.cs`

- [ ] Add `TestConfiguredOnReturnFailureCanRetry` and `TestListenerOnReturnFailureCanRetry`, asserting a failed return leaves `Count` at zero and permits the same reference to be returned successfully after the callback stops throwing. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Patterns.TestObjectPool.TestConfiguredOnReturnFailureCanRetry|FullyQualifiedName~Test.Patterns.TestObjectPool.TestListenerOnReturnFailureCanRetry"`; characterize the current implementation without calling a passing run RED.
- [ ] Temporarily isolate the regression by removing membership rollback from the catch path, run the same focused command and require both tests to fail as duplicate returns, restore the rollback, then rerun and require both tests to pass. Commit no production change unless the characterization reveals a defect.

#### Task 3 Review Repair N: Correct state-machine XML ownership contracts

**Files:** `Patterns/StateMachine.cs`

- [ ] Move the `toState`, event-name, and ownership exception documentation from `AddEventHandler` to `AddTransition`; remove the nonexistent parameter reference and give `AddEventHandler`, `SetActive`, and touched child-state APIs accurate Chinese parameter, return, and exception contracts.
- [ ] Run the Release solution build and source-quality fixture; expect zero warnings/errors and the source-quality test to pass.

#### Task 3 Final Repair O: Make activation and deactivation exception-atomic

**Files:** `Patterns/StateMachine.cs`, `Test/Patterns/UnitTestStateMachine.cs`

- [ ] Add `TestDeactivationExitExceptionPreservesActiveState`, `TestUncaughtReentrantActivationPreservesActiveState`, and `TestActivationEnterExceptionPreservesInactiveState`. Assert the original exception instance propagates, failed deactivation retains `IsActive == true` and the original `CurrentState`, failed activation retains `IsActive == false` and null `CurrentState`, callback counts are exact, and a later lifecycle call proves the guard reset. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Patterns.TestStateMachine.TestDeactivationExitExceptionPreservesActiveState|FullyQualifiedName~Test.Patterns.TestStateMachine.TestUncaughtReentrantActivationPreservesActiveState|FullyQualifiedName~Test.Patterns.TestStateMachine.TestActivationEnterExceptionPreservesInactiveState"`; expect all three to fail because lifecycle fields are currently mutated before callbacks complete.
- [ ] Commit activation/deactivation fields only after successful callbacks or restore their prior values when activation entry fails; keep the shared reentrancy guard checked before mutation and reset in `finally`. Document direct callback exception propagation and post-failure field state in Chinese XML. Rerun the focused command; expect exact exception identity and consistent final fields for all three paths.

#### Task 3 Final Repair P: Roll back failed buffered replay subscriptions

**Files:** `Patterns/MessageChannel.cs`, `Test/Patterns/UnitTestMessageChannel.cs`

- [ ] Add `TestBufferedReplayFailureDoesNotLeakSubscription`, buffering one value, subscribing a handler that throws the same exception during replay, then publishing again and asserting the failed handler is not invoked. Also add `TestBufferedReplayFailureDuringPublishDoesNotLeakPendingSubscription` to exercise the same rollback while the channel is dispatching. Run the first test with `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Patterns.TestMessageChannel.TestBufferedReplayFailureDoesNotLeakSubscription"`; expect failure because `base.Subscribe` leaves the handler pending when replay throws. After the repair, run both tests and expect no leaked ordinary or in-dispatch pending registration.
- [ ] Catch replay failure, dispose the subscription token to cancel pending or active registration, and rethrow without wrapping so exception identity is preserved. Rerun the focused command; expect one replay invocation, no later delivery, and the original exception instance.

#### Task 3 Failure-State Repair Q: Fail closed after state lifecycle callback errors

**Files:** `Patterns/StateMachine.cs`, `Test/Patterns/UnitTestStateMachine.cs`

- [ ] Update the prior deactivation exception tests to require inactive/null fail-closed fields, and add `TestHierarchicalDeactivationFailureClosesParentAndChild` plus `TestTransitionTargetChildEnterFailureClosesHierarchy`. Assert original exception identity, coherent parent/child active/current pairs, guard reset, and explicit reactivation after callbacks stop throwing. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Patterns.TestStateMachine.TestDeactivationExitExceptionFailsClosed|FullyQualifiedName~Test.Patterns.TestStateMachine.TestUncaughtReentrantActivationFailsClosed|FullyQualifiedName~Test.Patterns.TestStateMachine.TestHierarchicalDeactivationFailureClosesParentAndChild|FullyQualifiedName~Test.Patterns.TestStateMachine.TestTransitionTargetChildEnterFailureClosesHierarchy"`; expect failures because deactivation currently restores active/current fields and transition entry failure leaves the target installed.
- [ ] Make `SetActive` and `ChangeToState` set inactive/null on any entry or exit callback failure, reset the guard in `finally`, and rethrow the original exception. Retain activation failure's existing inactive/null behavior and document fail-closed semantics in Chinese. Rerun the focused command; expect every failed hierarchy to be inactive/null and recover only through an explicit later activation.

#### Task 3 Failure-State Repair R: Roll back state ownership when setup fails

**Files:** `Patterns/StateMachine.cs`, `Test/Patterns/UnitTestStateMachine.cs`

- [ ] Add `TestRootSetupFailureDoesNotPublishOwnership` and `TestChildSetupFailureDoesNotMutateHierarchy`, using the same exception instance and retry flags. Assert failed states have no owner, cannot be transition targets, retain depth/event propagation, do not create a child hierarchy, and can be added successfully after setup stops throwing. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Patterns.TestStateMachine.TestRootSetupFailureDoesNotPublishOwnership|FullyQualifiedName~Test.Patterns.TestStateMachine.TestChildSetupFailureDoesNotMutateHierarchy"`; expect failures because root setup leaves list/owner state published and child setup leaks its child-machine owner.
- [ ] Preserve setup-time access to `StateMachine`, but remove the state from the machine and clear its owner when setup throws; publish parent/hierarchy references only after child setup succeeds. Rethrow without wrapping and rerun the focused command; expect clean retryable state after both failures.

#### Task 3 Failure-State Repair S: Give each message subscription token real ownership

**Files:** `Patterns/MessageChannel.cs`, `Test/Patterns/UnitTestMessageChannel.cs`

- [ ] Add `TestFailedDuplicateBufferedReplayKeepsOriginalSubscription`, where the first buffered subscription of handler `h` succeeds and a duplicate replay throws before the publish boundary; assert rollback of the failed attempt does not remove the first registration. Add `TestSubscribeRejectsNullHandler` for base and buffered channels, asserting `ArgumentNullException` names `handler`. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.Patterns.TestMessageChannel.TestFailedDuplicateBufferedReplayKeepsOriginalSubscription|FullyQualifiedName~Test.Patterns.TestMessageChannel.TestSubscribeRejectsNullHandler"`; expect duplicate rollback to remove the earlier pending registration and null handling to lack the explicit public guard.
- [ ] Reject null at the base boundary; when a handler is already effectively subscribed, leave mutation state unchanged and return a no-op token. Return an owning token only when that call creates or restores a registration, so buffered replay rollback removes only its own registration. Document the boundary and rerun the focused command; expect original duplicate registration and pending semantics to survive.

#### Task 3 Ownership Closure Repair T: Bind subscription tokens to registration generations

**Files:** `Patterns/MessageChannel.cs`, `Test/Patterns/UnitTestMessageChannel.cs`

- [ ] Add `TestStaleSubscriptionTokenDoesNotCancelNewRegistration`: subscribe handler `h`, explicitly unsubscribe it, subscribe `h` again, dispose the first token, publish, then dispose the second token and publish again. Require the first disposal to leave the newer registration active and the second disposal to remove only that registration. Run its exact fully-qualified filter and expect failure because tokens currently unsubscribe by handler identity only.
- [ ] Give every actually created active or pending registration a monotonically increasing identifier. Make owning-token disposal remove only the matching handler and identifier; keep duplicate/effective subscriptions non-owning and preserve deferred mutation plus nested publication semantics. Rerun the focused test and affected message-channel tests.

#### Task 3 Ownership Closure Repair U: Make subscription lifetime explicitly controlled

**Files:** `Patterns/MessageChannel.cs`, `Test/Patterns/UnitTestMessageChannel.cs`

- [ ] Add `TestCollectedSubscriptionTokenDoesNotUnsubscribeHandler` using a no-inline helper, `WeakReference`, and forced collection/finalization to prove a dropped token is collected while publication still invokes its handler. Add `TestSubscriptionImplementationIsNotPublicApi` to prevent callers manufacturing a token for an unowned registration. Run both exact filters and expect failure from finalizer-driven unsubscription and the exported `DisposableSubscription<T>` type.
- [ ] Remove finalizer-driven token disposal; only explicit token disposal or channel subscription APIs may mutate registration state. Hide the concrete token implementation while retaining the public `IDisposable` return contract, and document explicit token lifetime in Chinese. Rerun the focused tests.

#### Task 3 Ownership Closure Repair V: Isolate setup from state-machine lifecycle mutation

**Files:** `Patterns/StateMachine.cs`, `Test/Patterns/UnitTestStateMachine.cs`

- [ ] Add `TestSetupCannotActivateOrDispatchAndFailureIsRetryable`, whose setup can access its owner, configures a transition/handler and child hierarchy, verifies `SetActive(true)` and `Dispatch` are rejected before mutation, then throws. Require inactive/null lifecycle fields, cleared owner/list/initial/transition/hierarchy state, reset guards, and a successful retry after failure is disabled. Run its exact filter and expect failure because setup currently permits activation and dispatch and failed setup retains hierarchy.
- [ ] Track setup depth independently from transition/lifecycle reentrancy. Reject `SetActive` and `Dispatch` at setup entry before logging or mutation, retain setup-time transition and handler configuration, and roll back hierarchy created by a failing setup together with existing ownership/list/initial/transition cleanup. Rerun the focused test and all state-machine tests.

#### Task 3 Ownership Closure Repair W: Enforce explicit null contracts

**Files:** `Patterns/MessageChannel.cs`, `Patterns/ObjectPool.cs`, `Test/Patterns/UnitTestMessageChannel.cs`, `Test/Patterns/UnitTestObjectPool.cs`

- [ ] Add `TestUnsubscribeRejectsNullHandler` and `TestCreateFuncCannotBeNull`, asserting `ArgumentNullException` with parameters `handler` and `createFunc`. Run their exact filters and expect failure because dictionary validation currently reports `key` and the pool constructor throws `ArgumentException`.
- [ ] Validate both public boundaries explicitly and add accurate Chinese XML exception contracts. Rerun the focused tests.

#### Task 3 Hierarchy Closure Repair X: Make setup isolation and rollback recursive

**Files:** `Patterns/StateMachine.cs`, `Test/Patterns/UnitTestStateMachine.cs`

- [ ] Add `TestSetupFailureRecursivelyRestoresExistingHierarchy`, starting with a pre-existing child machine and state. During root setup, catch the expected descendant activation guard, add a grandchild below the existing child, mutate child-machine initial state, transition, handler, and `TriggerUpdateWhenStateChange`, then throw one original exception. Assert exact exception identity; inactive/null lifecycle state throughout the hierarchy; detached grandchild ownership, parent, depth, and propagation; exact pre-existing configuration restoration; no leaked transition/handler/list entry; reset guards; and successful retry. Run its exact fully-qualified filter and expect failure because setup guards and snapshots currently stop at the current machine and immediate hierarchy.
- [ ] Give child machines an owner-state link and consult ancestor setup scopes before lifecycle mutation. Replace shallow setup rollback with focused recursive machine/state snapshots that restore pre-existing and newly-created descendant hierarchy, lifecycle fields, initial state, transitions, handlers, and `TriggerUpdateWhenStateChange`, while preserving successful setup configuration. Rerun the focused test and all state-machine tests.

#### Task 3 Hierarchy Closure Repair Y: Fail closed recursively after update and lifecycle errors

**Files:** `Patterns/StateMachine.cs`, `Test/Patterns/UnitTestStateMachine.cs`

- [ ] Add `TestTransitionTargetUpdateFailureRecursivelyClosesHierarchy`, transitioning to a target with an active child/grandchild hierarchy and `TriggerUpdateWhenStateChange == true`, whose immediate target update throws a retained exception. Assert exact exception identity, inactive/null fields for every entered machine, reset guards, and explicit recovery when throwing is disabled. Run its exact fully-qualified filter and expect failure because only the root catch is currently failed closed.
- [ ] Add a callback-free recursive fail-close helper and use it consistently from activation, transition, and update exception catches. It must clear active/current lifecycle fields and guards for every reachable descendant without invoking more enter/exit/update callbacks, then rethrow the original exception unchanged. Rerun the focused test and all state-machine tests.

#### Task 3 Hierarchy Closure Repair Z: Clarify duplicate subscription handle ownership

**Files:** `Patterns/MessageChannel.cs`

- [ ] Update base and buffered `Subscribe` XML so the return contract explicitly states an already-effective duplicate subscription may return a non-owning no-op handle. Run the Release build and source-quality fixture.

#### Task 3 Setup Transaction Repair AA: Recursively detach removed setup subtrees

**Files:** `Patterns/StateMachine.cs`, `Test/Patterns/UnitTestStateMachine.cs`

- [ ] Add `TestSetupFailureRecursivelyDetachesNewSubtree`, whose ancestor setup adds a temporary state whose own setup successfully adds a child and deeper descendant before the ancestor throws one retained exception. Assert exact exception identity; null ownership for all three new states; depth one and no event propagation for every detached node; inactive/null child machines; no leaked transitions or membership; and a clean successful retry. Run its exact fully-qualified filter and expect failure because setup rollback clears only the removed temporary state's direct ownership and parent link.
- [ ] Before clearing any removed state's direct owner/parent, recursively detach every state in its `ChildrenStateMachine`; make `DetachAllStates` apply the same descendant-first cleanup. Define failed setup cleanup as removal of all newly-added subtree membership, transitions, lifecycle state, ownership, and parent propagation links while leaving pre-existing hierarchy objects reusable. Rerun the focused test and all state-machine tests.

#### Task 3 Setup Transaction Repair AB: Reject updates throughout an active setup hierarchy

**Files:** `Patterns/StateMachine.cs`, `Test/Patterns/UnitTestStateMachine.cs`

- [ ] Add `TestSetupCannotUpdateRootOrActiveDescendant`, keeping a root and pre-existing descendant active while another root state is being set up. Invoke both machines' public `Update` methods from setup, capture `InvalidOperationException`, and assert neither update callback ran; then fail and retry setup and prove normal root/descendant update cascading is preserved after the guard resets. Run its exact fully-qualified filter and expect failure because `Update` currently reaches active state callbacks without consulting setup state.
- [ ] Check `IsSetupInHierarchy()` at the first line of public `Update`, before active/current-state access or callback execution, and document the setup exception contract in Chinese. Rerun the focused test and all state-machine tests.

#### Task 3 Setup Transaction Repair AC: Snapshot all state-owned setup configuration

**Files:** `Patterns/StateMachine.cs`, `Test/Patterns/UnitTestStateMachine.cs`

- [ ] Add `TestSetupFailureRestoresStateOwnedConfiguration`, starting with a named state, an existing state event handler, and configured setup/enter/update/exit callbacks. During failed setup replace/add handlers, rename the state, replace all callbacks, and throw one retained exception; assert the original name, handlers, and callbacks are restored exactly, failed handlers/callbacks are absent, retry invokes the original setup callback, and successful retry handler configuration remains. Run its exact fully-qualified filter and expect failure because setup snapshots omit every state-owned mutable configuration field.
- [ ] Add a state configuration snapshot containing `_name`, a clone of `_eventHandlers`, `_onSetupCallback`, `_onEnterCallback`, `_onUpdateCallback`, and `_onExitCallback`; capture/restore it for the directly added state and every recursively snapshotted pre-existing state. Keep ownership, parent, and child-machine topology in the existing hierarchy snapshot path. Rerun the focused test and all state-machine tests.

#### Task 3 Lifecycle Update Closure Repair AD: Reject updates during lifecycle callbacks

**Files:** `Patterns/StateMachine.cs`, `Test/Patterns/UnitTestStateMachine.cs`

- [x] Add `TestInitialEnterCannotUpdateMachine` and `TestTransitionExitCannotUpdateMachine`. The first calls the owning machine's public `Update` from the initial state's enter callback; the second calls it from the current state's exit callback during a transition. Assert `InvalidOperationException` occurs before any update callback side effect, the outer operation preserves the recursive fail-close inactive/null contract, and a later activation/update or transition succeeds after the callback condition is disabled. Run the exact two-test filter and expect both tests to fail because public `Update` does not consult `_isChangingState`.
- [x] Reject `_isChangingState` at the first public `Update` boundary, after the setup-hierarchy guard but before active/current-state inspection, and document both rejection conditions in Chinese XML. Keep the existing catch path so lifecycle callback exceptions propagate unchanged and recursively fail close. Rerun the exact filter and all state-machine tests.

#### Task 3 Documentation Closure Repair AE: Correct Blackboard notification XML

**Files:** `Patterns/BlackBoard.cs`

- [x] Replace the nonexistent `key`, `type`, `oldValue`, and `newValue` parameter documentation on `NotifyDataChanged` with accurate Chinese documentation for the captured `handler` and event `args`. Record that a null handler is a no-op and handler exceptions propagate directly. Verify with the source-quality fixture and Release build.

### Task 4: ECS

**Files:**
- Review: `ECS/Archetype.cs`, `ECS/CommandBuffer.cs`, `ECS/Component.cs`, `ECS/ComponentColumn.cs`, `ECS/ECSExtension.cs`, `ECS/Entity.cs`, `ECS/EntityPrefab.cs`, `ECS/Query.cs`, `ECS/System.cs`, `ECS/SystemDependencyAttributes.cs`, `ECS/SystemGroup.cs`, `ECS/TypeSignature.cs`, `ECS/World.cs`
- Review tests: `Test/ECS`
- Review docs: `ECS/README.md`, `ECS/USAGE.md`
- Create: `docs/superpowers/reviews/2026-07-10-round-4-ecs.md`

**Interfaces:**
- Consumes: static monotonically increasing entity IDs, archetype/query update contracts, and Utility/Collections behavior.
- Produces: verified structural changes, query membership, command replay, prefab application, system scheduling, and one reviewable commit.

- [x] **Step 1: Run the ECS baseline**

```powershell
dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.ECS"
```

Expected: all ECS tests pass before repairs.

- [x] **Step 2: Review every ECS source, test, and user-facing example**

Trace create/destroy, add/set/remove component, archetype migration, component-column swaps, stale Entity values, world ownership, query creation/update/disposal, command-buffer ordering and invalid targets, prefab inheritance/application, TypeSignature equality, dependency cycles, deterministic system ordering, disabled systems, mutation during update, hot-path allocations, public API usability, Chinese XML docs, and example accuracy.

- [x] **Step 3: Repair findings using appended TDD subtasks**

Every structural bug test must assert world, archetype, query, and entity observations after the operation. Scheduling tests must assert deterministic order and failure details. Avoid optimization that reduces ECS readability unless measurement or obvious repeated allocation justifies it.

- [x] **Step 4: Record, verify, commit, and review round 4**

```powershell
dotnet test .\Test\Test.csproj --no-restore
dotnet build .\SimpleFramework.sln --no-restore --configuration Release
git diff --check
git add -- ECS Test/ECS docs/superpowers/plans/2026-07-10-full-codebase-review.md docs/superpowers/reviews/2026-07-10-round-4-ecs.md
git commit -m "review: harden ecs lifecycle and scheduling"
```

Request review for the round range, resolve Critical and Important feedback, rerun verification, and record the disposition.

#### Task 4 Repair A: Make archetype insertion transactional

**Files:** `ECS/Archetype.cs`, `Test/ECS/UnitTestArchetype.cs`

- [x] Add `ArchetypeRejectedRowDoesNotCorruptAlignedStorage`, attempting to add a row whose component dictionary omits one signature type; assert the expected `InvalidOperationException`, zero entity rows, then add a valid row and assert its entity and both component columns occupy row zero. Run `dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~Test.ECS.UnitTestArchetype.ArchetypeRejectedRowDoesNotCorruptAlignedStorage"`; expect failure because `Archetype.Add` currently appends the entity and an earlier component column before discovering the missing component.
- [x] Validate and collect every signature component before mutating `_entities` or any column, then append the complete row. Rerun the exact filter and all `UnitTestArchetype` tests; expect aligned storage and all selected tests to pass.

#### Task 4 Repair B: Bind buffered handles and playback results to their command buffer

**Files:** `ECS/CommandBuffer.cs`, `Test/ECS/UnitTestCommandBuffer.cs`

- [x] Add `PlaybackRejectsBufferedEntityFromAnotherBuffer` and `ResultRejectsBufferedEntityFromAnotherBufferWithSameLocalId`. Use two buffers whose first placeholders share local ID zero; assert the foreign command throws `InvalidOperationException` before changing either target entity, and the foreign result cannot resolve the other buffer's placeholder. Run their exact fully-qualified filter; expect failure because `BufferedEntity` equality and resolution currently use only the local ID.
- [x] Give each `CommandBuffer` a unique owner ID, include it in `BufferedEntity` equality/hash identity, validate ownership when recording every buffered-target command, and carry the identity into `CommandBufferResult`. Keep `BufferedEntity.Id` as the documented local number and preserve recorded command order. Rerun the two tests and all command-buffer tests; expect foreign handles to be rejected deterministically and existing same-buffer playback to pass.

#### Task 4 Repair C: Reject reentrant SystemGroup updates before dispatch

**Files:** `ECS/SystemGroup.cs`, `Test/ECS/UnitTestSystemGroup.cs`

- [x] Add `UpdateRejectsReentrantDispatchAndRecovers`, whose first system calls the same group's `Update()` once, captures `InvalidOperationException`, and whose later normal update proves the guard resets; assert exact deterministic call order and no nested system dispatch. Run its exact fully-qualified filter; expect failure because `UpdateCore` currently permits recursive dispatch.
- [x] Check `_isUpdating` at the start of `UpdateCore` before changing state or sorting, throw `InvalidOperationException` with a direct reentrancy diagnostic, and retain the existing `finally` reset for callback failures. Document the rejection on both public update overloads in Chinese XML. Rerun the focused test and all `TestSystemGroup` tests; expect deterministic single dispatch and recovery.

#### Task 4 Repair D: Return a genuinely read-only query archetype view

**Files:** `ECS/Query.cs`, `Test/ECS/UnitTestQuery.cs`

- [x] Add `GetArchetypesCannotMutateQueryCache`, cast the returned view to `IList<Archetype>`, assert mutation throws `NotSupportedException`, then assert a subsequent query enumeration and archetype observation remain correct. Run its exact fully-qualified filter; expect failure because the returned object is the mutable internal `List<Archetype>`.
- [x] Create one `ReadOnlyCollection<Archetype>` wrapper over the internal list and return it from `GetArchetypes`, preserving lazy refresh without per-call copies. Update the Chinese XML return contract. Rerun the focused test and all query tests; expect the cache to remain protected and live refresh behavior to pass.

#### Task 4 Repair E: Distinguish component types with identical full names

**Files:** `ECS/TypeSignature.cs`, `Test/ECS/UnitTestSignature.cs`

- [x] Add `TypeSignatureDoesNotConfuseSameNamedTypesFromDifferentAssemblies`, creating two deterministic dynamic assemblies with component types that have the same namespace/name and implement `IComponent`; assert a signature containing the first does not `Has` the second, the two one-type signatures are unequal, and their set behavior remains coherent. Run its exact fully-qualified filter; expect failure because the binary-search comparer currently compares only `Type.FullName`.
- [x] Extend the stable type ordering tie-breaker with assembly identity so comparer equality implies runtime type equality for distinct component types. Rerun the focused test and all signature tests; expect correct membership and equality/hash behavior.

#### Task 4 Repair F: Reject null reference components at public boundaries

**Files:** `ECS/World.cs`, `ECS/EntityPrefab.cs`, `ECS/CommandBuffer.cs`, `Test/ECS/UnitTestWorld.cs`, `Test/ECS/UnitTestEntityPrefab.cs`, `Test/ECS/UnitTestCommandBuffer.cs`

- [x] Add `WorldRejectsNullReferenceComponentsWithoutStructuralChange`, `PrefabRejectsNullReferenceComponentWithoutMutation`, and `CommandBufferRejectsNullReferenceComponentBeforeRecording`. Assert `ArgumentNullException` uses the public component parameter, world entity/archetype/query observations remain unchanged, the prefab remains reusable, and the command buffer can subsequently play one valid command exactly once. Run their exact fully-qualified filter; expect failure because creation currently dereferences null and add/set paths can store null component values.
- [x] Add a small generic null guard at every public component-taking boundary in `World`, `EntityPrefab`, and `CommandBuffer`; reject null before recording commands or mutating world/prefab state, while preserving struct component paths and class-component reference semantics. Add accurate Chinese XML exception contracts to touched public declarations. Rerun the focused tests and all ECS tests; expect deterministic `ArgumentNullException` and unchanged structural state.

#### Task 4 Repair G: Reject a null world when constructing a system

**Files:** `ECS/System.cs`, `Test/ECS/UnitTestEcsSystem.cs`

- [x] Add `SystemConstructorRejectsNullWorld`, constructing the fixture's concrete counting system with null and asserting `ArgumentNullException` names `world`. Run its exact fully-qualified filter; expect failure because `EcsSystem` currently publishes a null `World` reference.
- [x] Guard the protected `EcsSystem` constructor before assigning `World` and document the exception in Chinese XML. Rerun the focused test and all ECS tests; expect deterministic construction failure and no regression.

#### Task 4 Review Repair H: Make TypeSignature ordering consistent with runtime Type identity

**Files:** `ECS/TypeSignature.cs`, `Test/ECS/UnitTestSignature.cs`

- [x] Replace the earlier collision fixture with `TypeSignatureDistinguishesRuntimeTypesWithIdenticalAssemblyQualifiedNames`. Create two collectible dynamic assemblies with the same `AssemblyName` identity and the same component `FullName`; assert the resulting `Type` objects are unequal while their assembly-qualified names are equal, `Has` distinguishes them, one-type signatures are unequal, and combined signatures normalize equally in both input orders. Run its exact fully-qualified filter; expect failure because `CompareTypes` still returns zero for the distinct runtime types.
- [x] Keep the useful full-name and assembly-qualified-name comparisons, then break remaining collisions with a thread-safe `ConditionalWeakTable<Type, TypeIdentity>` whose values contain `Interlocked`-allocated monotonic IDs and do not reference their collectible keys. Ensure `CompareTypes` returns zero only for reference-equal runtime types and document the runtime-identity membership contract in Chinese XML. Rerun the exact test and all signature tests; expect identity-safe, transitive, order-independent behavior.

#### Task 4 Review Repair I: Key prefab components by runtime identity

**Files:** `ECS/EntityPrefab.cs`, `Test/ECS/UnitTestEntityPrefab.cs`

- [x] Add `PrefabRejectsWidenedDuplicateRuntimeTypeWithoutMutation` and `PrefabKeepsDistinctWidenedRuntimeTypesWithIdenticalNames`. The first adds a position normally and again through `With<IComponent>`, expecting immediate `ArgumentException` and successful instantiation of only the original value. The second passes instances of the two colliding runtime component types through `With<IComponent>`, then asserts both exact runtime types remain in the instantiated entity's signature/columns. Run their exact filter; expect failure because `With` keys by `typeof(T)` rather than `component.GetType()`.
- [x] After the null guard, key `EntityPrefab` storage by `component.GetType()` so duplicate detection and `World.Instantiate` use the same runtime identity. Update the Chinese XML type/duplicate contract. Rerun the focused tests and all prefab/world/signature tests; expect widened duplicates to fail before mutation and distinct runtime types to instantiate correctly.

#### Task 4 Review Repair J: Consume every CommandBuffer playback generation

**Files:** `ECS/CommandBuffer.cs`, `Test/ECS/UnitTestCommandBuffer.cs`, `ECS/USAGE.md`

- [x] Add `FailedPlaybackConsumesBatchPreservesPrefixAndAllowsRecovery` and `SuccessfulPlaybackInvalidatesOldHandlesButKeepsItsResultResolvable`. The failed case queues create/failing-add/create, asserts the original direct exception is unwrapped, the prefix world effect remains, the suffix is absent, command count becomes zero, an empty second playback does not repeat effects, both old handles are rejected at record time, and a fresh batch reuses local ID zero with a new identity and succeeds. The successful case asserts its result still resolves the completed generation while recording with its old handle is rejected and the next generation also starts at local ID zero. Run their exact filter; expect failure because commands/owner/local IDs currently reset only after successful playback and the owner never rotates.
- [x] In `Playback`, snapshot the current command list and generation, execute in order, and in `finally` clear the recorded batch, reset the local handle counter, and advance to a fresh unique owner generation on both success and failure. A successful result retains the completed generation's dictionary; failure returns no partial result, preserves earlier world effects, skips the suffix, and propagates the original exception without wrapping. Update Chinese XML and usage docs to state one-shot consumption. Rerun the focused tests and all command-buffer tests.

#### Task 4 Review Documentation K: Correct Entity.Id terminology

**Files:** `ECS/Entity.cs`

- [x] Replace the `Entity.Id` XML description with an opaque, globally allocated entity identifier that the owning world maps to a world-local slot; explicitly avoid describing it as the slot number. Verify with source quality and Release build.

### Task 5: Net

**Files:**
- Review: every tracked `.cs` and `.csproj` file under `Net/`
- Review tests: every tracked `.cs` file under `Test/Net/`
- Review docs: `Net/README.md`
- Create: `docs/superpowers/reviews/2026-07-10-round-5-net.md`

**Interfaces:**
- Consumes: transport, codec, registry, messaging, session, discovery, flow, stats, dispatcher, and `GameNet` public contracts.
- Produces: verified asynchronous lifecycle, concurrency, packet/state behavior, practical hot-path performance, and one reviewable commit; network security remains out of scope.

- [ ] **Step 1: Enumerate the exact Net review set and run its baseline**

```powershell
rg --files Net Test/Net -g '*.cs' -g '*.csproj' | Sort-Object
dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~SimpleFramework.Test.Net"
```

Expected: the command lists every Net source/test file and all selected tests pass before repairs.

- [ ] **Step 2: Review by data flow from transport to GameNet**

Trace start/stop/dispose idempotence, cancellation ownership, connect/disconnect races, partial TCP reads/writes, transport callback ordering, packet validation, codec/registry mismatch, request correlation, duplicate/late responses, session handshake and reconnect, discovery expiry, flow limits, statistics snapshots, dispatcher exceptions, background task observation, thread-safe collections, lock ordering, buffer allocation/copying, repeated serialization, polling, public API usability, and Chinese XML docs. Exclude authentication, encryption, hostile-peer defense, and denial-of-service design.

- [ ] **Step 3: Repair findings using appended TDD subtasks**

Use `Test/Net/TestDoubles/ManualTimeProvider.cs`, in-memory transports, explicit task gates, and cancellation tokens for deterministic tests. Any performance edit must identify the repeated allocation, copy, computation, or lock it removes and retain behavior tests; add a focused measurement only when code inspection is insufficient to establish the improvement.

- [ ] **Step 4: Record, verify, commit, and review round 5**

```powershell
dotnet test .\Test\Test.csproj --no-restore
dotnet build .\SimpleFramework.sln --no-restore --configuration Release
git diff --check
git add -- Net Test/Net docs/superpowers/plans/2026-07-10-full-codebase-review.md docs/superpowers/reviews/2026-07-10-round-5-net.md
git commit -m "review: harden net lifecycle and performance"
```

Request review for the round range, resolve Critical and Important feedback, rerun verification, and record the disposition.

### Task 6: Cross-Module API, Documentation, Syntax, and Test Simplification

**Files:**
- Review: all tracked production `.cs` files outside `Test/`, `bin/`, and `obj/`
- Review: all tracked `.csproj`, `README.md`, `docs/domain-lifecycle.md`, and module README/USAGE files
- Review tests: all tracked files under `Test/`
- Create: `docs/superpowers/reviews/2026-07-10-round-6-cross-cutting.md`

**Interfaces:**
- Consumes: all repaired module APIs and round records.
- Produces: coherent naming and documentation, concise modern syntax, a smaller high-value test suite where safely possible, and one reviewable commit.

- [ ] **Step 1: Enumerate public declarations and documentation risks**

```powershell
rg -n "^public |^\s+public " -g '*.cs' -g '!Test/**' -g '!**/bin/**' -g '!**/obj/**'
rg -n "!;|!\)|!\]|default!|#pragma warning disable|TODO|FIXME|NotImplementedException" -g '*.cs' -g '!**/bin/**' -g '!**/obj/**'
```

Expected: concrete inventories for manual comparison; every match receives a decision in the round record.

- [ ] **Step 2: Review cross-module consistency**

Check public naming, Try/Get/Require semantics, nullable annotations, exception consistency, cancellation parameter placement, disposal shape, event subscription shape, collection exposure, Chinese XML summaries/parameters/returns/exceptions, README examples, and use of collection expressions, primary constructors only where clear, pattern matching, switch expressions, property patterns, target-typed construction, ranges, expression bodies, and `using` declarations.

- [ ] **Step 3: Simplify tests only with preserved contract coverage**

Identify duplicate tests by matching setup, operation, and asserted external behavior. Delete a test only when another named test covers the same contract and boundary; record both test names and the retained protection. Consolidate repeated setup into an existing fixture helper only when the helper makes intent clearer. Do not delete regression, error-path, ordering, concurrency, or boundary tests merely to reduce count.

- [ ] **Step 4: Repair findings using appended TDD subtasks where behavior changes**

API changes and refactors follow RED → GREEN → REFACTOR. Documentation, syntax-equivalent rewrites, and proven duplicate-test deletions use the existing suite plus source-quality checks.

- [ ] **Step 5: Record, verify, commit, and review round 6**

```powershell
dotnet test .\SimpleFramework.sln --no-restore --configuration Debug
dotnet build .\SimpleFramework.sln --no-restore --configuration Release
dotnet test .\Test\Test.csproj --no-restore --filter "FullyQualifiedName~TestSourceTextQuality"
git diff --check
git add -- '*.cs' '*.csproj' '*.md'
git commit -m "review: align public apis and simplify code"
```

Expected: the complete suite and source-quality test pass, Release has zero warnings/errors, and no whitespace errors exist. Request review for the round range, resolve Critical and Important feedback, rerun verification, and record the disposition.

### Task 7: Independent Final Audit and Release Verification

**Files:**
- Review: Git diff from `61a02f5` to branch HEAD, every round record, approved design, and this plan
- Create: `docs/superpowers/reviews/2026-07-10-round-7-release.md`

**Interfaces:**
- Consumes: all six module/cross-cutting rounds and independent reviewer dispositions.
- Produces: requirement-by-requirement release evidence and the final review commit.

- [ ] **Step 1: Request a full-range independent review**

Use `superpowers:requesting-code-review` with base `61a02f5`, current HEAD, the approved design, AGENTS.md constraints, and explicit instructions to inspect the entire repository rather than only changed files. Require file:line evidence for every issue and a merge-readiness verdict.

- [ ] **Step 2: Reconcile every requirement and finding**

Read all round records and reviewer output. Confirm every design requirement has direct evidence, every Critical/Important item is repaired, every accepted Minor item improves clarity or size, every rejected item has technical reasoning, and all public API changes have Chinese XML docs and tests.

- [ ] **Step 3: Inspect final performance and scope boundaries**

Confirm major hot paths were inspected for avoidable allocation, copying, computation, and lock contention; confirm any small justified fixes are present; confirm no network-security expansion, architecture rewrite, unrelated dependency, or unnecessary file move entered the branch.

- [ ] **Step 4: Run fresh release gates**

```powershell
dotnet restore .\SimpleFramework.sln
dotnet test .\SimpleFramework.sln --no-restore --configuration Debug
dotnet build .\SimpleFramework.sln --no-restore --configuration Release
dotnet test .\Test\Test.csproj --no-build --configuration Debug --filter "FullyQualifiedName~TestSourceTextQuality"
git diff --check 61a02f5..HEAD
git status --short
```

Expected: restore succeeds; all tests pass with zero failures; Release builds with zero warnings/errors; source-quality passes; Git reports no whitespace errors; only the final release record is uncommitted before Step 5.

- [ ] **Step 5: Commit the final audit record**

```powershell
git add -- docs/superpowers/reviews/2026-07-10-round-7-release.md docs/superpowers/plans/2026-07-10-full-codebase-review.md
git commit -m "review: complete release readiness audit"
git status --short
```

Expected: commit succeeds and the working tree is clean.

- [ ] **Step 6: Run the completion audit after the final commit**

Repeat the full commands from Step 4 against the committed tree, inspect `git log --oneline 61a02f5..HEAD`, and mark the goal complete only when every approved design condition has authoritative evidence and no required work remains.
