using System.Runtime.ExceptionServices;
using SimpleFramework.FrameworkImpl;

namespace SimpleFramework;

/// <summary>
/// 域的实例级抽象实现，提供初始化、父子域、组件注册、事件分发和资源释放能力。
/// </summary>
/// <remarks>
/// Domain 不是线程安全的。实例应由同一线程（通常是游戏或应用主线程）创建、访问和释放；
/// 调用方负责在调用 Domain API 前将后台工作调度回该线程。
/// </remarks>
public abstract class AbstractDomain : IDomain, IComponentEventRegistrar
{
    private readonly EventBus _eventBus = new();
    private readonly DomainComponentRegistry _registry = new();
    private readonly List<IDomain> _children = new();

    private WeakReference<IDomain>? _parent;
    private DomainState _state = DomainState.Created;
    private InitializationTransaction? _initializationTransaction;
    private int _componentInitializationDepth;
    private int _componentReleaseDepth;
    private int _executionDepth;
    private bool _isDetachingFromExternalParent;
    private IDomain? _childBeingRemoved;

    /// <summary>
    /// 执行一次性域初始化；派生类型应在其受控创建入口中调用。
    /// </summary>
    /// <remarks>
    /// 初始化失败时会清理已经注册的域资源并使当前实例永久进入已释放状态。
    /// 若清理也失败，抛出的聚合异常同时保留初始化和清理失败。
    /// </remarks>
    /// <exception cref="InvalidOperationException">当前实例不处于首次创建状态。</exception>
    /// <exception cref="AggregateException">初始化与失败清理同时抛出异常。</exception>
    protected void Initialize()
    {
        if (_state != DomainState.Created)
        {
            throw new InvalidOperationException("Domain initialization can only run once.");
        }

        _state = DomainState.Initializing;
        try
        {
            Init();
            OnInitialized();
            _state = DomainState.Active;
        }
        catch (Exception initializationException)
        {
            var cleanupExceptions = CleanupCore();
            ThrowWithCleanup(
                "Domain initialization and cleanup both failed.",
                initializationException,
                cleanupExceptions);
        }
    }

    /// <summary>
    /// 初始化新创建的域。
    /// </summary>
    protected abstract void Init();

    /// <summary>
    /// 在 <see cref="Init"/> 成功完成后执行派生创建策略的收尾逻辑。
    /// </summary>
    protected virtual void OnInitialized() { }

    /// <inheritdoc />
    public void UnInitialize()
    {
        if (_componentReleaseDepth > 0 || _state is DomainState.Uninitializing or DomainState.Disposed)
        {
            return;
        }

        EnsureOwnedTreeCanUninitialize();

        var exceptions = CleanupCore();
        if (exceptions.Count > 0)
        {
            throw new AggregateException(exceptions);
        }
    }

    /// <summary>
    /// 在域内容清理完成、域释放回调执行前更新派生创建策略持有的状态。
    /// </summary>
    private protected virtual void OnDomainCleared() { }

    /// <summary>
    /// 在子域、生命周期组件和本地事件清理完成后释放域自身资源。
    /// </summary>
    /// <remarks><see cref="Init"/> 抛出后也会调用，因此实现必须能处理部分初始化状态。</remarks>
    protected virtual void UnInit() { }

    /// <inheritdoc />
    public IDomain? Parent => _parent?.TryGetTarget(out var parent) == true ? parent : null;

    /// <inheritdoc />
    public void SetParent(IDomain? parent)
    {
        EnsureRelationshipMutationAllowed();
        if (_isDetachingFromExternalParent && parent is null)
        {
            _parent = null;
            return;
        }

        if (ReferenceEquals(Parent, parent))
        {
            return;
        }

        if (CheckCycle(parent))
        {
            var exception = new ArgumentException("Detect cycle reference!");
            Log.Error("ArgumentError!", exception);
            throw exception;
        }

        var oldParent = Parent;
        var candidateParent = parent is null ? null : new WeakReference<IDomain>(parent);
        try
        {
            if (oldParent is AbstractDomain parentDomain)
            {
                parentDomain.DetachOwnedChildForParentChange(this);
            }
            else if (oldParent is not null)
            {
                _isDetachingFromExternalParent = true;
                try
                {
                    oldParent.RemoveChild(this);
                }
                finally
                {
                    _isDetachingFromExternalParent = false;
                }
            }

            _parent = candidateParent;
        }
        catch
        {
            _parent = oldParent is null ? null : new WeakReference<IDomain>(oldParent);
            throw;
        }
    }

