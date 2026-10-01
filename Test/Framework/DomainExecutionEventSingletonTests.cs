using NUnit.Framework;
using SimpleFramework;

namespace Test.Framework;

/// <summary>验证 Command/Query 上下文、能力规则、本地事件和严格单例契约。</summary>
[TestFixture]
public sealed class DomainExecutionEventSingletonTests
{
    /// <summary>清理单例测试留下的回调和实例。</summary>
    [TearDown]
    public void TearDownSingleton()
    {
        StrictProbeDomain.SetHooks();
        StrictProbeDomain.DestroyInstance();
    }

    /// <summary>测试 Command 与 Query 上下文是 ref struct。</summary>
    [Test]
    public void ContextsAreByRefLike()
    {
        Assert.That(typeof(CommandContext).IsByRefLike, Is.True);
        Assert.That(typeof(QueryContext).IsByRefLike, Is.True);
    }

    /// <summary>测试命令和查询可以同步嵌套执行并返回结果。</summary>
    [Test]
    public void CommandsAndQueriesNestSynchronously()
    {
        var model = new ProbeModel();
        using var domain = ProbeDomain.Create(configure: value => value.AddModel(model));
        var order = new List<string>();

        var result = domain.SendCommand(new DelegateCommand<int>(context =>
        {
            order.Add("command");
            context.SendCommand(new DelegateCommand(_ => order.Add("nested")));
            var resolved = context.SendQuery(new DelegateQuery<IPlayerModel>(query => query.GetModel<IPlayerModel>()));
            Assert.That(resolved, Is.SameAs(model));
            return 42;
        }));

        Assert.That(result, Is.EqualTo(42));
        Assert.That(order, Is.EqualTo(new[] { "command", "nested" }));
    }

    /// <summary>测试可以直接构造上下文来执行命令。</summary>
    [Test]
    public void ContextsCanBeConstructedDirectlyForTests()
    {
        var utility = new ClockUtility { Value = 3 };
        using var domain = ProbeDomain.Create(configure: value => value.AddUtility<IClockUtility>(utility));
        var command = new DelegateCommand<int>(context => context.GetUtility<IClockUtility>().Value);

        Assert.That(command.Execute(new CommandContext(domain)), Is.EqualTo(3));
        Assert.Throws<ArgumentNullException>(() => _ = new QueryContext(null!));
    }

    /// <summary>测试 IController 通过扩展方法获取组件、发送命令与查询、订阅事件。</summary>
    [Test]
    public void ControllerUsesCapabilityExtensions()
    {
        var model = new ProbeModel();
        var system = new ProbeSystem();
        var utility = new ClockUtility();
        using var domain = ProbeDomain.Create(configure: value =>
        {
            value.AddModel(model);
            value.AddSystem(system);
            value.AddUtility(utility);
        });
        var controller = new ProbeController(domain);
        var received = new List<int>();
        var executed = false;

        using var token = controller.RegisterEvent<Ping>(ping => received.Add(ping.Value));
        controller.SendCommand(new DelegateCommand(context =>
        {
            executed = true;
            context.SendEvent(new Ping(1));
        }));

        Assert.That(controller.GetModel<IPlayerModel>(), Is.SameAs(model));
        Assert.That(controller.GetSystem<IPlayerSystem>(), Is.SameAs(system));
        Assert.That(controller.GetUtility<ClockUtility>(), Is.SameAs(utility));
        Assert.That(controller.SendQuery(new DelegateQuery<int>(_ => 5)), Is.EqualTo(5));
        Assert.That(executed, Is.True);
        Assert.That(received, Is.EqualTo(new[] { 1 }));
    }

    /// <summary>测试基类 System 的受保护能力可用，释放后失效。</summary>
    [Test]
    public void BaseClassSystemUsesProtectedCapabilitiesAndLosesThemAfterRelease()
    {
        var model = new ProbeModel();
        var received = new List<int>();
        var system = new DerivedSystem(self => self.Subscribe<Ping>(ping => received.Add(ping.Value)));
        var domain = ProbeDomain.Create(configure: value =>
        {
            value.AddModel(model);
            value.AddSystem(system);
        });

        system.Publish(new Ping(7));

        Assert.That(system.ReadModel<IPlayerModel>(), Is.SameAs(model));
        Assert.That(system.ReadSystem<IPlayerSystem>(), Is.SameAs(system));
        Assert.That(received, Is.EqualTo(new[] { 7 }));

        domain.Dispose();

        Assert.Throws<InvalidOperationException>(() => system.ReadModel<IPlayerModel>());
    }

    /// <summary>测试基类 Model 的能力在释放后失效。</summary>
    [Test]
    public void BaseClassModelLosesCapabilitiesAfterRelease()
    {
        var utility = new ClockUtility();
        var model = new DerivedModel();
        var domain = ProbeDomain.Create(configure: value =>
        {
            value.AddModel(model);
            value.AddUtility(utility);
        });

        Assert.That(model.ReadUtility<ClockUtility>(), Is.SameAs(utility));

        domain.Dispose();

        Assert.Throws<InvalidOperationException>(() => model.ReadUtility<ClockUtility>());
        Assert.Throws<InvalidOperationException>(() => model.Publish(new Ping(0)));
    }

