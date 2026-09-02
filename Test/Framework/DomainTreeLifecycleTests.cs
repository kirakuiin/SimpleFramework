using NUnit.Framework;
using SimpleFramework;

namespace Test.Framework;

/// <summary>验证动态 Domain 树、释放顺序和失败后结构一致性。</summary>
[TestFixture]
public sealed class DomainTreeLifecycleTests
{
    [Test]
    public void AddAndRemoveMaintainStrongBidirectionalRelationWithoutLifecycleCallbacks()
    {
        var model = new ProbeModel();
        using var parent = ProbeDomain.Create();
        var child = ProbeDomain.Create(configure: value => value.RegisterModel(model));
        parent.AddChild(child);
        Assert.That(child.Parent, Is.SameAs(parent));
        Assert.That(model.InitializeCount, Is.EqualTo(1));

        parent.RemoveChild(child);
        Assert.That(child.Parent, Is.Null);
        Assert.That(child.GetModel<ProbeModel>(), Is.SameAs(model));
        Assert.That(model.ReleaseCount, Is.Zero);
        child.Dispose();
    }

    [Test]
    public void InvalidAttachmentLeavesBothTreesUnchanged()
    {
        using var parent = ProbeDomain.Create("parent");
        using var child = ProbeDomain.Create("child");
        parent.AddChild(child);
        Assert.Throws<InvalidOperationException>(() => parent.AddChild(child));
        Assert.Throws<InvalidOperationException>(() => child.AddChild(parent));
        Assert.That(child.Parent, Is.SameAs(parent));
    }

    [Test]
    public void RemoveRejectsNonDirectChild()
    {
        using var parent = ProbeDomain.Create();
        using var stranger = ProbeDomain.Create();
        Assert.Throws<InvalidOperationException>(() => parent.RemoveChild(stranger));
        Assert.That(stranger.Parent, Is.Null);
    }

    [Test]
    public void MoveChangesFutureParentFallbackOnly()
    {
        var firstModel = new ProbeModel();
        var secondModel = new AlternateModel();
        using var first = ProbeDomain.Create(configure: value => value.RegisterModel(firstModel));
        using var second = ProbeDomain.Create(configure: value => value.RegisterModel(secondModel));
        using var child = ProbeDomain.Create();
        first.AddChild(child);
        var cached = child.GetModel<IPlayerModel>();
        first.RemoveChild(child);
        second.AddChild(child);
        Assert.That(cached, Is.SameAs(firstModel));
        Assert.That(child.GetModel<IPlayerModel>(), Is.SameAs(secondModel));
    }

    [Test]
    public void TreeMutationAndDisposalAreRejectedDuringExecutionAcrossWholeTree()
    {
        using var root = ProbeDomain.Create();
        using var child = ProbeDomain.Create();
        using var candidate = ProbeDomain.Create();
        root.AddChild(child);
        child.SendCommand(new DelegateCommand(_ =>
        {
            Assert.Throws<InvalidOperationException>(() => root.AddChild(candidate));
            Assert.Throws<InvalidOperationException>(() => root.RegisterUtility(new ClockUtility()));
            Assert.Throws<InvalidOperationException>(() => root.Dispose());
        }));
        root.AddChild(candidate);
        Assert.That(candidate.Parent, Is.SameAs(root));
    }

    [Test]
    public void DisposeUsesReverseAttachmentPostorderThenSystemsThenModels()
    {
        var order = new List<string>();
        var parent = ProbeDomain.Create("parent", configure: value =>
        {
            value.RegisterModel(new ProbeModel(release: () => order.Add("parent-model")));
            value.RegisterSystem(new ProbeSystem(release: () => order.Add("parent-system")));
        }, deactivating: _ => order.Add("parent-hook"));
        var first = ProbeDomain.Create("first", deactivating: _ => order.Add("first"));
        var second = ProbeDomain.Create("second", deactivating: _ => order.Add("second"));
        var grandchild = ProbeDomain.Create("grandchild", deactivating: _ => order.Add("grandchild"));
        first.AddChild(grandchild);
        parent.AddChild(first);
        parent.AddChild(second);

        parent.Dispose();
        Assert.That(order, Is.EqualTo(new[]
        {
            "second", "grandchild", "first", "parent-hook", "parent-system", "parent-model"
        }));
        Assert.Throws<ObjectDisposedException>(() => grandchild.TryGetModel<IPlayerModel>(out _));
    }

