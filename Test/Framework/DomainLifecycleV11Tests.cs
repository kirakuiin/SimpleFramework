#nullable enable
using NUnit.Framework;
using SimpleFramework;
using SimpleFramework.FrameworkImpl;

namespace Test.Framework;

[TestFixture]
public sealed class DomainLifecycleV11Tests
{
    [TearDown]
    public void TearDown()
    {
        ReentrantAccessDomain.GetInstance()?.UnInitialize();
    }

    [Test]
    public void SingletonReentrancyFailsWithoutPublishingAndCanRetry()
    {
        ReentrantAccessDomain.Reenter = true;

        var exception = Assert.Throws<InvalidOperationException>(() => _ = ReentrantAccessDomain.Instance);

        Assert.That(exception!.Message, Does.Contain("while the same Domain type is initializing"));
        Assert.IsNull(ReentrantAccessDomain.GetInstance());
        Assert.Throws<InvalidOperationException>(
            () => ReentrantAccessDomain.LastCreated!.RegisterUtility(new SharedUtility()));

        ReentrantAccessDomain.Reenter = false;
        var retry = ReentrantAccessDomain.Instance;

        Assert.AreNotSame(ReentrantAccessDomain.LastFailed, retry);
        Assert.AreSame(retry, ReentrantAccessDomain.GetInstance());
    }

    [Test]
    public void IndependentCreateCannotAccessExistingSingletonDuringInitialization()
    {
        var singleton = ReentrantAccessDomain.Instance;
        ReentrantAccessDomain.Reenter = true;

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() => ReentrantAccessDomain.Create());

