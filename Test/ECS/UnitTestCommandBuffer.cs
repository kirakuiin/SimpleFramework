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

    [Test]
    public void PlaybackRejectsBufferedEntityFromAnotherBuffer()
    {
        var world = new World();
        var first = new CommandBuffer(world);
        var second = new CommandBuffer(world);
        var foreign = first.CreateEntity(new TestPosition { X = 1 });
        var local = second.CreateEntity(new TestPosition { X = 2 });

        Assert.AreEqual(foreign.Id, local.Id);
        Assert.Throws<InvalidOperationException>(() => second.Add(foreign, new TestVelocity { X = 3 }));
        Assert.AreEqual(0, world.EntityCount);

        var result = second.Playback();
        var entity = result.Resolve(local);
        Assert.AreEqual(1, world.EntityCount);
        Assert.IsFalse(world.Has<TestVelocity>(entity));
    }

    [Test]
    public void ResultRejectsBufferedEntityFromAnotherBufferWithSameLocalId()
    {
        var world = new World();
        var first = new CommandBuffer(world);
        var second = new CommandBuffer(world);
        var firstHandle = first.CreateEntity(new TestPosition { X = 1 });
        var secondHandle = second.CreateEntity(new TestPosition { X = 2 });

        var firstResult = first.Playback();
        var secondResult = second.Playback();

        Assert.AreEqual(firstHandle.Id, secondHandle.Id);
        Assert.IsTrue(firstResult.TryResolve(firstHandle, out _));
        Assert.IsFalse(secondResult.TryResolve(firstHandle, out _));
        Assert.Throws<InvalidOperationException>(() => secondResult.Resolve(firstHandle));
    }

    [Test]
    public void CommandBufferRejectsNullReferenceComponentBeforeRecording()
    {
        var world = new World();
        var buffer = new CommandBuffer(world);

        var createException = Assert.Throws<ArgumentNullException>(() => buffer.CreateEntity<TestName>(null!));
        Assert.AreEqual("c1", createException!.ParamName);

        var created = buffer.CreateEntity(new TestName { Value = "valid" });
        var addException = Assert.Throws<ArgumentNullException>(() => buffer.Add<TestName>(created, null!));
        Assert.AreEqual("component", addException!.ParamName);

        var result = buffer.Playback();
        var entity = result.Resolve(created);
        buffer.Playback();

        Assert.AreEqual(1, world.EntityCount);
        Assert.AreEqual("valid", world.Get<TestName>(entity).Value);
    }
}
