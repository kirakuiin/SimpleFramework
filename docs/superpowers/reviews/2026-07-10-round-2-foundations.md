# Round 2: Collections, Utility, Toolkit, and Maths

## Files Reviewed

- Collections production: `Collections/Counter.cs`, `Collections/DefaultDict.cs`.
- Utility production: `Utility/Disposable.cs`, `Utility/FileUtil.cs`, `Utility/Logging.cs`, `Utility/MiscUtil.cs`, `Utility/SerializeUtil.cs`, `Utility/TaskUtil.cs`, `Utility/TimeUtil.cs`, `Utility/Extensions/EnumeratorExtension.cs`, `Utility/Extensions/ListExtension.cs`, `Utility/Extensions/RandomExtension.cs`, `Utility/Extensions/StringExtension.cs`.
- Toolkit production: `Toolkit/ConfigTool.cs`, `Toolkit/ToolkitDefine.cs`.
- Maths production: `Maths/Common.cs`, `Maths/Matrix.cs`, `Maths/HexagonGrid.cs`.
- Tests: every tracked source under `Test/Collections`, `Test/Utility`, `Test/Toolkit`, and `Test/Maths`; `Test/Extensions/UnitTestExtension.cs` was also inspected and run because it is the existing protection for the reviewed Utility extension sources.
- Contract references: `AGENTS.md`, `.superpowers/sdd/task-2-brief.md`, `docs/superpowers/specs/2026-07-10-full-codebase-review-design.md`, Task 2 of `docs/superpowers/plans/2026-07-10-full-codebase-review.md`, and the Round 1 review record.

## Contracts Checked

- `DefaultDict` inserts a factory value only through its indexer; `TryGetValue` remains a non-mutating probe. Null factories now fail at construction, optional key comparers flow into the backing dictionary, and the effective comparer is observable.
- `Counter` preserves explicit zero/negative counts, treats missing comparison keys as zero, excludes zero entries from value hashing, returns only positive repetitions from `Elements`, and preserves the left/copy comparer. Addition and subtraction now inspect the complete union of logical keys.
- `DisposableGroup` keeps repeated disposal idempotent, ignores null entries, closes ownership before callbacks, attempts every child in insertion order, aggregates failures, and immediately disposes non-null children added after closure.
- File loads return false for missing, unreadable, or malformed data and log the failure. JSON `null` remains a successfully parsed nullable result. Serializer defaults include fields; byte APIs now operate directly on UTF-8 JSON without an intermediate string.
- Logger handler mutation uses a snapshot, handler clearing waits for in-flight emission, and file handlers serialize write/dispose access. No new handler-exception containment policy was introduced because the existing API does not define one.
- `WaitUntil` preserves predicate exceptions and silent timeout completion while now propagating cancellation before polling and during delay. Seconds-to-milliseconds conversion now reports overflow rather than wrapping; Unix timestamp conversion retains framework range exceptions.
- Random choice rejects an empty list with the caller parameter name; sampling and string repetition reject negative counts. Sampling a short finite sequence still returns all available elements, matching the existing at-most-count behavior.
- INI string values remain byte-for-byte logical text after parsing whitespace rules. Typed `IFormattable` writes and reads use invariant culture, and `DateTime` uses round-trip format so persisted typed values survive a culture change.
- Matrix inversion rejects singular, near-singular under the retained absolute `1e-12` threshold, NaN-determinant, and infinite-determinant matrices. The fixed 2x2 multiplication/transform dimension contract remains unchanged.
- Integer hex coordinates validate `q + r + s` in widened arithmetic and coordinate operators use checked arithmetic. Fractional coordinates, layout size, and layout origin reject non-finite values; finite negative layout scales remain valid mirrors while zero axes are unusable and rejected. Preset orientations are immutable properties and invalid directions report `direction`.
- Every public declaration in the listed production files was checked for a nearby Chinese XML contract; missing summaries, parameters, returns, nullability, cancellation, overflow, and invalid-input exceptions were completed without changing behavior.

## Findings and Decisions