    [Test]
    public void DisposeSelfOnlyPreservesChildrenAsUsableRoots()
    {
        var childModel = new ProbeModel();
        var parent = ProbeDomain.Create();
        var first = ProbeDomain.Create(configure: value => value.RegisterModel(childModel));
        var second = ProbeDomain.Create();
        parent.AddChild(first);
        parent.AddChild(second);

        parent.DisposeSelfOnly();
        Assert.That(first.Parent, Is.Null);
        Assert.That(second.Parent, Is.Null);
        Assert.That(first.GetModel<ProbeModel>(), Is.SameAs(childModel));
        Assert.That(childModel.ReleaseCount, Is.Zero);
        first.Dispose();
        second.Dispose();
    }

    [Test]
    public void DisposeSelfOnlyUnlocksChildrenEvenWhenParentCleanupFails()
    {
        var parent = ProbeDomain.Create(deactivating: _ => throw new ApplicationException("parent"));
        var child = ProbeDomain.Create();
        parent.AddChild(child);
        var error = Assert.Throws<ApplicationException>(() => parent.DisposeSelfOnly());
        Assert.That(error!.Message, Is.EqualTo("parent"));
        Assert.That(child.Parent, Is.Null);
        child.RegisterUtility(new ClockUtility());
        child.Dispose();
    }

    [Test]
    public void PreservedChildrenStayTransitionLockedDuringSelfOnlyCleanup()
    {
        ProbeDomain? child = null;
        var blocked = false;
        var parent = ProbeDomain.Create(deactivating: _ =>
        {
            blocked = Assert.Throws<InvalidOperationException>(() => child!.RegisterUtility(new ClockUtility())) is not null;
        });
        child = ProbeDomain.Create();
        parent.AddChild(child);
        parent.DisposeSelfOnly();
        Assert.That(blocked, Is.True);
        child.RegisterUtility(new ClockUtility());
        child.Dispose();
    }

    [Test]
    public void CleanupIsExhaustiveAndFlattensFailuresInOccurrenceOrder()
    {
        var released = new List<string>();
        var parent = ProbeDomain.Create(configure: value =>
        {
            value.RegisterModel(new ProbeModel(release: () =>
            {
                released.Add("model");
                throw new ApplicationException("model");
            }));
            value.RegisterSystem(new ProbeSystem(release: () =>
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

    [Test]
    public void AllSavedContextsAreInvalidBeforeOnDeactivatingAndRelease()
    {
        ProbeModel? model = null;
        ProbeSystem? system = null;
        var checks = 0;
        var domain = ProbeDomain.Create(configure: value =>
        {
            value.RegisterUtility(new ClockUtility());
            model = new ProbeModel(release: () =>
            {
                Assert.Throws<InvalidOperationException>(() => model!.SavedContext!.GetUtility<IClockUtility>());
                checks++;
            });
            system = new ProbeSystem(release: () =>
            {
                Assert.Throws<InvalidOperationException>(() => system!.SavedContext!.GetModel<IPlayerModel>());
                checks++;
            });
            value.RegisterModel(model);
            value.RegisterSystem(system);
        }, deactivating: _ =>
        {
            Assert.Throws<InvalidOperationException>(() => model!.SavedContext!.GetUtility<IClockUtility>());
            Assert.Throws<InvalidOperationException>(() => system!.SavedContext!.GetModel<IPlayerModel>());
            checks += 2;
        });
        domain.Dispose();
        Assert.That(checks, Is.EqualTo(4));
    }

    [Test]
    public void DisposeIsIdempotent()
    {
        var model = new ProbeModel();
        var domain = ProbeDomain.Create(configure: value => value.RegisterModel(model));
        domain.Dispose();
        domain.Dispose();
        Assert.That(model.ReleaseCount, Is.EqualTo(1));
    }
}
