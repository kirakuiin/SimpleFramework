# AGENTS.md

This file is for coding agents working in this repository. README.md is for humans; this file captures project context, commands, and local conventions.

## Project Summary

SimpleFramework is a multi-project C#/.NET solution for lightweight game/application infrastructure. It targets `net8.0`; `global.json` pins SDK `9.0.0` with `rollForward: latestMinor`.

Main projects:

- `SimpleFramework.csproj`: core domain framework with `IDomain`, `ISystem`, `IModel`, `IUtility`, command/query abstractions, event bus, bindable properties, and base abstract classes.
- `Collections/Collections.csproj`: `DefaultDict<TKey,TValue>` and `Counter<T>`.
- `ECS/ECS.csproj`: readable ECS implementation with `World`, `Entity`, `Archetype`, `TypeSignature`, `Query`, `IComponent`, and `EcsSystem`.
- `Patterns/Patterns.csproj`: singleton, service locator, object pool, message channel, blackboard, and event-driven hierarchical state machine.
- `Maths/Maths.csproj`: point/math helpers, matrix, and hexagon grid utilities.
- `Utility/Utility.csproj`: disposable helpers, logging, serialization, file/time/task/misc helpers, and collection/string/random extensions.
- `Net/Net.csproj`: `GameNet` orchestration, transport abstractions, sessions and peer directory, typed messaging and codecs, LAN discovery, multiplayer flow, statistics, and diagnostics.
- `Toolkit/Toolkit.csproj`: `IniConfigTool`, an INI config reader/writer implementing `IUtility`.
- `GDExt/GDExt.csproj`: Godot node helpers and pooling extensions. The project defines `GODOT`, references `GodotSharp`, and guards its source files with `#if GODOT`.
- `Test/Test.csproj`: NUnit tests for the major modules.

## Commands

Run from repository root:

```powershell
dotnet restore .\SimpleFramework.sln
dotnet build .\SimpleFramework.sln
dotnet test .\SimpleFramework.sln
dotnet test .\Test\Test.csproj
```

## Coding Conventions

- Use existing namespaces and project boundaries. Do not move source files between projects unless the task requires it.
- Keep public API comments in Chinese where surrounding code uses Chinese XML docs.
- The root core project intentionally excludes submodule folders through `Compile Remove`; add code to the correct subproject rather than relying on root compilation.
- `Directory.Build.props` enables nullable reference types for every project; match the existing annotations.
- Do not edit generated `bin/` or `obj/` files.
- Prefer small, direct APIs consistent with the existing codebase. This project values readability over heavy optimization, especially in ECS.
- Use `System.Text.Json` helpers from `Utility/SerializeUtil.cs` where existing protocol or serialization code expects them.

## Core Behavior Notes

- `AbstractDomain<T>.Instance` remains a lazy singleton; `Create()` creates independent initialized domains; `GetInstance()` does not create.
- `AddChild` expresses lifecycle ownership and keeps a strong child reference. `SetParent` only expresses lookup inheritance.
- Component lookup falls back to the parent Domain.
- `RegisterSystemAs`/`RegisterModelAs` are lifecycle-managed; `RegisterUtilityAs` is not lifecycle-managed even if the runtime type implements `ISystem` or `IModel`.
- Re-registering a System/Model under a lifecycle key releases the previous lifecycle-managed instance when no other lifecycle key references it. Re-registering the same instance should not initialize twice. Utility registration has no lifecycle.
- Domain events are local to the registering/sending Domain. Parent lookup applies to components, not events. Use `EventBus.Global` for explicit cross-Domain events.
- `BindableProperty<T>.WithComparer` is instance-scoped.

## Module Notes

- ECS entity IDs are static and monotonically increasing across worlds.
- `Query` owns no event subscription or disposal lifetime; it refreshes cached archetype matches lazily from the world's archetype version.
- `BlackBoard` uses reader/writer locks and parent lookup. Local writes do not modify parent boards.
- Godot code is guarded with `#if GODOT`; normal .NET builds may still restore `GDExt` dependencies through the solution.
