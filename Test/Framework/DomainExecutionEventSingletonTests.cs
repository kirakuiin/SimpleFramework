using NUnit.Framework;
using SimpleFramework;
using SimpleFramework.FrameworkImpl;

namespace Test.Framework;

/// <summary>验证同步 CQ、本地事件、严格单例和热路径分配特征。</summary>
[TestFixture]
public sealed class DomainExecutionEventSingletonTests
{
    [TearDown]
    public void TearDownSingleton()
    {
        StrictProbeDomain.SetHooks();
        StrictProbeDomain.DestroyInstance();
    }

    [Test]
    public void InitializingContextsExposeOnlyDeclaredCapabilities()
    {
        var handled = 0;
        var model = new ProbeModel(context =>
        {
            Assert.That(context.GetUtility<IClockUtility>(), Is.Not.Null);
            Assert.Throws<InvalidOperationException>(() => context.SendEvent(new Ping(1)));
        });
        var system = new ProbeSystem(context =>
        {
            Assert.That(context.GetModel<IPlayerModel>(), Is.SameAs(model));
            Assert.That(context.GetUtility<IClockUtility>(), Is.Not.Null);
            context.RegisterEvent<Ping>(_ => handled++);
            Assert.Throws<InvalidOperationException>(() => context.GetSystem<IPlayerSystem>());
            Assert.Throws<InvalidOperationException>(() => context.SendEvent(new Ping(1)));
        });
        using var domain = ProbeDomain.Create(configure: value =>
        {
            value.RegisterUtility<IClockUtility>(new ClockUtility());
            value.RegisterModel(model);
            value.RegisterSystem(system);
        });
        system.SavedContext!.SendEvent(new Ping(1));
        Assert.That(system.SavedContext.GetSystem<IPlayerSystem>(), Is.SameAs(system));
        Assert.That(handled, Is.EqualTo(1));
    }

    [Test]
    public void CommandAndQueryContextsHaveDistinctCapabilitiesAndAreByRefLike()
    {
        Assert.That(typeof(CommandContext).IsByRefLike, Is.True);
        Assert.That(typeof(QueryContext).IsByRefLike, Is.True);
        Assert.That(typeof(QueryContext).GetMethod(nameof(CommandContext.SendEvent)), Is.Null);
        Assert.That(typeof(QueryContext).GetMethod(nameof(CommandContext.SendCommand), Type.EmptyTypes), Is.Null);
    }

    [Test]
    public void CommandsAndQueriesNestSynchronouslyAndRestoreExecutionAfterFailure()
    {
        using var domain = ProbeDomain.Create();
        var order = new List<string>();
        domain.SendCommand(new DelegateCommand(context =>
        {
            order.Add("outer-start");
            var result = context.SendQuery(new DelegateQuery<int>(_ =>
            {
                order.Add("query");
                return 7;
            }));
            order.Add($"outer-{result}");
        }));
        Assert.That(order, Is.EqualTo(new[] { "outer-start", "query", "outer-7" }));

        var expected = new ApplicationException("command");
        Assert.That(Assert.Throws<ApplicationException>(() =>
            domain.SendCommand(new DelegateCommand(_ => throw expected))), Is.SameAs(expected));
        domain.RegisterUtility(new ClockUtility());
    }

