# ECS SystemGroup Dependency Sort Design

## Summary

Extend `SystemGroup` so ECS systems can declare execution dependencies with `RunBefore` and `RunAfter`. The group will compute a stable topological order immediately before update, then execute only through the existing `SystemGroup.Update()` and `SystemGroup.Update(float)` paths. This feature only changes system execution order; it does not touch ECS entity, component, archetype, query, or world storage.

## Goals

- Let a system declare that it must run before another system type.
- Let a system declare that it must run after another system type.
- Preserve existing manual `order` behavior as the coarse execution phase.
- Preserve stable ordering for systems that are otherwise unrelated.
- Detect dependency cycles before any system update runs.
- Include the dependency chain in cycle exceptions so callers can diagnose the cycle quickly.
- Keep the API small and readable.

## Non-Goals

- No scheduler, parallel execution, or async update.
- No dependency injection or automatic system creation.
- No dependency declaration by string name.
- No global ordering across multiple `SystemGroup` instances.
- No changes to ECS storage, query, archetype, entity, component, or command-buffer behavior.

## Public API

Dependencies are declared with attributes on concrete `EcsSystem` types:

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class RunBeforeAttribute : Attribute
{
    public RunBeforeAttribute(Type systemType);
    public Type SystemType { get; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class RunAfterAttribute : Attribute
{
    public RunAfterAttribute(Type systemType);
    public Type SystemType { get; }
}
```

`systemType` must not be `null`. The sorting algorithm only applies an attribute when the referenced type is present in the same `SystemGroup`. Missing referenced systems are ignored so optional systems do not force registration.

The referenced type can be an exact concrete system type or a base/interface type assignable from an added system. This keeps declarations usable with abstract phase systems while still supporting the common concrete-type case.

## Ordering Rules

Existing manual order remains the first ordering key. `SystemGroup` starts from entries sorted by `order` ascending, then insertion sequence ascending. This sorted list is the stable baseline for topological sorting.

Dependency edges are added from earlier system to later system:

- `[RunBefore(typeof(TargetSystem))]` on `SourceSystem` creates `SourceSystem -> TargetSystem`.
- `[RunAfter(typeof(TargetSystem))]` on `SourceSystem` creates `TargetSystem -> SourceSystem`.

Dependencies refine order only inside the same manual `order` value. If a dependency points across different manual orders, `SystemGroup` ignores that edge and keeps manual order authoritative. This avoids surprising phase jumps where a late rendering system could pull itself before an early simulation system through an attribute.

When multiple systems have no dependency relationship, the result keeps their existing manual-order and insertion-order baseline. This is implemented with a stable Kahn topological sort that always chooses the next zero-indegree entry with the smallest baseline index.

## Cycle Detection

If the dependency graph contains a cycle, sorting throws `InvalidOperationException` before any system update runs. The message includes a readable dependency chain using system type names:

```text
SystemGroup dependency cycle detected: ASystem -> BSystem -> CSystem -> ASystem
```

The chain is found with a DFS over the remaining cyclic nodes after Kahn sorting cannot consume every entry. The first discovered cycle is enough; the goal is actionable diagnostics, not listing every cycle.

## Error Handling

- `RunBeforeAttribute(null)` throws `ArgumentNullException`.
- `RunAfterAttribute(null)` throws `ArgumentNullException`.
- Dependencies that reference missing system types are ignored.
- Dependencies that reference system types in another manual-order phase are ignored.
- Cycles throw `InvalidOperationException` before invoking any system.
- Exceptions thrown by system update methods remain unwrapped.

## Approaches Considered

### Recommended: Attribute-Based Dependencies

Attributes keep dependency declarations local to the system type and avoid adding more overloads to `SystemGroup.Add`. The group can inspect metadata at sort time, which fits the current small API. This approach is concise for users and easy to test with nested test systems.

### Alternative: Add-Time Dependency Builder

`SystemGroup.Add(system).RunAfter<T>()` would make dependencies instance-specific, but it would require a new builder API and more mutable state. It also makes dependency declarations less discoverable when reading a system class.

### Alternative: Global Type Registry

A central registry could support broad scheduling features later, but it is too heavy for this iteration. It would introduce global state and ordering behavior outside the local `SystemGroup`, which conflicts with the existing multiple-group design.

## Testing

Add NUnit coverage in `Test/ECS/UnitTestSystemGroup.cs`:

- `RunAfter` places a system after a referenced system even when added earlier.
- `RunBefore` places a system before a referenced system even when added later.
- unrelated systems keep stable manual-order and insertion-order behavior.
- dependencies are ignored when the referenced system is absent.
- manual `order` remains authoritative across different order values.
- dependency cycles throw `InvalidOperationException` and include the dependency chain.
- cycle detection happens before any system update runs.
- `Update(float)` uses the same dependency order as `Update()`.

Verification commands:

```powershell
dotnet test .\SimpleFramework.sln --no-restore -m:1 /nr:false
rg -n "瀛|鎸|绯|涓嶅|銆|€" ECS
```
