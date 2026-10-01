using NUnit.Framework;
using SimpleFramework;

namespace Test.Framework;

/// <summary>验证 Domain 创建、启动、注册和分类查找契约。</summary>
[TestFixture]
public sealed class DomainCreationRegistryTests
{
    /// <summary>测试工厂能传递构造参数，并返回可用的 Active Domain。</summary>
    [Test]
    public void FactorySupportsConstructorParametersAndReturnsActiveDomain()
    {
        using var domain = ProbeDomain.Create("parameterized");

        Assert.That(domain.Name, Is.EqualTo("parameterized"));
        Assert.That(domain.TryGetUtility<IClockUtility>(out _), Is.False);
    }

    /// <summary>测试无论注册顺序如何，Model 都先于 System 初始化。</summary>
    [Test]
    public void ModelsInitializeBeforeSystemsRegardlessOfRegistrationOrder()
    {
        var order = new List<string>();
        var model = new ProbeModel(_ => order.Add("model"));
        var system = new ProbeSystem(domain =>
        {
            order.Add("system");
            Assert.That(domain.GetModel<IPlayerModel>(), Is.SameAs(model));
        });

        using var domain = ProbeDomain.Create(configure: value =>
        {
            value.AddSystem(system);
            value.AddModel(model);
        });

        Assert.That(order, Is.EqualTo(new[] { "model", "system" }));
    }

    /// <summary>测试 Model 初始化时可以读取同一 Configure 注册的 Utility。</summary>
    [Test]
    public void ModelCanReadUtilityDuringInitialization()
    {
        var utility = new ClockUtility { Value = 7 };
        IClockUtility? observed = null;
        var model = new DerivedModel(self => observed = self.ReadUtility<IClockUtility>());

        using var domain = ProbeDomain.Create(configure: value =>
        {
            value.AddModel(model);
            value.AddUtility<IClockUtility>(utility);
        });

        Assert.That(observed, Is.SameAs(utility));
    }

    /// <summary>测试 System 初始化时能取到后注册的 System 引用。</summary>
    [Test]
    public void SystemCanCacheLaterRegisteredSystemDuringInitialization()
    {
        var later = new AltDerivedSystem();
        IAltPlayerSystem? cached = null;
        var earlier = new DerivedSystem(self => cached = self.ReadSystem<IAltPlayerSystem>());

        using var domain = ProbeDomain.Create(configure: value =>
        {
            value.AddSystem(earlier);
            value.AddSystem<IAltPlayerSystem>(later);
        });

        Assert.That(cached, Is.SameAs(later));
        Assert.That(later.InitializeCount, Is.EqualTo(1));
    }

    /// <summary>测试在 Configure 之外注册会被拒绝。</summary>
    [Test]
    public void RegistrationOutsideConfigureIsRejected()
    {
        using var domain = ProbeDomain.Create();

        Assert.Throws<InvalidOperationException>(() => domain.AddModel(new ProbeModel()));
        Assert.That(domain.TryGetModel<ProbeModel>(out _), Is.False);
    }

    /// <summary>测试默认注册键是运行时类型，按接口也能查到同一实例。</summary>
    [Test]
    public void DefaultKeyUsesRuntimeTypeAndInterfaceResolvesSameInstance()
    {
        var model = new ProbeModel();
        using var domain = ProbeDomain.Create(configure: value => value.AddModel(model));

        Assert.That(domain.GetModel<ProbeModel>(), Is.SameAs(model));
        Assert.That(domain.GetModel<IPlayerModel>(), Is.SameAs(model));
    }

    /// <summary>测试显式契约作为精确键优先，按具体类型仍能查到。</summary>
    [Test]
    public void ExplicitContractIsExactKeyAndConcreteTypeStillResolves()
    {
        var model = new AlternateModel();
        var other = new ProbeModel();
        using var domain = ProbeDomain.Create(configure: value =>
        {
            value.AddModel<IPlayerModel>(model);
            value.AddModel(other);
        });

        // 精确键优先于可赋值扫描，所以两个 IPlayerModel 实现并存时也不会歧义。
        Assert.That(domain.GetModel<IPlayerModel>(), Is.SameAs(model));
        Assert.That(domain.GetModel<AlternateModel>(), Is.SameAs(model));
    }

    /// <summary>测试实例不实现显式契约时注册被拒绝。</summary>
    [Test]
    public void IncompatibleExplicitContractIsRejected()
    {
        Assert.Throws<ArgumentException>(() => ProbeDomain.Create(configure: value =>
            value.AddModel<IAltPlayerModel>(new ProbeModel())));
    }

    /// <summary>测试本地可赋值候选优先于父域的精确键。</summary>
    [Test]
    public void LocalAssignableCandidateShadowsParentExactKey()
    {
        var parentModel = new ProbeModel();
        var childModel = new AlternateModel();
        using var parent = ProbeDomain.Create(configure: value => value.AddModel<IPlayerModel>(parentModel));
        var child = ProbeDomain.Create(configure: value => value.AddModel(childModel));
        parent.AddChild(child);

        Assert.That(child.GetModel<IPlayerModel>(), Is.SameAs(childModel));
    }

