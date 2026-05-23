# ECS Rewrite Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current ECS module with a clean archetype ECS using entity handles, world-owned lifecycle, strongly typed component columns, lazy queries, and practical API sugar.

**Architecture:** Delete and rewrite the existing ECS implementation around `World` as the only structural mutation entry point. `Entity` is a value handle with `WorldId`, `Id`, and `Version`; `Archetype` stores aligned entity rows and strongly typed `ComponentColumn<T>` values; `Query` lazily refreshes matching archetypes by world archetype version.

**Tech Stack:** C#/.NET 8, NUnit, `System.Runtime.InteropServices.CollectionsMarshal`, existing `SimpleFramework.ECS` namespace.

---

## File Structure

- Replace: `ECS/Entity.cs` with the value-handle `readonly struct Entity`.
- Replace: `ECS/Component.cs` only if needed to keep `IComponent` unchanged.
- Replace: `ECS/TypeSignature.cs` with an immutable signature value object.
- Replace: `ECS/Archetype.cs` with an internal row/column archetype store and public read-only inspection surface.
- Create: `ECS/ComponentColumn.cs` for `IComponentColumn` and `ComponentColumn<T>`.
- Replace: `ECS/World.cs` with lifecycle, component access, archetype migration, query factory, and validation logic.
- Replace: `ECS/Query.cs` with lazy-refresh include/exclude query enumeration.
- Replace: `ECS/System.cs` with the small `EcsSystem` base.
- Replace: `ECS/TemplateFunc.cs` with generic creation/query sugar or delete it if the sugar lives in `World.cs`.
- Replace: `Test/ECS/UnitTestComponent.cs` with shared class and struct test components used by the rewritten tests.
- Replace tests in `Test/ECS/*.cs` around the new API. Old tests should not be preserved when they encode old `entity.Add/Get/Remove` behavior.

## Task 0: Shared ECS Test Components

**Files:**
- Modify: `Test/ECS/UnitTestComponent.cs`

- [ ] **Step 1: Replace test components with shared class and struct components**

Use this content shape so subsequent tests have consistent component names:

```csharp
using NUnit.Framework;
using SimpleFramework.ECS;

namespace Test.ECS;

public struct TestPosition : IComponent
{
    public float X;
    public float Y;
}

public struct TestVelocity : IComponent
{
    public float X;
    public float Y;
}

public struct TestHealth : IComponent
{
    public int Current;
    public int Max;
}

public struct TestDeadTag : IComponent
{
}

public sealed class TestName : IComponent
{
    public string Value { get; set; } = string.Empty;
}

[TestFixture]
public class TestComponent
{
    [Test]
    public void ComponentTypesCanBeStructsOrClasses()
    {
        IComponent position = new TestPosition { X = 1, Y = 2 };
        IComponent name = new TestName { Value = "player" };

        Assert.IsInstanceOf<TestPosition>(position);
        Assert.IsInstanceOf<TestName>(name);
    }
}
```

- [ ] **Step 2: Run component test**

Run:

```powershell
dotnet test .\Test\Test.csproj --filter FullyQualifiedName~Test.ECS.TestComponent
```

Expected: pass before production changes because `IComponent` already exists.

## Task 1: Entity Handle and Immutable TypeSignature

**Files:**
- Modify: `ECS/Entity.cs`
- Modify: `ECS/TypeSignature.cs`
- Test: `Test/ECS/UnitTestEntity.cs`
- Test: `Test/ECS/UnitTestSignature.cs`

- [ ] **Step 1: Replace entity/signature tests with new expected behavior**

Use tests with this shape:

