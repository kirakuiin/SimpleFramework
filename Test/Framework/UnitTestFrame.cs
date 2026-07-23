#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SimpleFramework;
using SimpleFramework.FrameworkImpl;

namespace Test.Framework;

[TestFixture]
public class TestFramework
{
    private ADomain _aDomain = default!;
    private Control _control = default!;

    private const int IntVal = 3;
    private const int AnoVal = 4;
    private const string StrVal = "hello";

    [SetUp]
    public void Setup()
    {
        _aDomain = ADomain.Instance;

        _aDomain.RegisterModel(new Model(StrVal));
        _aDomain.RegisterSystem(new System());
        _aDomain.RegisterUtility(new Utility(IntVal));

        _control = new Control();
    }

    [TearDown]
    public void TearDown()
    {
        _aDomain.UnInitialize();
        BDomain.Instance.UnInitialize();
        DDomain.Instance.UnInitialize();
    }

    [Test]
    public void TestModelExists()
    {
        Assert.IsNotNull(_aDomain.GetModel<Model>());
        Assert.IsNull(_aDomain.GetModel<ModelNull>());
    }

    [Test]
    public void TestCommand()
    {
        Assert.AreEqual(_aDomain.GetUtility<Utility>()!.Value, _control.SendCommand());
    }

    [Test]
    public void TestQuery()
    {
        Assert.AreEqual(_aDomain.GetModel<Model>()!.Value.Value, _control.SendQuery());
    }

    [Test]
    public void TestBindable()
    {
        const string newWord = "world";
        _aDomain.GetModel<Model>()!.Value.Value = newWord;
        Assert.AreEqual(StrVal, _control.Old);
        Assert.AreEqual(newWord, _control.New);
    }

    [Test]
    public void TestBindableComparerIsInstanceScoped()
    {
        var left = new BindableProperty<int>(10)
            .WithComparer((prev, current) => Math.Abs(prev - current) < 5);
        var right = new BindableProperty<int>(10);

        var leftChanged = false;
        var rightChanged = false;

        left.Register((_, _) => leftChanged = true);
        right.Register((_, _) => rightChanged = true);

        left.Value = 12;
        right.Value = 12;

        Assert.IsFalse(leftChanged);
        Assert.IsTrue(rightChanged);
    }

    [Test]
    public void TestBindableComparerHandlesNullValues()
    {
        var property = new BindableProperty<string?>(null);
        var changedCount = 0;

        property.Register((_, _) => changedCount++);

        property.Value = null;
        Assert.AreEqual(0, changedCount);

        property.Value = "hello";
        Assert.AreEqual(1, changedCount);

        property.Value = null;
        Assert.AreEqual(2, changedCount);
    }

    [Test]
    public void TestBindableToStringHandlesNullValue()
    {
        var property = new BindableProperty<string?>(null);

        Assert.AreEqual(string.Empty, property.ToString());
    }

    [Test]
    public void TestBindableSetterReadsOldValueOnce()
    {
        var property = new CountingBindableProperty(1);
        property.Register((_, _) => { });

        property.Value = 2;

        Assert.AreEqual(2, property.GetValueCount);
    }

    [Test]
    public void TestUnRegister()
    {
        _control.UnRegister.UnRegister();
        _aDomain.GetModel<Model>()!.Value.Value = "find";

        Assert.IsNull(_control.Old);
        Assert.IsNull(_control.New);
    }

    [Test]
    public void TestBindableUnRegisterIsIdempotent()
    {
        var property = new BindableProperty<int>(1);
        var unregister = property.Register((_, _) => { });

        unregister.UnRegister();

        Assert.DoesNotThrow(() => unregister.UnRegister());
        Assert.DoesNotThrow(() => unregister.Dispose());
    }

    [Test]
    public void TestCustomUnregisterThrowingCallbackRunsOnce()
    {
        var callCount = 0;
        var unregister = new CustomUnRegister(() =>
        {
            callCount++;
            throw new InvalidOperationException("Unregister failed.");
        });

        var exception = Assert.Throws<InvalidOperationException>(unregister.UnRegister);

        Assert.That(exception!.Message, Is.EqualTo("Unregister failed."));
        Assert.DoesNotThrow(((IDisposable)unregister).Dispose);
        Assert.AreEqual(1, callCount);
    }

    [Test]
    public void TestRegister()
    {
        var system = _aDomain.GetSystem<System>()!;

        Assert.AreEqual(System.InitVal, system.Value);

        _aDomain.GetModel<Model>()!.Notify();

        Assert.AreNotEqual(System.InitVal, system.Value);
    }

    [Test]
    public void TestParentExists()
    {
        BDomain.Instance.SetParent(ADomain.Instance);

        Assert.AreEqual(IntVal, BDomain.Instance.GetUtility<Utility>()!.Value);
    }

    [Test]
    public void TestParentOverride()
    {
        BDomain.Instance.SetParent(ADomain.Instance);
        BDomain.Instance.RegisterUtility(new Utility(AnoVal));

        Assert.AreEqual(AnoVal, BDomain.Instance.GetUtility<Utility>()!.Value);
    }

    [Test]
    public void TestExplicitGenericUtilityRegistrationUsesInterfaceKey()
    {
        ADomain.Instance.RegisterUtility<ITestUtility>(new InterfaceUtility(IntVal));

        Assert.IsNull(ADomain.Instance.GetUtility<InterfaceUtility>());
        Assert.AreEqual(IntVal, ADomain.Instance.GetUtility<ITestUtility>()!.Value);
    }

    [Test]
    public void TestTryGetAndRequireModel()
    {
        Assert.IsTrue(ADomain.Instance.TryGetModel<Model>(out var model));
        Assert.AreSame(ADomain.Instance.GetModel<Model>(), model);

        Assert.IsFalse(ADomain.Instance.TryGetModel<ModelNull>(out var missing));
        Assert.IsNull(missing);

        Assert.AreSame(model, ADomain.Instance.RequireModel<Model>());
        var ex = Assert.Throws<InvalidOperationException>(() => ADomain.Instance.RequireModel<ModelNull>());
        Assert.That(ex!.Message, Does.Contain(typeof(ModelNull).FullName));
        Assert.That(ex.Message, Does.Contain(typeof(ADomain).FullName));
    }