    /// <inheritdoc />
    public void AddChild(IDomain child)
    {
        EnsureRelationshipMutationAllowed();
        ArgumentNullException.ThrowIfNull(child);
        child.SetParent(this);
        if (_children.Any(existing => ReferenceEquals(existing, child)))
        {
            return;
        }

        _children.Add(child);
    }

    /// <inheritdoc />
    public void RemoveChild(IDomain child)
    {
        EnsureRelationshipMutationAllowed();
        ArgumentNullException.ThrowIfNull(child);
        if (ReferenceEquals(_childBeingRemoved, child))
        {
            return;
        }

        if (!_children.Any(existing => ReferenceEquals(existing, child)))
        {
            return;
        }

        if (ReferenceEquals(child.Parent, this))
        {
            var previousRemoval = _childBeingRemoved;
            _childBeingRemoved = child;
            try
            {
                child.SetParent(null);
            }
            finally
            {
                _childBeingRemoved = previousRemoval;
            }
        }

        RemoveChildReference(child);
    }

    /// <inheritdoc />
    public void RegisterSystem<TSystem>(TSystem system) where TSystem : ISystem
        => RegisterSystemAs(system);

    /// <inheritdoc />
    public void RegisterSystemAs<TSystem>(TSystem system) where TSystem : ISystem
        => RegisterLifecycle(ComponentCategory.System, typeof(TSystem), system);

    /// <inheritdoc />
    public void RegisterModel<TModel>(TModel model) where TModel : IModel
        => RegisterModelAs(model);

    /// <inheritdoc />
    public void RegisterModelAs<TModel>(TModel model) where TModel : IModel
        => RegisterLifecycle(ComponentCategory.Model, typeof(TModel), model);

    /// <inheritdoc />
    public void RegisterUtility<TUtility>(TUtility utility) where TUtility : IUtility
        => RegisterUtilityAs(utility);

    /// <inheritdoc />
    public void RegisterUtilityAs<TUtility>(TUtility utility) where TUtility : IUtility
    {
        EnsureRegistrationAllowed();
        ArgumentNullException.ThrowIfNull(utility);

        var key = typeof(TUtility);
        var byInstance = _registry.GetByInstance(utility);
        if (byInstance is not null)
        {
            if (byInstance.Category == ComponentCategory.Utility &&
                byInstance.PrimaryKey == key &&
                _componentInitializationDepth == 0)
            {
                return;
            }

            throw CreateDuplicateInstanceException(byInstance);
        }

        var previous = _registry.GetExact(ComponentCategory.Utility, key);
        if (previous is not null)
        {
            EnsureReplacementAllowed(ComponentCategory.Utility, key);
            _registry.Unpublish(previous);
        }

        var entry = _registry.Publish(ComponentCategory.Utility, key, utility);
        _initializationTransaction?.Enlist(entry);
    }

    /// <inheritdoc />
    public TSystem? GetSystem<TSystem>() where TSystem : class, ISystem
        => Resolve<TSystem>(ComponentCategory.System, static parent => parent.GetSystem<TSystem>());

    /// <inheritdoc />
    public bool TryGetSystem<TSystem>(out TSystem? system) where TSystem : class, ISystem
    {
        system = GetSystem<TSystem>();
        return system is not null;
    }

    /// <inheritdoc />
    public TSystem RequireSystem<TSystem>() where TSystem : class, ISystem
    {
        return GetSystem<TSystem>() ?? throw new InvalidOperationException(
            $"System not found: {typeof(TSystem).FullName} in {GetType().FullName}.");
    }

    /// <inheritdoc />
    public TModel? GetModel<TModel>() where TModel : class, IModel
        => Resolve<TModel>(ComponentCategory.Model, static parent => parent.GetModel<TModel>());

    /// <inheritdoc />
    public bool TryGetModel<TModel>(out TModel? model) where TModel : class, IModel
    {
        model = GetModel<TModel>();
        return model is not null;
    }

    /// <inheritdoc />
    public TModel RequireModel<TModel>() where TModel : class, IModel
    {
        return GetModel<TModel>() ?? throw new InvalidOperationException(
            $"Model not found: {typeof(TModel).FullName} in {GetType().FullName}.");
    }