    [Test]
    public void SameCommandInstanceCanRunSequentiallyAcrossDomains()
    {
        var values = new List<int>();
        var command = new DelegateCommand(context => values.Add(context.GetUtility<IClockUtility>().Value));
        using var first = ProbeDomain.Create(configure: value => value.RegisterUtility<IClockUtility>(new ClockUtility { Value = 1 }));
        using var second = ProbeDomain.Create(configure: value => value.RegisterUtility<IClockUtility>(new ClockUtility { Value = 2 }));
        first.SendCommand(command);
        second.SendCommand(command);
        Assert.That(values, Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public void EventsAreLocalAndRunInRegistrationOrder()
    {
        using var parent = ProbeDomain.Create();
        using var child = ProbeDomain.Create();
        parent.AddChild(child);
        var order = new List<string>();
        parent.RegisterEvent<Ping>(_ => order.Add("parent"));
        child.RegisterEvent<Ping>(_ => order.Add("child-1"));
        child.RegisterEvent<Ping>(_ => order.Add("child-2"));
        child.SendEvent(new Ping(1));
        Assert.That(order, Is.EqualTo(new[] { "child-1", "child-2" }));
    }

    [Test]
    public void EventRegistrationChangesOnlyFutureCopyOnWriteSnapshots()
    {
        using var domain = ProbeDomain.Create();
        var calls = new List<string>();
        IUnRegister? secondToken = null;
        domain.RegisterEvent<Ping>(_ =>
        {
            calls.Add("first");
            secondToken?.UnRegister();
            domain.RegisterEvent<Ping>(_ => calls.Add("late"));
        });
        secondToken = domain.RegisterEvent<Ping>(_ => calls.Add("second"));
        domain.SendEvent(new Ping(1));
        Assert.That(calls, Is.EqualTo(new[] { "first", "second" }));
        calls.Clear();
        domain.SendEvent(new Ping(2));
        Assert.That(calls, Is.EqualTo(new[] { "first", "late" }));
    }

    [Test]
    public void EventFailureIsFailFastAndRestoresTreeExecution()
    {
        using var domain = ProbeDomain.Create();
        var laterRan = false;
        var expected = new ApplicationException("event");
        domain.RegisterEvent<Ping>(_ => throw expected);
        domain.RegisterEvent<Ping>(_ => laterRan = true);
        Assert.That(Assert.Throws<ApplicationException>(() => domain.SendEvent(new Ping(1))), Is.SameAs(expected));
        Assert.That(laterRan, Is.False);
        domain.RegisterUtility(new ClockUtility());
    }

    [Test]
    public void SystemOwnedEventsAreCanceledBeforeRelease()
    {
        var calls = 0;
        ProbeSystem? system = null;
        var domain = ProbeDomain.Create(configure: value =>
        {
            system = new ProbeSystem(
                context => context.RegisterEvent<Ping>(_ => calls++),
                () => system!.SavedContext!.SendEvent(new Ping(2)));
            value.RegisterSystem(system);
        });
        domain.SendEvent(new Ping(1));
        Assert.That(calls, Is.EqualTo(1));
        Assert.Throws<InvalidOperationException>(() => domain.Dispose());
        Assert.That(calls, Is.EqualTo(1));
        domain.Dispose();
    }

    [Test]
    public void EventTokensAreIdempotentAndHarmlessAfterDisposal()
    {
        var domain = ProbeDomain.Create();
        var token = domain.RegisterEvent<Ping>(_ => { });
        token.UnRegister();
        token.UnRegister();
        domain.Dispose();
        token.UnRegister();
    }

    [Test]
    public void EventTokenOnlyRemovesItsExactDuplicateDelegateRegistration()
    {
        var @event = new Event<int>();
        var calls = 0;
        Action<int> handler = _ => calls++;
        var first = @event.Register(handler);
        _ = @event.Register(handler);

        @event.UnRegister(handler);
        first.UnRegister();
        @event.Trigger(1);

        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void SingletonPublishesOnlyAfterOnActivated()
    {
        StrictProbeDomain? seenDuringConfigure = null;
        StrictProbeDomain.SetHooks(configure: _ => seenDuringConfigure = StrictProbeDomain.GetInstance());
        var instance = StrictProbeDomain.Instance;
        Assert.That(seenDuringConfigure, Is.Null);
        Assert.That(StrictProbeDomain.GetInstance(), Is.SameAs(instance));
        Assert.That(StrictProbeDomain.Instance, Is.SameAs(instance));
    }

    [Test]
    public void SingletonRejectsCreationReentrancyAndCanRetryAfterFailure()
    {
        StrictProbeDomain.SetHooks(configure: _ => _ = StrictProbeDomain.Instance);
        Assert.Throws<InvalidOperationException>(() => _ = StrictProbeDomain.Instance);
        Assert.That(StrictProbeDomain.GetInstance(), Is.Null);

        StrictProbeDomain.SetHooks();
        Assert.That(StrictProbeDomain.Instance, Is.Not.Null);
    }

    [Test]
    public void SingletonDestroyDuringCreationIsRejectedAndAbsentDestroyDoesNotCreate()
    {
        StrictProbeDomain.DestroyInstance();
        Assert.That(StrictProbeDomain.GetInstance(), Is.Null);
        StrictProbeDomain.SetHooks(configure: _ => StrictProbeDomain.DestroyInstance());
        Assert.Throws<InvalidOperationException>(() => _ = StrictProbeDomain.Instance);
        Assert.That(StrictProbeDomain.GetInstance(), Is.Null);
    }

    [Test]
    public void DirectSingletonDisposeClearsPublishedReference()
    {
        var first = StrictProbeDomain.Instance;
        first.Dispose();
        Assert.That(StrictProbeDomain.GetInstance(), Is.Null);
        var second = StrictProbeDomain.Instance;
        Assert.That(second, Is.Not.SameAs(first));
    }

    [Test]
    public void OnActivatedCannotReturnDisposedCandidate()
    {
        ProbeDomain? candidate = null;
        Assert.Throws<InvalidOperationException>(() => ProbeDomain.Create(activated: value =>
        {
            candidate = value;
            value.Dispose();
        }));
        Assert.Throws<ObjectDisposedException>(() => candidate!.TryGetUtility<IClockUtility>(out _));
    }

    [Test]
    public void ExactLookupEventSendAndCommandContextHaveNoPerCallAllocationAfterWarmup()
    {
        using var domain = ProbeDomain.Create(configure: value => value.RegisterUtility<IClockUtility>(new ClockUtility()));
        var command = new EmptyCommand();
        domain.RegisterEvent<Ping>(_ => { });
        _ = domain.GetUtility<IClockUtility>();
        domain.SendEvent(new Ping(0));
        domain.SendCommand(command);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1_000; index++)
        {
            _ = domain.GetUtility<IClockUtility>();
            domain.SendEvent(new Ping(index));
            domain.SendCommand(command);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.That(allocated, Is.Zero);
    }

    [Test]
    public void AssignableFallbackReflectsCurrentRegistryWithoutCache()
    {
        using var domain = ProbeDomain.Create(configure: value => value.RegisterModel(new ProbeModel()));
        Assert.That(domain.GetModel<IPlayerModel>(), Is.TypeOf<ProbeModel>());
        _ = domain.GetModel<IPlayerModel>();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1_000; index++) _ = domain.GetModel<IPlayerModel>();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.That(allocated, Is.Zero);
        domain.RegisterModel(new AlternateModel());
        Assert.Throws<InvalidOperationException>(() => domain.GetModel<IPlayerModel>());
    }

    [Test]
    public void DeepTreeUsesSharedExecutionGuard()
    {
        var nodes = new List<ProbeDomain>();
        var root = ProbeDomain.Create("0");
        nodes.Add(root);
        var current = root;
        for (var index = 1; index <= 128; index++)
        {
            var child = ProbeDomain.Create(index.ToString());
            current.AddChild(child);
            nodes.Add(child);
            current = child;
        }
        var detached = ProbeDomain.Create("detached");
        current.SendQuery(new DelegateQuery<int>(_ =>
        {
            Assert.Throws<InvalidOperationException>(() => root.AddChild(detached));
            return 0;
        }));
        root.Dispose();
        detached.Dispose();
    }
}