    [Test]
    public void TestTryGetAndRequireUtility()
    {
        Assert.IsTrue(ADomain.Instance.TryGetUtility<Utility>(out var utility));
        Assert.AreSame(ADomain.Instance.GetUtility<Utility>(), utility);

        Assert.IsFalse(ADomain.Instance.TryGetUtility<ITestUtility>(out var missing));
        Assert.IsNull(missing);

        Assert.AreSame(utility, ADomain.Instance.RequireUtility<Utility>());
        var ex = Assert.Throws<InvalidOperationException>(() => ADomain.Instance.RequireUtility<ITestUtility>());
        Assert.That(ex!.Message, Does.Contain(typeof(ITestUtility).FullName));
        Assert.That(ex.Message, Does.Contain(typeof(ADomain).FullName));
    }

    [Test]
    public void TestTryGetAndRequireSystem()
    {
        Assert.IsTrue(ADomain.Instance.TryGetSystem<System>(out var system));
        Assert.AreSame(ADomain.Instance.GetSystem<System>(), system);

        Assert.IsFalse(ADomain.Instance.TryGetSystem<LifecycleSystem>(out var missing));
        Assert.IsNull(missing);

        Assert.AreSame(system, ADomain.Instance.RequireSystem<System>());
        var ex = Assert.Throws<InvalidOperationException>(() => ADomain.Instance.RequireSystem<LifecycleSystem>());
        Assert.That(ex!.Message, Does.Contain(typeof(LifecycleSystem).FullName));
        Assert.That(ex.Message, Does.Contain(typeof(ADomain).FullName));
    }

    [Test]
    public void TestRequireExtensionsReturnInstances()
    {
        Assert.AreSame(ADomain.Instance.GetModel<Model>(), _control.RequireModel<Model>());
        Assert.AreSame(ADomain.Instance.GetUtility<Utility>(), _control.RequireUtility<Utility>());
        Assert.AreSame(ADomain.Instance.GetSystem<System>(), _control.RequireSystem<System>());

        _control.Require<Model>();
        _control.Require<Utility>();
    }

    [Test]
    public void TestRegisterSystemAsUsesServiceKey()
    {
        var domain = ADomain.Create();
        var system = new LifecycleSystem();

        domain.RegisterSystemAs<ISystem>(system);

        Assert.IsNull(domain.GetSystem<LifecycleSystem>());
        Assert.AreSame(system, domain.GetSystem<ISystem>());
    }

    [Test]
    public void TestRegisterModelAsUsesServiceKey()
    {
        var domain = ADomain.Create();
        var model = new LifecycleModel();

        domain.RegisterModelAs<IModel>(model);

        Assert.IsNull(domain.GetModel<LifecycleModel>());
        Assert.AreSame(model, domain.GetModel<IModel>());
    }

    [Test]
    public void TestRegisterUtilityAsUsesServiceKey()
    {
        ADomain.Instance.RegisterUtilityAs<ITestUtility>(new InterfaceUtility(IntVal));

        Assert.IsNull(ADomain.Instance.GetUtility<InterfaceUtility>());
        Assert.AreEqual(IntVal, ADomain.Instance.RequireUtility<ITestUtility>().Value);
    }

    [Test]
    public void TestContainerTryGetAndRemove()
    {
        var container = new Container();
        container.Register<IUtility>(new Utility(IntVal));

        Assert.IsTrue(container.TryGet<IUtility>(out var utility));
        Assert.AreEqual(IntVal, ((Utility)utility!).Value);

        Assert.IsTrue(container.Remove<IUtility>());
        Assert.IsFalse(container.TryGet<IUtility>(out _));
    }

    [Test]
    public void TestContainerRegisterReturnsPreviousInstance()
    {
        var container = new Container();
        var first = new Utility(IntVal);
        var second = new Utility(AnoVal);

        var empty = container.Register<IUtility>(first);
        var previous = container.Register<IUtility>(second);

        Assert.IsNull(empty);
        Assert.AreSame(first, previous);
        Assert.AreSame(second, container.Get<IUtility>());
    }

    [Test]
    public void TestContainerRegisterRejectsNull()
    {
        var container = new Container();

        var exception = Assert.Throws<ArgumentNullException>(() => container.Register<IUtility>(null!));

        Assert.That(exception!.ParamName, Is.EqualTo("instance"));
        Assert.IsNull(container.Get<IUtility>());
    }

    [Test]
    public void TestSetParentLifeCycle()
    {
        DDomain.Instance.SetParent(PDomain.Instance);
        PDomain.Instance.RegisterUtility(new Utility(IntVal));

        D1Domain.Instance.SetParent(DDomain.Instance);
        D2Domain.Instance.SetParent(DDomain.Instance);
        DDomain.Instance.RegisterModel(new Model("hello world"));

        DDomain.Instance.UnInitialize();

        Assert.IsNotNull(PDomain.GetInstance());
        Assert.IsNull(DDomain.GetInstance());
        Assert.IsNotNull(D1Domain.GetInstance());
        Assert.IsNotNull(D2Domain.GetInstance());
    }

    [Test]
    public void TestAddChildLifeCycle()
    {
        PDomain.Instance.AddChild(DDomain.Instance);
        PDomain.Instance.RegisterUtility(new Utility(IntVal));

        DDomain.Instance.AddChild(D1Domain.Instance);
        DDomain.Instance.AddChild(D2Domain.Instance);
        DDomain.Instance.RegisterModel(new Model("hello world"));

        Assert.AreEqual(IntVal, DDomain.Instance.GetUtility<Utility>()!.Value);
        Assert.AreEqual(IntVal, D1Domain.Instance.GetUtility<Utility>()!.Value);
        Assert.AreEqual("hello world", D2Domain.Instance.GetModel<Model>()!.Value.Value);

        PDomain.Instance.UnInitialize();

        Assert.IsNull(PDomain.GetInstance());
        Assert.IsNull(DDomain.GetInstance());
        Assert.IsNull(D1Domain.GetInstance());
        Assert.IsNull(D2Domain.GetInstance());
    }

    [Test]
    public void TestGlobalEvent()
    {
        var receiver = new GlobalEventReceiver();
        var receiver2 = new GlobalEventReceiver();
        var receiverUnregister = receiver.RegisterEvent();
        var receiver2Unregister = receiver2.RegisterEvent();

        try
        {
            Assert.AreEqual(0, receiver.Value);
            Assert.AreEqual(0, receiver2.Value);

            var value = 3;
            EventBus.Global.Send(value);

            Assert.AreEqual(value, receiver.Value);
            Assert.AreEqual(value, receiver2.Value);

            receiverUnregister.UnRegister();

            value = 4;
            EventBus.Global.Send(value);
            Assert.AreNotEqual(value, receiver.Value);
            Assert.AreEqual(value, receiver2.Value);
        }
        finally
        {
            receiverUnregister.UnRegister();
            receiver2Unregister.UnRegister();
        }
    }

