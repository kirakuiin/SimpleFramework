# Nullable, Docs, and Project Config Design

## Goal

Improve release hygiene without changing runtime behavior: centralize shared project configuration, enable nullable reference types across the solution, document Domain lifecycle contracts, and add a lightweight guard for corrupted Chinese XML comments.

## Scope

- Add a root `Directory.Build.props` for shared project settings.
- Remove repeated shared settings from project files while preserving project-specific settings and references.
- Enable nullable reference types for every project through the shared props file.
- Fix nullable warnings by making public and internal null contracts explicit.
- Add `docs/domain-lifecycle.md` to document Domain ownership, lookup, lifecycle, and event boundaries.
- Add an automated check that scans production C# sources for common mojibake and replacement characters.
- Repair any currently detected public Chinese comments.

## Non-Goals

- Do not change public API behavior.
- Do not restructure modules or rename assemblies.
- Do not add runtime dependencies.
- Do not migrate the framework to a different dependency injection model.
- Do not make defensive code more verbose unless nullable correctness requires a clear contract.

## Architecture

Shared MSBuild configuration belongs in `Directory.Build.props`; individual `.csproj` files keep only their distinct identity and references. Nullable migration should prefer accurate signatures such as nullable returns for optional lookups and non-null returns for `Require*` APIs, rather than suppressing warnings broadly.

Documentation has two layers: `docs/domain-lifecycle.md` explains design contracts for users, while a test or script enforces basic source documentation integrity by failing on known corrupted text patterns.

## Testing

- `dotnet build .\SimpleFramework.sln` must complete with 0 warnings and 0 errors.
- `dotnet test .\SimpleFramework.sln` must pass.
- The documentation integrity check must fail when production `.cs` files contain known mojibake or replacement characters.

## Risks

Enabling nullable on the root framework project can surface many warnings in core abstractions. The mitigation is to keep changes contract-focused: update signatures and fields where null is already part of the design, and avoid broad `!` suppression except at well-understood initialization boundaries.