    /// <summary>测试只有挂载之后查找才会回退到父域。</summary>
    [Test]
    public void ParentFallbackBeginsOnlyAfterAttachment()
    {
        var model = new ProbeModel();
        using var parent = ProbeDomain.Create(configure: value => value.AddModel(model));
        var child = ProbeDomain.Create();

        Assert.That(child.TryGetModel<IPlayerModel>(out _), Is.False);

        parent.AddChild(child);

        Assert.That(child.GetModel<IPlayerModel>(), Is.SameAs(model));
    }

    /// <summary>测试可赋值查找有歧义时 Get 和 TryGet 都抛异常并列出候选。</summary>
    [Test]
    public void AssignableAmbiguityThrowsForGetAndTryGetWithCandidateDetails()
    {
        using var domain = ProbeDomain.Create(configure: value =>
        {
            value.AddModel(new ProbeModel());
            value.AddModel(new AlternateModel());
        });

        var error = Assert.Throws<InvalidOperationException>(() => domain.GetModel<IPlayerModel>());

        Assert.That(error!.Message, Does.Contain(nameof(ProbeModel)).And.Contain(nameof(AlternateModel)));
        Assert.Throws<InvalidOperationException>(() => domain.TryGetModel<IPlayerModel>(out _));
    }

    /// <summary>测试查不到时 Get 抛异常、TryGet 返回 false。</summary>
    [Test]
    public void GetThrowsForMissingAndTryGetReturnsFalse()
    {
        using var domain = ProbeDomain.Create();

        Assert.Throws<KeyNotFoundException>(() => domain.GetUtility<IClockUtility>());
        Assert.That(domain.TryGetUtility<IClockUtility>(out var utility), Is.False);
        Assert.That(utility, Is.Null);
    }

    /// <summary>测试重复键、重复实例和多分类组件都会被拒绝。</summary>
    [Test]
    public void DuplicateKeyInstanceAndMultiCategoryAreRejected()
    {
        var model = new ProbeModel();

        Assert.Throws<InvalidOperationException>(() => ProbeDomain.Create(configure: value =>
        {
            value.AddModel(new ProbeModel());
            value.AddModel(new ProbeModel());
        }));
        Assert.Throws<InvalidOperationException>(() => ProbeDomain.Create(configure: value =>
        {
            value.AddModel(model);
            value.AddModel<IPlayerModel>(model);
        }));
        Assert.Throws<InvalidOperationException>(() => ProbeDomain.Create(configure: value =>
            value.AddModel(new MultiCategoryComponent())));
    }