```csharp
[Test]
public void EntityStoresWorldIdIdAndVersion()
{
    var entity = new Entity(10, 20, 30);

    Assert.AreEqual(10, entity.WorldId);
    Assert.AreEqual(20, entity.Id);
    Assert.AreEqual(30, entity.Version);
}

[Test]
public void TypeSignatureEqualityIsOrderIndependent()
{
    var a = new TypeSignature(typeof(TestPosition), typeof(TestVelocity));
    var b = new TypeSignature(typeof(TestVelocity), typeof(TestPosition));

    Assert.AreEqual(a, b);
    Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
}

[Test]
public void TypeSignatureRejectsNonComponentTypes()
{
    Assert.Throws<ArgumentException>(() => new TypeSignature(typeof(string)));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~Test.ECS.UnitTestEntity|FullyQualifiedName~Test.ECS.TestTypeSignature"
```

Expected: fail because `Entity` does not have the new constructor/properties and `TypeSignature` is still mutable.

- [ ] **Step 3: Implement minimal entity and immutable signature**

`Entity.cs` should become a value handle:

```csharp
public readonly struct Entity : IEquatable<Entity>
{
    public Entity(int worldId, int id, int version)
    {
        WorldId = worldId;
        Id = id;
        Version = version;
    }

    public int WorldId { get; }
    public int Id { get; }
    public int Version { get; }

    public bool Equals(Entity other) =>
        WorldId == other.WorldId && Id == other.Id && Version == other.Version;

    public override bool Equals(object? obj) => obj is Entity other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(WorldId, Id, Version);
    public override string ToString() => $"Entity({WorldId}:{Id}:{Version})";

    public static bool operator ==(Entity left, Entity right) => left.Equals(right);
    public static bool operator !=(Entity left, Entity right) => !left.Equals(right);
}
```

`TypeSignature.cs` should expose no mutation API and should validate `IComponent` types.

- [ ] **Step 4: Run tests to verify they pass**

Run the same filtered command. Expected: pass.

## Task 2: Strongly Typed Component Columns and Archetype Rows

**Files:**
- Create: `ECS/ComponentColumn.cs`
- Modify: `ECS/Archetype.cs`
- Test: `Test/ECS/UnitTestArchetype.cs`

- [ ] **Step 1: Write failing archetype storage tests**

Use tests with this shape:

```csharp
[Test]
public void ArchetypeStoresTypedComponentsByAlignedRow()
{
    var signature = new TypeSignature(typeof(TestPosition), typeof(TestVelocity));
    var archetype = new Archetype(signature);
    var entity = new Entity(1, 1, 1);

    var row = archetype.Add(entity, new IComponent[]
    {
        new TestPosition { X = 1, Y = 2 },
        new TestVelocity { X = 3, Y = 4 }
    });

    Assert.AreEqual(0, row);
    Assert.AreEqual(entity, archetype.GetEntity(0));
    Assert.AreEqual(1, archetype.Get<TestPosition>(0).X);
    Assert.AreEqual(4, archetype.Get<TestVelocity>(0).Y);
}

[Test]
public void ArchetypeRemoveAtSwapBackReturnsMovedEntity()
{
    var signature = new TypeSignature(typeof(TestPosition));
    var archetype = new Archetype(signature);
    var first = new Entity(1, 1, 1);
    var second = new Entity(1, 2, 1);
    archetype.Add(first, new IComponent[] { new TestPosition { X = 1 } });
    archetype.Add(second, new IComponent[] { new TestPosition { X = 2 } });

    var moved = archetype.RemoveAtSwapBack(0);

    Assert.AreEqual(second, moved);
    Assert.AreEqual(second, archetype.GetEntity(0));
    Assert.AreEqual(2, archetype.Get<TestPosition>(0).X);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test .\Test\Test.csproj --filter FullyQualifiedName~Test.ECS.UnitTestArchetype
```

Expected: fail because the old `Archetype` delegates storage to `World`.

- [ ] **Step 3: Implement columns and archetype**

`ComponentColumn<T>` should store `List<T>` and return refs with:

```csharp
public ref T GetRef(int row)
{
    return ref CollectionsMarshal.AsSpan(_items)[row];
}
```

`Archetype` should keep:

```csharp
private readonly List<Entity> _entities = new();
private readonly Dictionary<Type, IComponentColumn> _columns = new();
```