    [Test]
    public void TestEventBusRemovesEmptyEventAfterUnregister()
    {
        var eventBus = new EventBus();
        void OnEvent(EventA _) { }

        eventBus.Register<EventA>(OnEvent);
        Assert.IsTrue(eventBus.Contains<EventA>());

        eventBus.UnRegister<EventA>(OnEvent);

        Assert.IsFalse(eventBus.Contains<EventA>());
    }

    [Test]
    public void TestEventContainerMissingEventReturnsNull()
    {
        var container = new EventContainer();
        var method = typeof(EventContainer).GetMethod(nameof(EventContainer.GetEvent))!;
        var nullability = new NullabilityInfoContext().Create(method.ReturnParameter);

        Assert.IsNull(container.GetEvent<Event<EventA>>());
        Assert.That(nullability.ReadState, Is.EqualTo(NullabilityState.Nullable));
    }

    [Test]
    public void TestEventBusClear()
    {
        var eventBus = new EventBus();
        eventBus.Register<EventA>(_ => { });

        Assert.IsTrue(eventBus.Contains<EventA>());

        eventBus.Clear();

        Assert.IsFalse(eventBus.Contains<EventA>());
    }

    [Test]
    public void TestEventBusClearMakesOldUnregisterTokenStale()
    {
        var eventBus = new EventBus();
        var called = false;
        void OnEvent(EventA _) => called = true;

        var oldUnregister = eventBus.Register<EventA>(OnEvent);
        eventBus.Clear();
        var newUnregister = eventBus.Register<EventA>(OnEvent);

        try
        {
            oldUnregister.UnRegister();
            eventBus.Send(new EventA("hello"));

            Assert.IsTrue(called);
        }
        finally
        {
            newUnregister.UnRegister();
        }
    }

    [Test]
    public void TestEventUnregisterDuringTriggerDoesNotBreakIteration()
    {
        var eventBus = new EventBus();
        IUnRegister? unregister = null;
        var firstCalled = false;
        var secondCalled = false;

        unregister = eventBus.Register<EventA>(_ =>
        {
            firstCalled = true;
            unregister!.UnRegister();
        });
        eventBus.Register<EventA>(_ => secondCalled = true);

        eventBus.Send(new EventA("hello"));

        Assert.IsTrue(firstCalled);
        Assert.IsTrue(secondCalled);
        Assert.IsTrue(eventBus.Contains<EventA>());
    }

    [Test]
    public void TestDomainEventsDoNotPropagateToParentOrChild()
    {
        var parentCalled = false;
        var childCalled = false;

        ADomain.Instance.RegisterEvent<EventA>(_ => parentCalled = true);
        BDomain.Instance.RegisterEvent<EventA>(_ => childCalled = true);
        BDomain.Instance.SetParent(ADomain.Instance);

        BDomain.Instance.SendEvent(new EventA("child"));

        Assert.IsFalse(parentCalled);
        Assert.IsTrue(childCalled);

        parentCalled = false;
        childCalled = false;

        ADomain.Instance.SendEvent(new EventA("parent"));

        Assert.IsTrue(parentCalled);
        Assert.IsFalse(childCalled);
    }

    [Test]
    public void TestDomainUninitializeClearsLocalEventsButKeepsGlobalEvents()
    {
        var domain = ADomain.Instance;
        var localCalled = false;
        var globalCalled = false;
        var globalUnregister = EventBus.Global.Register<int>(_ => globalCalled = true);

        try
        {
            domain.RegisterEvent<EventA>(_ => localCalled = true);

            domain.UnInitialize();
            domain.SendEvent(new EventA("local"));
            ADomain.Instance.SendEvent(new EventA("recreated"));
            EventBus.Global.Send(1);

            Assert.IsFalse(localCalled);
            Assert.IsTrue(globalCalled);
        }
        finally
        {
            globalUnregister.UnRegister();
        }
    }

    [Test]
    public void TestCreateReturnsIndependentDomain()
    {
        var singleton = ADomain.Instance;
        var created = ADomain.Create();

        Assert.IsNotNull(created);
        Assert.AreNotSame(singleton, created);

        created.RegisterUtility(new Utility(AnoVal));
        Assert.AreEqual(AnoVal, created.GetUtility<Utility>()!.Value);
        Assert.AreEqual(IntVal, singleton.GetUtility<Utility>()!.Value);

        created.UnInitialize();

        Assert.AreSame(singleton, ADomain.GetInstance());
    }

    [Test]
    public void TestUnconfiguredDomainCreationApiRemainsDeclaredOnGenericLayer()
    {
        var domainType = typeof(AbstractDomain<>);
        const BindingFlags declaredPublicStatic =
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;

        Assert.IsNotNull(domainType.GetProperty(nameof(ADomain.Instance), declaredPublicStatic));
        Assert.IsNotNull(domainType.GetMethod(nameof(ADomain.GetInstance), declaredPublicStatic));
        Assert.IsNotNull(
            domainType.GetMethod(
                nameof(ADomain.Create),
                declaredPublicStatic,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null));
        Assert.IsTrue(domainType.GetGenericArguments()[0].GenericParameterAttributes
            .HasFlag(GenericParameterAttributes.DefaultConstructorConstraint));
    }

    [Test]
    public void TestConfiguredDomainProvidesConfigurationBeforeComponentInitialization()
    {
        var domain = ConfiguredTestDomain.Create("configured");

        Assert.AreEqual("configured", domain.ConfigurationObservedByInit);
        Assert.AreEqual(
            "configured",
            domain.RequireModel<ConfigurationObservingModel>().ObservedConfiguration);
        Assert.That(
            domain.ReadConfigurationAfterInitialization,
            Throws.TypeOf<InvalidOperationException>()
                .With.Message.EqualTo("Domain configuration is only available during initialization."));

        domain.UnInitialize();
    }