    /// <summary>测试注册 null 抛出 ArgumentNullException。</summary>
    [Test]
    public void NullRegistrationThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ProbeDomain.Create(configure: value => value.AddModel(null!)));
        Assert.Throws<ArgumentNullException>(() => ProbeDomain.Create(configure: value => value.AddSystem(null!)));
        Assert.Throws<ArgumentNullException>(() => ProbeDomain.Create(configure: value => value.AddUtility(null!)));
    }

    /// <summary>测试初始化失败时逆序释放已初始化组件、不释放失败组件，并释放 Domain。</summary>
    [Test]
    public void InitializationFailureReleasesInitializedComponentsInReverseAndDisposesDomain()
    {
        var released = new List<string>();
        var failure = new ApplicationException("initialize");
        var model = new ProbeModel(release: () => released.Add("model"));
        var system = new ProbeSystem(release: () => released.Add("system"));
        var failing = new DerivedSystem(_ => throw failure);
        ProbeDomain? candidate = null;

        var actual = Assert.Throws<ApplicationException>(() => ProbeDomain.Create(configure: value =>
        {
            candidate = value;
            value.AddModel(model);
            value.AddSystem(system);
            value.AddSystem<IAltPlayerSystem>(new AltDerivedSystem());
            value.AddSystem<IPlayerSystem>(failing);
        }));

        Assert.That(actual, Is.SameAs(failure));
        Assert.That(released, Is.EqualTo(new[] { "system", "model" }));
        Assert.That(failing.ReleaseCount, Is.Zero);
        Assert.Throws<ObjectDisposedException>(() => candidate!.TryGetModel<IPlayerModel>(out _));
    }

    /// <summary>测试创建失败与清理失败按顺序汇总为一个扁平的 AggregateException。</summary>
    [Test]
    public void CreationFailurePrecedesCleanupFailureInFlatAggregate()
    {
        var creation = new ApplicationException("creation");
        var cleanup = new ApplicationException("cleanup");

        var actual = Assert.Throws<AggregateException>(() => ProbeDomain.Create(
            configure: value => value.AddModel(new ProbeModel(release: () => throw cleanup)),
            activated: _ => throw creation));

        Assert.That(actual!.InnerExceptions, Is.EqualTo(new Exception[] { creation, cleanup }));
    }

    /// <summary>测试创建时抛出的空 AggregateException 被原样抛出。</summary>
    [Test]
    public void EmptyAggregateFromCreationIsPropagatedUnchanged()
    {
        var failure = new AggregateException("creation failed");

        var actual = Assert.Throws<AggregateException>(() => ProbeDomain.Create(configure: _ => throw failure));

        Assert.That(actual, Is.SameAs(failure));
    }

    /// <summary>测试嵌套的 AggregateException 按发生顺序展开。</summary>
    [Test]
    public void NestedAggregatesAreFlattenedInOccurrenceOrder()
    {
        var empty = new AggregateException("empty failure");
        var other = new ApplicationException("other failure");
        var cleanup = new ApplicationException("cleanup failure");

        var actual = Assert.Throws<AggregateException>(() => ProbeDomain.Create(
            configure: value => value.AddModel(new ProbeModel(release: () => throw cleanup)),
            activated: _ => throw new AggregateException(new AggregateException(empty), other)));

        Assert.That(actual!.InnerExceptions, Is.EqualTo(new Exception[] { empty, other, cleanup }));
    }

    /// <summary>测试未进入 Active 就启动失败时不调用 OnDeactivating。</summary>
    [Test]
    public void OnDeactivatingIsSkippedWhenStartupFailsBeforeActivation()
    {
        var failure = new ApplicationException("configure");
        var deactivated = false;

        var actual = Assert.Throws<ApplicationException>(() => ProbeDomain.Create(
            configure: _ => throw failure,
            deactivating: _ => deactivated = true));

        Assert.That(actual, Is.SameAs(failure));
        Assert.That(deactivated, Is.False);
    }

    /// <summary>测试 OnActivated 抛异常时已处于 Active，仍会调用 OnDeactivating 并抛出原异常。</summary>
    [Test]
    public void OnDeactivatingRunsWhenOnActivatedThrows()
    {
        var failure = new ApplicationException("activated");
        var deactivated = false;

        var actual = Assert.Throws<ApplicationException>(() => ProbeDomain.Create(
            activated: _ => throw failure,
            deactivating: _ => deactivated = true));

        Assert.That(actual, Is.SameAs(failure));
        Assert.That(deactivated, Is.True);
    }

    /// <summary>测试在 Configure 中再次启动同一实例会抛异常，而不是无限递归。</summary>
    [Test]
    public void RestartingSameInstanceDuringConfigureThrows()
    {
        ProbeDomain? candidate = null;

        Assert.Throws<InvalidOperationException>(() => ProbeDomain.Create(configure: value =>
        {
            candidate = value;
            ProbeDomain.CreateFrom(value);
        }));

        Assert.Throws<ObjectDisposedException>(() => _ = candidate!.Parent);
    }

    /// <summary>测试工厂误返回已在使用的 Domain 时抛异常且不会释放它。</summary>
    [Test]
    public void FactoryReturningExistingDomainThrowsWithoutDisposingIt()
    {
        var model = new ProbeModel();
        using var existing = ProbeDomain.Create(configure: value => value.AddModel(model));

        Assert.Throws<InvalidOperationException>(() => ProbeDomain.CreateFrom(existing));
        Assert.That(existing.GetModel<IPlayerModel>(), Is.SameAs(model));
        Assert.That(model.ReleaseCount, Is.Zero);
    }

    /// <summary>测试 Utility 可在多个 Domain 间共享，且不会被 Domain 释放。</summary>
    [Test]
    public void PureUtilityMayBeSharedAndIsNeverDisposedByDomain()
    {
        var utility = new DisposableUtility();
        var first = ProbeDomain.Create(configure: value => value.AddUtility(utility));
        using var second = ProbeDomain.Create(configure: value => value.AddUtility(utility));

        first.Dispose();

        Assert.That(second.GetUtility<DisposableUtility>(), Is.SameAs(utility));
        Assert.That(utility.DisposeCount, Is.Zero);
    }

    /// <summary>测试基类 Model 在使用中不能注册到第二个 Domain，释放后可以复用。</summary>
    [Test]
    public void BaseClassModelCannotJoinSecondDomainWhileActiveButCanAfterRelease()
    {
        var model = new DerivedModel();
        var first = ProbeDomain.Create(configure: value => value.AddModel(model));

        Assert.Throws<InvalidOperationException>(() => ProbeDomain.Create(configure: value => value.AddModel(model)));
        Assert.That(first.GetModel<IPlayerModel>(), Is.SameAs(model));

        first.Dispose();
        using var second = ProbeDomain.Create(configure: value => value.AddModel(model));

        Assert.That(model.InitializeCount, Is.EqualTo(2));
    }

    /// <summary>记录 Dispose 调用次数的 Utility，用于证明 Domain 不释放 Utility。</summary>
    private sealed class DisposableUtility : IUtility, IDisposable
    {
        /// <summary>Dispose 调用次数。</summary>
        public int DisposeCount { get; private set; }

        /// <inheritdoc />
        public void Dispose() => DisposeCount++;
    }
}
