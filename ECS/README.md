# SimpleFramework.ECS

`SimpleFramework.ECS` is a lightweight archetype ECS module.

Core concepts:

- `Entity` is a value handle with `WorldId`, `Id`, and `Version`.
- Entity IDs are static and monotonically increasing across all worlds. Treat `Id` as an opaque handle part, not as a world-local array index.
- `World` owns entity lifecycle, component access, and structural changes.
- `TypeSignature` is an immutable component type set.
- `Archetype` stores entities with the same component signature.
- `Query` filters entities by included and excluded component types.
- `EcsSystem` is a small base class for update logic.
- `SystemGroup` updates systems by manual order, then dependency attributes, then insertion order.

Example:

```csharp
var world = new World("Battle");
var entity = world.CreateEntity<Position, Velocity>(
    new Position { X = 0, Y = 0 },
    new Velocity { X = 1, Y = 0 });

foreach (var item in world.Query<Position, Velocity>().Not<Dead>())
{
    ref var position = ref world.Get<Position>(item);
    ref var velocity = ref world.Get<Velocity>(item);
    position.X += velocity.X;
}
```

System ordering:

```csharp
[RunAfter(typeof(InputSystem))]
[RunBefore(typeof(RenderSystem))]
public sealed class MovementSystem : EcsSystem
{
    public MovementSystem(World world) : base(world)
    {
    }

    public override void Update()
    {
        // Simulation logic.
    }
}
```

`RunBefore` and `RunAfter` targets must inherit from `EcsSystem`. Dependencies are applied only between systems that share the same manual `order` value in `SystemGroup`.
