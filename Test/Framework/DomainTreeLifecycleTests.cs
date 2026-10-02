using NUnit.Framework;
using SimpleFramework;

namespace Test.Framework;

/// <summary>验证 Domain 树结构、执行期间的结构变更和释放顺序。</summary>
[TestFixture]
public sealed class DomainTreeLifecycleTests
{
    /// <summary>测试挂载和移除维护父子关系且不触发生命周期回调。</summary>
    [Test]
    public void AddAndRemoveMaintainRelationWithoutLifecycleCallbacks()
    {
        var model = new ProbeModel();
        using var parent = ProbeDomain.Create();
        using var child = ProbeDomain.Create(configure: value => value.AddModel(model));

        parent.AddChild(child);

        Assert.That(child.Parent, Is.SameAs(parent));

        parent.RemoveChild(child);

        Assert.That(child.Parent, Is.Null);
        Assert.That(child.GetModel<IPlayerModel>(), Is.SameAs(model));
        Assert.That(model.InitializeCount, Is.EqualTo(1));
        Assert.That(model.ReleaseCount, Is.Zero);
    }

    /// <summary>测试创建时指定父域，子域初始化期间即可回退查找父域组件，启动后自动挂载。</summary>
    [Test]
    public void ChildCreatedWithParentResolvesParentDuringInitialization()
    {
        var model = new ProbeModel();
        using var parent = ProbeDomain.Create(configure: value => value.AddModel(model));
        IPlayerModel? observed = null;

        var child = ProbeDomain.Create(
            configure: value => value.AddSystem(new DerivedSystem(self => observed = self.ReadModel<IPlayerModel>())),
            parent: parent);

        Assert.That(observed, Is.SameAs(model));
        Assert.That(child.Parent, Is.SameAs(parent));
        Assert.DoesNotThrow(() => parent.RemoveChild(child));
        child.Dispose();
    }

