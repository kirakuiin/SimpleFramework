using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimpleFramework.ECS;

namespace Test.ECS;

[TestFixture]
public class TestSystemGroup
{
    [Test]
    public void UpdateCallsEveryAddedSystem()
    {
        var world = new World();
        var first = new RecordingSystem(world);
        var second = new RecordingSystem(world);
        var group = new SystemGroup();

        group.Add(first);
        group.Add(second);
        group.Update();

        Assert.AreEqual(1, first.UpdateCount);
        Assert.AreEqual(1, second.UpdateCount);
    }

    [Test]
    public void UpdateRunsSystemsByOrderAscending()
    {
        var world = new World();
        var calls = new List<string>();
        var group = new SystemGroup();

        group.Add(new RecordingSystem(world, calls, "late"), 20);
        group.Add(new RecordingSystem(world, calls, "early"), -10);
        group.Add(new RecordingSystem(world, calls, "middle"), 0);

        group.Update();

        CollectionAssert.AreEqual(new[] { "early", "middle", "late" }, calls);
    }

    [Test]
    public void UpdateKeepsInsertionOrderForSameOrder()
    {
        var world = new World();
        var calls = new List<string>();
        var group = new SystemGroup();

        group.Add(new RecordingSystem(world, calls, "first"), 10);
        group.Add(new RecordingSystem(world, calls, "second"), 10);
        group.Add(new RecordingSystem(world, calls, "third"), 10);

        group.Update();

        CollectionAssert.AreEqual(new[] { "first", "second", "third" }, calls);
    }

    [Test]
    public void DeltaTimeUpdatePassesDeltaTimeToEverySystem()
    {
        var world = new World();
        var first = new RecordingSystem(world);
        var second = new RecordingSystem(world);
        var group = new SystemGroup();

        group.Add(first);
        group.Add(second);
        group.Update(0.5f);

        Assert.AreEqual(1, first.DeltaUpdateCount);
        Assert.AreEqual(1, second.DeltaUpdateCount);
        Assert.AreEqual(0.5f, first.LastDeltaTime);
        Assert.AreEqual(0.5f, second.LastDeltaTime);
    }

    [Test]
    public void RemoveDeletesSystemAndReportsResult()
    {
        var world = new World();
        var removed = new RecordingSystem(world);
        var remaining = new RecordingSystem(world);
        var missing = new RecordingSystem(world);
        var group = new SystemGroup();

        group.Add(removed);
        group.Add(remaining);

        Assert.IsTrue(group.Remove(removed));
        Assert.IsFalse(group.Remove(missing));

        group.Update();

        Assert.AreEqual(0, removed.UpdateCount);
        Assert.AreEqual(1, remaining.UpdateCount);
    }

    [Test]
    public void AddThrowsWhenSystemAlreadyExists()
    {
        var world = new World();
        var system = new RecordingSystem(world);
        var group = new SystemGroup();

        group.Add(system);

        Assert.Throws<ArgumentException>(() => group.Add(system));
    }

    [Test]
    public void AddThrowsWhenSystemIsNull()
    {
        var group = new SystemGroup();

        Assert.Throws<ArgumentNullException>(() => group.Add(null));
    }

    [Test]
    public void RemoveThrowsWhenSystemIsNull()
    {
        var group = new SystemGroup();

        Assert.Throws<ArgumentNullException>(() => group.Remove(null));
    }

    [Test]
    public void AddThrowsDuringUpdate()
    {
        var world = new World();
        var group = new SystemGroup();
        var system = new MutatingSystem(world, () => group.Add(new RecordingSystem(world)));

        group.Add(system);

        Assert.Throws<InvalidOperationException>(() => group.Update());
    }

    [Test]
    public void RemoveThrowsDuringUpdate()
    {
        var world = new World();
        var group = new SystemGroup();
        var target = new RecordingSystem(world);
        var system = new MutatingSystem(world, () => group.Remove(target));

        group.Add(system);
        group.Add(target);

        Assert.Throws<InvalidOperationException>(() => group.Update());
    }

    [Test]
    public void UpdateDoesNotSwallowSystemExceptions()
    {
        var world = new World();
        var group = new SystemGroup();
        var exception = new TestSystemException();

        group.Add(new ThrowingSystem(world, exception));

        Assert.AreSame(exception, Assert.Throws<TestSystemException>(() => group.Update()));
    }

    [Test]
    public void DeltaTimeUpdateDoesNotSwallowSystemExceptions()
    {
        var world = new World();
        var group = new SystemGroup();
        var exception = new TestSystemException();

        group.Add(new ThrowingSystem(world, exception));

        Assert.AreSame(exception, Assert.Throws<TestSystemException>(() => group.Update(0.5f)));
    }

    private sealed class RecordingSystem : EcsSystem
    {
        private readonly List<string> _calls;
        private readonly string _name;

        public RecordingSystem(World world, List<string> calls = null, string name = "") : base(world)
        {
            _calls = calls;
            _name = name;
        }

        public int UpdateCount { get; private set; }

        public int DeltaUpdateCount { get; private set; }

        public float LastDeltaTime { get; private set; }

        public override void Update()
        {
            UpdateCount++;
            if (_calls != null)
            {
                _calls.Add(_name);
            }
        }

        public override void Update(float deltaTime)
        {
            DeltaUpdateCount++;
            LastDeltaTime = deltaTime;
            Update();
        }
    }

    private sealed class MutatingSystem : EcsSystem
    {
        private readonly Action _action;

        public MutatingSystem(World world, Action action) : base(world)
        {
            _action = action;
        }

        public override void Update()
        {
            _action();
        }
    }

    private sealed class ThrowingSystem : EcsSystem
    {
        private readonly Exception _exception;

        public ThrowingSystem(World world, Exception exception) : base(world)
        {
            _exception = exception;
        }

        public override void Update()
        {
            throw _exception;
        }
    }

    private sealed class TestSystemException : Exception
    {
    }
}