    /// <inheritdoc />
    public TUtility? GetUtility<TUtility>() where TUtility : class, IUtility
        => Resolve<TUtility>(ComponentCategory.Utility, static parent => parent.GetUtility<TUtility>());

    /// <inheritdoc />
    public bool TryGetUtility<TUtility>(out TUtility? utility) where TUtility : class, IUtility
    {
        utility = GetUtility<TUtility>();
        return utility is not null;
    }

    /// <inheritdoc />
    public TUtility RequireUtility<TUtility>() where TUtility : class, IUtility
    {
        return GetUtility<TUtility>() ?? throw new InvalidOperationException(
            $"Utility not found: {typeof(TUtility).FullName} in {GetType().FullName}.");
    }

    /// <inheritdoc />
    public IUnRegister RegisterEvent<TEvent>(Action<TEvent> onEvent)
    {
        EnsureEventRegistrationAllowed();
        ArgumentNullException.ThrowIfNull(onEvent);
        var unRegister = _eventBus.Register(onEvent);
        _initializationTransaction?.Enlist(unRegister);
        return new GuardedDomainUnRegister(this, unRegister);
    }

    /// <inheritdoc />
    public IUnRegister RegisterComponentEvent<TEvent>(ISystem owner, Action<TEvent> onEvent)
    {
        EnsureEventRegistrationAllowed();
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(onEvent);

        var entry = _registry.GetByInstance(owner);
        if (entry is null || entry.Category != ComponentCategory.System || !_registry.IsPublished(entry))
        {
            throw new InvalidOperationException("System must be registered in this Domain before owning local events.");
        }

        var unRegister = _eventBus.Register(onEvent);
        entry.OwnResource(unRegister);
        _initializationTransaction?.Enlist(unRegister);
        return new GuardedDomainUnRegister(this, unRegister);
    }

    /// <inheritdoc />
    public void UnRegisterEvent<TEvent>(Action<TEvent> onEvent)
    {
        EnsureEventUnregistrationAllowed();
        ArgumentNullException.ThrowIfNull(onEvent);
        _eventBus.UnRegister(onEvent);
    }

    /// <inheritdoc />
    public void SendEvent<TEvent>() where TEvent : new()
    {
        EnsureActiveExecution("send local events");
        _executionDepth++;
        try
        {
            _eventBus.Send<TEvent>();
        }
        finally
        {
            _executionDepth--;
        }
    }

    /// <inheritdoc />
    public void SendEvent<TEvent>(TEvent @event)
    {
        EnsureActiveExecution("send local events");
        _executionDepth++;
        try
        {
            _eventBus.Send(@event);
        }
        finally
        {
            _executionDepth--;
        }
    }

    /// <inheritdoc />
    public void SendCommand<TCommand>(TCommand command) where TCommand : ICommand
    {
        EnsureActiveExecution("send commands");
        _executionDepth++;
        try
        {
            ExecuteCommand(command);
        }
        finally
        {
            _executionDepth--;
        }
    }

    /// <summary>
    /// 注入当前域并执行无返回值命令；派生域可重写调度过程。
    /// </summary>
    protected virtual void ExecuteCommand<TCommand>(TCommand command) where TCommand : ICommand
    {
        command.SetDomain(this);
        command.Execute();
    }

    /// <inheritdoc />
    public TResult SendCommand<TResult>(ICommand<TResult> command)
    {
        EnsureActiveExecution("send commands");
        _executionDepth++;
        try
        {
            return ExecuteCommand(command);
        }
        finally
        {
            _executionDepth--;
        }
    }

    /// <summary>
    /// 注入当前域并执行带返回值命令；派生域可重写调度过程。
    /// </summary>
    protected virtual TResult ExecuteCommand<TResult>(ICommand<TResult> command)
    {
        command.SetDomain(this);
        return command.Execute();
    }

    /// <inheritdoc />
    public TResult SendQuery<TResult>(IQuery<TResult> query)
    {
        EnsureQueryAllowed();
        _executionDepth++;
        try
        {
            return ExecuteQuery(query);
        }
        finally
        {
            _executionDepth--;
        }
    }

    /// <summary>
    /// 注入当前域并执行查询；派生域可重写调度过程。
    /// </summary>
    protected virtual TResult ExecuteQuery<TResult>(IQuery<TResult> query)
    {
        query.SetDomain(this);
        return query.Execute();
    }

    /// <inheritdoc />
    public override string ToString() => _registry.ToString();