    /// <summary>测试命令上下文支持无参的命令和事件便利方法。</summary>
    [Test]
    public void CommandContextSupportsParameterlessCommandsAndEvents()
    {
        using var domain = ProbeDomain.Create();
        var received = 0;
        domain.RegisterEvent<Ping>(_ => received++);

        domain.SendCommand(new DelegateCommand(context =>
        {
            context.SendEvent<Ping>();
            context.SendCommand<EmptyCommand>();
        }));

        Assert.That(received, Is.EqualTo(1));
    }

    /// <summary>测试事件只在本 Domain 内按注册顺序分发。</summary>
    [Test]
    public void EventsAreLocalAndRunInRegistrationOrder()
    {
        using var parent = ProbeDomain.Create();
        var child = ProbeDomain.Create();
        parent.AddChild(child);
        var order = new List<string>();
        parent.RegisterEvent<Ping>(_ => order.Add("parent"));
        child.RegisterEvent<Ping>(_ => order.Add("first"));
        child.RegisterEvent<Ping>(_ => order.Add("second"));

        child.SendEvent(new Ping(0));

        Assert.That(order, Is.EqualTo(new[] { "first", "second" }));
    }

    /// <summary>测试分发中新注册的处理器从下一次发送开始生效。</summary>
    [Test]
    public void HandlerRegisteredDuringSendRunsOnlyOnNextSend()
    {
        using var domain = ProbeDomain.Create();
        var calls = new List<string>();
        domain.RegisterEvent<Ping>(_ =>
        {
            calls.Add("outer");
            if (calls.Count == 1) domain.RegisterEvent<Ping>(_ => calls.Add("inner"));
        });

        domain.SendEvent(new Ping(0));
        domain.SendEvent(new Ping(1));

        Assert.That(calls, Is.EqualTo(new[] { "outer", "outer", "inner" }));
    }

    /// <summary>测试分发中被取消的处理器本轮不再调用。</summary>
    [Test]
    public void HandlerUnregisteredDuringSendIsSkippedInThatSend()
    {
        using var domain = ProbeDomain.Create();
        var calls = new List<string>();
        IUnRegister? later = null;
        domain.RegisterEvent<Ping>(_ =>
        {
            calls.Add("first");
            later!.UnRegister();
        });
        later = domain.RegisterEvent<Ping>(_ => calls.Add("later"));

        domain.SendEvent(new Ping(0));

        Assert.That(calls, Is.EqualTo(new[] { "first" }));
    }

    /// <summary>测试分发中释放 Domain 后剩余处理器不再调用。</summary>
    [Test]
    public void DisposingDomainDuringSendStopsRemainingHandlers()
    {
        var domain = ProbeDomain.Create();
        var calls = new List<string>();
        domain.RegisterEvent<Ping>(_ =>
        {
            calls.Add("first");
            domain.Dispose();
        });
        domain.RegisterEvent<Ping>(_ => calls.Add("second"));

        Assert.DoesNotThrow(() => domain.SendEvent(new Ping(0)));
        Assert.That(calls, Is.EqualTo(new[] { "first" }));
    }

    /// <summary>测试处理器抛异常时停止后续处理器并原样抛出。</summary>
    [Test]
    public void EventFailureIsFailFast()
    {
        using var domain = ProbeDomain.Create();
        var failure = new ApplicationException("handler");
        var calls = 0;
        domain.RegisterEvent<Ping>(_ => throw failure);
        domain.RegisterEvent<Ping>(_ => calls++);

        Assert.That(Assert.Throws<ApplicationException>(() => domain.SendEvent(new Ping(0))), Is.SameAs(failure));
        Assert.That(calls, Is.Zero);
    }

    /// <summary>测试取消注册句柄幂等，Domain 释放后调用也无害。</summary>
    [Test]
    public void EventTokensAreIdempotentAndHarmlessAfterDisposal()
    {
        var domain = ProbeDomain.Create();
        var calls = 0;
        var token = domain.RegisterEvent<Ping>(_ => calls++);

        token.UnRegister();
        token.UnRegister();
        domain.SendEvent(new Ping(0));
        var lateToken = domain.RegisterEvent<Ping>(_ => calls++);
        domain.Dispose();

        Assert.That(calls, Is.Zero);
        Assert.DoesNotThrow(() => lateToken.UnRegister());
    }

    /// <summary>测试同一委托注册两次时句柄只取消自己那一次。</summary>
    [Test]
    public void EventTokenOnlyRemovesItsOwnDuplicateDelegateRegistration()
    {
        using var domain = ProbeDomain.Create();
        var calls = 0;
        void Handler(Ping _) => calls++;
        var first = domain.RegisterEvent<Ping>(Handler);
        domain.RegisterEvent<Ping>(Handler);

        first.UnRegister();
        domain.SendEvent(new Ping(0));

        Assert.That(calls, Is.EqualTo(1));
    }