    [Test]
    public void TestConfiguredDomainHasNoFrameworkSingletonOrParameterlessCreationApi()
    {
        var domainType = typeof(ConfiguredTestDomain);
        const BindingFlags publicStatic = BindingFlags.Public | BindingFlags.Static;

        Assert.IsNull(domainType.GetProperty("Instance", publicStatic));
        Assert.IsNull(domainType.GetMethod("GetInstance", publicStatic));
        Assert.IsNull(
            domainType.GetMethod(
                "Create",
                publicStatic,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null));
        Assert.IsEmpty(domainType.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [Test]
    public void TestConfiguredDomainCannotInitializeTwice()
    {
        var domain = ConfiguredTestDomain.Create("configured");

        var exception = Assert.Throws<InvalidOperationException>(domain.InitializeAgain);

        Assert.That(exception!.Message, Is.EqualTo("Domain initialization can only run once."));
        domain.UnInitialize();
    }

    [Test]
    public void TestConfiguredDomainInitializationFailureReleasesEarlierComponents()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => FailingConfiguredDomain.Create(throwDuringCleanup: false));
        var failedDomain = FailingConfiguredDomain.LastCreated!;

        Assert.That(exception!.Message, Is.EqualTo("Configured initialization failed."));
        Assert.AreEqual(1, failedDomain.EarlierModel.InitializeCount);
        Assert.AreEqual(1, failedDomain.EarlierModel.UninitializeCount);
        Assert.IsNull(failedDomain.GetModel<ControllableLifecycleModel>());
        Assert.DoesNotThrow(failedDomain.UnInitialize);
    }

    [Test]
    public void TestConfiguredDomainPreservesInitializationAndCleanupFailures()
    {
        var exception = Assert.Throws<AggregateException>(
            () => FailingConfiguredDomain.Create(throwDuringCleanup: true));
        var flattened = exception!.Flatten().InnerExceptions;

        Assert.IsTrue(flattened.Any(inner => inner.Message == "Configured initialization failed."));
        Assert.IsTrue(flattened.Any(inner => inner.Message == "Replacement cleanup failed."));

        var failedDomain = FailingConfiguredDomain.LastCreated!;
        failedDomain.EarlierModel.ThrowOnUninitialize = false;
        Assert.DoesNotThrow(failedDomain.UnInitialize);
    }

    [Test]
    public void TestUnconfiguredDomainInitializationFailureCleansRegisteredState()
    {
        FailingUnconfiguredDomain.Configure(failInitialization: true, throwDuringCleanup: false);
        var exception = Assert.Throws<InvalidOperationException>(
            () => FailingUnconfiguredDomain.Create());
        var failedDomain = FailingUnconfiguredDomain.LastCreated!;

        Assert.That(exception!.Message, Is.EqualTo("Configured initialization failed."));
        Assert.AreEqual(1, failedDomain.EarlierModel.InitializeCount);
        Assert.AreEqual(1, failedDomain.EarlierModel.UninitializeCount);
        Assert.IsNull(failedDomain.GetModel<ControllableLifecycleModel>());
        Assert.DoesNotThrow(failedDomain.UnInitialize);
    }

    [Test]
    public void TestUnconfiguredDomainPreservesInitializationAndCleanupFailures()
    {
        FailingUnconfiguredDomain.Configure(failInitialization: true, throwDuringCleanup: true);

        var exception = Assert.Throws<AggregateException>(
            () => FailingUnconfiguredDomain.Create());
        var flattened = exception!.Flatten().InnerExceptions;

        Assert.IsTrue(flattened.Any(inner => inner.Message == "Configured initialization failed."));
        Assert.IsTrue(flattened.Any(inner => inner.Message == "Replacement cleanup failed."));

        var failedDomain = FailingUnconfiguredDomain.LastCreated!;
        Assert.AreEqual(1, failedDomain.EarlierModel.UninitializeCount);
        Assert.IsNull(failedDomain.GetModel<ControllableLifecycleModel>());
        failedDomain.EarlierModel.ThrowOnUninitialize = false;
        Assert.DoesNotThrow(failedDomain.UnInitialize);
    }

    [Test]
    public void TestUnconfiguredSingletonFailureLeavesEmptySlotAndCanRetry()
    {
        FailingUnconfiguredDomain.Configure(failInitialization: true, throwDuringCleanup: false);

        var exception = Assert.Throws<InvalidOperationException>(
            () => _ = FailingUnconfiguredDomain.Instance);
        var failedDomain = FailingUnconfiguredDomain.LastCreated!;

        Assert.That(exception!.Message, Is.EqualTo("Configured initialization failed."));
        Assert.AreEqual(1, failedDomain.EarlierModel.UninitializeCount);
        Assert.IsNull(FailingUnconfiguredDomain.GetInstance());

        FailingUnconfiguredDomain.Configure(failInitialization: false, throwDuringCleanup: false);
        var retry = FailingUnconfiguredDomain.Instance;

        Assert.AreNotSame(failedDomain, retry);
        Assert.AreSame(retry, FailingUnconfiguredDomain.GetInstance());
        Assert.AreEqual(1, retry.EarlierModel.InitializeCount);
        retry.UnInitialize();
    }

    [Test]
    public void TestAddChildOwnsCreatedChildLifecycle()
    {
        var parent = ADomain.Create();
        var child = BDomain.Create();

        parent.AddChild(child);

        parent.UnInitialize();

        Assert.IsNull(child.Parent);
    }

    [Test]
    public void TestRegisterModelReleasesPreviousInstance()
    {
        var domain = ADomain.Create();
        var first = new LifecycleModel();
        var second = new LifecycleModel();

        domain.RegisterModel(first);
        domain.RegisterModel(second);

        Assert.AreEqual(1, first.InitializeCount);
        Assert.AreEqual(1, first.UninitializeCount);
        Assert.AreEqual(1, second.InitializeCount);
        Assert.AreEqual(0, second.UninitializeCount);

        domain.UnInitialize();

        Assert.AreEqual(1, second.UninitializeCount);
    }

    [Test]
    public void TestRegisterModelReplacementCleanupFailureKeepsPreviousRegistration()
    {
        var domain = ADomain.Create();
        var first = new ControllableLifecycleModel { ThrowOnUninitialize = true };
        var replacement = new ControllableLifecycleModel();

        domain.RegisterModel(first);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() => domain.RegisterModel(replacement));

