# SimpleFramework.ECS Usage

This document shows the recommended shape for a small ECS loop using `World`, `Query`, `EcsSystem`, and `SystemGroup`.

## Components

Components are plain value types that implement `IComponent`.

```csharp
using SimpleFramework.ECS;

public struct Position : IComponent
{
    public float X;
    public float Y;
}

public struct Velocity : IComponent
{
    public float X;
    public float Y;
}

public struct Dead : IComponent
{
}
```

## Systems

Systems inherit from `EcsSystem`. Use `World.Query<T>()` to find matching entities, then use `World.Get<T>()` for component references.

```csharp
public sealed class InputSystem : EcsSystem
{
    public InputSystem(World world) : base(world)
    {
    }

    public override void Update()
    {
        // Read input and write intent components here.
    }
}

[RunAfter(typeof(InputSystem))]
[RunBefore(typeof(RenderSystem))]
public sealed class MovementSystem : EcsSystem
{
    public MovementSystem(World world) : base(world)
    {
    }

    public override void Update(float deltaTime)
    {
        foreach (var entity in World.Query<Position, Velocity>().Not<Dead>())
        {
            ref var position = ref World.Get<Position>(entity);
            ref var velocity = ref World.Get<Velocity>(entity);

            position.X += velocity.X * deltaTime;
            position.Y += velocity.Y * deltaTime;
        }
    }

    public override void Update()
    {
        Update(0f);
    }
}

[RunAfter(typeof(MovementSystem))]
public sealed class RenderSystem : EcsSystem
{
    public RenderSystem(World world) : base(world)
    {
    }

    public override void Update()
    {
        foreach (var entity in World.Query<Position>().Not<Dead>())
        {
            ref var position = ref World.Get<Position>(entity);
            // Draw entity at position.
        }
    }
}
```

`RunAfter` and `RunBefore` can be used multiple times on the same system. Dependencies are applied inside the same manual order value. If two systems have no dependency relationship, `SystemGroup` keeps stable order by manual `order`, then insertion order.

## Group Setup

Create systems explicitly, add them to a group, then call `Validate()` once after registration. `Validate()` computes and caches the sorted order and throws early if dependencies contain a cycle.

```csharp
var world = new World("Game");

var input = new InputSystem(world);
var movement = new MovementSystem(world);
var render = new RenderSystem(world);

var simulation = new SystemGroup();
simulation.Add(input);
simulation.Add(movement);
simulation.Add(render);

simulation.Validate();
```

`Validate()` does not call any system update method. `Update()` and `Update(deltaTime)` also validate before dispatch if the cached order is missing or invalidated.

## Runtime Loop

Use `Update(deltaTime)` when systems need frame time. Systems that do not override `Update(float)` fall back to `Update()`.

```csharp
world.CreateEntity(
    new Position { X = 0f, Y = 0f },
    new Velocity { X = 3f, Y = 0f });

simulation.Update(1f / 60f);
```

## Enable And Disable

Each `EcsSystem` has an `Enabled` flag. Disabled systems stay in the dependency graph and still participate in `Validate()`, but `SystemGroup` skips their update call.

```csharp
render.Enabled = false;
simulation.Update(1f / 60f); // InputSystem and MovementSystem run; RenderSystem is skipped.

render.Enabled = true;
simulation.Update(1f / 60f); // RenderSystem runs again in the same dependency order.
```

Keeping disabled systems in the graph makes ordering stable. It also means dependency cycles are still reported even when one system in the cycle is disabled.

## Structural Changes During Queries

Do not add, remove, or destroy entities while enumerating a `Query`. The query checks the world's structural version and throws if the world changes during enumeration. Collect entity handles first or use a command-buffer style step when structural changes are needed.