    private void RegisterLifecycle(ComponentCategory category, Type key, IDomainBindable component)
    {
        var ownsTransaction = _initializationTransaction is null;
        if (ownsTransaction)
        {
            _initializationTransaction = new InitializationTransaction();
        }

        try
        {
            EnsureRegistrationAllowed();
            ArgumentNullException.ThrowIfNull(component);
            if (component is not IConstructable constructable)
            {
                throw new ArgumentException("Lifecycle component must implement IConstructable.", nameof(component));
            }

            var byInstance = _registry.GetByInstance(component);
            if (byInstance is not null)
            {
                if (byInstance.Category == category &&
                    byInstance.PrimaryKey == key &&
                    _componentInitializationDepth == 0)
                {
                    return;
                }

                throw CreateDuplicateInstanceException(byInstance);
            }

            var previous = _registry.GetExact(category, key);
            if (previous is not null)
            {
                EnsureReplacementAllowed(category, key);
                ReleaseEntry(previous);
            }

            InitializeEntry(category, key, component, constructable);
            if (ownsTransaction)
            {
                _initializationTransaction!.ThrowIfPoisoned();
            }
        }
        catch (Exception initializationException)
        {
            if (!ownsTransaction)
            {
                _initializationTransaction!.Poison(initializationException);
                ExceptionDispatchInfo.Capture(initializationException).Throw();
            }

            var rollbackExceptions = Rollback(_initializationTransaction!);
            ThrowWithCleanup(
                "Component initialization and rollback both failed.",
                initializationException,
                rollbackExceptions);
        }
        finally
        {
            if (ownsTransaction)
            {
                _initializationTransaction = null;
            }
        }
    }

    private void InitializeEntry(
        ComponentCategory category,
        Type key,
        IDomainBindable configurable,
        IConstructable constructable)
    {
        LifecycleOwnershipTracker.Reserve(configurable, _registry.OwnershipToken, category, key);

        DomainComponentEntry? entry = null;
        var initializationStarted = false;
        try
        {
            entry = _registry.Publish(category, key, configurable);
            _initializationTransaction!.Enlist(entry);
            _componentInitializationDepth++;
            try
            {
                configurable.BindDomain(this);
                LifecycleOwnershipTracker.BeginInitialization(configurable, _registry.OwnershipToken);
                initializationStarted = true;
                constructable.Initialize();
            }
            finally
            {
                _componentInitializationDepth--;
            }

            LifecycleOwnershipTracker.MarkActive(configurable, _registry.OwnershipToken);
            _registry.MarkActive(entry);
            _initializationTransaction.MarkActivated(entry);
        }
        catch (Exception initializationException)
        {
            var cleanupExceptions = new List<Exception>();
            if (entry is not null)
            {
                _registry.Unpublish(entry);
                _initializationTransaction!.Forget(entry);
            }

            if (initializationStarted)
            {
                ReleaseStartedComponent(configurable, constructable, cleanupExceptions);
            }

            if (entry is not null)
            {
                ReleaseEntryResources(entry, cleanupExceptions);
            }

            if (!initializationStarted)
            {
                LifecycleOwnershipTracker.CancelReservation(configurable, _registry.OwnershipToken);
            }

            ThrowWithCleanup(
                "Component initialization and cleanup both failed.",
                initializationException,
                cleanupExceptions);
        }
    }

    private List<Exception> Rollback(InitializationTransaction transaction)
    {
        var exceptions = new List<Exception>();
        foreach (var entry in transaction.ActivatedEntries.Reverse())
        {
            if (!_registry.IsPublished(entry))
            {
                continue;
            }

            try
            {
                ReleaseEntry(entry);
            }
            catch (Exception exception)
            {
                exceptions.Add(exception);
            }
        }

        foreach (var entry in transaction.Entries.Reverse())
        {
            if (entry.Category == ComponentCategory.Utility && _registry.IsPublished(entry))
            {
                _registry.Unpublish(entry);
            }
        }

        foreach (var unRegister in transaction.EventSubscriptions.Reverse())
        {
            try
            {
                unRegister.UnRegister();
            }
            catch (Exception exception)
            {
                exceptions.Add(exception);
            }
        }

        return exceptions;
    }

