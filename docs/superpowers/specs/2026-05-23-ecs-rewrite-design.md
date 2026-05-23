# ECS Rewrite Design

## Goal

Rewrite the ECS module as a clean, small, archetype-based ECS for SimpleFramework.
The current ECS module is not used by downstream code, so this rewrite may break the old API when doing so produces a simpler and more correct design.

The first phase focuses only on the core ECS:

- Entity handles
- World-managed lifecycle
- Component storage
- Immutable type signatures
- Archetype grouping
- Queries
- Basic system update flow
- Tests for the core behavior

The first phase does not implement:

- Prefab templates
- Command buffers
- Automatic system dependency sorting
- Reactive ECS events
- Multithreaded scheduling
- Source generators
- Unsafe memory optimization
- Complex entity relationship graphs

These features should be considered later, after the core model is stable.

## Design Principles

The rewrite should follow KISS:

- Keep files small and responsibilities clear.
- Prefer direct APIs over clever abstractions.
- Make invalid states hard to represent.
- Keep `World` as the only lifecycle and structural mutation entry point.
- Keep public XML documentation in Chinese, matching the existing module style.
- Optimize for correctness and readability before raw performance.

This is still an archetype ECS, but not an engine-scale ECS. The implementation should avoid unsafe code, generated code, multithreading, and complex schedulers in the first phase.

## Entity

`Entity` becomes a lightweight value handle:

```csharp
public readonly struct Entity : IEquatable<Entity>
{
    public int Id { get; }
    public int Version { get; }
}
```

An entity does not own components. It does not expose `Add`, `Remove`, or `Get` methods. All component access and structural changes go through `World`.

The `Version` value prevents stale handles from being accepted after an entity has been destroyed and the same slot is reused. A stale entity handle should fail validation before any read or write.

## World

`World` is the only public entry point for entity lifecycle, component access, and structural mutation.

Expected API shape:

```csharp
var world = new World();

var entity = world.CreateEntity();
world.Add(entity, new Position { X = 1, Y = 2 });
world.Add(entity, new Velocity { X = 0.5f, Y = 0 });

ref var position = ref world.Get<Position>(entity);
position.X += 1;

world.Remove<Velocity>(entity);
world.DestroyEntity(entity);
```

`World` owns:

- Entity slot records: id, version, alive state, current archetype, row index
- Archetype lookup by immutable `TypeSignature`
- Query registration and archetype matching
- Entity creation and destruction
- Component add, remove, get, set, and try-get operations

`World` should reject stale or foreign entity handles. Since `Entity` no longer stores a `World` reference, "foreign" means the id/version pair is not valid in this world.

`DestroyEntity` should mark the entity dead and remove its component data from the current archetype. Repeated destroy calls should return `false` instead of throwing.

## Components

Components keep the existing marker interface:

```csharp
public interface IComponent
{
}
```

The ECS should support both class and struct components implementing `IComponent`, but the recommended style is small data-only components.

The first phase does not require component constructors, reflection-based default creation, or automatic component initialization. Creating an entity with component types but no values is not a core requirement. Prefer value-based creation:

```csharp
var entity = world.CreateEntity(
    new Position { X = 0, Y = 0 },
    new Health { Current = 100, Max = 100 });
```

## TypeSignature

`TypeSignature` represents an immutable set of component types.

Requirements:

- Order independent equality
- Stable hash code
- No public mutation after construction
- Reject non-component types where practical
- Useful `ToString` for debugging

Because signatures are used as dictionary keys, no public API may mutate a signature after it has been created.

## Archetype

An `Archetype` owns entities with the same component type set.

Internal storage should be simple and clear:

```text
Archetype: Position + Velocity
  Entities: Entity[]
  Position: component column
  Velocity: component column
```

The first phase may implement columns with `List<IComponent>` or a small column abstraction. It does not need unsafe typed arrays. The important behavior is that each component type has a column aligned with the entity row.

Archetype responsibilities:

- Store entity rows
- Store component columns
- Add a row with component values
- Remove a row by swapping with the last row
- Return moved entity information so `World` can update row indices
- Expose read-only signature and entity count

`Archetype` should not be the public mutation API. `World` controls movement between archetypes.

## Structural Changes

Adding or removing a component moves an entity between archetypes:

```text
Old: Position
Add Velocity
New: Position + Velocity
```

The move process:

1. Validate entity handle.
2. Read existing component values from the old archetype.
3. Add or remove the target component value.
4. Find or create the destination archetype.
5. Add the entity to the destination archetype.
6. Remove the old row from the source archetype.
7. Update row indices for the moved entity and any swapped entity.