`RemoveAtSwapBack` returns the entity moved into the removed row, or `null` when the removed row was the last row.

- [ ] **Step 4: Run tests to verify they pass**

Run the same filtered command. Expected: pass.

## Task 3: World Lifecycle, Validation, and Component Access

**Files:**
- Modify: `ECS/World.cs`
- Test: `Test/ECS/UnitTestWorld.cs`
- Test: `Test/ECS/UnitTestEntity.cs`

- [ ] **Step 1: Write failing world lifecycle tests**

Use tests with this shape:

```csharp
[Test]
public void DestroyInvalidatesEntityHandle()
{
    var world = new World();
    var entity = world.CreateEntity();

    Assert.IsTrue(world.IsAlive(entity));
    Assert.IsTrue(world.DestroyEntity(entity));
    Assert.IsFalse(world.IsAlive(entity));
    Assert.IsFalse(world.DestroyEntity(entity));
    Assert.Throws<InvalidOperationException>(() => world.Get<TestPosition>(entity));
}

[Test]
public void ForeignEntityIsRejected()
{
    var a = new World();
    var b = new World();
    var entity = a.CreateEntity();

    Assert.IsFalse(b.IsAlive(entity));
    Assert.Throws<InvalidOperationException>(() => b.Add(entity, new TestPosition()));
}

[Test]
public void GetReturnsWritableReferenceForStructComponent()
{
    var world = new World();
    var entity = world.CreateEntity<TestPosition>(new TestPosition { X = 1, Y = 2 });

    ref var position = ref world.Get<TestPosition>(entity);
    position.X = 10;

    Assert.AreEqual(10, world.Get<TestPosition>(entity).X);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test .\Test\Test.csproj --filter "FullyQualifiedName~Test.ECS.UnitTestWorld|FullyQualifiedName~Test.ECS.UnitTestEntity"
```

Expected: fail because `World` does not yet own the new lifecycle and component access API.

- [ ] **Step 3: Implement World slot records and accessors**

`World` should keep slot records:

```csharp
private sealed class EntitySlot
{
    public int Version;
    public bool Alive;
    public Archetype Archetype = null!;
    public int Row;
}
```

Implement:

```csharp
public Entity CreateEntity();
public Entity CreateEntity<T1>(T1 c1) where T1 : IComponent;
public Entity CreateEntity<T1, T2>(T1 c1, T2 c2) where T1 : IComponent where T2 : IComponent;
public bool DestroyEntity(Entity entity);
public bool IsAlive(Entity entity);
public bool Has<T>(Entity entity) where T : IComponent;
public ref T Get<T>(Entity entity) where T : IComponent;
public bool TryGet<T>(Entity entity, out T component) where T : IComponent;
public Entity Add<T>(Entity entity, T component) where T : IComponent;
public Entity Set<T>(Entity entity, T component) where T : IComponent;
public bool Remove<T>(Entity entity) where T : IComponent;
```

Use `Validate(entity)` internally for APIs that throw and `TryGetSlot(entity, out slot)` for APIs that return `false`.

- [ ] **Step 4: Run tests to verify they pass**

Run the same filtered command. Expected: pass.

## Task 4: Archetype Migration

**Files:**
- Modify: `ECS/World.cs`
- Modify: `ECS/Archetype.cs`
- Test: `Test/ECS/UnitTestWorld.cs`

- [ ] **Step 1: Write failing migration tests**

Use tests with this shape:

