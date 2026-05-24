# ECS SystemGroup Scheduling Polish Design

## Summary

Extend the current `SystemGroup` dependency sorting work with three practical scheduling features: cached sorted order, explicit validation, and per-system enabled state. Also add a usage document showing the recommended way to combine `World`, `SystemGroup`, `Query`, dependency attributes, and `Validate()`.

## Goals

- Avoid rebuilding the dependency graph on every `Update()` when group membership has not changed.
- Let callers detect dependency cycles during initialization with `SystemGroup.Validate()`.
- Let systems be temporarily skipped with `EcsSystem.Enabled`.
- Keep disabled systems in the dependency graph so ordering and validation remain stable.
- Document a complete recommended ECS workflow.

## Non-Goals

- No `OnEnable` or `OnDisable` lifecycle hooks.
- No group-level enabled flag in this iteration.
- No system creation, ownership, or dependency injection changes.
- No changes to ECS storage, queries, archetypes, entities, or command buffers.
- No automatic cache invalidation for runtime attribute metadata changes; dependency attributes are treated as static type metadata.

## Public API

```csharp
public abstract class EcsSystem
{
    public bool Enabled { get; set; } = true;
    public abstract void Update();
    public virtual void Update(float deltaTime);
}

public sealed class SystemGroup
{
    public void Validate();
}
```

`Enabled` defaults to `true`. Callers can keep a reference to a system instance and toggle it directly:

```csharp
physicsSystem.Enabled = false;
```

`Validate()` computes the sorted execution cache and throws the same dependency-cycle exception that `Update()` would throw. It does not call any system update method.

## Behavior

`SystemGroup` stores a cached sorted list. `Add()` and `Remove()` invalidate the cache. `Validate()`, `Update()`, and `Update(float)` call the same internal method to ensure the cache exists. When the cache is valid, `Update()` reuses it without reflection or topological sorting.

Disabled systems remain in the cached sorted list and remain part of cycle detection. During update dispatch, the group skips entries whose `System.Enabled` is `false`.

This gives stable semantics:

- Disabling a system does not reorder other systems.
- Disabling a system does not hide dependency cycles.
- Re-enabling a system does not require rebuilding the dependency graph.
- Disabled systems do not receive either `Update()` or `Update(float)`.

## Error Handling

- `Validate()` throws `InvalidOperationException` with the dependency chain when the graph has a cycle.
- `Update()` and `Update(float)` continue to throw the same cycle exception before any system executes.
- Exceptions from enabled systems are still not swallowed.
- Disabled systems cannot throw from update because update is not called.

## Documentation

Add `ECS/USAGE.md` with a compact example that shows:

- creating a `World`;
- defining components;
- implementing systems with `Query`;
- declaring `[RunBefore]` and `[RunAfter]`;
- building a `SystemGroup`;
- calling `Validate()` after registration;
- toggling `Enabled`;
- running `Update(deltaTime)`.

The document should be example-first and avoid introducing APIs that do not exist.

## Testing

Add NUnit coverage under `Test/ECS/UnitTestSystemGroup.cs`:

- `Validate()` detects dependency cycles before update.
- `Validate()` does not execute systems.
- `Update()` reuses the validated cached order.
- `Add()` invalidates the cached order.
- `Remove()` invalidates the cached order.
- disabled systems are skipped by `Update()`.
- disabled systems are skipped by `Update(float)`.
- disabled systems still participate in dependency cycle detection.
- disabling a system does not reorder related systems.

Verification commands:

```powershell
dotnet test .\SimpleFramework.sln --no-restore -m:1 /nr:false
rg -n "瀛|鎸|绯|涓嶅|銆|€" ECS
```