- Important — binary `Counter` subtraction enumerated only the left side, so a right-only key disappeared instead of producing a negative count. Both arithmetic operators now use the full key union and preserve the left comparer.
- Important — `DisposableGroup` stopped at the first throwing child and permanently skipped later resources; adding after disposal silently retained an object that could never be released. Cleanup now attempts all children, reports deterministic `AggregateException`, and immediately disposes late additions.
- Important — INI typed persistence used ambient culture on both sides. A value written as `1234,5` under `fr-FR` was read as `12345` under `en-US`, and a date changed month/day while losing fractional ticks. Typed persistence is invariant and round-trip; raw strings such as `1,5` are unchanged.
- Important — integer overflow could make invalid cube coordinates appear to satisfy the hex invariant, coordinate arithmetic wrapped, and NaN/infinity bypassed fractional/layout validation. Widened validation, checked operations, and explicit finite-value guards now reject these states at their source.
- Moderate — collection callers could not supply or preserve non-default key equality, and `DefaultDict` accepted a null factory until a later missing-key read. Comparer-aware constructors/properties and immediate null rejection make both contracts explicit.
- Moderate — deserializers declared non-null results while JSON `null` returned runtime null, and byte serialization allocated a JSON string before UTF-8 encoding. Nullable metadata now matches behavior; warmed regression evidence changed byte allocation from greater than string serialization to lower than it through direct UTF-8 APIs and cached options.
- Moderate — `WaitUntil` could not participate directly in caller cancellation. An optional trailing token now cancels before predicate evaluation or during the polling delay without introducing another abstraction.
- Moderate — non-finite matrix determinants passed the epsilon comparison and produced non-finite inverse matrices. They are now treated as non-invertible.
- Minor — milliseconds overflow wrapped, empty random choice failed indirectly through `index`, and negative sample/repeat counts silently returned empty results. Direct checked arithmetic and named argument validation now give stable failure contracts.
- Minor API safety — `HexOrientation.Pointy` and `Flat` were writable process-wide fields. They are now get-only static properties on a readonly struct.
- Retained — missing file/malformed JSON logging rather than exception propagation, `Counter` storage of zero/negative entries, absolute near-singular matrix tolerance, finite negative layout scale mirroring, `WaitUntil` silent timeout, and logger callback exception propagation all match established behavior or lack evidence for a safer incompatible policy. No dependency, project-boundary, file move, or architecture change was introduced.

## Changes

- Added twelve exact Task 2 repair amendments before production edits and followed RED → GREEN for each; the amendments cover 22 regression tests across collections, disposal, serialization allocation/nullability, cancellation, time/extensions, INI culture, matrix inversion, and hex validation/immutability.
- Added optional comparer support to `DefaultDict` and `Counter`, corrected Counter arithmetic, and made comparer-aware hashing consistent with logical keys.
- Hardened grouped disposal, serializer byte paths/nullability, cancellable waiting, utility boundary validation, invariant INI conversion, matrix inversion, and hex coordinate/layout contracts.
- Replaced mutable orientation presets with immutable properties and added exact parameter-name validation for invalid directions and layout inputs.
- Completed Chinese XML documentation across all reviewed public declarations and used concise modern syntax only where it clarified the audited implementation.

## Verification

- The planned filter uses nonexistent `SimpleFramework.Test.Collections`, `.Utility`, `.Toolkit`, and `.Maths` namespaces. Its `--list-tests` output exposed the entire assembly rather than the intended fixtures. The actual namespaces are `Test.Collections`, `Test.Utility`, `Test.Toolkit`, and `Test.Maths`; the corrected pre-repair baseline passed 102/102.
- Because the reviewed extension sources are protected by `Test.Extensions`, the final deterministic scope was expanded to `FullyQualifiedName~Test.Collections|FullyQualifiedName~Test.Utility|FullyQualifiedName~Test.Toolkit|FullyQualifiedName~Test.Maths|FullyQualifiedName~Test.Extensions` and passed 130/130.
- The first combined RED command selected 21 tests and failed 21/21 for the expected causes: missing right-only count/comparer APIs, deferred factory failure, aborted/leaking disposal, inaccurate nullability, UTF-8 allocation of 988,768 bytes versus 660,232 for strings, missing cancellation, invalid boundary behavior, culture corruption, and accepted non-finite/overflow geometry. A separately appended origin regression failed 1/1 because a NaN origin was accepted.
- The consolidated GREEN regression command passed 22/22. Focused intermediate GREEN commands passed 1/1 Counter subtraction, 3/3 comparer/factory, 4/4 grouped disposal, 12/12 serializer/file, 1/1 cancellation, 4/4 utility boundaries, 3/3 INI culture/conversion, 3/3 matrix inversion, and 19/19 complete hex fixture.
- Full Debug test project: `dotnet test .\Test\Test.csproj --no-restore` passed 657/657 in 10 seconds.
- Release solution build: `dotnet build .\SimpleFramework.sln --no-restore --configuration Release` succeeded with 0 warnings and 0 errors.
- `git diff --check` completed with no whitespace errors.

## Independent Review

- Per the task dispatch, this worker did not dispatch a reviewer; the root controller owns independent review of the exact range beginning at `b224a156adea8a9ce1ed091dd89be2b57e6bab70` after the round commit. No independent-review feedback was available for disposition inside this implementation task.
