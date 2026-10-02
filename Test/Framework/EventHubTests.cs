using System.Runtime.CompilerServices;
using NUnit.Framework;
using SimpleFramework;
using SimpleFramework.FrameworkImpl;

namespace Test.Framework;

/// <summary>验证跨 Domain 事件中心、发布订阅语法糖和订阅随 System 释放取消。</summary>
[TestFixture]
public sealed class EventHubTests
{
    /// <summary>测试注册在根 Domain 的事件中心被整棵树共享，Controller 订阅、子域 Command 发布可以互通。</summary>
    [Test]
    public void HubRegisteredInRootIsSharedThroughTree()
    {
        using var hub = new EventHub();
        using var root = ProbeDomain.Create(configure: value => value.AddUtility<IEventHub>(hub));
        var received = new List<string>();
        var system = new DerivedSystem(self => self.SubscribeToHub<Ping>(_ => received.Add("system")));
        var child = ProbeDomain.Create(configure: value => value.AddSystem(system), parent: root);
        using var token = new ProbeController(root).SubscribeEvent<Ping>(_ => received.Add("controller"));

        child.SendCommand(new DelegateCommand(context => context.PublishEvent(new Ping(1))));

        Assert.That(received, Is.EqualTo(new[] { "system", "controller" }));
    }

    /// <summary>测试子域注册的事件中心会遮蔽父域的事件中心。</summary>
    [Test]
    public void ChildHubShadowsParentHub()
    {
        using var parentHub = new EventHub();
        using var childHub = new EventHub();
        using var parent = ProbeDomain.Create(configure: value => value.AddUtility<IEventHub>(parentHub));
        var child = ProbeDomain.Create(configure: value => value.AddUtility<IEventHub>(childHub), parent: parent);
        var parentCalls = 0;
        var childCalls = 0;
        parentHub.Subscribe<Ping>(_ => parentCalls++);
        childHub.Subscribe<Ping>(_ => childCalls++);

        child.SendCommand(new DelegateCommand(context => context.PublishEvent<Ping>()));

        Assert.That(parentCalls, Is.Zero);
        Assert.That(childCalls, Is.EqualTo(1));
    }

    /// <summary>测试注册过事件中心的 Domain 释放后，事件中心仍可被其他持有者使用。</summary>
    [Test]
    public void DomainDisposalDoesNotDisposeHub()
    {
        using var hub = new EventHub();
        var domain = ProbeDomain.Create(configure: value => value.AddUtility<IEventHub>(hub));
        var calls = 0;
        hub.Subscribe<Ping>(_ => calls++);

        domain.Dispose();
        hub.Publish(new Ping(0));

        Assert.That(calls, Is.EqualTo(1));
    }

    /// <summary>测试事件中心按注册顺序分发，分发中被取消的处理器本轮跳过，首个异常停止后续处理器。</summary>
    [Test]
    public void HubDispatchMatchesLocalEventSemantics()
    {
        using var hub = new EventHub();
        var calls = new List<string>();
        IUnRegister? later = null;
        hub.Subscribe<Ping>(_ =>
        {
            calls.Add("first");
            later!.UnRegister();
        });
        later = hub.Subscribe<Ping>(_ => calls.Add("later"));
        hub.Subscribe<Ping>(_ => calls.Add("last"));

        hub.Publish(new Ping(0));

        Assert.That(calls, Is.EqualTo(new[] { "first", "last" }));

        var failure = new ApplicationException("handler");
        using var failing = new EventHub();
        var after = 0;
        failing.Subscribe<Ping>(_ => throw failure);
        failing.Subscribe<Ping>(_ => after++);

        Assert.That(Assert.Throws<ApplicationException>(() => failing.Publish(new Ping(0))), Is.SameAs(failure));
        Assert.That(after, Is.Zero);
    }

    /// <summary>测试事件中心释放后订阅失效、发布与订阅抛 ObjectDisposedException，重复释放无效果。</summary>
    [Test]
    public void DisposedHubRejectsUseAndDeactivatesSubscriptions()
    {
        var hub = new EventHub();
        var calls = 0;
        var token = hub.Subscribe<Ping>(_ => calls++);

        hub.Dispose();
        hub.Dispose();

        Assert.Throws<ObjectDisposedException>(() => hub.Publish(new Ping(0)));
        Assert.Throws<ObjectDisposedException>(() => hub.Subscribe<Ping>(_ => calls++));
        Assert.DoesNotThrow(() => token.UnRegister());
        Assert.That(calls, Is.Zero);
    }

    /// <summary>测试没有注册事件中心时抛出带注册提示的 KeyNotFoundException。</summary>
    [Test]
    public void MissingHubThrowsWithRegistrationHint()
    {
        using var domain = ProbeDomain.Create();

        var error = Assert.Throws<KeyNotFoundException>(() =>
            domain.SendCommand(new DelegateCommand(context => context.PublishEvent(new Ping(0)))));

        Assert.That(error!.Message, Does.Contain(nameof(IEventHub)));
    }

