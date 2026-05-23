using System;
using System.Linq;
using NUnit.Framework;
using SimpleFramework.ECS;

namespace Test.ECS;

[TestFixture]
public class UnitTestWorld
{
    [Test]
    public void WorldCreationSetsNameAndWorldId()
    {
        var world = new World("TestWorld");

        Assert.AreEqual("TestWorld", world.Name);
        Assert.Greater(world.WorldId, 0);
        Assert.AreEqual(0, world.EntityCount);
    }

    [Test]
    public void CreateEntityReturnsAliveHandle()
    {
        var world = new World();

        var entity = world.CreateEntity();

        Assert.AreEqual(world.WorldId, entity.WorldId);
        Assert.AreEqual(0, entity.Id);
        Assert.AreEqual(1, entity.Version);
        Assert.IsTrue(world.IsAlive(entity));
        Assert.AreEqual(1, world.EntityCount);
    }

    [Test]
    public void CreateEntityWithInitialComponentsStoresValues()
    {
        var world = new World();

        var entity = world.CreateEntity(
            new TestPosition { X = 1, Y = 2 },
            new TestVelocity { X = 3, Y = 4 },
            new TestHealth { Current = 5, Max = 6 },
            new TestName { Value = "player" });

        Assert.IsTrue(world.Has<TestPosition>(entity));
        Assert.IsTrue(world.Has<TestVelocity>(entity));
        Assert.IsTrue(world.Has<TestHealth>(entity));
        Assert.IsTrue(world.Has<TestName>(entity));
        Assert.AreEqual(1, world.Get<TestPosition>(entity).X);
        Assert.AreEqual(4, world.Get<TestVelocity>(entity).Y);
        Assert.AreEqual(5, world.Get<TestHealth>(entity).Current);
        Assert.AreEqual("player", world.Get<TestName>(entity).Value);
    }

    [Test]
    public void GetReturnsWritableReferenceForStructComponent()
    {
        var world = new World();
        var entity = world.CreateEntity(new TestPosition { X = 1, Y = 2 });

        ref var position = ref world.Get<TestPosition>(entity);
        position.X = 10;

        Assert.AreEqual(10, world.Get<TestPosition>(entity).X);
    }

    [Test]
    public void AddExistingComponentUpdatesInPlace()
    {
        var world = new World();
        var entity = world.CreateEntity(new TestPosition { X = 1 });
        var before = world.GetArchetype(entity);

        world.Add(entity, new TestPosition { X = 9 });

        Assert.AreSame(before, world.GetArchetype(entity));
        Assert.AreEqual(9, world.Get<TestPosition>(entity).X);
    }

    [Test]
    public void AddNewComponentMovesEntityToNewSignature()
    {
        var world = new World();
        var entity = world.CreateEntity(new TestPosition { X = 1 });

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
        var entity = world.CreateEntity(
            new TestPosition { X = 1 },
            new TestVelocity { X = 2 });

        Assert.IsTrue(world.Remove<TestVelocity>(entity));

        Assert.IsTrue(world.Has<TestPosition>(entity));
        Assert.IsFalse(world.Has<TestVelocity>(entity));
        Assert.IsFalse(world.Remove<TestVelocity>(entity));
        Assert.IsTrue(world.GetArchetype(entity).Signature.Has<TestPosition>());
        Assert.IsFalse(world.GetArchetype(entity).Signature.Has<TestVelocity>());
    }

    [Test]
    public void SetMissingComponentThrows()
    {
        var world = new World();
        var entity = world.CreateEntity();

        Assert.Throws<InvalidOperationException>(() => world.Set(entity, new TestPosition()));
    }

    [Test]
    public void TryGetReturnsFalseForMissingOrInvalidEntity()
    {
        var world = new World();
        var entity = world.CreateEntity(new TestPosition { X = 1 });
        var missing = new Entity(world.WorldId, 100, 1);

        Assert.IsTrue(world.TryGet(entity, out TestPosition position));
        Assert.AreEqual(1, position.X);
        Assert.IsFalse(world.TryGet(entity, out TestVelocity _));
        Assert.IsFalse(world.TryGet(missing, out TestPosition _));
    }

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
        Assert.AreEqual(0, world.EntityCount);
    }

    [Test]
    public void ForeignEntityIsRejected()
    {
        var a = new World();
        var b = new World();
        var entity = a.CreateEntity();

        Assert.IsFalse(b.IsAlive(entity));
        Assert.IsFalse(b.Has<TestPosition>(entity));
        Assert.Throws<InvalidOperationException>(() => b.Add(entity, new TestPosition()));
    }

    [Test]
    public void DestroyUpdatesSwappedEntityRow()
    {
        var world = new World();
        var first = world.CreateEntity(new TestPosition { X = 1 });
        var second = world.CreateEntity(new TestPosition { X = 2 });

        Assert.IsTrue(world.DestroyEntity(first));

        Assert.IsTrue(world.IsAlive(second));
        Assert.AreEqual(2, world.Get<TestPosition>(second).X);
    }

    [Test]
    public void GetEntityReturnsAliveHandleById()
    {
        var world = new World();
        var entity = world.CreateEntity();

        Assert.AreEqual(entity, world.GetEntity(entity.Id));
        world.DestroyEntity(entity);
        Assert.IsNull(world.GetEntity(entity.Id));
    }

    [Test]
    public void GetEntitiesReturnsOnlyAliveEntities()
    {
        var world = new World();
        var first = world.CreateEntity();
        var second = world.CreateEntity(new TestPosition());
        world.DestroyEntity(first);

        CollectionAssert.AreEqual(new[] { second }, world.GetEntities().ToList());
    }

    [Test]
    public void QuerySugarCreatesIncludedQueries()
    {
        var world = new World();
        var entity = world.CreateEntity(
            new TestPosition(),
            new TestVelocity(),
            new TestHealth(),
            new TestDeadTag());

        CollectionAssert.AreEqual(new[] { entity }, world.Query<TestPosition>().ToList());
        CollectionAssert.AreEqual(new[] { entity }, world.Query<TestPosition, TestVelocity>().ToList());
        CollectionAssert.AreEqual(new[] { entity }, world.Query<TestPosition, TestVelocity, TestHealth>().ToList());
        CollectionAssert.AreEqual(new[] { entity },
            world.Query<TestPosition, TestVelocity, TestHealth, TestDeadTag>().ToList());
    }

    [Test]
    public void DestroyClearsWorld()
    {
        var world = new World();
        world.CreateEntity();
        world.CreateEntity(new TestPosition());

        world.Destroy();

        Assert.AreEqual(0, world.EntityCount);
        Assert.IsEmpty(world.GetEntities());
    }

    [Test]
    public void DestroyDoesNotMakeOldHandlesValidAgain()
    {
        var world = new World();
        var oldEntity = world.CreateEntity(new TestPosition { X = 1 });

        world.Destroy();
        var newEntity = world.CreateEntity(new TestPosition { X = 2 });

        Assert.AreNotEqual(oldEntity, newEntity);
        Assert.IsFalse(world.IsAlive(oldEntity));
        Assert.Throws<InvalidOperationException>(() => world.Get<TestPosition>(oldEntity));
        Assert.AreEqual(2, world.Get<TestPosition>(newEntity).X);
    }

    [Test]
    public void WorldEnumeratesNonEmptyArchetypes()
    {
        var world = new World();
        world.CreateEntity();
        world.CreateEntity(new TestPosition());

        Assert.AreEqual(2, world.Count());
    }
}
