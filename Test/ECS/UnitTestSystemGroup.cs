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

    [Test]
    public void DependencyAttributesThrowWhenSystemTypeIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new RunBeforeAttribute(null));
        Assert.Throws<ArgumentNullException>(() => new RunAfterAttribute(null));
    }

    [Test]
    public void DependencyAttributesThrowWhenSystemTypeDoesNotInheritEcsSystem()
    {
        Assert.Throws<ArgumentException>(() => new RunBeforeAttribute(typeof(TestPosition)));
        Assert.Throws<ArgumentException>(() => new RunAfterAttribute(typeof(TestPosition)));
    }

    [Test]
    public void UpdateRunsRunAfterSystemAfterReferencedSystem()
    {
        var world = new World();
        var calls = new List<string>();
        var group = new SystemGroup();

        group.Add(new AfterFirstSystem(world, calls, "after"));
        group.Add(new FirstDependencySystem(world, calls, "first"));

        group.Update();

        CollectionAssert.AreEqual(new[] { "first", "after" }, calls);
    }

    [Test]
    public void UpdateRunsRunBeforeSystemBeforeReferencedSystem()
    {
        var world = new World();
        var calls = new List<string>();
        var group = new SystemGroup();

        group.Add(new LastDependencySystem(world, calls, "last"));
        group.Add(new BeforeLastSystem(world, calls, "before"));

        group.Update();

        CollectionAssert.AreEqual(new[] { "before", "last" }, calls);
    }

    [Test]
    public void DependencyMissingReferencedSystemKeepsStableOrder()
    {
        var world = new World();
        var calls = new List<string>();
        var group = new SystemGroup();

        group.Add(new AfterMissingSystem(world, calls, "first"));
        group.Add(new RecordingSystem(world, calls, "second"));

        group.Update();

        CollectionAssert.AreEqual(new[] { "first", "second" }, calls);
    }

    [Test]
    public void DependencyDoesNotOverrideManualOrder()
    {
        var world = new World();
        var calls = new List<string>();
        var group = new SystemGroup();

        group.Add(new CrossOrderAfterSystem(world, calls, "early"), -10);
        group.Add(new CrossOrderDependencySystem(world, calls, "late"), 10);

        group.Update();

        CollectionAssert.AreEqual(new[] { "early", "late" }, calls);
    }

    [Test]
    public void DeltaTimeUpdateUsesDependencyOrder()
    {
        var world = new World();
        var calls = new List<string>();
        var group = new SystemGroup();

        group.Add(new AfterFirstSystem(world, calls, "after"));
        group.Add(new FirstDependencySystem(world, calls, "first"));

        group.Update(0.5f);

        CollectionAssert.AreEqual(new[] { "first", "after" }, calls);
    }

    [Test]
    public void DependencyCanReferenceBaseSystemType()
    {
        var world = new World();
        var calls = new List<string>();
        var group = new SystemGroup();

        group.Add(new AfterBaseDependencySystem(world, calls, "after"));
        group.Add(new ConcreteBaseDependencySystem(world, calls, "base"));

        group.Update();

        CollectionAssert.AreEqual(new[] { "base", "after" }, calls);
    }

    [Test]
    public void DependencyCycleThrowsWithDependencyChain()
    {
        var world = new World();
        var calls = new List<string>();
        var group = new SystemGroup();

        group.Add(new CycleASystem(world, calls, "a"));
        group.Add(new CycleBSystem(world, calls, "b"));

        var exception = Assert.Throws<InvalidOperationException>(() => group.Update());

        StringAssert.Contains("SystemGroup dependency cycle detected:", exception.Message);
        StringAssert.Contains("CycleASystem -> CycleBSystem -> CycleASystem", exception.Message);
    }

    [Test]
    public void DependencyCycleThrowsBeforeAnySystemUpdates()
    {
        var world = new World();
        var calls = new List<string>();
        var group = new SystemGroup();

        group.Add(new CycleASystem(world, calls, "a"));
        group.Add(new CycleBSystem(world, calls, "b"));

        Assert.Throws<InvalidOperationException>(() => group.Update());

        CollectionAssert.IsEmpty(calls);
    }

    [Test]
    public void UpdateSkipsDisabledSystems()
    {
        var world = new World();
        var enabled = new RecordingSystem(world);
        var disabled = new RecordingSystem(world) { Enabled = false };
        var group = new SystemGroup();

        group.Add(enabled);
        group.Add(disabled);

        group.Update();

        Assert.AreEqual(1, enabled.UpdateCount);
        Assert.AreEqual(0, disabled.UpdateCount);
    }

    [Test]
    public void DeltaTimeUpdateSkipsDisabledSystems()
    {
        var world = new World();
        var enabled = new RecordingSystem(world);
        var disabled = new RecordingSystem(world) { Enabled = false };
        var group = new SystemGroup();

        group.Add(enabled);
        group.Add(disabled);

        group.Update(0.5f);

        Assert.AreEqual(1, enabled.DeltaUpdateCount);
        Assert.AreEqual(0, disabled.DeltaUpdateCount);
    }

    [Test]
    public void ValidateDetectsDependencyCycleWithoutUpdatingSystems()
    {
        var world = new World();
        var calls = new List<string>();
        var group = new SystemGroup();

        group.Add(new CycleASystem(world, calls, "a"));
        group.Add(new CycleBSystem(world, calls, "b"));

        var exception = Assert.Throws<InvalidOperationException>(() => group.Validate());

        StringAssert.Contains("CycleASystem -> CycleBSystem -> CycleASystem", exception.Message);
        CollectionAssert.IsEmpty(calls);
    }

    [Test]
    public void ValidateDoesNotUpdateSystems()
    {
        var world = new World();
        var system = new RecordingSystem(world);
        var group = new SystemGroup();

        group.Add(system);

        group.Validate();

        Assert.AreEqual(0, system.UpdateCount);
    }

    [Test]
    public void AddInvalidatesValidatedOrderCache()
    {
        var world = new World();
        var calls = new List<string>();
        var group = new SystemGroup();

        group.Add(new AfterFirstSystem(world, calls, "after"));
        group.Validate();
        group.Add(new FirstDependencySystem(world, calls, "first"));

        group.Update();

        CollectionAssert.AreEqual(new[] { "first", "after" }, calls);
    }

    [Test]
    public void RemoveInvalidatesValidatedOrderCache()
    {
        var world = new World();
        var calls = new List<string>();
        var group = new SystemGroup();
        var first = new FirstDependencySystem(world, calls, "first");

        group.Add(new AfterFirstSystem(world, calls, "after"));
        group.Add(first);
        group.Validate();
        group.Remove(first);

        group.Update();

        CollectionAssert.AreEqual(new[] { "after" }, calls);
    }

    [Test]
    public void DisabledSystemsStillParticipateInDependencyCycleDetection()
    {
        var world = new World();
        var calls = new List<string>();
        var group = new SystemGroup();

        group.Add(new CycleASystem(world, calls, "a") { Enabled = false });
        group.Add(new CycleBSystem(world, calls, "b"));

        Assert.Throws<InvalidOperationException>(() => group.Validate());
    }

    [Test]
    public void DisabledSystemsKeepDependencyOrderForEnabledSystems()
    {
        var world = new World();
        var calls = new List<string>();
        var group = new SystemGroup();

        group.Add(new AfterDisabledMiddleSystem(world, calls, "after"));
        group.Add(new DisabledMiddleSystem(world, calls, "middle") { Enabled = false });
        group.Add(new BeforeDisabledMiddleSystem(world, calls, "before"));

        group.Update();

        CollectionAssert.AreEqual(new[] { "before", "after" }, calls);
    }

    private class RecordingSystem : EcsSystem
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

    [RunAfter(typeof(FirstDependencySystem))]
    private sealed class AfterFirstSystem : RecordingSystem
    {
        public AfterFirstSystem(World world, List<string> calls, string name) : base(world, calls, name)
        {
        }
    }

    private sealed class FirstDependencySystem : RecordingSystem
    {
        public FirstDependencySystem(World world, List<string> calls, string name) : base(world, calls, name)
        {
        }
    }

    [RunBefore(typeof(LastDependencySystem))]
    private sealed class BeforeLastSystem : RecordingSystem
    {
        public BeforeLastSystem(World world, List<string> calls, string name) : base(world, calls, name)
        {
        }
    }

    private sealed class LastDependencySystem : RecordingSystem
    {
        public LastDependencySystem(World world, List<string> calls, string name) : base(world, calls, name)
        {
        }
    }

    [RunAfter(typeof(MissingDependencySystem))]
    private sealed class AfterMissingSystem : RecordingSystem
    {
        public AfterMissingSystem(World world, List<string> calls, string name) : base(world, calls, name)
        {
        }
    }

    private sealed class MissingDependencySystem : RecordingSystem
    {
        public MissingDependencySystem(World world, List<string> calls, string name) : base(world, calls, name)
        {
        }
    }

    [RunAfter(typeof(CrossOrderDependencySystem))]
    private sealed class CrossOrderAfterSystem : RecordingSystem
    {
        public CrossOrderAfterSystem(World world, List<string> calls, string name) : base(world, calls, name)
        {
        }
    }

    private sealed class CrossOrderDependencySystem : RecordingSystem
    {
        public CrossOrderDependencySystem(World world, List<string> calls, string name) : base(world, calls, name)
        {
        }
    }

    [RunAfter(typeof(BaseDependencySystem))]
    private sealed class AfterBaseDependencySystem : RecordingSystem
    {
        public AfterBaseDependencySystem(World world, List<string> calls, string name) : base(world, calls, name)
        {
        }
    }

    private abstract class BaseDependencySystem : RecordingSystem
    {
        protected BaseDependencySystem(World world, List<string> calls, string name) : base(world, calls, name)
        {
        }
    }

    private sealed class ConcreteBaseDependencySystem : BaseDependencySystem
    {
        public ConcreteBaseDependencySystem(World world, List<string> calls, string name) : base(world, calls, name)
        {
        }
    }

    [RunAfter(typeof(CycleBSystem))]
    private sealed class CycleASystem : RecordingSystem
    {
        public CycleASystem(World world, List<string> calls, string name) : base(world, calls, name)
        {
        }
    }

    [RunAfter(typeof(CycleASystem))]
    private sealed class CycleBSystem : RecordingSystem
    {
        public CycleBSystem(World world, List<string> calls, string name) : base(world, calls, name)
        {
        }
    }

    [RunAfter(typeof(DisabledMiddleSystem))]
    private sealed class AfterDisabledMiddleSystem : RecordingSystem
    {
        public AfterDisabledMiddleSystem(World world, List<string> calls, string name) : base(world, calls, name)
        {
        }
    }

    [RunAfter(typeof(BeforeDisabledMiddleSystem))]
    private sealed class DisabledMiddleSystem : RecordingSystem
    {
        public DisabledMiddleSystem(World world, List<string> calls, string name) : base(world, calls, name)
        {
        }
    }

    private sealed class BeforeDisabledMiddleSystem : RecordingSystem
    {
        public BeforeDisabledMiddleSystem(World world, List<string> calls, string name) : base(world, calls, name)
        {
        }
    }
}