```csharp
[Test]
public void AddNewComponentMovesEntityToNewSignature()
{
    var world = new World();
    var entity = world.CreateEntity<TestPosition>(new TestPosition { X = 1 });

    world.Add(entity, new TestVelocity { X = 2 });

    Assert.IsTrue(world.Has<TestPosition>(entity));
    Assert.IsTrue(world.Has<TestVelocity>(entity));
    Assert.AreEqual(2, world.Get<TestVelocity>(entity).X);
    Assert.IsTrue(world.GetArchetype(entity).Signature.Has<TestPosition>());
    Assert.IsTrue(world.GetArchetype(entity).Signature.Has<TestVelocity>());
}

[Test]
public void RemoveComponentMovesEntityToReducedSignature()
{
    var world = new World();
    var entity = world.CreateEntity<TestPosition, TestVelocity>(
        new TestPosition { X = 1 },
        new TestVelocity { X = 2 });

    Assert.IsTrue(world.Remove<TestVelocity>(entity));

    Assert.IsTrue(world.Has<TestPosition>(entity));
    Assert.IsFalse(world.Has<TestVelocity>(entity));
    Assert.IsFalse(world.Remove<TestVelocity>(entity));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test .\Test\Test.csproj --filter FullyQualifiedName~Test.ECS.UnitTestWorld
```

Expected: fail until migration is implemented.

- [ ] **Step 3: Implement migration**

When adding a new component, copy current boxed values from the source archetype, add the new typed component, append to destination archetype, then remove old row with swap-back and update the moved entity row if needed.

When removing a component, copy all current boxed values except the removed type, append to destination archetype, then remove old row and update indices.

Increment `World` structural version for create, destroy, add-new-component, and remove-component operations.

- [ ] **Step 4: Run tests to verify they pass**

Run the same filtered command. Expected: pass.

## Task 5: Lazy Query and Typed Query Sugar

**Files:**
- Modify: `ECS/Query.cs`
- Modify: `ECS/World.cs`
- Test: `Test/ECS/UnitTestQuery.cs`

- [ ] **Step 1: Write failing query tests**

Use tests with this shape:

```csharp
[Test]
public void QueryIncludesAndExcludesByComponent()
{
    var world = new World();
    var query = world.Query<TestPosition>().Not<TestDeadTag>();
    var alive = world.CreateEntity<TestPosition>(new TestPosition { X = 1 });
    var dead = world.CreateEntity<TestPosition, TestDeadTag>(
        new TestPosition { X = 2 },
        new TestDeadTag());

    CollectionAssert.AreEquivalent(new[] { alive }, query.ToList());
}

[Test]
public void QueryLazyRefreshSeesNewArchetype()
{
    var world = new World();
    var query = world.Query<TestPosition, TestVelocity>();

    Assert.AreEqual(0, query.Count());

    var entity = world.CreateEntity<TestPosition, TestVelocity>(
        new TestPosition(),
        new TestVelocity());

    CollectionAssert.AreEqual(new[] { entity }, query.ToList());
}

[Test]
public void QueryThrowsWhenStructureChangesDuringEnumeration()
{
    var world = new World();
    world.CreateEntity<TestPosition>(new TestPosition());
    var query = world.Query<TestPosition>();

    Assert.Throws<InvalidOperationException>(() =>
    {
        foreach (var entity in query)
        {
            world.DestroyEntity(entity);
        }
    });
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test .\Test\Test.csproj --filter FullyQualifiedName~Test.ECS.UnitTestQuery
```

Expected: fail until lazy query is implemented.

- [ ] **Step 3: Implement Query**

`Query` should keep include/exclude `TypeSignature`, cached matching archetypes, and last observed world archetype version. Before enumeration and `GetArchetypes()`, refresh if the world archetype version changed.

The enumerator should capture `World.StructuralVersion` and check it between yields. If it changes, throw `InvalidOperationException`.

Add world sugar:

```csharp
public Query Query();
public Query Query<T1>() where T1 : IComponent;
public Query Query<T1, T2>() where T1 : IComponent where T2 : IComponent;
```

- [ ] **Step 4: Run tests to verify they pass**

Run the same filtered command. Expected: pass.

## Task 6: EcsSystem and End-to-End Tests

**Files:**
- Modify: `ECS/System.cs`
- Test: `Test/ECS/UnitTestEcsSystem.cs`

- [ ] **Step 1: Write failing system test**

