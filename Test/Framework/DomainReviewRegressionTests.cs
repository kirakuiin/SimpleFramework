using System.Runtime.CompilerServices;
using NUnit.Framework;
using SimpleFramework;

namespace Test.Framework;

/// <summary>验证失败传播、树节点引用身份和订阅归属清理的回归场景。</summary>
[TestFixture]
public sealed class DomainReviewRegressionTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void EmptyAggregateFromCreationIsPropagatedAndCandidateIsDisposed(bool duringActivation)
    {
        var failure = new AggregateException("creation failed");
        ProbeDomain? candidate = null;
        void Fail(ProbeDomain domain)
        {
            candidate = domain;
            throw failure;
        }

        var actual = Assert.Throws<AggregateException>(() => ProbeDomain.Create(
            configure: duringActivation ? null : Fail,
            activated: duringActivation ? Fail : null));

        Assert.That(actual, Is.SameAs(failure));
        Assert.That(actual!.StackTrace, Does.Contain(nameof(Fail)));
        Assert.That(candidate, Is.Not.Null);
        Assert.Throws<ObjectDisposedException>(() => candidate!.TryGetModel<ProbeModel>(out _));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EmptyAggregateFromDynamicInitializationPreservesFailureAndExistingEntries(bool cleanupFails)
    {
        var failure = new AggregateException("initialize failed");
        var cleanupFailure = new ApplicationException("release failed");
        var existing = new AlternateModel();
        using var domain = ProbeDomain.Create(configure: value => value.RegisterModel(existing));
        var candidate = new ProbeModel(
            initialize: _ => throw failure,
            release: () => { if (cleanupFails) throw cleanupFailure; });

        var actual = Assert.Throws<AggregateException>(() => domain.RegisterModel(candidate));

        if (cleanupFails)
            Assert.That(actual!.InnerExceptions, Is.EqualTo(new Exception[] { failure, cleanupFailure }));
        else
            Assert.That(actual, Is.SameAs(failure));
        Assert.That(candidate.ReleaseCount, Is.EqualTo(1));
        Assert.That(domain.TryGetModel<ProbeModel>(out _), Is.False);
        Assert.That(domain.GetModel<AlternateModel>(), Is.SameAs(existing));
        Assert.Throws<InvalidOperationException>(() => domain.RegisterModel(candidate));
    }

    [Test]
    public void EmptyAggregateFromReleaseIsPropagatedAfterRemainingCleanup()
    {
        var failure = new AggregateException("release failed");
        var model = new ProbeModel();
        var system = new ProbeSystem(release: () => throw failure);
        var domain = ProbeDomain.Create(configure: value =>
        {
            value.RegisterModel(model);
            value.RegisterSystem(system);
        });

        Assert.That(Assert.Throws<AggregateException>(() => domain.Dispose()), Is.SameAs(failure));
        Assert.That(system.ReleaseCount, Is.EqualTo(1));
        Assert.That(model.ReleaseCount, Is.EqualTo(1));
        Assert.Throws<ObjectDisposedException>(() => domain.TryGetModel<ProbeModel>(out _));
        Assert.DoesNotThrow(() => domain.Dispose());
    }

    [Test]
    public void NestedAggregatesPreserveEmptyFailuresInOccurrenceOrder()
    {
        var empty = new AggregateException("empty failure");
        var other = new ApplicationException("other failure");
        var cleanup = new ApplicationException("cleanup failure");

        var actual = Assert.Throws<AggregateException>(() => ProbeDomain.Create(
            configure: _ => throw new AggregateException(new AggregateException(empty), other),
            deactivating: _ => throw cleanup));

        Assert.That(actual!.InnerExceptions, Is.EqualTo(new Exception[] { empty, other, cleanup }));
    }

    [Test]
    public void EqualDomainInstancesCanBeAttachedIndependently()
    {
        using var parent = ProbeDomain.Create();
        using var first = ValueEqualDomain.Create("same");
        using var second = ValueEqualDomain.Create("same");

        parent.AddChild(first);
        parent.AddChild(second);

        Assert.That(first.Parent, Is.SameAs(parent));
        Assert.That(second.Parent, Is.SameAs(parent));
        Assert.Throws<InvalidOperationException>(() => parent.AddChild(second));
        parent.Dispose();
        Assert.That(first.ReleaseCount, Is.EqualTo(1));
        Assert.That(second.ReleaseCount, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DetachAndDirectDisposeUseReferenceIdentityWhenBusinessEqualityChanges(bool disposeChild)
    {
        using var parent = ProbeDomain.Create();
        using var first = ValueEqualDomain.Create("first");
        using var second = ValueEqualDomain.Create("second");
        parent.AddChild(first);
        parent.AddChild(second);
        second.Key = first.Key;

        if (disposeChild) second.Dispose();
        else parent.RemoveChild(second);

        Assert.That(first.Parent, Is.SameAs(parent));
        if (!disposeChild) Assert.That(second.Parent, Is.Null);
        parent.Dispose();
        Assert.That(first.ReleaseCount, Is.EqualTo(1));
        Assert.That(second.ReleaseCount, Is.EqualTo(disposeChild ? 1 : 0));
    }

    [Test]
    public void CanceledSystemTokensCanBeCollectedWhileSystemAndDomainRemainActive()
    {
        var system = new ProbeSystem();
        using var domain = ProbeDomain.Create(configure: value => value.RegisterSystem(system));
        var references = CreateCanceledTokens(system.SavedContext!);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.That(references.All(reference => !reference.IsAlive), Is.True,
            "手动取消后的 token 不应继续由活跃 System 保留。");
        Assert.That(domain.GetSystem<ProbeSystem>(), Is.SameAs(system));
        GC.KeepAlive(system);
        GC.KeepAlive(domain);
    }

    [Test]
    public void CancelingSomeOwnedTokensPreservesRemainingSubscriptionsAndDisposal()
    {
        var handled = new List<int>();
        var system = new ProbeSystem();
        var domain = ProbeDomain.Create(configure: value => value.RegisterSystem(system));
        var first = system.SavedContext!.RegisterEvent<int>(_ => handled.Add(1));
        var canceled = system.SavedContext.RegisterEvent<int>(_ => handled.Add(2));
        var last = system.SavedContext.RegisterEvent<int>(_ => handled.Add(3));
        canceled.UnRegister();
        canceled.UnRegister();

        domain.SendEvent(0);
        Assert.That(handled, Is.EqualTo(new[] { 1, 3 }));
        domain.Dispose();
        Assert.That(system.ReleaseCount, Is.EqualTo(1));
        Assert.DoesNotThrow(() => first.UnRegister());
        Assert.DoesNotThrow(() => last.UnRegister());
        Assert.DoesNotThrow(() => canceled.UnRegister());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] CreateCanceledTokens(ISystemContext context)
    {
        var references = new WeakReference[1_000];
        for (var index = 0; index < references.Length; index++)
        {
            var token = context.RegisterEvent<int>(_ => { });
            references[index] = new WeakReference(token);
            token.UnRegister();
        }
        return references;
    }

    /// <summary>按可变业务键定义相等性，用于确认框架树始终按引用识别节点。</summary>
    private sealed class ValueEqualDomain : AbstractDomain
    {
        private ValueEqualDomain(string key) => Key = key;
        public string Key { get; set; }
        public int ReleaseCount { get; private set; }
        public static ValueEqualDomain Create(string key) => CreateDomain(() => new ValueEqualDomain(key));
        protected override void Configure() { }
        protected override void OnDeactivating() => ReleaseCount++;
        public override bool Equals(object? obj) => obj is ValueEqualDomain other && Key == other.Key;
        public override int GetHashCode() => Key.GetHashCode();
    }
}
