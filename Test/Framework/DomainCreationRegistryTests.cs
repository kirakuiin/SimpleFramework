using NUnit.Framework;
using SimpleFramework;

namespace Test.Framework;

/// <summary>验证 Domain v2 创建、启动、注册和分类解析契约。</summary>
[TestFixture]
public sealed class DomainCreationRegistryTests
{
    [Test]
    public void FactorySupportsConstructorParametersAndReturnsActiveDomain()
    {
        using var domain = ProbeDomain.Create("parameterized");
        Assert.That(domain.Name, Is.EqualTo("parameterized"));
        Assert.That(domain.TryGetUtility<IClockUtility>(out _), Is.False);
    }

    [Test]
    public void ConfigureCanOnlyRegister()
    {
        ProbeDomain? candidate = null;
        Assert.Throws<InvalidOperationException>(() => ProbeDomain.Create(configure: domain =>
        {
            candidate = domain;
            domain.TryGetUtility<IClockUtility>(out _);
        }));
        Assert.That(candidate, Is.Not.Null);
        Assert.Throws<ObjectDisposedException>(() => candidate!.TryGetUtility<IClockUtility>(out _));
    }

    [Test]
    public void ModelsInitializeBeforeSystemsRegardlessOfRegistrationOrder()
    {
        var order = new List<string>();
        var model = new ProbeModel(_ => order.Add("model"));
        var system = new ProbeSystem(context =>
        {
            order.Add("system");
            Assert.That(context.GetModel<IPlayerModel>(), Is.SameAs(model));
        });
        using var domain = ProbeDomain.Create(configure: value =>
        {
            value.RegisterSystem(system);
            value.RegisterModel(model);
        });
        Assert.That(order, Is.EqualTo(new[] { "model", "system" }));
    }

    [Test]
    public void UtilityIsAvailableDuringModelInitialization()
    {
        var utility = new ClockUtility { Value = 7 };
        var model = new ProbeModel(context => Assert.That(context.GetUtility<IClockUtility>(), Is.SameAs(utility)));
        using var domain = ProbeDomain.Create(configure: value =>
        {
            value.RegisterModel(model);
            value.RegisterUtility<IClockUtility>(utility);
        });
    }

    [Test]
    public void OnActivatedHasNormalActiveCapabilities()
    {
        var child = ProbeDomain.Create("child");
        var commandRan = false;
        using var domain = ProbeDomain.Create(activated: value =>
        {
            value.RegisterUtility(new ClockUtility());
            value.SendCommand(new DelegateCommand(_ => commandRan = true));
            value.AddChild(child);
        });
        Assert.That(commandRan, Is.True);
        Assert.That(child.Parent, Is.SameAs(domain));
    }

    [Test]
    public void InitializationFailureReleasesStartedCandidateAndDisposesDomain()
    {
        ProbeDomain? candidate = null;
        var model = new ProbeModel(_ => throw new ApplicationException("init"));
        var error = Assert.Throws<ApplicationException>(() => ProbeDomain.Create(configure: value =>
        {
            candidate = value;
            value.RegisterModel(model);
        }));
        Assert.That(error!.Message, Is.EqualTo("init"));
        Assert.That(model.ReleaseCount, Is.EqualTo(1));
        Assert.Throws<ObjectDisposedException>(() => candidate!.GetModel<IPlayerModel>());
        Assert.Throws<InvalidOperationException>(() => ProbeDomain.Create(configure: value => value.RegisterModel(model)));
    }

    [Test]
    public void ConfigureFailureReleasesUntouchedReservationForReuse()
    {
        var model = new ProbeModel();
        Assert.Throws<ApplicationException>(() => ProbeDomain.Create(configure: value =>
        {
            value.RegisterModel(model);
            throw new ApplicationException("configure");
        }));
        using var retry = ProbeDomain.Create(configure: value => value.RegisterModel(model));
        Assert.That(retry.GetModel<ProbeModel>(), Is.SameAs(model));
    }