    /// <summary>测试父域仍在启动时不能作为新子域的父域。</summary>
    [Test]
    public void ParentStillStartingIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => ProbeDomain.Create(configure: value =>
            ProbeDomain.Create("child", parent: value)));
    }

    /// <summary>测试指定父域的子域启动失败时，父域的子域列表不受影响。</summary>
    [Test]
    public void ChildStartupFailureLeavesParentUnchanged()
    {
        var parentModel = new ProbeModel();
        using var parent = ProbeDomain.Create(configure: value => value.AddModel(parentModel));
        ProbeDomain? candidate = null;

        Assert.Throws<ApplicationException>(() => ProbeDomain.Create(
            configure: value =>
            {
                candidate = value;
                throw new ApplicationException("child");
            },
            parent: parent));

        Assert.Throws<ObjectDisposedException>(() => _ = candidate!.Parent);
        Assert.Throws<InvalidOperationException>(() => parent.RemoveChild(candidate!));
        Assert.That(parentModel.ReleaseCount, Is.Zero);
    }

    /// <summary>测试子域初始化期间释放了父域时，子域启动失败而不会挂到已释放的父域上。</summary>
    [Test]
    public void ChildStartupFailsWhenParentIsDisposedDuringInitialization()
    {
        var parent = ProbeDomain.Create();

        Assert.Throws<ObjectDisposedException>(() => ProbeDomain.Create(
            configure: value => value.AddSystem(new DerivedSystem(_ => parent.Dispose())),
            parent: parent));
    }

    /// <summary>测试带父域创建的子域在 OnActivated 抛异常时，会从父域的子域列表中移除。</summary>
    [Test]
    public void ChildWithParentFailingInOnActivatedIsRemovedFromParent()
    {
        using var parent = ProbeDomain.Create();
        ProbeDomain? candidate = null;

        Assert.Throws<ApplicationException>(() => ProbeDomain.Create(
            activated: value =>
            {
                candidate = value;
                throw new ApplicationException("activated");
            },
            parent: parent));

        Assert.Throws<InvalidOperationException>(() => parent.RemoveChild(candidate!));
    }

    /// <summary>测试子域在 OnActivated 中释放父域时，创建失败并说明可能是父域被释放。</summary>
    [Test]
    public void ParentDisposedDuringChildOnActivatedFailsCreation()
    {
        var parent = ProbeDomain.Create();

        var error = Assert.Throws<InvalidOperationException>(() => ProbeDomain.Create(
            activated: _ => parent.Dispose(),
            parent: parent));

        Assert.That(error!.Message, Does.Contain("父域"));
    }

    /// <summary>测试子域初始化期间父域被释放后，回退查找报告父域已释放，而不是找不到组件。</summary>
    [Test]
    public void LookupThroughDisposedParentReportsParentDisposed()
    {
        var parent = ProbeDomain.Create(configure: value => value.AddModel(new ProbeModel()));
        string? message = null;

        Assert.Throws<ObjectDisposedException>(() => ProbeDomain.Create(
            configure: value => value.AddSystem(new DerivedSystem(self =>
            {
                parent.Dispose();
                try { self.ReadModel<IPlayerModel>(); }
                catch (ObjectDisposedException error) { message = error.Message; }
            })),
            parent: parent));

        Assert.That(message, Does.Contain("父 Domain 已释放"));
    }

    /// <summary>测试非法挂载被拒绝且两棵树都保持不变。</summary>
    [Test]
    public void InvalidAttachmentLeavesBothTreesUnchanged()
    {
        using var root = ProbeDomain.Create("root");
        var middle = ProbeDomain.Create("middle");
        var other = ProbeDomain.Create("other");
        root.AddChild(middle);

        Assert.Throws<InvalidOperationException>(() => root.AddChild(root));
        Assert.Throws<InvalidOperationException>(() => other.AddChild(middle));
        Assert.Throws<InvalidOperationException>(() => middle.AddChild(root));
        Assert.Throws<ArgumentNullException>(() => root.AddChild(null!));

        Assert.That(middle.Parent, Is.SameAs(root));
        Assert.That(root.Parent, Is.Null);
        Assert.That(other.Parent, Is.Null);
        other.Dispose();
    }

    /// <summary>测试只能移除直接子域。</summary>
    [Test]
    public void RemoveRejectsNonDirectChild()
    {
        using var root = ProbeDomain.Create();
        var child = ProbeDomain.Create();
        var grandchild = ProbeDomain.Create();
        root.AddChild(child);
        child.AddChild(grandchild);

        Assert.Throws<InvalidOperationException>(() => root.RemoveChild(grandchild));
        Assert.That(grandchild.Parent, Is.SameAs(child));
    }

    /// <summary>测试移动子树后查找回退到新父域。</summary>
    [Test]
    public void MoveChangesFutureParentFallback()
    {
        var first = new ProbeModel();
        var second = new ProbeModel();
        using var oldParent = ProbeDomain.Create(configure: value => value.AddModel(first));
        using var newParent = ProbeDomain.Create(configure: value => value.AddModel(second));
        var child = ProbeDomain.Create();
        oldParent.AddChild(child);

        oldParent.RemoveChild(child);
        newParent.AddChild(child);

        Assert.That(child.GetModel<IPlayerModel>(), Is.SameAs(second));
    }

    /// <summary>测试命令中可以移除、释放和挂载子域。</summary>
    [Test]
    public void CommandCanSwitchChildDomains()
    {
        using var root = ProbeDomain.Create();
        var menu = ProbeDomain.Create("menu");
        root.AddChild(menu);
        ProbeDomain? game = null;

        root.SendCommand(new DelegateCommand(_ =>
        {
            root.RemoveChild(menu);
            menu.Dispose();
            game = ProbeDomain.Create("game");
            root.AddChild(game);
        }));

        Assert.Throws<ObjectDisposedException>(() => _ = menu.Parent);
        Assert.That(game!.Parent, Is.SameAs(root));
    }

    /// <summary>测试事件处理中可以释放已挂载的子域。</summary>
    [Test]
    public void EventHandlerCanDisposeAttachedChild()
    {
        using var root = ProbeDomain.Create();
        var battle = ProbeDomain.Create("battle");
        root.AddChild(battle);
        root.RegisterEvent<Ping>(_ => battle.Dispose());

        Assert.DoesNotThrow(() => root.SendEvent(new Ping(0)));
        Assert.Throws<ObjectDisposedException>(() => _ = battle.Parent);
        Assert.Throws<InvalidOperationException>(() => root.RemoveChild(battle));
    }

    /// <summary>测试命令释放自身 Domain 后继续使用上下文会抛 ObjectDisposedException。</summary>
    [Test]
    public void CommandUsingItsDomainAfterDisposingItThrowsObjectDisposed()
    {
        var domain = ProbeDomain.Create(configure: value => value.AddModel(new ProbeModel()));

        Assert.Throws<ObjectDisposedException>(() => domain.SendCommand(new DelegateCommand(context =>
        {
            domain.Dispose();
            context.GetModel<IPlayerModel>();
        })));
    }

    /// <summary>测试释放顺序为子树逆序后序、再 System、再 Model。</summary>
    [Test]
    public void DisposeUsesReverseAttachmentPostorderThenSystemsThenModels()
    {
        var order = new List<string>();
        var parent = ProbeDomain.Create("parent", configure: value =>
        {
            value.AddModel(new ProbeModel(release: () => order.Add("parent-model")));
            value.AddSystem(new ProbeSystem(release: () => order.Add("parent-system")));
        });
        var first = ProbeDomain.Create("first", deactivating: _ => order.Add("first"));
        var second = ProbeDomain.Create("second", deactivating: _ => order.Add("second"));
        var grandchild = ProbeDomain.Create("grandchild", deactivating: _ => order.Add("grandchild"));
        parent.AddChild(first);
        parent.AddChild(second);
        first.AddChild(grandchild);

        parent.Dispose();

        Assert.That(order, Is.EqualTo(new[] { "second", "grandchild", "first", "parent-system", "parent-model" }));
    }

    /// <summary>测试直接释放子域会把它从父域移除。</summary>
    [Test]
    public void DisposingAttachedChildRemovesItFromParent()
    {
        using var parent = ProbeDomain.Create();
        var child = ProbeDomain.Create();
        var other = ProbeDomain.Create();
        parent.AddChild(child);

        child.Dispose();

        Assert.Throws<InvalidOperationException>(() => parent.RemoveChild(child));
        Assert.DoesNotThrow(() => parent.AddChild(other));
    }

    /// <summary>测试正在释放的子域不能被提前从父域移除，其释放回调仍可回退查找父域组件。</summary>
    [Test]
    public void DisposingChildCannotBeRemovedFromParent()
    {
        using var parent = ProbeDomain.Create(configure: value => value.AddModel(new ProbeModel()));
        var resolvedInRelease = false;
        var child = ProbeDomain.Create(
            configure: value => value.AddSystem(new DerivedSystem(release: self =>
                resolvedInRelease = self.ReadModel<IPlayerModel>() is not null)),
            deactivating: self => parent.RemoveChild(self),
            parent: parent);

        Assert.Throws<InvalidOperationException>(() => child.Dispose());
        Assert.That(resolvedInRelease, Is.True);
        Assert.Throws<InvalidOperationException>(() => parent.RemoveChild(child));
    }

    /// <summary>测试释放回调中仍可读取组件。</summary>
    [Test]
    public void ReleaseCallbacksCanStillReadComponents()
    {
        var utility = new ClockUtility { Value = 9 };
        var saved = 0;
        var model = new ProbeModel();
        var system = new DerivedSystem(release: self =>
        {
            Assert.That(self.ReadModel<IPlayerModel>(), Is.SameAs(model));
            saved = self.ReadUtility<IClockUtility>().Value;
        });
        var domain = ProbeDomain.Create(configure: value =>
        {
            value.AddModel(model);
            value.AddSystem(system);
            value.AddUtility<IClockUtility>(utility);
        });

        domain.Dispose();

        Assert.That(saved, Is.EqualTo(9));
    }

    /// <summary>测试 OnDeactivating 中所有组件仍可使用，后注册 System 的能力在此可正常调用。</summary>
    [Test]
    public void OnDeactivatingCanUseEveryComponentBeforeRelease()
    {
        var model = new ProbeModel();
        var later = new DerivedSystem();
        IPlayerModel? observed = null;
        var domain = ProbeDomain.Create(
            configure: value =>
            {
                value.AddModel(model);
                value.AddSystem<IAltPlayerSystem>(new AltDerivedSystem());
                value.AddSystem(later);
            },
            deactivating: value => observed = value.GetSystem<DerivedSystem>().ReadModel<IPlayerModel>());

        domain.Dispose();

        Assert.That(observed, Is.SameAs(model));
    }

    /// <summary>测试 System 在 OnRelease 中调用已释放的后注册 System 会抛出说明可能已释放的异常。</summary>
    [Test]
    public void CallingLaterRegisteredSystemFromOnReleaseThrows()
    {
        var later = new DerivedSystem();
        var earlier = new AltReleaseCallerSystem(() => later.ReadModel<IPlayerModel>());
        var domain = ProbeDomain.Create(configure: value =>
        {
            value.AddModel(new ProbeModel());
            value.AddSystem<IAltPlayerSystem>(earlier);
            value.AddSystem(later);
        });

        var error = Assert.Throws<InvalidOperationException>(() => domain.Dispose());

        Assert.That(error!.Message, Does.Contain("已经释放"));
    }

    /// <summary>测试释放期间发送事件不调用处理器，订阅事件被拒绝。</summary>
    [Test]
    public void EventsDuringReleaseReachNoHandlerAndCannotSubscribe()
    {
        var calls = 0;
        var domain = ProbeDomain.Create(
            configure: value => value.AddSystem(new DerivedSystem(
                initialize: self => self.Subscribe<Ping>(_ => calls++),
                release: self =>
                {
                    self.Publish(new Ping(0));
                    Assert.Throws<InvalidOperationException>(() => self.Subscribe<Ping>(_ => { }));
                })));

        domain.Dispose();

        Assert.That(calls, Is.Zero);
    }

    /// <summary>测试释放期间修改树结构被拒绝。</summary>
    [Test]
    public void TreeMutationDuringDisposalIsRejected()
    {
        var other = ProbeDomain.Create();
        var parent = ProbeDomain.Create(deactivating: value => value.AddChild(other));

        Assert.Throws<InvalidOperationException>(() => parent.Dispose());
        Assert.That(other.Parent, Is.Null);
        other.Dispose();
    }

    /// <summary>测试子域释放中再次释放父域是无操作，且子域先于父域完成。</summary>
    [Test]
    public void ReentrantParentDisposeDuringChildDisposalIsNoOp()
    {
        var order = new List<string>();
        ProbeDomain? parent = null;
        var child = ProbeDomain.Create("child", deactivating: _ =>
        {
            order.Add("child");
            parent!.Dispose();
        });
        parent = ProbeDomain.Create("parent", deactivating: _ => order.Add("parent"));
        parent.AddChild(child);

        parent.Dispose();

        Assert.That(order, Is.EqualTo(new[] { "child", "parent" }));
        Assert.Throws<ObjectDisposedException>(() => _ = parent.Parent);
    }

    /// <summary>测试子域释放回调向正在释放的父域发事件不会调用处理器，订阅会被拒绝。</summary>
    [Test]
    public void ChildReleaseCallbackCannotUseDisposingParentEvents()
    {
        var calls = 0;
        ProbeDomain? parent = null;
        var child = ProbeDomain.Create(deactivating: _ =>
        {
            parent!.SendEvent(new Ping(0));
            Assert.Throws<InvalidOperationException>(() => parent.RegisterEvent<Ping>(_ => calls++));
        });
        parent = ProbeDomain.Create();
        parent.RegisterEvent<Ping>(_ => calls++);
        parent.AddChild(child);

        parent.Dispose();

        Assert.That(calls, Is.Zero);
    }

    /// <summary>测试子域释放中修改父域的子域列表被拒绝。</summary>
    [Test]
    public void ParentTreeMutationDuringChildDisposalIsRejected()
    {
        ProbeDomain? parent = null;
        var kept = ProbeDomain.Create("kept");
        var trigger = ProbeDomain.Create("trigger", deactivating: _ => parent!.RemoveChild(kept));
        parent = ProbeDomain.Create("parent");
        parent.AddChild(kept);
        parent.AddChild(trigger);

        Assert.Throws<InvalidOperationException>(() => parent.Dispose());
        Assert.Throws<ObjectDisposedException>(() => _ = kept.Parent);
    }

    /// <summary>测试组件释放回调中重入 Dispose 是无操作。</summary>
    [Test]
    public void ReentrantDisposeFromReleaseIsNoOp()
    {
        ProbeDomain? domain = null;
        var model = new ProbeModel(release: () => domain!.Dispose());
        domain = ProbeDomain.Create(configure: value => value.AddModel(model));

        Assert.DoesNotThrow(() => domain.Dispose());
        Assert.That(model.ReleaseCount, Is.EqualTo(1));
    }

    /// <summary>测试在 Configure 中释放会导致启动失败。</summary>
    [Test]
    public void DisposeDuringConfigureFailsStartup()
    {
        Assert.Throws<InvalidOperationException>(() => ProbeDomain.Create(configure: value => value.Dispose()));
    }

    /// <summary>测试释放步骤全部执行，多个失败按顺序汇总。</summary>
    [Test]
    public void CleanupIsExhaustiveAndFlattensFailuresInOccurrenceOrder()
    {
        var released = new List<string>();
        var parent = ProbeDomain.Create(configure: value =>
        {
            value.AddModel(new ProbeModel(release: () =>
            {
                released.Add("model");
                throw new ApplicationException("model");
            }));
            value.AddSystem(new ProbeSystem(release: () =>
            {
                released.Add("system");
                throw new ApplicationException("system");
            }));
        }, deactivating: _ => throw new ApplicationException("parent-hook"));
        var child = ProbeDomain.Create(deactivating: _ => throw new ApplicationException("child-hook"));
        parent.AddChild(child);

        var aggregate = Assert.Throws<AggregateException>(() => parent.Dispose());

        Assert.That(aggregate!.InnerExceptions.Select(error => error.Message),
            Is.EqualTo(new[] { "child-hook", "parent-hook", "system", "model" }));
        Assert.That(released, Is.EqualTo(new[] { "system", "model" }));
        Assert.Throws<ObjectDisposedException>(() => parent.TryGetModel<IPlayerModel>(out _));
        Assert.Throws<ObjectDisposedException>(() => child.TryGetModel<IPlayerModel>(out _));
    }

    /// <summary>测试重复 Dispose 无效果。</summary>
    [Test]
    public void DisposeIsIdempotent()
    {
        var model = new ProbeModel();
        var domain = ProbeDomain.Create(configure: value => value.AddModel(model));

        domain.Dispose();
        domain.Dispose();

        Assert.That(model.ReleaseCount, Is.EqualTo(1));
    }

    /// <summary>测试树操作对 Equals 相等的 Domain 也按引用区分。</summary>
    [Test]
    public void TreeOperationsUseReferenceIdentityForEqualDomains()
    {
        using var parent = ProbeDomain.Create();
        var first = EqualByNameDomain.Create();
        var second = EqualByNameDomain.Create();
        parent.AddChild(first);
        parent.AddChild(second);

        second.Dispose();
        parent.RemoveChild(first);

        Assert.That(first.Parent, Is.Null);
        first.Dispose();
    }
}