    private void ReleaseEntry(DomainComponentEntry entry)
    {
        _registry.Unpublish(entry);
        var exceptions = new List<Exception>();
        ReleaseStartedComponent(entry.Instance, (IConstructable)entry.Instance, exceptions);
        ReleaseEntryResources(entry, exceptions);
        if (exceptions.Count == 1)
        {
            ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
        }

        if (exceptions.Count > 1)
        {
            throw new AggregateException(exceptions);
        }
    }

    private void ReleaseStartedComponent(
        object instance,
        IConstructable constructable,
        List<Exception> exceptions)
    {
        var releaseStarted = false;
        try
        {
            LifecycleOwnershipTracker.BeginRelease(instance, _registry.OwnershipToken);
            releaseStarted = true;
            _componentReleaseDepth++;
            try
            {
                constructable.UnInitialize();
            }
            catch (Exception exception)
            {
                exceptions.Add(exception);
            }
            finally
            {
                _componentReleaseDepth--;
            }
        }
        finally
        {
            if (releaseStarted)
            {
                LifecycleOwnershipTracker.MarkReleased(instance, _registry.OwnershipToken);
            }
        }
    }

    private static void ReleaseEntryResources(DomainComponentEntry entry, List<Exception> exceptions)
    {
        foreach (var resource in entry.TakeOwnedResourcesReverse())
        {
            try
            {
                resource.UnRegister();
            }
            catch (Exception exception)
            {
                exceptions.Add(exception);
            }
        }
    }

    private List<Exception> CleanupCore()
    {
        var exceptions = new List<Exception>();
        _state = DomainState.Uninitializing;
        try
        {
            foreach (var child in _children.ToArray())
            {
                try
                {
                    child.UnInitialize();
                }
                catch (Exception exception)
                {
                    exceptions.Add(exception);
                }
            }

            _children.Clear();
            ReleaseCategory(ComponentCategory.System, exceptions);
            ReleaseCategory(ComponentCategory.Model, exceptions);
            _registry.ClearUtilities();
            _registry.Clear();
            _eventBus.Clear();
            DetachParent(exceptions);

            try
            {
                OnDomainCleared();
            }
            catch (Exception exception)
            {
                exceptions.Add(exception);
            }

            try
            {
                UnInit();
            }
            catch (Exception exception)
            {
                exceptions.Add(exception);
            }
        }
        finally
        {
            _initializationTransaction = null;
            _state = DomainState.Disposed;
        }

        return exceptions;
    }

    private void ReleaseCategory(ComponentCategory category, List<Exception> exceptions)
    {
        foreach (var entry in _registry.GetReverseActivationOrder(category))
        {
            try
            {
                ReleaseEntry(entry);
            }
            catch (Exception exception)
            {
                exceptions.Add(exception);
            }
        }
    }

    private void DetachParent(List<Exception> exceptions)
    {
        var oldParent = Parent;
        _parent = null;
        if (oldParent is null)
        {
            return;
        }

        if (oldParent is AbstractDomain parentDomain)
        {
            parentDomain.RemoveChildReference(this);
            return;
        }

        try
        {
            oldParent.RemoveChild(this);
        }
        catch (Exception exception)
        {
            exceptions.Add(exception);
        }
    }

    private bool RemoveChildReference(IDomain child)
    {
        for (var index = _children.Count - 1; index >= 0; index--)
        {
            if (!ReferenceEquals(_children[index], child))
            {
                continue;
            }

            _children.RemoveAt(index);
            return true;
        }

        return false;
    }

    private void DetachOwnedChildForParentChange(IDomain child)
    {
        if (!_children.Any(existing => ReferenceEquals(existing, child)))
        {
            return;
        }

        EnsureRelationshipMutationAllowed();
        RemoveChildReference(child);
    }