    [Test]
    public void DefaultKeyUsesRuntimeTypeAndBothInterfaceAndConcreteResolveSameInstance()
    {
        IModelLifecycle model = new ProbeModel();
        using var domain = ProbeDomain.Create(configure: value => value.RegisterModel(model));
        Assert.That(domain.GetModel<IPlayerModel>(), Is.SameAs(model));
        Assert.That(domain.GetModel<ProbeModel>(), Is.SameAs(model));
    }

    [Test]
    public void ExplicitContractIsTheSingleExactKeyButConcreteStillAssignable()
    {
        var model = new ProbeModel();
        using var domain = ProbeDomain.Create(configure: value => value.RegisterModel<IPlayerModel>(model));
        Assert.That(domain.GetModel<IPlayerModel>(), Is.SameAs(model));
        Assert.That(domain.GetModel<ProbeModel>(), Is.SameAs(model));
    }

    [Test]
    public void ExplicitContractIsValidatedBeforeLifecycleOwnership()
    {
        var model = new ProbeModel();
        Assert.Throws<ArgumentException>(() => ProbeDomain.Create(configure: value => value.RegisterModel<IAltPlayerModel>(model)));
        using var valid = ProbeDomain.Create(configure: value => value.RegisterModel(model));
        Assert.That(valid.GetModel<ProbeModel>(), Is.SameAs(model));
    }

    [Test]
    public void LocalAssignableCandidateShadowsParentExactKey()
    {
        var parentModel = new ProbeModel();
        var localModel = new AlternateModel();
        using var parent = ProbeDomain.Create(configure: value => value.RegisterModel<IPlayerModel>(parentModel));
        using var child = ProbeDomain.Create(configure: value => value.RegisterModel(localModel));
        parent.AddChild(child);
        Assert.That(child.GetModel<IPlayerModel>(), Is.SameAs(localModel));
    }

    [Test]
    public void ParentFallbackBeginsOnlyAfterAttachment()
    {
        var model = new ProbeModel();
        using var parent = ProbeDomain.Create(configure: value => value.RegisterModel(model));
        using var child = ProbeDomain.Create();
        Assert.That(child.TryGetModel<IPlayerModel>(out _), Is.False);
        parent.AddChild(child);
        Assert.That(child.GetModel<IPlayerModel>(), Is.SameAs(model));
    }

    [Test]
    public void AssignableAmbiguityThrowsForGetAndTryGetWithCandidateDetails()
    {
        using var domain = ProbeDomain.Create(configure: value =>
        {
            value.RegisterModel(new ProbeModel());
            value.RegisterModel(new AlternateModel());
        });
        var getError = Assert.Throws<InvalidOperationException>(() => domain.GetModel<IPlayerModel>());
        Assert.That(getError!.Message, Does.Contain(nameof(IPlayerModel)).And.Contain(nameof(ProbeModel)).And.Contain(nameof(AlternateModel)));
        Assert.Throws<InvalidOperationException>(() => domain.TryGetModel<IPlayerModel>(out _));
    }

    [Test]
    public void GetThrowsForMissingAndTryGetReturnsFalse()
    {
        using var domain = ProbeDomain.Create();
        Assert.Throws<KeyNotFoundException>(() => domain.GetSystem<IPlayerSystem>());
        Assert.That(domain.TryGetSystem<IPlayerSystem>(out var system), Is.False);
        Assert.That(system, Is.Null);
    }

    [Test]
    public void DuplicateKeyInstanceAndCategoryConflictsAreRejected()
    {
        var model = new ProbeModel();
        using var domain = ProbeDomain.Create(configure: value => value.RegisterModel(model));
        Assert.Throws<InvalidOperationException>(() => domain.RegisterModel(new ProbeModel()));
        Assert.Throws<InvalidOperationException>(() => domain.RegisterModel<IPlayerModel>(model));
        Assert.Throws<InvalidOperationException>(() => domain.RegisterUtility(new MultiCategoryComponent()));
    }