    /// <summary>测试 Model 可以通过基类方法发布跨 Domain 事件。</summary>
    [Test]
    public void ModelCanPublishToHub()
    {
        using var hub = new EventHub();
        var model = new DerivedModel();
        using var domain = ProbeDomain.Create(configure: value =>
        {
            value.AddUtility<IEventHub>(hub);
            value.AddModel(model);
        });
        var calls = 0;
        hub.Subscribe<Ping>(_ => calls++);

        model.PublishToHub(new Ping(0));

        Assert.That(calls, Is.EqualTo(1));
    }

    /// <summary>测试子域释放后，其中 System 在根事件中心上的订阅自动取消。</summary>
    [Test]
    public void SystemHubSubscriptionIsCanceledWhenItsDomainIsDisposed()
    {
        using var hub = new EventHub();
        using var root = ProbeDomain.Create(configure: value => value.AddUtility<IEventHub>(hub));
        var calls = 0;
        var child = ProbeDomain.Create(
            configure: value => value.AddSystem(new DerivedSystem(self => self.SubscribeToHub<Ping>(_ => calls++))),
            parent: root);

        child.Dispose();
        hub.Publish(new Ping(0));

        Assert.That(calls, Is.Zero);
    }

    /// <summary>测试 System 在自己的 OnRelease 中不再收到已订阅的事件中心事件。</summary>
    [Test]
    public void SystemDoesNotReceiveHubEventsDuringItsOwnRelease()
    {
        using var hub = new EventHub();
        var calls = 0;
        var system = new DerivedSystem(
            initialize: self => self.SubscribeToHub<Ping>(_ => calls++),
            release: self => self.PublishToHub(new Ping(0)));
        var domain = ProbeDomain.Create(configure: value =>
        {
            value.AddUtility<IEventHub>(hub);
            value.AddSystem(system);
        });

        domain.Dispose();

        Assert.That(calls, Is.Zero);
    }

    /// <summary>测试 System 初始化失败时，初始化期间的事件中心订阅会被取消。</summary>
    [Test]
    public void FailedSystemInitializationCancelsItsHubSubscriptions()
    {
        using var hub = new EventHub();
        var calls = 0;
        var failing = new DerivedSystem(self =>
        {
            self.SubscribeToHub<Ping>(_ => calls++);
            throw new ApplicationException("initialize");
        });

        Assert.Throws<ApplicationException>(() => ProbeDomain.Create(configure: value =>
        {
            value.AddUtility<IEventHub>(hub);
            value.AddSystem(failing);
        }));
        hub.Publish(new Ping(0));

        Assert.That(calls, Is.Zero);
    }

    /// <summary>测试订阅父域 BindableProperty 后用 UnRegisterOnRelease，子域释放时订阅自动取消。</summary>
    [Test]
    public void BindablePropertySubscriptionFollowsSystemRelease()
    {
        var score = new ScoreModel();
        using var parent = ProbeDomain.Create(configure: value => value.AddModel<IScoreModel>(score));
        var calls = 0;
        var child = ProbeDomain.Create(
            configure: value => value.AddSystem(new DerivedSystem(self =>
                self.ReadModel<IScoreModel>().Score.Register((_, _) => calls++).UnRegisterOnRelease(self))),
            parent: parent);

        score.Score.Value = 1;
        child.Dispose();
        score.Score.Value = 2;

        Assert.That(calls, Is.EqualTo(1));
    }

    /// <summary>测试提前取消 SubscribeEvent 返回的句柄后不再收到事件，之后释放也不会出错。</summary>
    [Test]
    public void EarlyCancellationStopsEventsAndReleaseStillSucceeds()
    {
        using var hub = new EventHub();
        var calls = 0;
        IUnRegister? token = null;
        var system = new DerivedSystem(self => token = self.SubscribeToHub<Ping>(_ => calls++));
        var domain = ProbeDomain.Create(configure: value =>
        {
            value.AddUtility<IEventHub>(hub);
            value.AddSystem(system);
        });

        token!.UnRegister();
        hub.Publish(new Ping(0));

        Assert.That(calls, Is.Zero);
        Assert.DoesNotThrow(() => domain.Dispose());
    }

    /// <summary>测试 System 在 OnRelease 中订阅或登记订阅会被拒绝，被拒绝的订阅也会被取消，释放后不会遗留回调。</summary>
    [Test]
    public void SubscribingDuringReleaseIsRejectedWithoutLeaking()
    {
        using var hub = new EventHub();
        var calls = 0;
        var subscribeRejected = false;
        var ownRejected = false;
        var system = new DerivedSystem(release: self =>
        {
            subscribeRejected = Throws<InvalidOperationException>(() => self.SubscribeToHub<Ping>(_ => calls++));
            var token = hub.Subscribe<Ping>(_ => calls++);
            ownRejected = Throws<InvalidOperationException>(() => token.UnRegisterOnRelease(self));
        });
        var domain = ProbeDomain.Create(configure: value =>
        {
            value.AddUtility<IEventHub>(hub);
            value.AddSystem(system);
        });

        domain.Dispose();
        hub.Publish(new Ping(0));

        Assert.That(subscribeRejected, Is.True);
        Assert.That(ownRejected, Is.True);
        Assert.That(calls, Is.Zero);
    }

