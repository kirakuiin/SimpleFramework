# ECS SystemGroup Design

## Summary

Add a small `SystemGroup` API to `SimpleFramework.ECS` for grouping `EcsSystem` instances and updating them through one entry point. The first version focuses on explicit, deterministic ordering. Automatic dependency sorting is intentionally left for a later iteration.

## Goals

- Provide a reusable collection for multiple ECS systems.
- Allow more than one group to exist at the same time.
- Let callers update all systems in a group through `SystemGroup.Update()` or `SystemGroup.Update(deltaTime)`.
- Make `EcsSystem` itself support both no-argument and delta-time updates.
- Support manual ordering with stable execution for systems that share the same order.
- Allow systems that need frame time to override a delta-time update method without breaking existing no-argument systems.
- Keep the feature independent from ECS storage, archetypes, queries, and component migration.

## Non-Goals

- No automatic dependency sorting in this iteration.
- No scheduler, parallel execution, or fixed-step timing.
- No ownership of `World`; each `EcsSystem` already owns its world reference.
- No changes to `Entity`, `World`, `Query`, `Archetype`, or component storage behavior.

## Public API

```csharp
public abstract class EcsSystem
{
    public abstract void Update();
    public virtual void Update(float deltaTime);
}

public sealed class SystemGroup
{
    public int Count { get; }

    public void Add(EcsSystem system);
    public void Add(EcsSystem system, int order);
    public bool Remove(EcsSystem system);
    public void Update();
    public void Update(float deltaTime);
}
```

`EcsSystem` is the base update contract. `Update()` remains abstract so existing system implementations stay explicit. `Update(float deltaTime)` is added to the base class and defaults to calling `Update()`. Existing systems that only implement no-argument `Update()` continue to work. Systems that need frame time can override `Update(float deltaTime)` and use the passed value.

`Add(system)` uses order `0`. `Add(system, order)` stores both the explicit order and an internal insertion index. `Update()` and `Update(deltaTime)` execute systems sorted by `order` ascending, then by insertion index ascending.

## Behavior

Multiple `SystemGroup` instances may exist. A group does not bind to a specific `World`; this keeps it useful for phase-based groups such as input, simulation, presentation, or editor-only groups. The framework will not prevent callers from adding systems that reference different worlds.

The same system instance cannot be added to the same group more than once. Re-adding the same instance throws `ArgumentException`, which prevents accidental double updates in one frame.

`Remove(system)` returns `true` when the system was found and removed, and `false` otherwise.

Changing the group while either update method is running is invalid. Calling `Add` or `Remove` during group update throws `InvalidOperationException`. This mirrors the existing ECS rule that query traversal rejects structural changes during enumeration.

## Error Handling

- `Add(null)` throws `ArgumentNullException`.
- `Remove(null)` throws `ArgumentNullException`.
- Adding the same system instance twice throws `ArgumentException`.
- Adding or removing while the group is updating throws `InvalidOperationException`.
- Exceptions thrown by either system update method are not swallowed by `SystemGroup`.

## Testing

Add NUnit tests under `Test/ECS` covering:

- `Update()` calls every added system.
- `Update(deltaTime)` calls every added system with the same delta-time value.
- A system that does not override `Update(deltaTime)` still runs through the default fallback to `Update()`.
- Systems run by `order` ascending.
- Systems with the same `order` run in insertion order.
- `Remove()` removes a system and reports whether removal happened.
- Duplicate add throws `ArgumentException`.
- Add or remove during `Update()` throws `InvalidOperationException`.

Run verification with:

```powershell
dotnet test .\SimpleFramework.sln --no-restore -m:1 /nr:false
rg -n "瀛|鎸|绯|涓嶅|銆|€" ECS
```
