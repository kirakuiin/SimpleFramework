# Task 1 Report: Centralize Project Configuration

## Files Changed
- `Directory.Build.props`
- `SimpleFramework.csproj`
- `Collections/Collections.csproj`
- `ECS/ECS.csproj`
- `GDExt/GDExt.csproj`
- `Maths/Maths.csproj`
- `Net/Net.csproj`
- `Patterns/Patterns.csproj`
- `Test/Test.csproj`
- `Toolkit/Toolkit.csproj`
- `Utility/Utility.csproj`
- `.superpowers/sdd/task-1-brief.md`

## What Changed
- Added shared MSBuild defaults in `Directory.Build.props` for `TargetFramework`, `ImplicitUsings`, `Nullable`, and common Debug/Release output paths.
- Removed duplicated framework, nullable, and output path settings from the individual project files.
- Kept project-specific settings such as `RootNamespace`, `AssemblyName`, `DefineConstants`, `ProjectReference`, and package references.
- Marked Task 1 complete in the task brief.

## Build Summary
- Command: `dotnet build .\SimpleFramework.sln`
- Result: build succeeded.
- Warning count: 157 warnings, 0 errors.
- First warning categories observed after enabling nullable analysis globally:
  - `CS8601`
  - `CS8625`
  - `CS8618`
  - `CS8603`
  - `CS8765`

## Concerns
- The solution now builds successfully but with a large nullable-warning surface in `SimpleFramework.csproj` and `Test/Test.csproj`.
- These warnings are expected for this task and should be addressed in later nullable-fix work rather than here.