    /// <summary>测试取消某个句柄失败时，其余订阅仍被取消，取消失败与 OnRelease 失败一起报告。</summary>
    [Test]
    public void FailingOwnedTokenDoesNotLeakOthersAndFailuresAreAggregated()
    {
        using var hub = new EventHub();
        var calls = 0;
        var cancelFailure = new ApplicationException("cancel");
        var releaseFailure = new ApplicationException("release");
        var system = new DerivedSystem(
            initialize: self =>
            {
                self.SubscribeToHub<Ping>(_ => calls++);
                new CustomUnRegister(() => throw cancelFailure).UnRegisterOnRelease(self);
            },
            release: _ => throw releaseFailure);
        var domain = ProbeDomain.Create(configure: value =>
        {
            value.AddUtility<IEventHub>(hub);
            value.AddSystem(system);
        });

        var error = Assert.Throws<AggregateException>(() => domain.Dispose());
        hub.Publish(new Ping(0));

        Assert.That(error!.InnerExceptions, Is.EqualTo(new Exception[] { cancelFailure, releaseFailure }));
        Assert.That(calls, Is.Zero);
    }

    /// <summary>测试 System 初始化失败且取消订阅也失败时，原始初始化异常排在首位不被掩盖。</summary>
    [Test]
    public void InitializationFailureIsReportedFirstWhenCancellationAlsoFails()
    {
        var initializeFailure = new FormatException("initialize");
        var cancelFailure = new ApplicationException("cancel");
        var system = new DerivedSystem(self =>
        {
            new CustomUnRegister(() => throw cancelFailure).UnRegisterOnRelease(self);
            throw initializeFailure;
        });

        var error = Assert.Throws<AggregateException>(() => ProbeDomain.Create(configure: value => value.AddSystem(system)));

        Assert.That(error!.InnerExceptions, Is.EqualTo(new Exception[] { initializeFailure, cancelFailure }));
    }

    /// <summary>测试对未绑定 Domain 的 System 调用 UnRegisterOnRelease 会抛 InvalidOperationException。</summary>
    [Test]
    public void UnRegisterOnReleaseRequiresBoundSystem()
    {
        var system = new DerivedSystem();
        var token = new ScoreModel().Score.Register((_, _) => { });

        Assert.Throws<InvalidOperationException>(() => token.UnRegisterOnRelease(system));
    }

    /// <summary>测试通过 UnRegisterOnRelease 返回的句柄提前取消后，System 不再持有该登记项。</summary>
    [Test]
    public void EarlyCancellationThroughReturnedTokenStopsTracking()
    {
        var system = new DerivedSystem();
        using var domain = ProbeDomain.Create(configure: value => value.AddSystem(system));

        var returned = OwnAndCancelThroughReturnedToken(system);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.That(returned.IsAlive, Is.False);
        GC.KeepAlive(system);
    }

    /// <summary>
    /// 登记一个订阅并取消返回的包装句柄，只返回包装句柄的弱引用。
    /// 必须观察包装句柄而不是原句柄：包装句柄取消后会清空回调、不再引用原句柄，
    /// 即使它仍留在登记列表里，原句柄也可回收，无法证明登记项已移除。
    /// 放在独立且不内联的方法里，确保调用方栈上没有残留的强引用，弱引用的存活只取决于 System 是否仍持有它。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference OwnAndCancelThroughReturnedToken(DerivedSystem system)
    {
        var returned = new CustomUnRegister(() => { }).UnRegisterOnRelease(system);
        returned.UnRegister();
        return new WeakReference(returned);
    }

    /// <summary>测试通过事件中心发布在热身后不分配内存。</summary>
    [Test]
    public void PublishHasNoPerCallAllocationAfterWarmup()
    {
        using var hub = new EventHub();
        using var domain = ProbeDomain.Create(configure: value => value.AddUtility<IEventHub>(hub));
        hub.Subscribe<Ping>(_ => { });
        var command = new DelegateCommand(context => context.PublishEvent(new Ping(0)));
        domain.SendCommand(command);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1_000; index++)
        {
            domain.SendCommand(command);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.Zero);
    }

    /// <summary>执行动作并返回是否抛出指定异常；用于在释放回调里检查，避免断言异常被框架当作清理失败收集。</summary>
    private static bool Throws<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }

    /// <summary>带可绑定分数的测试 Model 契约。</summary>
    private interface IScoreModel : IModel
    {
        /// <summary>可订阅的分数。</summary>
        BindableProperty<int> Score { get; }
    }

    /// <summary>持有可绑定分数的测试 Model。</summary>
    private sealed class ScoreModel : AbstractModel, IScoreModel
    {
        /// <inheritdoc />
        public BindableProperty<int> Score { get; } = new(0);

        /// <inheritdoc />
        protected override void OnInitialize() { }
    }
}