            Assert.That(exception!.Message, Is.EqualTo("Replacement cleanup failed."));
            Assert.AreSame(first, domain.GetModel<ControllableLifecycleModel>());
            Assert.AreEqual(1, first.UninitializeCount);
            Assert.AreEqual(0, replacement.InitializeCount);
        }
        finally
        {
            first.ThrowOnUninitialize = false;
            domain.UnInitialize();
        }
    }

    [Test]
    public void TestRegisterModelInitializationFailureDoesNotPublishComponent()
    {
        var domain = ADomain.Create();
        var model = new FailingInitializeModel();

        var exception = Assert.Throws<InvalidOperationException>(() => domain.RegisterModel(model));

        Assert.That(exception!.Message, Is.EqualTo("Initialization failed."));
        Assert.IsNull(domain.GetModel<FailingInitializeModel>());
        Assert.DoesNotThrow(domain.UnInitialize);
    }

    [Test]
    public void TestRegisterDuringReplacementCleanupDoesNotPublishNestedComponent()
    {
        var domain = ADomain.Create();
        var first = new RegisteringDuringReplacementCleanupModel();
        var replacement = new LifecycleModel();

        domain.RegisterModelAs<IModel>(first);

        try
        {
            Assert.Throws<InvalidOperationException>(() => domain.RegisterModelAs<IModel>(replacement));
            Assert.AreSame(first, domain.GetModel<IModel>());
            Assert.AreEqual(0, first.NestedReplacement.InitializeCount);
            Assert.AreEqual(0, replacement.InitializeCount);
        }
        finally
        {
            domain.UnInitialize();
        }
    }

    [Test]
    public void TestRegisterSameModelInstanceDoesNotInitializeTwice()
    {
        var domain = ADomain.Create();
        var model = new LifecycleModel();

        domain.RegisterModel(model);
        domain.RegisterModel(model);

        Assert.AreEqual(1, model.InitializeCount);
        Assert.AreEqual(0, model.UninitializeCount);
    }

    [Test]
    public void TestRegisterSystemReleasesPreviousInstance()
    {
        var domain = ADomain.Create();
        var first = new LifecycleSystem();
        var second = new LifecycleSystem();

        domain.RegisterSystem(first);
        domain.RegisterSystem(second);

        Assert.AreEqual(1, first.InitializeCount);
        Assert.AreEqual(1, first.UninitializeCount);
        Assert.AreEqual(1, second.InitializeCount);
        Assert.AreEqual(0, second.UninitializeCount);
    }

    [Test]
    public void TestRegisterModelSameInstanceAcrossKeysLifecycleOnce()
    {
        var domain = ADomain.Create();
        var model = new LifecycleModel();
        var replacement = new LifecycleModel();

        domain.RegisterModel(model);
        domain.RegisterModelAs<IModel>(model);

        Assert.AreEqual(1, model.InitializeCount);
        Assert.AreEqual(0, model.UninitializeCount);

        domain.RegisterModelAs<IModel>(replacement);

        Assert.AreEqual(0, model.UninitializeCount);
        Assert.AreEqual(1, replacement.InitializeCount);

        domain.UnInitialize();

        Assert.AreEqual(1, model.UninitializeCount);
        Assert.AreEqual(1, replacement.UninitializeCount);
    }

    [Test]
    public void TestRegisterSystemSameInstanceAcrossKeysLifecycleOnce()
    {
        var domain = ADomain.Create();
        var system = new LifecycleSystem();

        domain.RegisterSystem(system);
        domain.RegisterSystemAs<ISystem>(system);

        Assert.AreEqual(1, system.InitializeCount);
        Assert.AreEqual(0, system.UninitializeCount);

        domain.UnInitialize();

        Assert.AreEqual(1, system.UninitializeCount);
    }

    [Test]
    public void TestUninitializeReleasesSystemsBeforeModels()
    {
        var domain = ADomain.Create();
        var order = new List<string>();
        var model = new OrderedLifecycleModel(order);
        var system = new OrderedLifecycleSystem(order);

        domain.RegisterModel(model);
        domain.RegisterSystem(system);

        domain.UnInitialize();

        CollectionAssert.AreEqual(new[] { "system", "model" }, order);
    }

    [Test]
    public void TestRegisterDuringUninitializeThrowsBeforeInitializingComponent()
    {
        var domain = ADomain.Create();
        var model = new RegisteringOnUninitializeModel();

        domain.RegisterModel(model);
        domain.RegisterModel(new LifecycleModel());

        var exception = Assert.Throws<AggregateException>(() => domain.UnInitialize());
        Assert.IsTrue(exception!.InnerExceptions.Any(inner => inner is InvalidOperationException));
        Assert.AreEqual(1, model.UninitializeCount);
        Assert.AreEqual(0, model.RegisteredModel.InitializeCount);
        Assert.AreEqual(0, model.RegisteredModel.UninitializeCount);
        Assert.IsNull(domain.GetModel<IModel>());

        var replacement = new LifecycleModel();
        domain.RegisterModel(replacement);
        Assert.AreEqual(1, replacement.InitializeCount);
    }

    [Test]
    public void TestThrowingModelUninitializeStillClearsDomainAndResetsGuard()
    {
        var domain = ADomain.Create();
        var throwing = new ThrowingOnUninitializeModel();

        domain.RegisterModel(throwing);

        var exception = Assert.Throws<AggregateException>(() => domain.UnInitialize());
        Assert.IsTrue(exception!.InnerExceptions.Any(inner => inner is InvalidOperationException));
        Assert.AreEqual(1, throwing.UninitializeCount);
        Assert.IsNull(domain.GetModel<ThrowingOnUninitializeModel>());

        var replacement = new LifecycleModel();
        domain.RegisterModel(replacement);
        Assert.AreEqual(1, replacement.InitializeCount);
    }

    [Test]
    public void TestReentrantUninitializeReleasesComponentOnce()
    {
        var domain = ADomain.Create();
        var model = new ReentrantUninitializeModel();
        domain.RegisterModel(model);

        domain.UnInitialize();

        Assert.AreEqual(1, model.UninitializeCount);
        Assert.IsNull(domain.GetModel<ReentrantUninitializeModel>());
    }

    [Test]
    public void TestDualRoleComponentLifecycleAcrossSystemAndModelKeys()
    {
        var domain = ADomain.Create();
        var dualRole = new DualRoleComponent();
        var replacementSystem = new LifecycleSystem();

        domain.RegisterSystemAs<ISystem>(dualRole);
        domain.RegisterModelAs<IModel>(dualRole);

        Assert.AreEqual(1, dualRole.InitializeCount);
        Assert.AreEqual(0, dualRole.UninitializeCount);

        domain.RegisterSystemAs<ISystem>(replacementSystem);

        Assert.AreEqual(0, dualRole.UninitializeCount);
        Assert.AreEqual(1, replacementSystem.InitializeCount);

        domain.UnInitialize();

        Assert.AreEqual(1, dualRole.UninitializeCount);
        Assert.AreEqual(1, replacementSystem.UninitializeCount);
    }

    [Test]
    public void TestUtilityRegisteredConstructableHasNoLifecycle()
    {
        var domain = ADomain.Create();
        var trap = new UtilityLifecycleTrap();

        domain.RegisterUtilityAs<IUtility>(trap);
        domain.UnInitialize();

        Assert.AreEqual(0, trap.InitializeCount);
        Assert.AreEqual(0, trap.UninitializeCount);
    }

    [Test]
    public void TestUtilityConstructableBecomesManagedWhenRegisteredAsSystem()
    {
        var domain = ADomain.Create();
        var trap = new UtilityLifecycleTrap();

        domain.RegisterUtilityAs<IUtility>(trap);
        domain.RegisterSystemAs<ISystem>(trap);

        Assert.AreEqual(1, trap.InitializeCount);
        Assert.AreEqual(0, trap.UninitializeCount);

        domain.UnInitialize();

        Assert.AreEqual(1, trap.UninitializeCount);
    }

    [Test]
    public void TestUtilityReferenceDoesNotKeepReplacedSystemAlive()
    {
        var domain = ADomain.Create();
        var trap = new UtilityLifecycleTrap();
        var replacement = new LifecycleSystem();

        domain.RegisterUtilityAs<IUtility>(trap);
        domain.RegisterSystemAs<ISystem>(trap);
        domain.RegisterSystemAs<ISystem>(replacement);

        Assert.AreEqual(1, trap.InitializeCount);
        Assert.AreEqual(1, trap.UninitializeCount);
        Assert.AreEqual(1, replacement.InitializeCount);
    }

    [Test]
    public void TestUtilityRegistrationUnderSameConcreteKeyReleasesManagedSystem()
    {
        var domain = ADomain.Create();
        var system = new UtilityLifecycleTrap();
        var utility = new UtilityLifecycleTrap();

        domain.RegisterSystem(system);
        domain.RegisterUtility(utility);

        Assert.AreEqual(1, system.InitializeCount);
        Assert.AreEqual(1, system.UninitializeCount);
        Assert.AreEqual(0, utility.InitializeCount);
        Assert.AreEqual(0, utility.UninitializeCount);

        domain.UnInitialize();

        Assert.AreEqual(1, system.UninitializeCount);
        Assert.AreEqual(0, utility.UninitializeCount);
    }

    #region AbstractDomain 核心功能测试

    [Test]
    public void TestUtilityOnlyConstructableReplacedBySystemUnderSameConcreteKeyIsNotUninitialized()
    {
        var domain = ADomain.Create();
        var utility = new UtilityLifecycleTrap();
        var system = new UtilityLifecycleTrap();

        domain.RegisterUtility(utility);
        domain.RegisterSystem(system);

        Assert.AreEqual(0, utility.InitializeCount);
        Assert.AreEqual(0, utility.UninitializeCount);
        Assert.AreEqual(1, system.InitializeCount);
    }

    [Test]
    public void TestUtilityOnlyConstructableReplacedByModelUnderSameConcreteKeyIsNotUninitialized()
    {
        var domain = ADomain.Create();
        var utility = new UtilityModelLifecycleTrap();
        var model = new UtilityModelLifecycleTrap();

        domain.RegisterUtility(utility);
        domain.RegisterModel(model);

        Assert.AreEqual(0, utility.InitializeCount);
        Assert.AreEqual(0, utility.UninitializeCount);
        Assert.AreEqual(1, model.InitializeCount);
    }

    [Test]
    public void TestSetParentWithNull()
    {
        // 测试设置父域为 null
        BDomain.Instance.SetParent(ADomain.Instance);
        Assert.IsNotNull(BDomain.Instance.Parent);

        BDomain.Instance.SetParent(null);
        Assert.IsNull(BDomain.Instance.Parent);
    }

    [Test]
    public void TestSetParentSameParent()
    {
        // 测试设置相同的父域不应该有任何副作用
        BDomain.Instance.SetParent(ADomain.Instance);
        var parentBefore = BDomain.Instance.Parent;

        BDomain.Instance.SetParent(ADomain.Instance);
        var parentAfter = BDomain.Instance.Parent;

        Assert.AreEqual(parentBefore, parentAfter);
        Assert.AreSame(ADomain.Instance, parentAfter);
    }

    [Test]
    public void TestCycleReferenceDetection()
    {
        // 测试循环依赖检测
        Assert.Throws<ArgumentException>(() =>
        {
            ADomain.Instance.SetParent(BDomain.Instance);
            BDomain.Instance.SetParent(ADomain.Instance);
        });

    }

    [Test]
    public void TestDeepCycleReferenceDetection()
    {
        // 测试深层循环依赖检测 A -> B -> C -> A
        Assert.Throws<ArgumentException>(() =>
        {
            ADomain.Instance.SetParent(BDomain.Instance);
            BDomain.Instance.SetParent(DDomain.Instance);
            DDomain.Instance.SetParent(ADomain.Instance);
        });

    }

    [Test]
    public void TestParentChildRelationship()
    {
        // 测试父子关系的双向维护
        BDomain.Instance.SetParent(ADomain.Instance);

        Assert.AreSame(ADomain.Instance, BDomain.Instance.Parent);

        // 清理
        BDomain.Instance.SetParent(null);
        Assert.IsNull(BDomain.Instance.Parent);
    }

    [Test]
    public void TestRemoveChild()
    {
        // 测试移除子域
        BDomain.Instance.AddChild(ADomain.Instance);
        Assert.AreSame(BDomain.Instance, ADomain.Instance.Parent);

        BDomain.Instance.RemoveChild(ADomain.Instance);
        Assert.IsNull(ADomain.Instance.Parent);
    }

    [Test]
    public void TestSetParentRemoveChildOptimization()
    {
        // 测试 SetParent 和 RemoveChild 的优化，避免递归调用
        BDomain.Instance.SetParent(ADomain.Instance);
        Assert.AreSame(ADomain.Instance, BDomain.Instance.Parent);

        // 当设置新父域时，应该自动从旧父域移除
        BDomain.Instance.SetParent(DDomain.Instance);
        Assert.AreSame(DDomain.Instance, BDomain.Instance.Parent);

        // 验证旧的父子关系已经断开
        Assert.AreNotSame(ADomain.Instance, BDomain.Instance.Parent);

        // 设置为 null 时应该清除父子关系
        BDomain.Instance.SetParent(null);
        Assert.IsNull(BDomain.Instance.Parent);
    }

    [Test]
    public void TestAddChildDuplicate()
    {
        // 测试重复添加子域不会导致异常
        ADomain.Instance.AddChild(BDomain.Instance);
        ADomain.Instance.AddChild(BDomain.Instance); // 重复添加不应该出错

        // 验证父子关系仍然正确
        Assert.AreSame(ADomain.Instance, BDomain.Instance.Parent);
    }

    [Test]
    public void TestUninitializeWithChildren()
    {
        // 测试带子域的反初始化
        ADomain.Instance.AddChild(BDomain.Instance);
        BDomain.Instance.AddChild(DDomain.Instance);

        ADomain.Instance.UnInitialize();

        Assert.IsNull(ADomain.GetInstance());
        Assert.IsNull(BDomain.GetInstance());
        Assert.IsNull(DDomain.GetInstance());
    }

    [Test]
    public void TestGetInstanceAfterUninitialize()
    {
        // 测试反初始化后获取实例
        var instanceBefore = ADomain.GetInstance();
        Assert.IsNotNull(instanceBefore);

        ADomain.Instance.UnInitialize();
        var instanceAfter = ADomain.GetInstance();
        Assert.IsNull(instanceAfter);

        // 重新创建实例
        var newInstance = ADomain.Instance;
        Assert.IsNotNull(newInstance);
        Assert.AreNotSame(instanceBefore, newInstance);
    }

    [Test]
    public void TestComponentInheritance()
    {
        // 测试组件继承（子域访问父域的组件）
        ADomain.Instance.RegisterUtility(new Utility(IntVal));
        BDomain.Instance.SetParent(ADomain.Instance);

        // BDomain 应该能访问 ADomain 的 Utility
        var utility = BDomain.Instance.GetUtility<Utility>();
        Assert.IsNotNull(utility);
        Assert.AreEqual(IntVal, utility!.Value);

        // BDomain 覆盖父域的 Utility
        BDomain.Instance.RegisterUtility(new Utility(AnoVal));
        var overriddenUtility = BDomain.Instance.GetUtility<Utility>();
        Assert.AreEqual(AnoVal, overriddenUtility!.Value);

        // ADomain 的 Utility 不应该受影响
        var originalUtility = ADomain.Instance.GetUtility<Utility>();
        Assert.AreEqual(IntVal, originalUtility!.Value);
    }

    [Test]
    public void TestToString()
    {
        // 测试 ToString 方法
        ADomain.Instance.RegisterModel(new Model(StrVal));
        ADomain.Instance.RegisterSystem(new System());
        ADomain.Instance.RegisterUtility(new Utility(IntVal));

        var result = ADomain.Instance.ToString();
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Length > 0);
    }

    #endregion
}