Use tests with this shape:

```csharp
[Test]
public void SystemProcessesMatchingQueryEntities()
{
    var world = new World();
    var system = new MovementTestSystem(world);
    var entity = world.CreateEntity<TestPosition, TestVelocity>(
        new TestPosition { X = 1 },
        new TestVelocity { X = 2 });

    system.Update();

    Assert.AreEqual(3, world.Get<TestPosition>(entity).X);
}

private sealed class MovementTestSystem : EcsSystem
{
    private readonly Query _query;

    public MovementTestSystem(World world) : base(world)
    {
        _query = world.Query<TestPosition, TestVelocity>();
    }

    public override void Update()
    {
        foreach (var entity in _query)
        {
            ref var position = ref World.Get<TestPosition>(entity);
            ref var velocity = ref World.Get<TestVelocity>(entity);
            position.X += velocity.X;
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test .\Test\Test.csproj --filter FullyQualifiedName~Test.ECS.TestEcsSystem
```

Expected: fail until `EcsSystem` and query integration compile.

- [ ] **Step 3: Implement `EcsSystem`**

Keep it small:

```csharp
public abstract class EcsSystem
{
    protected EcsSystem(World world)
    {
        World = world;
    }

    protected World World { get; }

    public abstract void Update();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the same filtered command. Expected: pass.

## Task 7: Optional Each Sugar

**Files:**
- Modify: `ECS/World.cs`
- Test: `Test/ECS/UnitTestWorld.cs`

- [ ] **Step 1: Decide whether `Each<T1, T2>` stays simple**

Implement only if it can be done with a small `RefAction` delegate and existing query/get APIs:

```csharp
public delegate void RefAction<T1, T2>(Entity entity, ref T1 c1, ref T2 c2)
    where T1 : IComponent
    where T2 : IComponent;
```

- [ ] **Step 2: If implementing, write failing test**

```csharp
[Test]
public void EachTwoComponentsMutatesByReference()
{
    var world = new World();
    var entity = world.CreateEntity<TestPosition, TestVelocity>(
        new TestPosition { X = 1 },
        new TestVelocity { X = 2 });

    world.Each<TestPosition, TestVelocity>((Entity _, ref TestPosition position, ref TestVelocity velocity) =>
    {
        position.X += velocity.X;
    });

    Assert.AreEqual(3, world.Get<TestPosition>(entity).X);
}
```

- [ ] **Step 3: Run test to verify it fails**

Run:

```powershell
dotnet test .\Test\Test.csproj --filter FullyQualifiedName~Test.ECS.UnitTestWorld
```

Expected: fail if `Each` is not implemented.

- [ ] **Step 4: Implement or explicitly defer**

If implementation introduces awkward overloads or compiler friction, defer `Each` and do not keep the failing test. The spec marks this as optional.

## Task 8: Full ECS Test Pass and Solution Verification

**Files:**
- Modify as needed: `Test/ECS/*.cs`
- Verify: full solution

- [ ] **Step 1: Remove old API assumptions from tests**

Search:

```powershell
rg -n "entity\\.Add|entity\\.Get|entity\\.Remove|HasTag|AddTag|RemoveTag|CreateQuery" Test\ECS ECS
```

Expected after cleanup: no old entity mutation API remains. `CreateQuery` may remain only if intentionally kept as alias; preferred API is `Query`.

- [ ] **Step 2: Run ECS tests**

Run:

```powershell
dotnet test .\Test\Test.csproj --filter FullyQualifiedName~Test.ECS
```

Expected: all ECS tests pass.

- [ ] **Step 3: Run full solution tests**

Run:

```powershell
dotnet test .\SimpleFramework.sln
```

Expected: all tests pass.

- [ ] **Step 4: Inspect diff**

Run:

```powershell
git diff --stat
git diff -- ECS Test\ECS
```

Expected: ECS rewrite is limited to ECS source and ECS tests, plus this plan/spec history.