    [Test]
    public void NonGenericRegistrationsRejectNullWithArgumentNullException()
    {
        using var domain = ProbeDomain.Create();
        Assert.Throws<ArgumentNullException>(() => domain.RegisterModel(null!));
        Assert.Throws<ArgumentNullException>(() => domain.RegisterSystem(null!));
        Assert.Throws<ArgumentNullException>(() => domain.RegisterUtility(null!));
    }

    [Test]
    public void ActiveRegistrationPublishesOnlyAfterSuccessfulInitialization()
    {
        using var domain = ProbeDomain.Create();
        var model = new ProbeModel();
        domain.RegisterModel(model);
        Assert.That(domain.GetModel<ProbeModel>(), Is.SameAs(model));

        var failing = new AlternateModelWithFailure();
        Assert.Throws<ApplicationException>(() => domain.RegisterModel(failing));
        Assert.That(domain.TryGetModel<AlternateModelWithFailure>(out _), Is.False);
        Assert.That(failing.ReleaseCount, Is.EqualTo(1));
        Assert.That(domain.GetModel<ProbeModel>(), Is.SameAs(model));
    }

    [Test]
    public void DynamicLifecycleCandidateIsInvisibleUntilItsInitializeSucceeds()
    {
        var observer = new ProbeSystem();
        using var domain = ProbeDomain.Create(configure: value => value.RegisterSystem(observer));
        var candidate = new DelayedSystem(() =>
            Assert.That(observer.SavedContext!.TryGetSystem<DelayedSystem>(out _), Is.False));
        domain.RegisterSystem(candidate);
        Assert.That(domain.GetSystem<DelayedSystem>(), Is.SameAs(candidate));
    }

    [Test]
    public void CreationFailurePrecedesCleanupFailureInFlatAggregate()
    {
        var model = new ProbeModel(
            _ => throw new ApplicationException("initialize"),
            () => throw new ApplicationException("release"));
        var aggregate = Assert.Throws<AggregateException>(() =>
            ProbeDomain.Create(configure: value => value.RegisterModel(model)));
        Assert.That(aggregate!.InnerExceptions.Select(error => error.Message),
            Is.EqualTo(new[] { "initialize", "release" }));
    }

    [Test]
    public void PureUtilityMayBeSharedAndIsNeverDisposedByDomain()
    {
        var utility = new DisposableUtility();
        var first = ProbeDomain.Create(configure: value => value.RegisterUtility(utility));
        var second = ProbeDomain.Create(configure: value => value.RegisterUtility<IClockUtility>(utility));
        first.Dispose();
        Assert.That(utility.DisposeCount, Is.Zero);
        Assert.That(second.GetUtility<IClockUtility>(), Is.SameAs(utility));
        second.Dispose();
        Assert.That(utility.DisposeCount, Is.Zero);
    }

    [Test]
    public void DirectAndAbstractLifecycleImplementationsAreBothSupported()
    {
        var direct = new ProbeModel();
        var derived = new DerivedSystem();
        using var domain = ProbeDomain.Create(configure: value =>
        {
            value.RegisterUtility(new ClockUtility());
            value.RegisterModel(direct);
            value.RegisterSystem(derived);
        });
        Assert.That(direct.InitializeCount, Is.EqualTo(1));
        Assert.That(derived.InitializeCount, Is.EqualTo(1));
    }

    /// <summary>初始化固定失败的动态注册测试 Model。</summary>
    private sealed class AlternateModelWithFailure : IAltPlayerModel, IModelLifecycle
    {
        public int ReleaseCount { get; private set; }
        public void Initialize(IModelContext context) => throw new ApplicationException("dynamic");
        public void Release() => ReleaseCount++;
    }

    /// <summary>在初始化回调中验证延迟发布的测试 System。</summary>
    private sealed class DelayedSystem(Action initialize) : IAltPlayerSystem, ISystemLifecycle
    {
        public void Initialize(ISystemContext context) => initialize();
        public void Release() { }
    }

    /// <summary>用于验证 Domain 不拥有 Utility 释放责任的测试对象。</summary>
    private sealed class DisposableUtility : IClockUtility, IDisposable
    {
        public int Value => 0;
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
}