#region DomainDefine

public class ADomain : AbstractDomain<ADomain>
{
    protected override void Init()
    {
    }
}

public class BDomain : AbstractDomain<BDomain>
{
    protected override void Init()
    {
    }
}

public class PDomain : AbstractDomain<PDomain>
{
    protected override void Init()
    {
    }
}


public class DDomain : AbstractDomain<DDomain>
{
    protected override void Init()
    {
    }
}


public class D1Domain : AbstractDomain<D1Domain>
{
    protected override void Init()
    {
    }
}


public class D2Domain : AbstractDomain<D2Domain>
{
    protected override void Init()
    {
    }
}

public sealed class ConfiguredTestDomain : AbstractConfiguredDomain<string>
{
    private ConfiguredTestDomain(string configuration)
        : base(configuration)
    {
    }

    public string? ConfigurationObservedByInit { get; private set; }

    public static ConfiguredTestDomain Create(string configuration)
    {
        var domain = new ConfiguredTestDomain(configuration);
        domain.Initialize();
        return domain;
    }

    public void InitializeAgain()
    {
        Initialize();
    }

    public string ReadConfigurationAfterInitialization()
    {
        return Configuration;
    }

    protected override void Init()
    {
        ConfigurationObservedByInit = Configuration;
        RegisterModel(new ConfigurationObservingModel(Configuration));
    }
}

