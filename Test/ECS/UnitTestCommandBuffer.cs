using System;
using System.Linq;
using NUnit.Framework;
using SimpleFramework.ECS;

namespace Test.ECS;

[TestFixture]
public class UnitTestCommandBuffer
{
    [Test]
    public void PlaybackCreatesEntitiesAndResolvesBufferedHandles()
    {
        var world = new World();
        var buffer = new CommandBuffer(world);

        var created = buffer.CreateEntity(new TestPosition { X = 1 });
        buffer.Add(created, new TestVelocity { X = 2 });

        var result = buffer.Playback();
        var entity = result.Resolve(created);

        Assert.IsTrue(world.IsAlive(entity));
        Assert.AreEqual(1, world.Get<TestPosition>(entity).X);
        Assert.AreEqual(2, world.Get<TestVelocity>(entity).X);
    }

    [Test]
    public void PlaybackExecutesCommandsInRecordedOrder()
    {
        var world = new World();
        var entity = world.CreateEntity(new TestPosition { X = 1 });
        var buffer = new CommandBuffer(world);

        buffer.Set(entity, new TestPosition { X = 2 });
        buffer.Add(entity, new TestVelocity { X = 3 });
        buffer.Remove<TestVelocity>(entity);
        buffer.DestroyEntity(entity);

        var result = buffer.Playback();

        Assert.IsFalse(world.IsAlive(entity));
        Assert.IsFalse(result.TryResolve(new BufferedEntity(999), out _));
    }

    [Test]
    public void PlaybackAllowsStructuralChangesCollectedDuringQueryEnumeration()
    {
        var world = new World();
        var first = world.CreateEntity(new TestPosition { X = 1 });
        var second = world.CreateEntity(new TestPosition { X = 2 });
        var buffer = new CommandBuffer(world);

        foreach (var entity in world.Query<TestPosition>())
        {
            buffer.Add(entity, new TestVelocity { X = world.Get<TestPosition>(entity).X });
        }

        buffer.Playback();

        CollectionAssert.AreEquivalent(new[] { first, second }, world.Query<TestPosition, TestVelocity>().ToList());
    }

    [Test]
    public void PlaybackStopsWhenWorldOperationThrowsForInvalidEntity()
    {
        var world = new World();
        var invalid = new Entity(world.WorldId, 100, 1);
        var buffer = new CommandBuffer(world);

        buffer.Add(invalid, new TestPosition());

        Assert.Throws<InvalidOperationException>(() => buffer.Playback());
    }

    [Test]
    public void DestroyInvalidEntityDuringPlaybackDoesNotThrow()
    {
        var world = new World();
        var invalid = new Entity(world.WorldId, 100, 1);
        var buffer = new CommandBuffer(world);

        buffer.DestroyEntity(invalid);

        Assert.DoesNotThrow(() => buffer.Playback());
    }

    [Test]
    public void BufferedEntityCanBeDestroyedInSamePlayback()
    {
        var world = new World();
        var buffer = new CommandBuffer(world);

        var created = buffer.CreateEntity(new TestPosition());
        buffer.DestroyEntity(created);

        var result = buffer.Playback();
        var entity = result.Resolve(created);

        Assert.IsFalse(world.IsAlive(entity));
    }

    [Test]
    public void PlaybackConsumesRecordedCommands()
    {
        var world = new World();
        var buffer = new CommandBuffer(world);

        _ = buffer.CreateEntity(new TestPosition());

        buffer.Playback();
        buffer.Playback();

        Assert.AreEqual(1, world.EntityCount);
    }
}