This should be implemented in `World`, not in `Entity`.

## Query

`Query` filters matching archetypes by include and exclude component signatures.

Expected API shape:

```csharp
using var query = world.CreateQuery()
    .Has<Position>()
    .Has<Velocity>()
    .Not<DeadTag>();

foreach (var entity in query)
{
    ref var position = ref world.Get<Position>(entity);
    ref var velocity = ref world.Get<Velocity>(entity);
}
```

`Query` should implement `IDisposable` if it subscribes to world events. It must not rely on a finalizer to unsubscribe.

The first phase supports component include/exclude filters. Tag filters can be dropped unless they naturally fall out of tag components. A tag should usually be represented as an empty component:

```csharp
public struct DeadTag : IComponent
{
}
```

Query behavior requirements:

- Existing archetypes are matched when the query is created or changed.
- Newly created archetypes are matched automatically.
- Destroyed entities do not appear in query results.
- Structural changes update query results through archetype movement.
- `GetArchetypes()` returns a read-only view.

## EcsSystem

`EcsSystem` remains intentionally small.

Expected shape:

```csharp
public abstract class EcsSystem
{
    protected World World { get; }

    protected EcsSystem(World world)
    {
        World = world;
    }

    public abstract void Update();
}
```

Systems may create and hold queries. If a system owns disposable queries, it should dispose them when the system is disposed. The first phase does not need a full system manager, system group, or automatic ordering.

## API Compatibility

This rewrite is allowed to break the current ECS API.

Remove or avoid these old patterns:

```csharp
entity.Add(component);
entity.Remove<T>();
entity.Get<T>();
entity.Archetype;
new Archetype(world, signature) for public construction;
mutable TypeSignature.Add/Remove/Clear after construction;
```

Prefer these patterns:

```csharp
world.Add(entity, component);
world.Remove<T>(entity);
world.Get<T>(entity);
world.TryGet<T>(entity, out var component);
world.GetArchetype(entity);
new TypeSignature(typeof(Position), typeof(Velocity));
```

Existing tests should be rewritten around the new API instead of preserving old behavior.

## Error Handling

The API should be predictable:

- `DestroyEntity` returns `false` when the entity is already dead or invalid.
- `Has<T>` returns `false` for invalid entities.
- `TryGet<T>` returns `false` for invalid entities or missing components.
- `Get<T>` throws a clear exception for invalid entities or missing components.
- `Add<T>` should replace or reject duplicate components consistently.

For KISS, choose replacement semantics for `Add<T>`:

```csharp
world.Add(entity, new Position { X = 1, Y = 2 });
world.Add(entity, new Position { X = 3, Y = 4 }); // updates Position in place
```

Adding a new component type moves archetypes. Adding an existing component type updates the value in the current archetype.

## Testing Strategy

Tests should be rewritten around the new model.

Core tests:

- Entity creation returns alive handles with stable ids and versions.
- Destroying an entity invalidates stale handles.
- Repeated destroy returns `false`.
- Adding a component makes `Has<T>` and `Get<T>` work.
- Adding the same component type updates the value.
- Removing a component moves the entity to a new signature.
- Removing a missing component returns `false`.
- Queries include matching entities.
- Queries exclude entities with excluded components.
- Queries update when an entity changes archetype.
- Queries stop returning destroyed entities.
- Query disposal detaches from world update events.
- Type signatures are immutable and order independent.
- EcsSystem can process query results through `World`.

Verification commands:

```powershell
dotnet test .\Test\Test.csproj --filter FullyQualifiedName~Test.ECS
dotnet test .\SimpleFramework.sln
```

## Future Extensions

After the core passes tests, consider these in order:

1. Simple prefab templates: component value sets that instantiate one entity.
2. Command buffer: deferred create, destroy, add, remove, and set operations.
3. System groups and manual ordering.
4. Automatic dependency ordering based on declared component reads and writes.
5. Reactive ECS events for component add, remove, set, and entity lifecycle.
6. Prefab serialization, nested prefab entities, and resource references.
7. Multithreaded scheduling.
8. Source generators or unsafe storage optimization, only if profiling justifies it.

## Open Decisions

These decisions are fixed for phase one:

- Entity is a value handle with `Id` and `Version`.
- World is the only structural mutation entry point.
- TypeSignature is immutable.
- Tags are represented as empty components, not string tags.
- Query supports include/exclude components only.
- Prefab and CommandBuffer are not part of phase one.
- API compatibility with the old ECS is not required.
