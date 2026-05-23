# SimpleFramework.ECS

`SimpleFramework.ECS` is a lightweight archetype ECS module.

Core concepts:

- `Entity` is a value handle with `WorldId`, `Id`, and `Version`.
- `World` owns entity lifecycle, component access, and structural changes.
- `TypeSignature` is an immutable component type set.
- `Archetype` stores entities with the same component signature.
- `Query` filters entities by included and excluded component types.
- `EcsSystem` is a small base class for update logic.

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