    private bool CheckCycle(IDomain? domain)
    {
        if (domain is null)
        {
            return false;
        }

        if (ReferenceEquals(this, domain))
        {
            return true;
        }

        var current = domain.Parent;
        while (current is not null)
        {
            if (ReferenceEquals(this, current))
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    private TComponent? Resolve<TComponent>(
        ComponentCategory category,
        Func<IDomain, TComponent?> resolveParent)
        where TComponent : class
    {
        if (_registry.Resolve(category, typeof(TComponent)) is TComponent result)
        {
            return result;
        }

        return Parent is { } parent ? resolveParent(parent) : null;
    }

    private void EnsureRegistrationAllowed()
    {
        if (_componentReleaseDepth > 0 || _state is not (DomainState.Initializing or DomainState.Active))
        {
            throw new InvalidOperationException($"Cannot register components while Domain is {_state}.");
        }

        EnsureTransactionCanEnlist("register components");
    }

    private void EnsureReplacementAllowed(ComponentCategory category, Type key)
    {
        if (_componentInitializationDepth > 0)
        {
            throw new InvalidOperationException(
                $"Cannot replace {category} key {key.FullName} during component initialization.");
        }

        if (_executionDepth > 0 && category is ComponentCategory.System or ComponentCategory.Model)
        {
            throw new InvalidOperationException(
                $"Cannot replace {category} key {key.FullName} during event, command, or query execution.");
        }
    }

    private void EnsureOwnedTreeCanUninitialize()
    {
        if (_state == DomainState.Disposed)
        {
            return;
        }

        if (_state == DomainState.Uninitializing)
        {
            throw new InvalidOperationException(
                "Cannot uninitialize an ancestor Domain while an owned Domain is still uninitializing.");
        }

        if (_state == DomainState.Initializing || _componentInitializationDepth > 0)
        {
            throw new InvalidOperationException("Cannot uninitialize a Domain while it is initializing.");
        }

        if (_componentReleaseDepth > 0)
        {
            throw new InvalidOperationException("Cannot uninitialize an owned Domain while it is releasing a component.");
        }

        if (_executionDepth > 0)
        {
            throw new InvalidOperationException(
                "Cannot uninitialize a Domain while it is executing events, commands, or queries.");
        }

        foreach (var child in _children)
        {
            if (child is AbstractDomain childDomain)
            {
                childDomain.EnsureOwnedTreeCanUninitialize();
            }
        }
    }

    private void EnsureRelationshipMutationAllowed()
    {
        if (_componentInitializationDepth > 0 ||
            _componentReleaseDepth > 0 ||
            _state is not (DomainState.Initializing or DomainState.Active))
        {
            throw new InvalidOperationException($"Cannot change Domain relationships while Domain is {_state}.");
        }
    }

    private void EnsureEventRegistrationAllowed()
    {
        if (_componentReleaseDepth > 0 || _state is not (DomainState.Initializing or DomainState.Active))
        {
            throw new InvalidOperationException($"Cannot register local events while Domain is {_state}.");
        }

        EnsureTransactionCanEnlist("register local events");
    }

    private void EnsureTransactionCanEnlist(string operation)
    {
        if (_initializationTransaction?.Failure is not { } failure)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Cannot {operation} because the current component initialization transaction has failed.",
            failure);
    }

    private void EnsureEventUnregistrationAllowed()
    {
        if (_componentInitializationDepth > 0 || _state is DomainState.Created or DomainState.Disposed)
        {
            throw new InvalidOperationException($"Cannot unregister local events while Domain is {_state}.");
        }
    }

    private void EnsureActiveExecution(string operation)
    {
        if (_state != DomainState.Active || _componentInitializationDepth > 0 || _componentReleaseDepth > 0)
        {
            throw new InvalidOperationException($"Cannot {operation} while Domain is {_state}.");
        }
    }

    private void EnsureQueryAllowed()
    {
        if (_componentReleaseDepth > 0 || _state is not (DomainState.Initializing or DomainState.Active))
        {
            throw new InvalidOperationException($"Cannot send queries while Domain is {_state}.");
        }
    }

    private InvalidOperationException CreateDuplicateInstanceException(DomainComponentEntry existing)
    {
        return new InvalidOperationException(
            $"Component instance is already registered as {existing.Category} with key " +
            $"{existing.PrimaryKey.FullName}.");
    }

    private static void ThrowWithCleanup(
        string message,
        Exception original,
        IReadOnlyCollection<Exception> cleanupExceptions)
    {
        if (cleanupExceptions.Count == 0)
        {
            ExceptionDispatchInfo.Capture(original).Throw();
        }

        throw new AggregateException(message, new[] { original }.Concat(cleanupExceptions));
    }

    private enum DomainState
    {
        Created,
        Initializing,
        Active,
        Uninitializing,
        Disposed
    }

    private sealed class GuardedDomainUnRegister : IUnRegister
    {
        private readonly WeakReference<AbstractDomain> _domain;
        private IUnRegister? _unRegister;

        public GuardedDomainUnRegister(AbstractDomain domain, IUnRegister unRegister)
        {
            _domain = new WeakReference<AbstractDomain>(domain);
            _unRegister = unRegister;
        }

        public void UnRegister()
        {
            var unRegister = _unRegister;
            if (unRegister is null)
            {
                return;
            }

            if (_domain.TryGetTarget(out var domain))
            {
                domain.EnsureEventUnregistrationAllowed();
            }

            _unRegister = null;
            unRegister.UnRegister();
        }
    }

    private sealed class InitializationTransaction
    {
        private readonly List<DomainComponentEntry> _entries = new();
        private readonly List<DomainComponentEntry> _activatedEntries = new();
        private readonly List<IUnRegister> _eventSubscriptions = new();
        private ExceptionDispatchInfo? _failure;

        public IReadOnlyList<DomainComponentEntry> Entries => _entries;

        public IReadOnlyList<DomainComponentEntry> ActivatedEntries => _activatedEntries;

        public IReadOnlyList<IUnRegister> EventSubscriptions => _eventSubscriptions;

        public Exception? Failure => _failure?.SourceException;

        public void Enlist(DomainComponentEntry entry) => _entries.Add(entry);

        public void Enlist(IUnRegister unRegister) => _eventSubscriptions.Add(unRegister);

        public void MarkActivated(DomainComponentEntry entry) => _activatedEntries.Add(entry);

        public void Poison(Exception exception)
        {
            _failure ??= ExceptionDispatchInfo.Capture(exception);
        }

        public void ThrowIfPoisoned() => _failure?.Throw();

        public void Forget(DomainComponentEntry entry)
        {
            _entries.Remove(entry);
            _activatedEntries.Remove(entry);
        }
    }
}

/// <summary>
/// 保留无参创建与可选单例访问策略的传统域基类。
/// </summary>
/// <typeparam name="T">具有公共无参构造函数的具体域类型。</typeparam>
public abstract class AbstractDomain<T> : AbstractDomain where T : AbstractDomain<T>, new()
{
    private static T? _domain;
    private static int _creationDepth;

