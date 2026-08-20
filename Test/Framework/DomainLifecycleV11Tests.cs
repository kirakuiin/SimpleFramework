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
    public void EventListenerCannotUninitializeDomainDuringDispatch()
    {
        var domain = ADomain.Create();
        Exception? teardownFailure = null;
        var laterListenerCalls = 0;
        domain.RegisterEvent<TransactionEvent>(_ =>
        {
            try
            {
                domain.UnInitialize();
            }
            catch (Exception exception)
            {
                teardownFailure = exception;
            }
        });
        domain.RegisterEvent<TransactionEvent>(_ => laterListenerCalls++);

        domain.SendEvent(new TransactionEvent());

        Assert.That(teardownFailure, Is.TypeOf<InvalidOperationException>());
        Assert.AreEqual(1, laterListenerCalls);
        Assert.DoesNotThrow(() => domain.RegisterUtility(new SharedUtility()));
        domain.UnInitialize();
    }

    [Test]
    public void CommandExecutionHooksCannotUninitializeDomain()
    {
        var domain = ExecutionOverrideDomain.Create();
        var command = new FlagCommand();
        var resultCommand = new CountingResultCommand();

        Assert.Throws<InvalidOperationException>(() => domain.SendCommand(command));
        Assert.Throws<InvalidOperationException>(() => domain.SendCommand(resultCommand));

        Assert.AreEqual(0, command.ExecutionCount);
        Assert.AreEqual(0, resultCommand.ExecutionCount);
        Assert.DoesNotThrow(() => domain.RegisterUtility(new SharedUtility()));
        domain.UnInitialize();
    }

    [Test]
    public void QueryExecutionHookCannotUninitializeDomain()
    {
        var domain = ExecutionOverrideDomain.Create();

        Assert.Throws<InvalidOperationException>(() => domain.SendQuery(new ConstantQuery()));

        Assert.DoesNotThrow(() => domain.RegisterUtility(new SharedUtility()));
        domain.UnInitialize();
    }

    [Test]
    public void ExecutionDepthRecoversAfterEventCommandAndQueryExceptions()
    {
        AssertExecutionFailureAllowsTeardown(domain =>
        {
            domain.RegisterEvent<TransactionEvent>(_ => throw new ExecutionProbeException());
            domain.SendEvent(new TransactionEvent());
        });
        AssertExecutionFailureAllowsTeardown(domain => domain.SendCommand(new ThrowingCommand()));
        AssertExecutionFailureAllowsTeardown(domain => domain.SendQuery(new ThrowingQuery()));
    }

    [Test]
    public void EventCommandAndQueryExecutionCanNestSynchronously()
    {
        var domain = ADomain.Create();
        var command = new FlagCommand();
        var queryResult = 0;
        domain.RegisterEvent<TransactionEvent>(_ =>
        {
            domain.SendCommand(command);
            queryResult = domain.SendQuery(new ConstantQuery());
        });

        domain.SendEvent(new TransactionEvent());

        Assert.AreEqual(1, command.ExecutionCount);
        Assert.AreEqual(42, queryResult);
        domain.UnInitialize();
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
        Assert.AreEqual(0, outer.OwnedEventCalls);
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
    public void BindDomainCannotUninitializeActiveDomain()
    {
        var domain = ADomain.Create();
        var model = new ForbiddenBindingModel(ForbiddenAction.Uninitialize);

        var exception = Assert.Throws<InvalidOperationException>(() => domain.RegisterModel(model));

        Assert.That(exception!.Message, Does.Contain("while it is initializing"));
        Assert.AreEqual(0, model.InitializeCount);
        Assert.AreEqual(0, model.UninitializeCount);
        Assert.IsNull(domain.GetModel<ForbiddenBindingModel>());
        Assert.DoesNotThrow(() => domain.RegisterUtility(new SharedUtility()));
        domain.UnInitialize();
        model.Child.UnInitialize();
    }

    [Test]
    public void BindDomainUsesComponentInitializationGuards()
    {
        foreach (var action in new[]
                 {
                     ForbiddenAction.Command,
                     ForbiddenAction.Event,
                     ForbiddenAction.EventUnregistration,
                     ForbiddenAction.Relationship,
                     ForbiddenAction.Replacement
                 })
        {
            AssertForbiddenBinding(action);
        }
    }

    [Test]
    public void BindDomainAllowsQueriesNestedRegistrationAndOwnedSubscription()
    {
        var domain = ADomain.Create();
        var system = new PermittedBindingSystem();

        domain.RegisterSystem(system);
        domain.SendEvent(new TransactionEvent());

        Assert.AreEqual(42, system.QueryResult);
        Assert.AreSame(system.Utility, domain.GetUtility<SharedUtility>());
        Assert.AreEqual(1, system.EventCalls);
        Assert.AreEqual(1, system.InitializeCount);
        domain.UnInitialize();
        Assert.AreEqual(1, system.UninitializeCount);
    }

    private static void AssertForbiddenBinding(ForbiddenAction action)
    {
        var domain = ADomain.Create();
        var model = new ForbiddenBindingModel(action);
        var existing = new ReplaceTargetModel();
        if (action == ForbiddenAction.EventUnregistration)
        {
            domain.RegisterEvent(model.EventHandler);
        }

        if (action == ForbiddenAction.Replacement)
        {
            domain.RegisterModel(existing);
        }

        Assert.Throws<InvalidOperationException>(() => domain.RegisterModel(model));
        domain.SendEvent(new TransactionEvent());

        Assert.AreEqual(0, model.InitializeCount);
        Assert.AreEqual(0, model.UninitializeCount);
        Assert.AreEqual(0, model.Command.ExecutionCount);
        Assert.AreEqual(action == ForbiddenAction.EventUnregistration ? 1 : 0, model.EventCalls);
        Assert.IsNull(model.Child.Parent);
        Assert.IsNull(domain.GetModel<ForbiddenBindingModel>());
        Assert.AreSame(action == ForbiddenAction.Replacement ? existing : null, domain.GetModel<ReplaceTargetModel>());
        Assert.AreEqual(0, existing.UninitializeCount);
        Assert.DoesNotThrow(() => domain.RegisterUtility(new SharedUtility()));
        domain.UnInitialize();
        model.Child.UnInitialize();
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

    [Test]
    public void LifecycleBindingIsOneShotWhileCommandsAndQueriesCanRebind()
    {
        var firstDomain = ADomain.Create();
        var secondDomain = BDomain.Create();
        var model = new BindingProbeModel();

        Assert.Throws<InvalidOperationException>(() => _ = model.Domain);
        firstDomain.RegisterModel(model);
        Assert.AreSame(firstDomain, model.Domain);
        Assert.Throws<InvalidOperationException>(() => ((IDomainBindable)model).BindDomain(firstDomain));
        Assert.Throws<InvalidOperationException>(() => ((IDomainBindable)model).BindDomain(secondDomain));

        var command = new DomainRecordingCommand();
        var query = new DomainRecordingQuery();
        firstDomain.SendCommand(command);
        firstDomain.SendQuery(query);
        secondDomain.SendCommand(command);
        secondDomain.SendQuery(query);

        Assert.AreSame(secondDomain, command.LastDomain);
        Assert.AreSame(secondDomain, query.LastDomain);

        firstDomain.UnInitialize();
        Assert.Throws<InvalidOperationException>(() => ((IDomainBindable)model).BindDomain(secondDomain));
        secondDomain.UnInitialize();
    }

    [Test]
    public void PublicBindingContractSupportsDirectDomainAndComponentImplementations()
    {
        var customDomain = new BindingOnlyDomain();
        var frameworkModel = new BindingProbeModel();
        var frameworkDomain = ADomain.Create();
        var directSystem = new DirectBindingComponent();
        var directModel = new DirectBindingComponent();

        customDomain.RegisterModel(frameworkModel);
        frameworkDomain.RegisterSystem(directSystem);
        frameworkDomain.RegisterModel(directModel);

        Assert.AreSame(customDomain, frameworkModel.Domain);
        Assert.AreSame(frameworkDomain, directSystem.Domain);
        Assert.AreSame(frameworkDomain, directModel.Domain);
        Assert.AreEqual(1, directSystem.InitializeCount);
        Assert.AreEqual(1, directModel.InitializeCount);
        frameworkDomain.UnInitialize();
    }

    [Test]
    public void ReplacingSystemCancelsItsOwnedLocalEventSubscription()
    {
        var domain = ADomain.Create();
        var oldSystem = new SubscribedSystem();
        var replacement = new SubscribedSystem();

        domain.RegisterSystemAs<ISubscribedSystem>(oldSystem);
        domain.SendEvent(new TransactionEvent());
        domain.RegisterSystemAs<ISubscribedSystem>(replacement);
        domain.SendEvent(new TransactionEvent());

        Assert.AreEqual(1, oldSystem.EventCalls);
        Assert.AreEqual(1, replacement.EventCalls);
        domain.UnInitialize();
    }

    [Test]
    public void OwnedSubscriptionCanBeCancelledEarlyAndCleanupStillCompletes()
    {
        var domain = ADomain.Create();
        var system = new SubscribedSystem();

        domain.RegisterSystemAs<ISubscribedSystem>(system);
        system.Subscription!.UnRegister();
        domain.SendEvent(new TransactionEvent());

        Assert.AreEqual(0, system.EventCalls);
        Assert.DoesNotThrow(domain.UnInitialize);
    }

    [Test]
    public void ReturnedUnregisterHandlesCannotBypassComponentInitializationGuards()
    {
        foreach (var ownership in new[] { SubscriptionOwnership.Domain, SubscriptionOwnership.System })
        foreach (var cancellation in new[] { CancellationMethod.UnRegister, CancellationMethod.Dispose })
        foreach (var phase in new[] { CancellationPhase.BindDomain, CancellationPhase.Initialize })
        {
            AssertGuardedSubscriptionHandle(ownership, cancellation, phase);
        }
    }

    [Test]
    public void ThrowingComponentCleanupStillCancelsOwnedSubscriptions()
    {
        var domain = ADomain.Create();
        var oldSystem = new ThrowingCleanupSubscribedSystem();

        domain.RegisterSystemAs<ISubscribedSystem>(oldSystem);
        Assert.Throws<InvalidOperationException>(
            () => domain.RegisterSystemAs<ISubscribedSystem>(new SubscribedSystem()));
        domain.SendEvent(new TransactionEvent());

        Assert.AreEqual(0, oldSystem.EventCalls);
        domain.UnInitialize();
    }

    [Test]
    public void DomainScopedSubscriptionOutlivesUnrelatedSystemReplacement()
    {
        var domain = ADomain.Create();
        var calls = 0;
        domain.RegisterEvent<TransactionEvent>(_ => calls++);
        domain.RegisterSystemAs<ISubscribedSystem>(new SubscribedSystem());
        domain.RegisterSystemAs<ISubscribedSystem>(new SubscribedSystem());

        domain.SendEvent(new TransactionEvent());

        Assert.AreEqual(1, calls);
        domain.UnInitialize();
    }

    [Test]
    public void EventDispatchRejectsSubscribedSystemReplacementBeforeRelease()
    {
        var domain = ADomain.Create();
        var oldSystem = new ExecutionSubscribedSystem();
        var replacement = new ExecutionSubscribedSystem();
        Exception? replacementFailure = null;
        var replacementAttempt = domain.RegisterEvent<TransactionEvent>(_ =>
        {
            try
            {
                domain.RegisterSystemAs<IExecutionSubscribedSystem>(replacement);
            }
            catch (Exception exception)
            {
                replacementFailure = exception;
            }
        });
        domain.RegisterSystemAs<IExecutionSubscribedSystem>(oldSystem);

        domain.SendEvent(new TransactionEvent());

        Assert.That(replacementFailure, Is.TypeOf<InvalidOperationException>());
        Assert.AreSame(oldSystem, domain.GetSystem<IExecutionSubscribedSystem>());
        Assert.AreEqual(0, oldSystem.UninitializeCount);
        Assert.AreEqual(1, oldSystem.EventCalls);

        replacementAttempt.UnRegister();
        domain.RegisterSystemAs<IExecutionSubscribedSystem>(replacement);
        domain.SendEvent(new TransactionEvent());
        Assert.AreEqual(1, oldSystem.UninitializeCount);
        Assert.AreEqual(1, oldSystem.EventCalls);
        Assert.AreEqual(1, replacement.EventCalls);
        domain.UnInitialize();
    }

    [Test]
    public void CommandAndQueryRejectModelReplacementBeforeRelease()
    {
        var domain = ADomain.Create();
        var oldModel = new ExecutionReplaceableModel();
        var commandReplacement = new ExecutionReplaceableModel();
        var queryReplacement = new ExecutionReplaceableModel();
        domain.RegisterModelAs<IExecutionReplaceableModel>(oldModel);

        var command = new ReplacingExecutionCommand(commandReplacement);
        var query = new ReplacingExecutionQuery(queryReplacement);
        domain.SendCommand(command);
        domain.SendQuery(query);

        Assert.That(command.ReplacementFailure, Is.TypeOf<InvalidOperationException>());
        Assert.That(query.ReplacementFailure, Is.TypeOf<InvalidOperationException>());
        Assert.AreSame(oldModel, domain.GetModel<IExecutionReplaceableModel>());
        Assert.AreEqual(0, oldModel.UninitializeCount);

        domain.RegisterModelAs<IExecutionReplaceableModel>(queryReplacement);
        Assert.AreEqual(1, oldModel.UninitializeCount);
        domain.UnInitialize();
    }

    [Test]
    public void ExecutionStillAllowsNewLifecycleKeysAndUtilityReplacement()
    {
        var domain = ADomain.Create();
        var oldUtility = new SharedUtility();
        var newUtility = new SharedUtility();
        var newModel = new ReplaceTargetModel();
        domain.RegisterUtility(oldUtility);
        domain.RegisterEvent<TransactionEvent>(_ =>
        {
            domain.RegisterModel(newModel);
            domain.RegisterUtility(newUtility);
        });

        domain.SendEvent(new TransactionEvent());

        Assert.AreSame(newModel, domain.GetModel<ReplaceTargetModel>());
        Assert.AreSame(newUtility, domain.GetUtility<SharedUtility>());
        domain.UnInitialize();
    }

    [Test]
    public void CaughtNestedRegistrationFailurePoisonsAndRollsBackTheWholeTransaction()
    {
        var domain = ADomain.Create();
        var outer = new CatchingFailureModel();

        var exception = Assert.Throws<InvalidOperationException>(() => domain.RegisterModel(outer));

        Assert.That(exception!.Message, Is.EqualTo("nested initialization failed"));
        Assert.IsNotNull(outer.CaughtFailure);
        Assert.IsNotNull(outer.PostFailure);
        Assert.IsNull(domain.GetModel<CatchingFailureModel>());
        Assert.IsNull(domain.GetModel<SuccessfulNestedModel>());
        Assert.IsNull(domain.GetUtility<SharedUtility>());
        domain.SendEvent(new TransactionEvent());
        Assert.AreEqual(0, outer.EventCalls);
        Assert.DoesNotThrow(() => domain.RegisterUtility(new SharedUtility()));
        domain.UnInitialize();
    }

    [Test]
    public void CaughtNestedPreflightFailurePoisonsAndRollsBackTheWholeTransaction()
    {
        var domain = ADomain.Create();
        var outer = new CatchingSelfRegistrationModel();

        Assert.Throws<InvalidOperationException>(() => domain.RegisterModel(outer));

        Assert.That(outer.CaughtFailure, Is.TypeOf<InvalidOperationException>());
        Assert.IsNull(domain.GetModel<CatchingSelfRegistrationModel>());
        Assert.DoesNotThrow(() => domain.RegisterUtility(new SharedUtility()));
        domain.UnInitialize();
    }

    [Test]
    public void BindDomainFailureCancelsOwnedSubscriptionBeforeInitializationStarts()
    {
        var domain = ADomain.Create();
        var system = new FailingBindingSubscribedSystem();

        Assert.Throws<InvalidOperationException>(() => domain.RegisterSystem(system));
        domain.SendEvent(new TransactionEvent());

        Assert.AreEqual(0, system.EventCalls);
        Assert.AreEqual(0, system.InitializeCount);
        Assert.AreEqual(0, system.UninitializeCount);
        domain.UnInitialize();
    }

    [Test]
    public void FailedComponentTransactionCancelsSubscriptionAddedByExistingSystem()
    {
        var domain = ADomain.Create();
        var system = new TransactionSubscriptionSystem();
        domain.RegisterSystem(system);

        Assert.Throws<InvalidOperationException>(() =>
            domain.RegisterModel(new ExistingSystemSubscriptionModel(system) { FailInitialization = true }));
        domain.SendEvent(new TransactionEvent());

        Assert.AreEqual(0, system.EventCalls);
        Assert.AreSame(system, domain.GetSystem<TransactionSubscriptionSystem>());
        domain.UnInitialize();
    }

    [Test]
    public void SuccessfulComponentTransactionKeepsSubscriptionAddedByExistingSystem()
    {
        var domain = ADomain.Create();
        var system = new TransactionSubscriptionSystem();
        domain.RegisterSystem(system);

        domain.RegisterModel(new ExistingSystemSubscriptionModel(system));
        domain.SendEvent(new TransactionEvent());

        Assert.AreEqual(1, system.EventCalls);
        domain.UnInitialize();
    }

    [Test]
    public void AncestorTeardownPreflightPreservesTreeWhileDescendantExecutes()
    {
        var parent = ADomain.Create();
        var child = BDomain.Create();
        var grandchild = DDomain.Create();
        parent.AddChild(child);
        child.AddChild(grandchild);
        Exception? teardownFailure = null;
        grandchild.RegisterEvent<TransactionEvent>(_ =>
        {
            try
            {
                parent.UnInitialize();
            }
            catch (Exception exception)
            {
                teardownFailure = exception;
            }
        });

        grandchild.SendEvent(new TransactionEvent());

        Assert.That(teardownFailure, Is.TypeOf<InvalidOperationException>());
        Assert.AreSame(parent, child.Parent);
        Assert.AreSame(child, grandchild.Parent);
        Assert.DoesNotThrow(() => parent.RegisterUtility(new SharedUtility()));
        Assert.DoesNotThrow(() => child.RegisterUtility(new SharedUtility()));
        Assert.DoesNotThrow(() => grandchild.RegisterUtility(new SharedUtility()));

        parent.UnInitialize();
        Assert.IsNull(child.Parent);
        Assert.IsNull(grandchild.Parent);
        Assert.Throws<InvalidOperationException>(() => child.RegisterUtility(new SharedUtility()));
        Assert.Throws<InvalidOperationException>(() => grandchild.RegisterUtility(new SharedUtility()));
    }

    [Test]
    public void AncestorTeardownPreflightPreservesTreeDuringChildComponentInitialization()
    {
        var parent = ADomain.Create();
        var child = BDomain.Create();
        parent.AddChild(child);
        var model = new AncestorTeardownInitializerModel(parent);

        child.RegisterModel(model);

        Assert.That(model.TeardownFailure, Is.TypeOf<InvalidOperationException>());
        Assert.AreSame(parent, child.Parent);
        Assert.DoesNotThrow(() => parent.RegisterUtility(new SharedUtility()));
        Assert.DoesNotThrow(() => child.RegisterUtility(new SharedUtility()));
        parent.UnInitialize();
    }

    [Test]
    public void AncestorTeardownPreflightPreservesTreeDuringChildComponentRelease()
    {
        var parent = ADomain.Create();
        var child = BDomain.Create();
        parent.AddChild(child);
        var oldModel = new AncestorTeardownCleanupModel(parent);
        var replacement = new AncestorTeardownCleanupModel(parent);
        child.RegisterModelAs<IAncestorTeardownCleanupModel>(oldModel);

        child.RegisterModelAs<IAncestorTeardownCleanupModel>(replacement);

        Assert.That(oldModel.TeardownFailure, Is.TypeOf<InvalidOperationException>());
        Assert.AreSame(parent, child.Parent);
        Assert.AreSame(replacement, child.GetModel<IAncestorTeardownCleanupModel>());
        Assert.DoesNotThrow(() => parent.RegisterUtility(new SharedUtility()));
        parent.UnInitialize();
    }

    [Test]
    public void AncestorTeardownPreflightRejectsUninitializingDescendant()
    {
        var parent = ADomain.Create();
        var utility = new SharedUtility();
        parent.RegisterUtility(utility);
        var child = BDomain.Create();
        var model = new AncestorTeardownCleanupModel(parent, utility);
        child.RegisterModel(model);
        parent.AddChild(child);

        child.UnInitialize();

        Assert.That(model.TeardownFailure, Is.TypeOf<InvalidOperationException>());
        Assert.IsNull(model.AncestorAccessFailure);
        Assert.AreSame(utility, model.AncestorUtilityAfterAttempt);
        Assert.DoesNotThrow(() => parent.RegisterUtility(new SharedUtility()));
        parent.UnInitialize();
    }

    [Test]
    public void SetParentKeepsOwnedRelationshipWhenOldParentRejectsMutation()
    {
        var oldParent = ADomain.Create();
        var child = BDomain.Create();
        var newParent = DDomain.Create();
        oldParent.AddChild(child);
        var system = new ReparentingInitializerSystem(child, newParent);

        oldParent.RegisterSystem(system);

        Assert.That(system.ReparentFailure, Is.TypeOf<InvalidOperationException>());
        Assert.AreSame(oldParent, child.Parent);
        oldParent.UnInitialize();
        newParent.UnInitialize();
    }

    [Test]
    public void RemoveChildKeepsOwnershipWhenChildRejectsParentMutation()
    {
        var parent = ADomain.Create();
        var child = BDomain.Create();
        parent.AddChild(child);
        var model = new RemovingOwnershipDuringInitializationModel(parent);

        child.RegisterModel(model);

        Assert.That(model.RemoveFailure, Is.TypeOf<InvalidOperationException>());
        Assert.AreSame(parent, child.Parent);
        parent.UnInitialize();
        Assert.IsNull(child.Parent);
        Assert.Throws<InvalidOperationException>(() => child.RegisterUtility(new SharedUtility()));
    }

    [Test]
    public void SetParentDetachesOwnedChildBeforeCommittingNewParent()
    {
        var oldParent = ADomain.Create();
        var child = BDomain.Create();
        var newParent = DDomain.Create();
        oldParent.AddChild(child);

        child.SetParent(newParent);
        oldParent.UnInitialize();

        Assert.AreSame(newParent, child.Parent);
        Assert.DoesNotThrow(() => child.RegisterUtility(new SharedUtility()));
        child.UnInitialize();
        newParent.UnInitialize();
    }

    [Test]
    public void SetParentChangesLookupOnlyRelationshipWithoutCreatingOwnership()
    {
        var oldParent = ADomain.Create();
        var child = BDomain.Create();
        var newParent = DDomain.Create();

        child.SetParent(oldParent);
        child.SetParent(newParent);
        oldParent.UnInitialize();

        Assert.AreSame(newParent, child.Parent);
        Assert.DoesNotThrow(() => child.RegisterUtility(new SharedUtility()));
        child.UnInitialize();
        newParent.UnInitialize();
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

    private static void AssertExecutionFailureAllowsTeardown(Action<ADomain> execute)
    {
        var domain = ADomain.Create();

        Assert.Throws<ExecutionProbeException>(() => execute(domain));
        Assert.DoesNotThrow(domain.UnInitialize);
    }

    private static void AssertGuardedSubscriptionHandle(
        SubscriptionOwnership ownership,
        CancellationMethod cancellation,
        CancellationPhase phase)
    {
        var domain = ADomain.Create();
        var eventCalls = 0;
        IUnRegister subscription;
        if (ownership == SubscriptionOwnership.System)
        {
            var system = new ExistingSubscriptionSystem(() => eventCalls++);
            domain.RegisterSystem(system);
            subscription = system.Subscription!;
        }
        else
        {
            subscription = domain.RegisterEvent<TransactionEvent>(_ => eventCalls++);
        }

        var model = new HandleCancellingModel(subscription, cancellation, phase);
        Assert.Throws<InvalidOperationException>(() => domain.RegisterModel(model));
        domain.SendEvent(new TransactionEvent());

        Assert.AreEqual(1, eventCalls, $"{ownership}, {cancellation}, {phase}");
        Assert.IsNull(domain.GetModel<HandleCancellingModel>());
        Assert.AreEqual(phase == CancellationPhase.Initialize ? 1 : 0, model.InitializeCount);
        Assert.AreEqual(phase == CancellationPhase.Initialize ? 1 : 0, model.UninitializeCount);

        Cancel(subscription, cancellation);
        domain.SendEvent(new TransactionEvent());
        Assert.AreEqual(1, eventCalls, $"retry: {ownership}, {cancellation}, {phase}");
        domain.UnInitialize();
    }

    private static void Cancel(IUnRegister subscription, CancellationMethod cancellation)
    {
        if (cancellation == CancellationMethod.Dispose)
        {
            subscription.Dispose();
        }
        else
        {
            subscription.UnRegister();
        }
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

    private sealed class ExecutionOverrideDomain : AbstractDomain
    {
        public static ExecutionOverrideDomain Create()
        {
            var domain = new ExecutionOverrideDomain();
            domain.Initialize();
            return domain;
        }

        protected override void Init() { }

        protected override void ExecuteCommand<TCommand>(TCommand command)
        {
            UnInitialize();
            base.ExecuteCommand(command);
        }

        protected override TResult ExecuteCommand<TResult>(ICommand<TResult> command)
        {
            UnInitialize();
            return base.ExecuteCommand(command);
        }

        protected override TResult ExecuteQuery<TResult>(IQuery<TResult> query)
        {
            UnInitialize();
            return base.ExecuteQuery(query);
        }
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

        public void BindDomain(IDomain domain) => Domain = domain;
        public void Initialize() => InitializeCount++;
        public void UnInitialize() => UninitializeCount++;
    }

    private sealed class NestedSystem : AbstractSystem
    {
        private readonly FailingTransactionModel _owner;

        public NestedSystem(FailingTransactionModel owner) => _owner = owner;

        public int UninitializeCount { get; private set; }
        protected override void OnInitialize() => this.RegisterEvent<TransactionEvent>(_ => _owner.RecordOwnedEvent());

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
        public int OwnedEventCalls { get; private set; }
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

        public void RecordOwnedEvent() => OwnedEventCalls++;
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

    private sealed class CountingResultCommand : AbstractCommand<int>
    {
        public int ExecutionCount { get; private set; }

        protected override int OnExecute()
        {
            ExecutionCount++;
            return 42;
        }
    }

    private sealed class ThrowingCommand : AbstractCommand
    {
        protected override void OnExecute() => throw new ExecutionProbeException();
    }

    private sealed class ThrowingQuery : AbstractQuery<int>
    {
        protected override int OnExecute() => throw new ExecutionProbeException();
    }

    private sealed class ReplacingExecutionCommand : AbstractCommand
    {
        private readonly IExecutionReplaceableModel _replacement;

        public ReplacingExecutionCommand(IExecutionReplaceableModel replacement) => _replacement = replacement;

        public Exception? ReplacementFailure { get; private set; }

        protected override void OnExecute()
        {
            try
            {
                Domain.RegisterModelAs<IExecutionReplaceableModel>(_replacement);
            }
            catch (Exception exception)
            {
                ReplacementFailure = exception;
            }
        }
    }

    private sealed class ReplacingExecutionQuery : AbstractQuery<int>
    {
        private readonly IExecutionReplaceableModel _replacement;

        public ReplacingExecutionQuery(IExecutionReplaceableModel replacement) => _replacement = replacement;

        public Exception? ReplacementFailure { get; private set; }

        protected override int OnExecute()
        {
            try
            {
                Domain.RegisterModelAs<IExecutionReplaceableModel>(_replacement);
            }
            catch (Exception exception)
            {
                ReplacementFailure = exception;
            }

            return 0;
        }
    }

    private sealed class ExecutionProbeException : Exception { }

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

    private sealed class ForbiddenBindingModel : IModel
    {
        private readonly ForbiddenAction _action;

        public ForbiddenBindingModel(ForbiddenAction action)
        {
            _action = action;
        }

        public IDomain Domain { get; private set; } = default!;
        public FlagCommand Command { get; } = new();
        public ADomain Child { get; } = ADomain.Create();
        public int EventCalls { get; private set; }
        public int InitializeCount { get; private set; }
        public int UninitializeCount { get; private set; }
        public Action<TransactionEvent> EventHandler => OnEvent;

        public void BindDomain(IDomain domain)
        {
            Domain = domain;
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
                case ForbiddenAction.Uninitialize:
                    Domain.UnInitialize();
                    break;
                case ForbiddenAction.Replacement:
                    Domain.RegisterModel(new ReplaceTargetModel());
                    break;
            }
        }

        public void Initialize() => InitializeCount++;
        public void UnInitialize() => UninitializeCount++;

        private void OnEvent(TransactionEvent _) => EventCalls++;
    }

    private sealed class PermittedBindingSystem : ISystem
    {
        private IDomain? _domain;

        public IDomain Domain => _domain ?? throw new InvalidOperationException("System has not been bound.");
        public SharedUtility Utility { get; } = new();
        public int QueryResult { get; private set; }
        public int EventCalls { get; private set; }
        public int InitializeCount { get; private set; }
        public int UninitializeCount { get; private set; }

        public void BindDomain(IDomain domain)
        {
            _domain = domain;
            QueryResult = Domain.SendQuery(new ConstantQuery());
            Domain.RegisterUtility(Utility);
            this.RegisterEvent<TransactionEvent>(_ => EventCalls++);
        }

        public void Initialize() => InitializeCount++;
        public void UnInitialize() => UninitializeCount++;
    }

    private enum ForbiddenAction
    {
        Command,
        Event,
        EventUnregistration,
        Relationship,
        Uninitialize,
        Replacement
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

    private sealed class BindingProbeModel : AbstractModel
    {
        protected override void OnInitialize() { }
    }

    private sealed class DomainRecordingCommand : AbstractCommand
    {
        public IDomain? LastDomain { get; private set; }
        protected override void OnExecute() => LastDomain = Domain;
    }

    private sealed class DomainRecordingQuery : AbstractQuery<int>
    {
        public IDomain? LastDomain { get; private set; }
        protected override int OnExecute()
        {
            LastDomain = Domain;
            return 0;
        }
    }

    private sealed class DirectBindingComponent : ISystem, IModel
    {
        private IDomain? _domain;

        public IDomain Domain => _domain ?? throw new InvalidOperationException("Component has not been bound.");
        public int InitializeCount { get; private set; }

        public void BindDomain(IDomain domain)
        {
            ArgumentNullException.ThrowIfNull(domain);
            if (_domain is not null)
            {
                throw new InvalidOperationException("Component is already bound.");
            }

            _domain = domain;
        }

        public void Initialize() => InitializeCount++;
        public void UnInitialize() { }
    }

    private sealed class BindingOnlyDomain : IDomain
    {
        public IDomain? Parent => null;

        public void SetParent(IDomain? domain) => throw new NotSupportedException();
        public void AddChild(IDomain domain) => throw new NotSupportedException();
        public void RemoveChild(IDomain domain) => throw new NotSupportedException();

        public void RegisterSystem<TSystem>(TSystem system) where TSystem : ISystem => RegisterLifecycle(system);
        public void RegisterSystemAs<TSystem>(TSystem system) where TSystem : ISystem => RegisterLifecycle(system);
        public void RegisterModel<TModel>(TModel model) where TModel : IModel => RegisterLifecycle(model);
        public void RegisterModelAs<TModel>(TModel model) where TModel : IModel => RegisterLifecycle(model);
        public void RegisterUtility<TUtility>(TUtility utility) where TUtility : IUtility => throw new NotSupportedException();
        public void RegisterUtilityAs<TUtility>(TUtility utility) where TUtility : IUtility => throw new NotSupportedException();

        public TSystem? GetSystem<TSystem>() where TSystem : class, ISystem => null;
        public bool TryGetSystem<TSystem>(out TSystem? system) where TSystem : class, ISystem
        {
            system = null;
            return false;
        }

        public TSystem RequireSystem<TSystem>() where TSystem : class, ISystem => throw new NotSupportedException();
        public TModel? GetModel<TModel>() where TModel : class, IModel => null;
        public bool TryGetModel<TModel>(out TModel? model) where TModel : class, IModel
        {
            model = null;
            return false;
        }

        public TModel RequireModel<TModel>() where TModel : class, IModel => throw new NotSupportedException();
        public TUtility? GetUtility<TUtility>() where TUtility : class, IUtility => null;
        public bool TryGetUtility<TUtility>(out TUtility? utility) where TUtility : class, IUtility
        {
            utility = null;
            return false;
        }

        public TUtility RequireUtility<TUtility>() where TUtility : class, IUtility => throw new NotSupportedException();
        public IUnRegister RegisterEvent<TEvent>(Action<TEvent> onEvent) => new CustomUnRegister(() => { });
        public void UnRegisterEvent<TEvent>(Action<TEvent> onEvent) => throw new NotSupportedException();
        public void SendEvent<TEvent>() where TEvent : new() => throw new NotSupportedException();
        public void SendEvent<TEvent>(TEvent @event) => throw new NotSupportedException();
        public void SendCommand<TCommand>(TCommand command) where TCommand : ICommand => throw new NotSupportedException();
        public TResult SendCommand<TResult>(ICommand<TResult> command) => throw new NotSupportedException();
        public TResult SendQuery<TResult>(IQuery<TResult> query) => throw new NotSupportedException();
        public void UnInitialize() { }

        private void RegisterLifecycle(IDomainBindable component)
        {
            component.BindDomain(this);
            ((IConstructable)component).Initialize();
        }
    }

    private interface ISubscribedSystem : ISystem { }

    private interface IExecutionSubscribedSystem : ISystem { }

    private interface IExecutionReplaceableModel : IModel { }

    private sealed class ExecutionSubscribedSystem : AbstractSystem, IExecutionSubscribedSystem
    {
        public int EventCalls { get; private set; }
        public int UninitializeCount { get; private set; }

        protected override void OnInitialize() =>
            this.RegisterEvent<TransactionEvent>(_ => EventCalls++);

        protected override void OnUninitialize() => UninitializeCount++;
    }

    private sealed class ExecutionReplaceableModel : AbstractModel, IExecutionReplaceableModel
    {
        public int UninitializeCount { get; private set; }

        protected override void OnInitialize() { }
        protected override void OnUninitialize() => UninitializeCount++;
    }

    private class SubscribedSystem : AbstractSystem, ISubscribedSystem
    {
        public int EventCalls { get; private set; }
        public IUnRegister? Subscription { get; private set; }

        protected override void OnInitialize() => Subscription = this.RegisterEvent<TransactionEvent>(_ => EventCalls++);
    }

    private sealed class ThrowingCleanupSubscribedSystem : SubscribedSystem
    {
        protected override void OnUninitialize() => throw new InvalidOperationException("subscribed cleanup failed");
    }

    private sealed class ExistingSubscriptionSystem : AbstractSystem
    {
        private readonly Action _onEvent;

        public ExistingSubscriptionSystem(Action onEvent) => _onEvent = onEvent;

        public IUnRegister? Subscription { get; private set; }

        protected override void OnInitialize() =>
            Subscription = this.RegisterEvent<TransactionEvent>(_ => _onEvent());
    }

    private sealed class HandleCancellingModel : IModel
    {
        private readonly IUnRegister _subscription;
        private readonly CancellationMethod _cancellation;
        private readonly CancellationPhase _phase;

        public HandleCancellingModel(
            IUnRegister subscription,
            CancellationMethod cancellation,
            CancellationPhase phase)
        {
            _subscription = subscription;
            _cancellation = cancellation;
            _phase = phase;
        }

        public IDomain Domain { get; private set; } = default!;
        public int InitializeCount { get; private set; }
        public int UninitializeCount { get; private set; }

        public void BindDomain(IDomain domain)
        {
            Domain = domain;
            if (_phase == CancellationPhase.BindDomain)
            {
                AttemptCancellation();
            }
        }

        public void Initialize()
        {
            InitializeCount++;
            if (_phase == CancellationPhase.Initialize)
            {
                AttemptCancellation();
            }
        }

        public void UnInitialize() => UninitializeCount++;

        private void AttemptCancellation()
        {
            Cancel(_subscription, _cancellation);
            throw new InvalidOperationException("Subscription cancellation unexpectedly succeeded.");
        }
    }

    private enum SubscriptionOwnership
    {
        Domain,
        System
    }

    private enum CancellationMethod
    {
        UnRegister,
        Dispose
    }

    private enum CancellationPhase
    {
        BindDomain,
        Initialize
    }

    private sealed class FailingBindingSubscribedSystem : ISystem
    {
        private IDomain? _domain;

        public IDomain Domain => _domain ?? throw new InvalidOperationException("System has not been bound.");
        public int EventCalls { get; private set; }
        public int InitializeCount { get; private set; }
        public int UninitializeCount { get; private set; }

        public void BindDomain(IDomain domain)
        {
            _domain = domain;
            this.RegisterEvent<TransactionEvent>(_ => EventCalls++);
            throw new InvalidOperationException("binding failed");
        }

        public void Initialize() => InitializeCount++;
        public void UnInitialize() => UninitializeCount++;
    }

    private sealed class TransactionSubscriptionSystem : AbstractSystem
    {
        public int EventCalls { get; private set; }

        protected override void OnInitialize() { }

        public void Subscribe() => this.RegisterEvent<TransactionEvent>(_ => EventCalls++);
    }

    private sealed class ExistingSystemSubscriptionModel : AbstractModel
    {
        private readonly TransactionSubscriptionSystem _system;

        public ExistingSystemSubscriptionModel(TransactionSubscriptionSystem system) => _system = system;

        public bool FailInitialization { get; init; }

        protected override void OnInitialize()
        {
            _system.Subscribe();
            if (FailInitialization)
            {
                throw new InvalidOperationException("outer initialization failed");
            }
        }
    }

    private sealed class CatchingFailureModel : AbstractModel
    {
        public Exception? CaughtFailure { get; private set; }
        public Exception? PostFailure { get; private set; }
        public int EventCalls { get; private set; }

        protected override void OnInitialize()
        {
            try
            {
                Domain.RegisterModel(new NestedFailureModel(this));
            }
            catch (Exception exception)
            {
                CaughtFailure = exception;
            }

            try
            {
                Domain.RegisterUtility(new SharedUtility());
            }
            catch (Exception exception)
            {
                PostFailure = exception;
            }
        }

        public void RecordEvent() => EventCalls++;
    }

    private sealed class NestedFailureModel : AbstractModel
    {
        private readonly CatchingFailureModel _owner;

        public NestedFailureModel(CatchingFailureModel owner) => _owner = owner;

        protected override void OnInitialize()
        {
            Domain.RegisterModel(new SuccessfulNestedModel());
            Domain.RegisterEvent<TransactionEvent>(_ => _owner.RecordEvent());
            throw new InvalidOperationException("nested initialization failed");
        }
    }

    private sealed class SuccessfulNestedModel : AbstractModel
    {
        protected override void OnInitialize() { }
    }

    private sealed class CatchingSelfRegistrationModel : AbstractModel
    {
        public Exception? CaughtFailure { get; private set; }

        protected override void OnInitialize()
        {
            try
            {
                Domain.RegisterModel(this);
            }
            catch (Exception exception)
            {
                CaughtFailure = exception;
            }
        }
    }

    private sealed class RemovingOwnershipDuringInitializationModel : AbstractModel
    {
        private readonly IDomain _parent;

        public RemovingOwnershipDuringInitializationModel(IDomain parent) => _parent = parent;

        public Exception? RemoveFailure { get; private set; }

        protected override void OnInitialize()
        {
            try
            {
                _parent.RemoveChild(Domain);
            }
            catch (Exception exception)
            {
                RemoveFailure = exception;
            }
        }
    }

    private sealed class AncestorTeardownInitializerModel : AbstractModel
    {
        private readonly IDomain _ancestor;

        public AncestorTeardownInitializerModel(IDomain ancestor) => _ancestor = ancestor;

        public Exception? TeardownFailure { get; private set; }

        protected override void OnInitialize()
        {
            try
            {
                _ancestor.UnInitialize();
            }
            catch (Exception exception)
            {
                TeardownFailure = exception;
            }
        }
    }

    private interface IAncestorTeardownCleanupModel : IModel { }

    private sealed class AncestorTeardownCleanupModel : AbstractModel, IAncestorTeardownCleanupModel
    {
        private readonly IDomain _ancestor;
        private readonly SharedUtility? _expectedUtility;

        public AncestorTeardownCleanupModel(IDomain ancestor, SharedUtility? expectedUtility = null)
        {
            _ancestor = ancestor;
            _expectedUtility = expectedUtility;
        }

        public Exception? TeardownFailure { get; private set; }
        public Exception? AncestorAccessFailure { get; private set; }
        public SharedUtility? AncestorUtilityAfterAttempt { get; private set; }

        protected override void OnInitialize() { }

        protected override void OnUninitialize()
        {
            try
            {
                _ancestor.UnInitialize();
            }
            catch (Exception exception)
            {
                TeardownFailure = exception;
            }

            if (_expectedUtility is not null)
            {
                try
                {
                    AncestorUtilityAfterAttempt = _ancestor.RequireUtility<SharedUtility>();
                }
                catch (Exception exception)
                {
                    AncestorAccessFailure = exception;
                }
            }
        }
    }

    private sealed class ReparentingInitializerSystem : AbstractSystem
    {
        private readonly IDomain _child;
        private readonly IDomain _newParent;

        public ReparentingInitializerSystem(IDomain child, IDomain newParent)
        {
            _child = child;
            _newParent = newParent;
        }

        public Exception? ReparentFailure { get; private set; }

        protected override void OnInitialize()
        {
            try
            {
                _child.SetParent(_newParent);
            }
            catch (Exception exception)
            {
                ReparentFailure = exception;
            }
        }
    }

    private sealed class TransactionEvent { }
}