public sealed class FailingConfiguredDomain : AbstractConfiguredDomain<bool>
{
    private FailingConfiguredDomain(bool throwDuringCleanup)
        : base(throwDuringCleanup)
    {
        EarlierModel = new ControllableLifecycleModel
        {
            ThrowOnUninitialize = throwDuringCleanup
        };
    }

    public static FailingConfiguredDomain? LastCreated { get; private set; }

    public ControllableLifecycleModel EarlierModel { get; }

    public static FailingConfiguredDomain Create(bool throwDuringCleanup)
    {
        var domain = new FailingConfiguredDomain(throwDuringCleanup);
        LastCreated = domain;
        domain.Initialize();
        return domain;
    }

    protected override void Init()
    {
        RegisterModel(EarlierModel);
        RegisterModel(new ConfiguredFailingInitializeModel());
    }
}

public sealed class FailingUnconfiguredDomain : AbstractDomain<FailingUnconfiguredDomain>
{
    private static bool _failInitialization = true;
    private static bool _throwDuringCleanup;

    public FailingUnconfiguredDomain()
    {
        EarlierModel = new ControllableLifecycleModel
        {
            ThrowOnUninitialize = _throwDuringCleanup
        };
        LastCreated = this;
    }

    public static FailingUnconfiguredDomain? LastCreated { get; private set; }

    public ControllableLifecycleModel EarlierModel { get; }

    public static void Configure(bool failInitialization, bool throwDuringCleanup)
    {
        GetInstance()?.UnInitialize();
        _failInitialization = failInitialization;
        _throwDuringCleanup = throwDuringCleanup;
        LastCreated = null;
    }

    protected override void Init()
    {
        RegisterModel(EarlierModel);
        if (_failInitialization)
        {
            RegisterModel(new ConfiguredFailingInitializeModel());
        }
    }
}

public sealed class ConfigurationObservingModel : AbstractModel
{
    private readonly string _configuration;

    public ConfigurationObservingModel(string configuration)
    {
        _configuration = configuration;
    }

    public string? ObservedConfiguration { get; private set; }

    protected override void OnInitialize()
    {
        ObservedConfiguration = _configuration;
    }
}

public sealed class ConfiguredFailingInitializeModel : AbstractModel
{
    protected override void OnInitialize()
    {
        throw new InvalidOperationException("Configured initialization failed.");
    }
}


public class Control : IController
{
    public IDomain Domain => ADomain.Instance;

    public IUnRegister UnRegister { get; private set; }

    public Control()
    {
        UnRegister = this.GetModel<Model>()!.Value.Register(OnValueChanged);
    }

    public string? Old { get; private set; }
    public string? New { get; private set; }

    private void OnValueChanged(string prev, string current)
    {
        Old = prev;
        New = current;
    }

    public int SendCommand()
    {
        return this.SendCommand(new Command());
    }

    public string SendQuery()
    {
        return this.SendQuery(new Query());
    }
}