    /// <summary>测试已释放的 Domain 对各种操作抛出 ObjectDisposedException。</summary>
    [Test]
    public void DisposedDomainRejectsOperationsWithObjectDisposedException()
    {
        var domain = ProbeDomain.Create();
        var other = ProbeDomain.Create();
        domain.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = domain.Parent);
        Assert.Throws<ObjectDisposedException>(() => domain.GetModel<IPlayerModel>());
        Assert.Throws<ObjectDisposedException>(() => domain.SendEvent(new Ping(0)));
        Assert.Throws<ObjectDisposedException>(() => domain.RegisterEvent<Ping>(_ => { }));
        Assert.Throws<ObjectDisposedException>(() => domain.SendCommand(new EmptyCommand()));
        Assert.Throws<ObjectDisposedException>(() => domain.AddChild(other));
        Assert.Throws<ObjectDisposedException>(() => other.AddChild(domain));
        other.Dispose();
    }

    /// <summary>测试查找、发送事件、命令与查询在热身后不分配内存。</summary>
    [Test]
    public void HotPathsHaveNoPerCallAllocationAfterWarmup()
    {
        using var domain = ProbeDomain.Create(configure: value =>
        {
            value.AddUtility<IClockUtility>(new ClockUtility());
            value.AddModel(new ProbeModel());
        });
        var command = new EmptyCommand();
        var query = new DelegateQuery<int>(_ => 1);
        domain.RegisterEvent<Ping>(_ => { });
        _ = domain.GetUtility<IClockUtility>();
        _ = domain.GetModel<IPlayerModel>();
        domain.SendEvent(new Ping(0));
        domain.SendCommand(command);
        _ = domain.SendQuery(query);

        // GetModel<IPlayerModel> 走的是可赋值扫描（注册键是 ProbeModel），GetUtility 走精确键。
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1_000; index++)
        {
            _ = domain.GetUtility<IClockUtility>();
            _ = domain.GetModel<IPlayerModel>();
            domain.SendEvent(new Ping(index));
            domain.SendCommand(command);
            _ = domain.SendQuery(query);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.Zero);
    }

    /// <summary>测试单例在激活完成后才发布。</summary>
    [Test]
    public void SingletonPublishesOnlyAfterActivation()
    {
        StrictProbeDomain? observed = null;
        StrictProbeDomain.SetHooks(activated: _ => observed = StrictProbeDomain.GetInstance());

        var instance = StrictProbeDomain.Instance;

        Assert.That(observed, Is.Null);
        Assert.That(StrictProbeDomain.GetInstance(), Is.SameAs(instance));
        Assert.That(StrictProbeDomain.Instance, Is.SameAs(instance));
    }

    /// <summary>测试单例拒绝创建期间重入，失败后可以重试。</summary>
    [Test]
    public void SingletonRejectsCreationReentrancyAndCanRetryAfterFailure()
    {
        StrictProbeDomain.SetHooks(configure: _ => _ = StrictProbeDomain.Instance);

        Assert.Throws<InvalidOperationException>(() => _ = StrictProbeDomain.Instance);
        Assert.That(StrictProbeDomain.GetInstance(), Is.Null);

        StrictProbeDomain.SetHooks();

        Assert.That(StrictProbeDomain.Instance, Is.Not.Null);
    }

    /// <summary>测试创建期间销毁单例被拒绝，单例不存在时销毁不会创建。</summary>
    [Test]
    public void SingletonDestroyDuringCreationIsRejectedAndAbsentDestroyDoesNotCreate()
    {
        StrictProbeDomain.SetHooks(configure: _ => StrictProbeDomain.DestroyInstance());

        Assert.Throws<InvalidOperationException>(() => _ = StrictProbeDomain.Instance);

        StrictProbeDomain.SetHooks();
        StrictProbeDomain.DestroyInstance();

        Assert.That(StrictProbeDomain.GetInstance(), Is.Null);
    }

    /// <summary>测试直接释放单例会清空静态引用。</summary>
    [Test]
    public void DirectSingletonDisposeClearsPublishedReference()
    {
        var first = StrictProbeDomain.Instance;

        first.Dispose();

        Assert.That(StrictProbeDomain.GetInstance(), Is.Null);
        Assert.That(StrictProbeDomain.Instance, Is.Not.SameAs(first));
    }

    /// <summary>测试 OnActivated 释放自身时创建失败。</summary>
    [Test]
    public void OnActivatedCannotReturnDisposedCandidate()
    {
        ProbeDomain? candidate = null;

        Assert.Throws<InvalidOperationException>(() => ProbeDomain.Create(activated: value =>
        {
            candidate = value;
            value.Dispose();
        }));
        Assert.Throws<ObjectDisposedException>(() => candidate!.GetModel<IPlayerModel>());
    }
}