            Assert.That(exception!.Message, Does.Contain("while the same Domain type is initializing"));
            Assert.AreSame(singleton, ReentrantAccessDomain.GetInstance());
        }
        finally
        {
            ReentrantAccessDomain.Reenter = false;
        }
    }

    [Test]
    public void UninitializeDuringDomainInitializationFailsAndDisposesInstance()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => UninitializingDomain.Create());
        var failed = UninitializingDomain.LastCreated!;

        Assert.That(exception!.Message, Does.Contain("while it is initializing"));
        Assert.DoesNotThrow(failed.UnInitialize);
        Assert.Throws<InvalidOperationException>(() => failed.RegisterUtility(new SharedUtility()));
    }

    [Test]
    public void DisposedDomainAllowsObservationButRejectsMutationAndExecution()
    {
        var domain = ADomain.Create();
        domain.RegisterUtility(new SharedUtility());
        domain.UnInitialize();

        Assert.IsNull(domain.GetUtility<SharedUtility>());
        Assert.IsFalse(domain.TryGetUtility<SharedUtility>(out _));
        Assert.IsNull(domain.Parent);
        Assert.That(domain.ToString(), Is.Empty);
        Assert.DoesNotThrow(domain.UnInitialize);

        Assert.Throws<InvalidOperationException>(() => domain.RegisterUtility(new SharedUtility()));
        var candidateParent = ADomain.Create();
        Assert.Throws<InvalidOperationException>(() => domain.SetParent(candidateParent));
        candidateParent.UnInitialize();
        Assert.Throws<InvalidOperationException>(() => domain.RegisterEvent<int>(_ => { }));
        Assert.Throws<InvalidOperationException>(() => domain.SendEvent(1));
        Assert.Throws<InvalidOperationException>(() => domain.SendCommand(new FlagCommand()));
        Assert.Throws<InvalidOperationException>(() => domain.SendQuery(new ConstantQuery()));
    }

    [Test]
    public void ConcreteModelsResolveByUniqueInterfaceAndExactKeyWins()
    {
        var domain = ADomain.Create();
        var first = new PlayerOne();
        var exact = new PlayerTwo();

        domain.RegisterModel(first);
        Assert.AreSame(first, domain.GetModel<IPlayerService>());

        domain.RegisterModelAs<IPlayerService>(exact);
        Assert.AreSame(exact, domain.GetModel<IPlayerService>());
        Assert.AreSame(first, domain.GetModel<PlayerOne>());
        Assert.AreSame(exact, domain.GetModel<PlayerTwo>());
        domain.UnInitialize();
    }

    [Test]
    public void AssignableAmbiguityExposesRequestedTypeAndCandidateKeys()
    {
        var domain = ADomain.Create();
        domain.RegisterModel(new PlayerOne());
        domain.RegisterModel(new PlayerTwo());

        var exception = Assert.Throws<AmbiguousComponentException>(
            () => domain.TryGetModel<IPlayerService>(out _));

        Assert.AreEqual(typeof(IPlayerService), exception!.RequestedType);
        CollectionAssert.AreEquivalent(
            new[] { typeof(PlayerOne), typeof(PlayerTwo) },
            exception.CandidateKeys);
        Assert.That(exception.Message, Does.Contain("Model"));
        domain.UnInitialize();
    }

    [Test]
    public void LocalResolutionShadowsParentAndLocalAmbiguityDoesNotFallThrough()
    {
        var parent = ADomain.Create();
        var child = BDomain.Create();
        var parentPlayer = new PlayerOne();
        var childPlayer = new PlayerTwo();

        parent.RegisterModelAs<IPlayerService>(parentPlayer);
        child.SetParent(parent);
        child.RegisterModel(childPlayer);

        Assert.AreSame(childPlayer, child.GetModel<IPlayerService>());

        child.RegisterModel(new PlayerThree());
        Assert.Throws<AmbiguousComponentException>(() => child.GetModel<IPlayerService>());

        child.UnInitialize();
        parent.UnInitialize();
    }

    [Test]
    public void LifecycleOwnershipRejectsCrossKeyDomainAndReuseAfterCleanup()
    {
        var firstDomain = ADomain.Create();
        var secondDomain = BDomain.Create();
        var model = new PlayerOne();

        firstDomain.RegisterModel(model);

        Assert.Throws<InvalidOperationException>(() => firstDomain.RegisterModelAs<IPlayerService>(model));
        Assert.Throws<InvalidOperationException>(() => secondDomain.RegisterModel(model));

        firstDomain.UnInitialize();
        Assert.Throws<InvalidOperationException>(() => secondDomain.RegisterModel(model));
        secondDomain.UnInitialize();
    }

    [Test]
    public void UtilityLifetimeCanBeSharedAcrossDomainsAndDoesNotLeakCategories()
    {
        var firstDomain = ADomain.Create();
        var secondDomain = BDomain.Create();
        var utility = new MultiRoleUtility();

        firstDomain.RegisterUtility(utility);
        secondDomain.RegisterUtility(utility);

        Assert.AreSame(utility, firstDomain.GetUtility<MultiRoleUtility>());
        Assert.AreSame(utility, secondDomain.GetUtility<MultiRoleUtility>());
        Assert.IsNull(firstDomain.GetSystem<ISystem>());
        Assert.IsNull(firstDomain.GetModel<IModel>());

        firstDomain.UnInitialize();
        Assert.AreSame(utility, secondDomain.GetUtility<MultiRoleUtility>());
        Assert.AreEqual(0, utility.InitializeCount);
        Assert.AreEqual(0, utility.UninitializeCount);
        secondDomain.UnInitialize();
    }

    [Test]
    public void FailedOuterInitializationRollsBackNestedEntriesAndLocalEvents()
    {
        var domain = ADomain.Create();
        var outer = new FailingTransactionModel();

        var exception = Assert.Throws<InvalidOperationException>(() => domain.RegisterModel(outer));

        Assert.That(exception!.Message, Is.EqualTo("outer initialization failed"));
        Assert.IsNull(domain.GetModel<FailingTransactionModel>());
        Assert.IsNull(domain.GetModel<NestedModel>());
        Assert.IsNull(domain.GetSystem<NestedSystem>());
        Assert.IsNull(domain.GetUtility<SharedUtility>());
        domain.SendEvent(new TransactionEvent());
        Assert.AreEqual(0, outer.EventCalls);
        Assert.AreEqual(1, outer.UninitializeCount);
        Assert.AreEqual(1, outer.Model.UninitializeCount);
        Assert.AreEqual(1, outer.System.UninitializeCount);
        domain.UnInitialize();
    }

    [Test]
    public void RollbackAggregatesInitializationAndCleanupFailures()
    {
        var domain = ADomain.Create();
        var outer = new FailingTransactionModel
        {
            ThrowDuringOwnCleanup = true,
            ThrowDuringNestedCleanup = true
        };

        var exception = Assert.Throws<AggregateException>(() => domain.RegisterModel(outer));
        var messages = exception!.Flatten().InnerExceptions.Select(item => item.Message).ToArray();

        CollectionAssert.Contains(messages, "outer initialization failed");
        CollectionAssert.Contains(messages, "outer cleanup failed");
        CollectionAssert.Contains(messages, "nested cleanup failed");
        Assert.IsNull(domain.GetModel<FailingTransactionModel>());
        Assert.IsNull(domain.GetModel<NestedModel>());
        domain.UnInitialize();
    }

    [Test]
    public void InitializerAllowsQueriesNestedRegistrationAndLocalSubscription()
    {
        var domain = ADomain.Create();
        var model = new PermittedInitializerModel();

        domain.RegisterModel(model);
        domain.SendEvent(new TransactionEvent());

        Assert.AreEqual(42, model.QueryResult);
        Assert.AreSame(model.Utility, domain.GetUtility<SharedUtility>());
        Assert.AreEqual(1, model.EventCalls);
        domain.UnInitialize();
    }

    [Test]
    public void InitializerRejectsCommandsEventsRelationshipsAndExistingKeyReplacement()
    {
        AssertForbiddenInitialization(ForbiddenAction.Command);
        AssertForbiddenInitialization(ForbiddenAction.Event);
        AssertForbiddenInitialization(ForbiddenAction.EventUnregistration);
        AssertForbiddenInitialization(ForbiddenAction.Relationship);

        var domain = ADomain.Create();
        var existing = new ReplaceTargetModel();
        domain.RegisterModel(existing);

        var replacementAttempt = new ReplacingInitializerModel();
        Assert.Throws<InvalidOperationException>(() => domain.RegisterModel(replacementAttempt));
        Assert.AreSame(existing, domain.GetModel<ReplaceTargetModel>());
        Assert.AreEqual(0, existing.UninitializeCount);
        domain.UnInitialize();
    }

    [Test]
    public void InitializerRejectsSelfRegistrationCycleAndCleansOnce()
    {
        var domain = ADomain.Create();
        var model = new SelfRegisteringModel();

        Assert.Throws<InvalidOperationException>(() => domain.RegisterModel(model));

        Assert.AreEqual(1, model.UninitializeCount);
        Assert.IsNull(domain.GetModel<SelfRegisteringModel>());
        domain.UnInitialize();
    }

    [Test]
    public void ComponentInitializerCannotUninitializeActiveDomain()
    {
        var domain = ADomain.Create();
        var model = new UninitializingInitializerModel();

        var exception = Assert.Throws<InvalidOperationException>(() => domain.RegisterModel(model));

        Assert.That(exception!.Message, Does.Contain("while it is initializing"));
        Assert.AreEqual(1, model.UninitializeCount);
        Assert.IsNull(domain.GetModel<UninitializingInitializerModel>());
        Assert.DoesNotThrow(() => domain.RegisterUtility(new SharedUtility()));
        domain.UnInitialize();
    }

    [Test]
    public void InitializationFailurePermanentlyPreventsComponentReuse()
    {
        var firstDomain = ADomain.Create();
        var secondDomain = BDomain.Create();
        var model = new AlwaysFailingModel();

        Assert.Throws<InvalidOperationException>(() => firstDomain.RegisterModel(model));
        Assert.AreEqual(1, model.UninitializeCount);
        Assert.Throws<InvalidOperationException>(() => secondDomain.RegisterModel(model));

        firstDomain.UnInitialize();
        secondDomain.UnInitialize();
    }

    [Test]
    public void TeardownUsesCategoryAndReverseActivationOrder()
    {
        var order = new List<string>();
        var domain = ADomain.Create();

        domain.RegisterModel(new RecordingModel("model-1", order));
        domain.RegisterModelAs<IRecordingModel>(new RecordingModel("model-2", order));
        domain.RegisterSystem(new RecordingSystem("system-1", order));
        domain.RegisterSystemAs<IRecordingSystem>(new RecordingSystem("system-2", order));

        domain.UnInitialize();

        CollectionAssert.AreEqual(
            new[] { "system-2", "system-1", "model-2", "model-1" },
            order);
    }

    [Test]
    public void TeardownReleasesOwnedChildrenBeforeLocalComponents()
    {
        var order = new List<string>();
        var parent = ADomain.Create();
        var child = RecordingChildDomain.Create(order);

        parent.AddChild(child);
        parent.RegisterSystem(new RecordingSystem("parent-system", order));
        parent.UnInitialize();

        CollectionAssert.AreEqual(new[] { "child-domain", "parent-system" }, order);
    }

    [Test]
    public void CleanupUnpublishesCurrentComponentButKeepsLaterDependenciesReadable()
    {
        var domain = ADomain.Create();
        var dependency = new DependencyModel();
        var observer = new CleanupVisibilityModel();

        domain.RegisterModel(dependency);
        domain.RegisterModel(observer);
        domain.UnInitialize();

        Assert.IsTrue(observer.SelfWasAbsent);
        Assert.IsTrue(observer.DependencyWasPresent);
    }

    [Test]
    public void DerivedUnInitCannotRepopulateDisposedDomain()
    {
        var domain = RepopulatingDomain.Create();

        domain.UnInitialize();

        Assert.That(domain.RegistrationFailure, Is.TypeOf<InvalidOperationException>());
        Assert.That(domain.EventFailure, Is.TypeOf<InvalidOperationException>());
        Assert.IsNull(domain.GetUtility<SharedUtility>());
        Assert.DoesNotThrow(domain.UnInitialize);
    }

    [Test]
    public void CleanupFailureStillDisposesAndPermanentlyReleasesComponent()
    {
        var firstDomain = ADomain.Create();
        var secondDomain = BDomain.Create();
        var model = new ThrowingCleanupModel();
        firstDomain.RegisterModel(model);

        var exception = Assert.Throws<AggregateException>(firstDomain.UnInitialize);

        Assert.That(exception!.Flatten().InnerExceptions.Any(item => item.Message == "cleanup failed"));
        Assert.IsNull(firstDomain.GetModel<ThrowingCleanupModel>());
        Assert.Throws<InvalidOperationException>(() => firstDomain.RegisterModel(new ThrowingCleanupModel()));
        Assert.Throws<InvalidOperationException>(() => secondDomain.RegisterModel(model));
        secondDomain.UnInitialize();
    }

    [Test]
    public void TeardownContinuesAfterMultipleComponentFailures()
    {
        var domain = ADomain.Create();
        var system = new ThrowingCleanupSystem();
        var model = new ThrowingCleanupModel();
        domain.RegisterSystem(system);
        domain.RegisterModel(model);

        var exception = Assert.Throws<AggregateException>(domain.UnInitialize);
        var messages = exception!.Flatten().InnerExceptions.Select(item => item.Message).ToArray();

        CollectionAssert.Contains(messages, "system cleanup failed");
        CollectionAssert.Contains(messages, "cleanup failed");
        Assert.AreEqual(1, system.UninitializeCount);
        Assert.AreEqual(1, model.UninitializeCount);
        Assert.DoesNotThrow(domain.UnInitialize);
    }

    [Test]
    public void PublicContainerRemainsExactKeyOnly()
    {
        var container = new Container();
        container.Register(new PlayerOne());

        Assert.IsNotNull(container.Get<PlayerOne>());
        Assert.IsNull(container.Get<IPlayerService>());
    }

    private static void AssertForbiddenInitialization(ForbiddenAction action)
    {
        var domain = ADomain.Create();
        var model = new ForbiddenInitializerModel(action);
        if (action == ForbiddenAction.EventUnregistration)
        {
            domain.RegisterEvent(model.EventHandler);
        }

        Assert.Throws<InvalidOperationException>(() => domain.RegisterModel(model));
        domain.SendEvent(new TransactionEvent());
        Assert.AreEqual(0, model.Command.ExecutionCount);
        Assert.AreEqual(action == ForbiddenAction.EventUnregistration ? 1 : 0, model.EventCalls);
        Assert.IsNull(model.Child.Parent);
        Assert.IsNull(domain.GetModel<ForbiddenInitializerModel>());
        domain.UnInitialize();
        model.Child.UnInitialize();
    }

    public sealed class ReentrantAccessDomain : AbstractDomain<ReentrantAccessDomain>
    {
        public static bool Reenter { get; set; }
        public static ReentrantAccessDomain? LastCreated { get; private set; }
        public static ReentrantAccessDomain? LastFailed { get; private set; }

        public ReentrantAccessDomain()
        {
            LastCreated = this;
        }

        protected override void Init()
        {
            if (!Reenter)
            {
                return;
            }

            LastFailed = this;
            _ = Instance;
        }
    }

    public sealed class UninitializingDomain : AbstractDomain<UninitializingDomain>
    {
        public static UninitializingDomain? LastCreated { get; private set; }

        public UninitializingDomain()
        {
            LastCreated = this;
        }

        protected override void Init() => UnInitialize();
    }

    public sealed class RepopulatingDomain : AbstractDomain<RepopulatingDomain>
    {
        public Exception? RegistrationFailure { get; private set; }
        public Exception? EventFailure { get; private set; }

        protected override void Init() { }

        protected override void UnInit()
        {
            try
            {
                RegisterUtility(new SharedUtility());
            }
            catch (Exception exception)
            {
                RegistrationFailure = exception;
            }

            try
            {
                RegisterEvent<int>(_ => { });
            }
            catch (Exception exception)
            {
                EventFailure = exception;
            }
        }
    }

    private sealed class RecordingChildDomain : AbstractDomain
    {
        private readonly List<string> _order;

        private RecordingChildDomain(List<string> order)
        {
            _order = order;
        }

        public static RecordingChildDomain Create(List<string> order)
        {
            var domain = new RecordingChildDomain(order);
            domain.Initialize();
            return domain;
        }

        protected override void Init() { }
        protected override void UnInit() => _order.Add("child-domain");
    }

    private interface IPlayerService : IModel { }

    private sealed class PlayerOne : AbstractModel, IPlayerService
    {
        protected override void OnInitialize() { }
    }

    private sealed class PlayerTwo : AbstractModel, IPlayerService
    {
        protected override void OnInitialize() { }
    }

    private sealed class PlayerThree : AbstractModel, IPlayerService
    {
        protected override void OnInitialize() { }
    }

    private sealed class SharedUtility : IUtility { }

    private sealed class MultiRoleUtility : IUtility, ISystem, IModel
    {
        public IDomain Domain { get; private set; } = default!;
        public int InitializeCount { get; private set; }
        public int UninitializeCount { get; private set; }

        public void SetDomain(IDomain domain) => Domain = domain;
        public void Initialize() => InitializeCount++;
        public void UnInitialize() => UninitializeCount++;
    }

    private sealed class NestedSystem : AbstractSystem
    {
        private readonly FailingTransactionModel _owner;

        public NestedSystem(FailingTransactionModel owner) => _owner = owner;

        public int UninitializeCount { get; private set; }
        protected override void OnInitialize() { }

        protected override void OnUninitialize()
        {
            UninitializeCount++;
            if (_owner.ThrowDuringNestedCleanup)
            {
                throw new InvalidOperationException("nested cleanup failed");
            }
        }
    }

    private sealed class NestedModel : AbstractModel
    {
        public int UninitializeCount { get; private set; }
        protected override void OnInitialize() { }
        protected override void OnUninitialize() => UninitializeCount++;
    }

    private sealed class FailingTransactionModel : AbstractModel
    {
        public FailingTransactionModel()
        {
            System = new NestedSystem(this);
        }

        public NestedSystem System { get; }
        public NestedModel Model { get; } = new();
        public SharedUtility Utility { get; } = new();
        public int EventCalls { get; private set; }
        public int UninitializeCount { get; private set; }
        public bool ThrowDuringOwnCleanup { get; init; }
        public bool ThrowDuringNestedCleanup { get; init; }

        protected override void OnInitialize()
        {
            Domain.RegisterSystem(System);
            Domain.RegisterModel(Model);
            Domain.RegisterUtility(Utility);
            Domain.RegisterEvent<TransactionEvent>(_ => EventCalls++);
            throw new InvalidOperationException("outer initialization failed");
        }

        protected override void OnUninitialize()
        {
            UninitializeCount++;
            if (ThrowDuringOwnCleanup)
            {
                throw new InvalidOperationException("outer cleanup failed");
            }
        }
    }

    private sealed class PermittedInitializerModel : AbstractModel
    {
        public SharedUtility Utility { get; } = new();
        public int QueryResult { get; private set; }
        public int EventCalls { get; private set; }

        protected override void OnInitialize()
        {
            QueryResult = Domain.SendQuery(new ConstantQuery());
            Domain.RegisterUtility(Utility);
            Domain.RegisterEvent<TransactionEvent>(_ => EventCalls++);
        }
    }

    private sealed class ConstantQuery : AbstractQuery<int>
    {
        protected override int OnExecute() => 42;
    }

    private sealed class FlagCommand : AbstractCommand
    {
        public int ExecutionCount { get; private set; }
        protected override void OnExecute() => ExecutionCount++;
    }

    private sealed class ForbiddenInitializerModel : AbstractModel
    {
        private readonly ForbiddenAction _action;

        public ForbiddenInitializerModel(ForbiddenAction action)
        {
            _action = action;
        }

        public FlagCommand Command { get; } = new();
        public ADomain Child { get; } = ADomain.Create();
        public int EventCalls { get; private set; }
        public Action<TransactionEvent> EventHandler => OnEvent;

        protected override void OnInitialize()
        {
            switch (_action)
            {
                case ForbiddenAction.Command:
                    Domain.SendCommand(Command);
                    break;
                case ForbiddenAction.Event:
                    Domain.SendEvent(new TransactionEvent());
                    break;
                case ForbiddenAction.EventUnregistration:
                    Domain.UnRegisterEvent(EventHandler);
                    break;
                case ForbiddenAction.Relationship:
                    Domain.AddChild(Child);
                    break;
            }
        }

        private void OnEvent(TransactionEvent _) => EventCalls++;
    }

    private enum ForbiddenAction
    {
        Command,
        Event,
        EventUnregistration,
        Relationship
    }

    private sealed class ReplaceTargetModel : AbstractModel
    {
        public int UninitializeCount { get; private set; }
        protected override void OnInitialize() { }
        protected override void OnUninitialize() => UninitializeCount++;
    }

    private sealed class ReplacingInitializerModel : AbstractModel
    {
        protected override void OnInitialize() => Domain.RegisterModel(new ReplaceTargetModel());
    }

    private sealed class SelfRegisteringModel : AbstractModel
    {
        public int UninitializeCount { get; private set; }
        protected override void OnInitialize() => Domain.RegisterModel(this);
        protected override void OnUninitialize() => UninitializeCount++;
    }

    private sealed class UninitializingInitializerModel : AbstractModel
    {
        public int UninitializeCount { get; private set; }
        protected override void OnInitialize() => Domain.UnInitialize();
        protected override void OnUninitialize() => UninitializeCount++;
    }

    private sealed class AlwaysFailingModel : AbstractModel
    {
        public int UninitializeCount { get; private set; }

        protected override void OnInitialize() => throw new InvalidOperationException("initialization failed");
        protected override void OnUninitialize() => UninitializeCount++;
    }

    private interface IRecordingModel : IModel { }
    private interface IRecordingSystem : ISystem { }

    private sealed class RecordingModel : AbstractModel, IRecordingModel
    {
        private readonly string _name;
        private readonly List<string> _order;

        public RecordingModel(string name, List<string> order)
        {
            _name = name;
            _order = order;
        }

        protected override void OnInitialize() { }
        protected override void OnUninitialize() => _order.Add(_name);
    }

    private sealed class RecordingSystem : AbstractSystem, IRecordingSystem
    {
        private readonly string _name;
        private readonly List<string> _order;

        public RecordingSystem(string name, List<string> order)
        {
            _name = name;
            _order = order;
        }

        protected override void OnInitialize() { }
        protected override void OnUninitialize() => _order.Add(_name);
    }

    private sealed class DependencyModel : AbstractModel
    {
        protected override void OnInitialize() { }
    }

    private sealed class CleanupVisibilityModel : AbstractModel
    {
        public bool SelfWasAbsent { get; private set; }
        public bool DependencyWasPresent { get; private set; }

        protected override void OnInitialize() { }

        protected override void OnUninitialize()
        {
            SelfWasAbsent = Domain.GetModel<CleanupVisibilityModel>() is null;
            DependencyWasPresent = Domain.GetModel<DependencyModel>() is not null;
        }
    }

    private sealed class ThrowingCleanupModel : AbstractModel
    {
        public int UninitializeCount { get; private set; }
        protected override void OnInitialize() { }
        protected override void OnUninitialize()
        {
            UninitializeCount++;
            throw new InvalidOperationException("cleanup failed");
        }
    }

    private sealed class ThrowingCleanupSystem : AbstractSystem
    {
        public int UninitializeCount { get; private set; }
        protected override void OnInitialize() { }

        protected override void OnUninitialize()
        {
            UninitializeCount++;
            throw new InvalidOperationException("system cleanup failed");
        }
    }

    private sealed class TransactionEvent { }
}