public class System : AbstractSystem
{
    public const string InitVal = "init";
    public string Value { get; private set; } = default!;

    protected override void OnInitialize()
    {
        Value = InitVal;
        this.RegisterEvent<EventA>(OnEvent);
    }

    private void OnEvent(EventA e)
    {
        Value = e.Value;
    }
}

public class Model : AbstractModel
{
    public BindableProperty<string> Value { get; }

    public Model(string init)
    {
        Value = new BindableProperty<string>
        {
            Value = init
        };
    }

    public void Notify()
    {
        this.SendEvent(new EventA(Value.Value));
    }

    protected override void OnInitialize()
    {
    }
}

public class ModelNull : AbstractModel
{
    protected override void OnInitialize()
    {
    }
}

public class LifecycleModel : AbstractModel
{
    public int InitializeCount { get; private set; }
    public int UninitializeCount { get; private set; }

    protected override void OnInitialize()
    {
        InitializeCount++;
    }

    protected override void OnUninitialize()
    {
        UninitializeCount++;
    }
}

public class ControllableLifecycleModel : AbstractModel
{
    public bool ThrowOnUninitialize { get; set; }
    public int InitializeCount { get; private set; }
    public int UninitializeCount { get; private set; }

    protected override void OnInitialize()
    {
        InitializeCount++;
    }

    protected override void OnUninitialize()
    {
        UninitializeCount++;
        if (ThrowOnUninitialize)
        {
            throw new InvalidOperationException("Replacement cleanup failed.");
        }
    }
}

public class FailingInitializeModel : AbstractModel
{
    protected override void OnInitialize()
    {
        throw new InvalidOperationException("Initialization failed.");
    }
}

public class ReentrantUninitializeModel : AbstractModel
{
    private bool _reentered;

    public int UninitializeCount { get; private set; }

    protected override void OnInitialize()
    {
    }

    protected override void OnUninitialize()
    {
        UninitializeCount++;
        if (_reentered)
        {
            return;
        }

        _reentered = true;
        Domain.UnInitialize();
    }
}

public class RegisteringDuringReplacementCleanupModel : AbstractModel
{
    private bool _attemptedRegistration;

    public LifecycleModel NestedReplacement { get; } = new();

    protected override void OnInitialize()
    {
    }

    protected override void OnUninitialize()
    {
        if (_attemptedRegistration)
        {
            return;
        }

        _attemptedRegistration = true;
        Domain.RegisterModelAs<IModel>(NestedReplacement);
    }
}

public class LifecycleSystem : AbstractSystem
{
    public int InitializeCount { get; private set; }
    public int UninitializeCount { get; private set; }

    protected override void OnInitialize()
    {
        InitializeCount++;
    }

    protected override void OnUninitialize()
    {
        UninitializeCount++;
    }
}

public class OrderedLifecycleModel : AbstractModel
{
    private readonly List<string> _order;

    public OrderedLifecycleModel(List<string> order)
    {
        _order = order;
    }

    protected override void OnInitialize()
    {
    }

    protected override void OnUninitialize()
    {
        _order.Add("model");
    }
}

public class OrderedLifecycleSystem : AbstractSystem
{
    private readonly List<string> _order;

    public OrderedLifecycleSystem(List<string> order)
    {
        _order = order;
    }

    protected override void OnInitialize()
    {
    }

    protected override void OnUninitialize()
    {
        _order.Add("system");
    }
}

public class RegisteringOnUninitializeModel : AbstractModel
{
    public int UninitializeCount { get; private set; }
    public LifecycleModel RegisteredModel { get; } = new();

    protected override void OnInitialize()
    {
    }

    protected override void OnUninitialize()
    {
        UninitializeCount++;
        Domain.RegisterModelAs<IModel>(RegisteredModel);
    }
}

public class ThrowingOnUninitializeModel : AbstractModel
{
    public int UninitializeCount { get; private set; }

    protected override void OnInitialize()
    {
    }

    protected override void OnUninitialize()
    {
        UninitializeCount++;
        throw new InvalidOperationException("Uninitialize failed.");
    }
}

public class DualRoleComponent : ISystem, IModel
{
    public IDomain Domain { get; private set; } = default!;
    public int InitializeCount { get; private set; }
    public int UninitializeCount { get; private set; }

    public void SetDomain(IDomain domain)
    {
        Domain = domain;
    }

    public void Initialize()
    {
        InitializeCount++;
    }

    public void UnInitialize()
    {
        UninitializeCount++;
    }
}

public class UtilityLifecycleTrap : IUtility, ISystem
{
    public IDomain Domain { get; private set; } = default!;
    public int InitializeCount { get; private set; }
    public int UninitializeCount { get; private set; }

    public void SetDomain(IDomain domain)
    {
        Domain = domain;
    }

    public void Initialize()
    {
        InitializeCount++;
    }

    public void UnInitialize()
    {
        UninitializeCount++;
    }
}

public class UtilityModelLifecycleTrap : IUtility, IModel
{
    public IDomain Domain { get; private set; } = default!;
    public int InitializeCount { get; private set; }
    public int UninitializeCount { get; private set; }

    public void SetDomain(IDomain domain)
    {
        Domain = domain;
    }

    public void Initialize()
    {
        InitializeCount++;
    }

    public void UnInitialize()
    {
        UninitializeCount++;
    }
}

public class EventA
{
    public string Value { get; private set; }

    public EventA(string val)
    {
        Value = val;
    }
}

public class Utility : IUtility
{
    public Utility(int init)
    {
        Value = init;
    }

    public int Value { get; private set; }
}

public class CountingBindableProperty : BindableProperty<int>
{
    public CountingBindableProperty(int initialValue) : base(initialValue)
    {
    }

    public int GetValueCount { get; private set; }

    protected override int GetValue()
    {
        GetValueCount++;
        return base.GetValue();
    }
}

public interface ITestUtility : IUtility
{
    int Value { get; }
}

public class InterfaceUtility : ITestUtility
{
    public InterfaceUtility(int value)
    {
        Value = value;
    }

    public int Value { get; }
}

public class Command : AbstractCommand<int>
{
    protected override int OnExecute()
    {
        return this.GetUtility<Utility>()!.Value;
    }
}

public class Query : AbstractQuery<string>
{
    protected override string OnExecute()
    {
        return this.GetModel<Model>()!.Value.Value;
    }
}


public class GlobalEventReceiver : IOnGlobalEvent<int>
{
    public int Value { get; private set; }

    public void OnEvent(int @event)
    {
        Value = @event;
    }
}

#endregion
