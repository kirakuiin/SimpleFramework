using System;
using System.Linq;
using System.Reflection;
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

    [Test]
    public void FailedPlaybackConsumesBatchPreservesPrefixAndAllowsRecovery()
    {
        var world = new World();
        var buffer = new CommandBuffer(world);
        var prefix = buffer.CreateEntity(new TestPosition { X = 1 });
        buffer.Add(new Entity(world.WorldId, int.MaxValue, 1), new TestVelocity());
        var suffix = buffer.CreateEntity(new TestPosition { X = 2 });
        var commandCount = GetRecordedCommandCount(buffer);
        CommandBufferResult? firstResult = null;

        var firstException = Assert.Throws<InvalidOperationException>(() => firstResult = buffer.Playback());

        Assert.IsNull(firstResult);
        Assert.IsNull(firstException!.InnerException);
        StringAssert.Contains("Invalid entity handle", firstException.Message);
        Assert.AreEqual(0, commandCount());
        Assert.AreEqual(1, world.EntityCount);
        CollectionAssert.AreEqual(new[] { 1f }, world.Query<TestPosition>().Select(entity => world.Get<TestPosition>(entity).X));
        Assert.Throws<InvalidOperationException>(() => buffer.Add(prefix, new TestVelocity()));
        Assert.Throws<InvalidOperationException>(() => buffer.Add(suffix, new TestVelocity()));

        var emptyResult = buffer.Playback();
        Assert.AreEqual(1, world.EntityCount);
        Assert.IsFalse(emptyResult.TryResolve(prefix, out _));

        var recovered = buffer.CreateEntity(new TestPosition { X = 3 });
        Assert.AreEqual(0, recovered.Id);
        Assert.AreNotEqual(prefix, recovered);
        var recoveredResult = buffer.Playback();
        var recoveredEntity = recoveredResult.Resolve(recovered);

        Assert.AreEqual(3, world.Get<TestPosition>(recoveredEntity).X);
        Assert.AreEqual(2, world.EntityCount);
        Assert.AreEqual(0, commandCount());
    }

    [Test]
    public void SuccessfulPlaybackInvalidatesOldHandlesButKeepsItsResultResolvable()
    {
        var world = new World();
        var buffer = new CommandBuffer(world);
        var first = buffer.CreateEntity(new TestPosition { X = 1 });

        var firstResult = buffer.Playback();
        var firstEntity = firstResult.Resolve(first);

        Assert.AreEqual(1, world.Get<TestPosition>(firstEntity).X);
        Assert.Throws<InvalidOperationException>(() => buffer.Add(first, new TestVelocity()));
        Assert.AreEqual(firstEntity, firstResult.Resolve(first));

        var second = buffer.CreateEntity(new TestPosition { X = 2 });
        Assert.AreEqual(0, second.Id);
        Assert.AreNotEqual(first, second);
        var secondResult = buffer.Playback();
        Assert.AreEqual(2, world.Get<TestPosition>(secondResult.Resolve(second)).X);
    }

    [Test]
    public void StaleHandleDiagnosticIdentifiesExpiredOrForeignBatch()
    {
        var world = new World();
        var buffer = new CommandBuffer(world);
        var stale = buffer.CreateEntity(new TestPosition());
        buffer.Playback();

        var exception = Assert.Throws<InvalidOperationException>(() => buffer.Add(stale, new TestVelocity()));

        StringAssert.Contains("expired or foreign command batch", exception!.Message);
        StringAssert.DoesNotContain("another command buffer", exception.Message);
    }

    private static Func<int> GetRecordedCommandCount(CommandBuffer buffer)
    {
        var field = typeof(CommandBuffer).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return () => ((System.Collections.ICollection)field.GetValue(buffer)!).Count;
    }
}