    /// <summary>
    /// 获取单例域实例；不存在时会创建并初始化。
    /// </summary>
    public static T Instance
    {
        get
        {
            if (_creationDepth > 0)
            {
                throw new InvalidOperationException(
                    $"Cannot access {typeof(T).FullName}.Instance while the same Domain type is initializing.");
            }

            if (_domain is not null)
            {
                return _domain;
            }

            var domain = Create();
            _domain = domain;
            return domain;
        }
    }

    /// <summary>
    /// 获取当前单例域实例；不存在时返回 <see langword="null"/>。
    /// </summary>
    public static T? GetInstance() => _domain;

    /// <summary>
    /// 创建一个独立的域实例，并立即完成初始化。
    /// </summary>
    public static T Create()
    {
        if (_creationDepth > 0)
        {
            throw new InvalidOperationException(
                $"Cannot create {typeof(T).FullName} while the same Domain type is initializing.");
        }

        _creationDepth++;
        try
        {
            var domain = new T();
            domain.Initialize();
            return domain;
        }
        finally
        {
            _creationDepth--;
        }
    }

    /// <inheritdoc />
    private protected sealed override void OnDomainCleared()
    {
        if (ReferenceEquals(_domain, this))
        {
            _domain = null;
        }
    }
}

/// <summary>
/// 要求派生域在构造期接收配置、并由受控工厂显式启动初始化的域基类。
/// </summary>
/// <typeparam name="TConfiguration">初始化前只读可用的配置类型。</typeparam>
public abstract class AbstractConfiguredDomain<TConfiguration> : AbstractDomain
{
    private TConfiguration? _configuration;
    private bool _configurationAvailable = true;

    /// <summary>
    /// 保存派生域创建所需的配置；框架不会复制或修改该对象。
    /// </summary>
    protected AbstractConfiguredDomain(TConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _configuration = configuration;
    }

    /// <summary>
    /// 获取构造期提供的域配置；该值只在 <see cref="AbstractDomain.Init"/> 执行期间可用。
    /// </summary>
    protected TConfiguration Configuration => _configurationAvailable
        ? _configuration!
        : throw new InvalidOperationException("Domain configuration is only available during initialization.");

    /// <inheritdoc />
    protected sealed override void OnInitialized()
    {
        ClearConfiguration();
    }

    /// <inheritdoc />
    private protected sealed override void OnDomainCleared()
    {
        ClearConfiguration();
    }

    private void ClearConfiguration()
    {
        _configuration = default;
        _configurationAvailable = false;
    }
}
