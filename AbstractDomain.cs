using System.Runtime.ExceptionServices;
using SimpleFramework.FrameworkImpl;

namespace SimpleFramework;

/// <summary>
/// Domain v2 的唯一受支持扩展基类。
/// <para>Domain 采用单线程协作模型；调用方应在同一拥有线程串行访问一棵树。</para>
/// </summary>
public abstract class AbstractDomain : IDomain, IDisposable
{
    private readonly DomainComponentRegistry _registry = new();
    private readonly DomainEventBus _events = new();
    private readonly List<AbstractDomain> _children = new();
    private readonly List<DomainComponentEntry> _configuredModels = new();
    private readonly List<DomainComponentEntry> _configuredSystems = new();
    private readonly List<DomainComponentEntry> _activeModels = new();
    private readonly List<DomainComponentEntry> _activeSystems = new();
    private readonly object _ownershipToken = new();

    private AbstractDomain? _parent;
    private DomainTreeState _treeState = new() { IsTransitioning = true };
    private DomainState _state = DomainState.Starting;
    private bool _isConfiguring;
    private bool _isRegistering;

    /// <summary>
    /// 使用显式工厂创建并启动 Domain。具体类型应通过私有构造函数和静态 Create 方法调用本方法。
    /// </summary>
    /// <typeparam name="TDomain">具体 Domain 类型。</typeparam>
    /// <param name="factory">可捕获构造参数的同步工厂。</param>
    /// <returns>完成 Configure、组件初始化和 OnActivated 的 Active Domain。</returns>
    protected static TDomain CreateDomain<TDomain>(Func<TDomain> factory) where TDomain : AbstractDomain
    {
        ArgumentNullException.ThrowIfNull(factory);
        var candidate = factory() ?? throw new InvalidOperationException("Domain 工厂返回了 null。");
        candidate.Start();
        return candidate;
    }

    /// <summary>收集初始 Utility、Model 和 System 注册；此阶段不能执行查找、消息、树操作或释放。</summary>
    protected abstract void Configure();

    /// <summary>Domain 已进入 Active 且创建转换锁已释放后的通知。</summary>
    protected virtual void OnActivated() { }

    /// <summary>Domain 进入 Disposing 且所有组件 Context 已失效后的通知。</summary>
    protected virtual void OnDeactivating() { }

    /// <inheritdoc />
    public IDomain? Parent
    {
        get
        {
            EnsurePublicReadable();
            return _parent;
        }
    }

    /// <summary>按运行时具体类型注册一个 Model 生命周期实例。</summary>
    public void RegisterModel(IModelLifecycle model)
    {
        ArgumentNullException.ThrowIfNull(model);
        RegisterLifecycle(ComponentCategory.Model, model.GetType(), model);
    }

    /// <summary>按显式业务契约注册一个 Model 生命周期实例；泛型契约就是唯一主键。</summary>
    public void RegisterModel<T>(IModelLifecycle model) where T : class, IModel =>
        RegisterLifecycle(ComponentCategory.Model, typeof(T), model);

    /// <summary>按运行时具体类型注册一个 System 生命周期实例。</summary>
    public void RegisterSystem(ISystemLifecycle system)
    {
        ArgumentNullException.ThrowIfNull(system);
        RegisterLifecycle(ComponentCategory.System, system.GetType(), system);
    }

    /// <summary>按显式业务契约注册一个 System 生命周期实例；泛型契约就是唯一主键。</summary>
    public void RegisterSystem<T>(ISystemLifecycle system) where T : class, ISystem =>
        RegisterLifecycle(ComponentCategory.System, typeof(T), system);

    /// <summary>按运行时具体类型注册一个由调用方管理的 Utility。</summary>
    public void RegisterUtility(IUtility utility)
    {
        ArgumentNullException.ThrowIfNull(utility);
        RegisterUtilityCore(utility.GetType(), utility);
    }

    /// <summary>按显式业务契约注册一个 Utility；泛型契约就是唯一主键。</summary>
    public void RegisterUtility<T>(IUtility utility) where T : class, IUtility =>
        RegisterUtilityCore(typeof(T), utility);

    /// <summary>
    /// 将一个已经独立创建完成的 Active 根 Domain 挂为直接子域。
    /// <para>挂载不触发生命周期回调；成功后组件查找才开始回退到父域。</para>
    /// </summary>
    public void AddChild(AbstractDomain child)
    {
        ArgumentNullException.ThrowIfNull(child);
        EnsureTreeMutationAllowed();
        child.EnsureTreeMutationAllowed();
        if (ReferenceEquals(this, child)) throw new InvalidOperationException("Domain 不能挂载自身。");
        if (child._parent is not null) throw new InvalidOperationException("待挂载 Domain 已经有父 Domain；移动必须先 RemoveChild。");
        if (_children.Contains(child)) throw new InvalidOperationException("该 Domain 已是直接子域。");
        for (var node = this; node is not null; node = node._parent)
        {
            if (ReferenceEquals(node, child)) throw new InvalidOperationException("挂载会形成 Domain 环路。");
        }

        var parentState = _treeState;
        var childState = child._treeState;
        parentState.IsTransitioning = true;
        childState.IsTransitioning = true;
        try
        {
            child._parent = this;
            _children.Add(child);
            child.AdoptTreeState(parentState);
        }
        finally
        {
            parentState.IsTransitioning = false;
            if (!ReferenceEquals(childState, parentState)) childState.IsTransitioning = false;
        }
    }

    /// <summary>移除一个直接子域，不释放它；移除后的子树仍为 Active 独立树。</summary>
    public void RemoveChild(AbstractDomain child)
    {
        ArgumentNullException.ThrowIfNull(child);
        EnsureTreeMutationAllowed();
        if (!_children.Contains(child) || !ReferenceEquals(child._parent, this))
        {
            throw new InvalidOperationException("只能移除当前 Domain 的直接子域。");
        }

        var oldState = _treeState;
        oldState.IsTransitioning = true;
        var newState = new DomainTreeState { IsTransitioning = true };
        try
        {
            _children.Remove(child);
            child._parent = null;
            child.AdoptTreeState(newState);
        }
        finally
        {
            oldState.IsTransitioning = false;
            newState.IsTransitioning = false;
        }
    }

    /// <inheritdoc />
    public T GetModel<T>() where T : class, IModel => ResolvePublic<T>(ComponentCategory.Model);

    /// <inheritdoc />
    public bool TryGetModel<T>(out T? model) where T : class, IModel => TryResolvePublic(ComponentCategory.Model, out model);

    /// <inheritdoc />
    public T GetSystem<T>() where T : class, ISystem => ResolvePublic<T>(ComponentCategory.System);

    /// <inheritdoc />
    public bool TryGetSystem<T>(out T? system) where T : class, ISystem => TryResolvePublic(ComponentCategory.System, out system);

    /// <inheritdoc />
    public T GetUtility<T>() where T : class, IUtility => ResolvePublic<T>(ComponentCategory.Utility);

    /// <inheritdoc />
    public bool TryGetUtility<T>(out T? utility) where T : class, IUtility => TryResolvePublic(ComponentCategory.Utility, out utility);

    /// <inheritdoc />
    public IUnRegister RegisterEvent<T>(Action<T> handler)
    {
        EnsureExecutable();
        return _events.Register(handler);
    }

    /// <inheritdoc />
    public void SendEvent<T>(T message)
    {
        EnsureExecutable();
        EnterExecution();
        try { _events.Send(message); }
        finally { ExitExecution(); }
    }

    /// <inheritdoc />
    public void SendCommand(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureExecutable();
        EnterExecution();
        try { command.Execute(new CommandContext(this)); }
        finally { ExitExecution(); }
    }

    /// <inheritdoc />
    public TResult SendCommand<TResult>(ICommand<TResult> command)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureExecutable();
        EnterExecution();
        try { return command.Execute(new CommandContext(this)); }
        finally { ExitExecution(); }
    }

    /// <inheritdoc />
    public TResult SendQuery<TResult>(IQuery<TResult> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        EnsureExecutable();
        EnterExecution();
        try { return query.Execute(new QueryContext(this)); }
        finally { ExitExecution(); }
    }

    /// <summary>按逆挂载后序释放当前 Domain 及其仍挂载的完整子树。</summary>
    public void Dispose()
    {
        if (_state is DomainState.Disposing or DomainState.Disposed) return;
        EnsureDisposalAllowed();
        var state = _treeState;
        state.IsTransitioning = true;
        var failures = new List<Exception>();
        try { DisposeNode(includeChildren: true, failures); }
        finally { state.IsTransitioning = false; }
        ThrowFailures(failures);
    }

    /// <summary>先保留并分离直接子树，再只释放当前 Domain。</summary>
    public void DisposeSelfOnly()
    {
        if (_state == DomainState.Disposed) throw new ObjectDisposedException(GetType().FullName);
        if (_state == DomainState.Disposing) throw new InvalidOperationException("Domain 正在释放，不能切换释放策略。");
        EnsureDisposalAllowed();
        var oldState = _treeState;
        oldState.IsTransitioning = true;
        var detachedStates = new List<DomainTreeState>(_children.Count);
        var failures = new List<Exception>();
        try
        {
            foreach (var child in _children.ToArray())
            {
                var state = new DomainTreeState { IsTransitioning = true };
                detachedStates.Add(state);
                child._parent = null;
                child.AdoptTreeState(state);
            }
            _children.Clear();
            DisposeNode(includeChildren: false, failures);
        }
        finally
        {
            oldState.IsTransitioning = false;
            foreach (var state in detachedStates) state.IsTransitioning = false;
        }
        ThrowFailures(failures);
    }

    internal T ResolveForContext<T>(ComponentCategory category) where T : class
    {
        if (TryResolveCore(category, typeof(T), out var value)) return (T)value!;
        throw Missing(category, typeof(T));
    }

    internal bool TryResolveForContext<T>(ComponentCategory category, out T? result) where T : class
    {
        if (TryResolveCore(category, typeof(T), out var value))
        {
            result = (T)value!;
            return true;
        }
        result = null;
        return false;
    }

    internal IUnRegister RegisterOwnedEvent<T>(DomainComponentEntry owner, Action<T> handler)
    {
        var token = _events.Register(handler);
        owner.Own(token);
        return token;
    }

    private void Start()
    {
        if (_state != DomainState.Starting) throw new InvalidOperationException("Domain 只能启动一次。");
        Exception? startupFailure = null;
        try
        {
            _isConfiguring = true;
            try { Configure(); }
            finally { _isConfiguring = false; }

            foreach (var entry in _configuredModels.ToArray()) InitializeLifecycle(entry);
            foreach (var entry in _configuredSystems.ToArray()) InitializeLifecycle(entry);

            _state = DomainState.Active;
            _treeState.IsTransitioning = false;
            OnActivated();
            if (_state != DomainState.Active)
            {
                throw new InvalidOperationException("OnActivated 必须让 Domain 保持 Active，不能在创建流程中释放或改变其终态。");
            }
            return;
        }
        catch (Exception exception)
        {
            startupFailure = exception;
        }

        var failures = new List<Exception>();
        AddFailure(failures, startupFailure!);
        if (_state != DomainState.Disposed)
        {
            _treeState.IsTransitioning = true;
            DisposeNode(includeChildren: true, failures);
            _treeState.IsTransitioning = false;
        }
        ThrowFailures(failures);
    }

    private void RegisterLifecycle(ComponentCategory category, Type key, object component)
    {
        ArgumentNullException.ThrowIfNull(component);
        ValidateRegistrationKey(key, component, category);
        ValidateCategory(component, category);
        RunRegistration(() =>
        {
            LifecycleOwnershipTracker.Reserve(component, _ownershipToken, category, key);
            DomainComponentEntry entry;
            try { entry = _registry.AddCandidate(category, key, component); }
            catch
            {
                LifecycleOwnershipTracker.CancelUntouched(component, _ownershipToken);
                throw;
            }

            var configured = category == ComponentCategory.Model ? _configuredModels : _configuredSystems;
            configured.Add(entry);
            if (_state == DomainState.Active)
            {
                try { InitializeLifecycle(entry); }
                catch
                {
                    configured.Remove(entry);
                    throw;
                }
            }
        });
    }

    private void RegisterUtilityCore(Type key, IUtility utility)
    {
        ArgumentNullException.ThrowIfNull(utility);
        ValidateRegistrationKey(key, utility, ComponentCategory.Utility);
        ValidateCategory(utility, ComponentCategory.Utility);
        RunRegistration(() =>
        {
            var entry = _registry.AddCandidate(ComponentCategory.Utility, key, utility);
            _registry.Publish(entry);
        });
    }

    private void RunRegistration(Action action)
    {
        EnsureRegistrationAllowed();
        if (_isRegistering) throw new InvalidOperationException("组件初始化或注册期间不能嵌套注册。");
        var active = _state == DomainState.Active;
        if (active) _treeState.IsTransitioning = true;
        _isRegistering = true;
        try { action(); }
        finally
        {
            _isRegistering = false;
            if (active) _treeState.IsTransitioning = false;
        }
    }

    private void InitializeLifecycle(DomainComponentEntry entry)
    {
        LifecycleOwnershipTracker.Begin(entry.Instance, _ownershipToken);
        entry.InitializationStarted = true;
        ComponentContextBase context;
        if (entry.Category == ComponentCategory.Model)
        {
            context = new ModelContext(this, entry);
            entry.Context = context;
        }
        else
        {
            context = new SystemContext(this, entry);
            entry.Context = context;
        }

        try
        {
            LifecycleOwnershipTracker.AttachContext(entry.Instance, _ownershipToken, context);
            if (entry.Instance is IModelLifecycle model) model.Initialize((IModelContext)context);
            else ((ISystemLifecycle)entry.Instance).Initialize((ISystemContext)context);
            context.MarkReady();
            _registry.Publish(entry);
            (entry.Category == ComponentCategory.Model ? _activeModels : _activeSystems).Add(entry);
        }
        catch (Exception initializationFailure)
        {
            var failures = new List<Exception>();
            AddFailure(failures, initializationFailure);
            context.Invalidate();
            CancelOwnedResources(entry, failures);
            try { ReleaseLifecycle(entry); }
            catch (Exception releaseFailure) { AddFailure(failures, releaseFailure); }
            _registry.Forget(entry);
            ThrowFailures(failures);
        }
    }

    private void DisposeNode(bool includeChildren, List<Exception> failures)
    {
        if (_state is DomainState.Disposing or DomainState.Disposed) return;
        if (includeChildren)
        {
            for (var index = _children.Count - 1; index >= 0; index--)
            {
                try { _children[index].DisposeNode(includeChildren: true, failures); }
                catch (Exception exception) { AddFailure(failures, exception); }
            }
        }

        _state = DomainState.Disposing;
        foreach (var entry in _activeSystems) entry.Context?.Invalidate();
        foreach (var entry in _activeModels) entry.Context?.Invalidate();

        try { OnDeactivating(); }
        catch (Exception exception) { AddFailure(failures, exception); }

        for (var index = _activeSystems.Count - 1; index >= 0; index--)
        {
            var entry = _activeSystems[index];
            _registry.Unpublish(entry);
            CancelOwnedResources(entry, failures);
            try { ReleaseLifecycle(entry); }
            catch (Exception exception) { AddFailure(failures, exception); }
            _registry.Forget(entry);
        }
        _activeSystems.Clear();

        for (var index = _activeModels.Count - 1; index >= 0; index--)
        {
            var entry = _activeModels[index];
            _registry.Unpublish(entry);
            try { ReleaseLifecycle(entry); }
            catch (Exception exception) { AddFailure(failures, exception); }
            _registry.Forget(entry);
        }
        _activeModels.Clear();

        foreach (var entry in _configuredSystems.Concat(_configuredModels).ToArray())
        {
            if (entry.InitializationStarted || entry.Published) continue;
            try { LifecycleOwnershipTracker.CancelUntouched(entry.Instance, _ownershipToken); }
            catch (Exception exception) { AddFailure(failures, exception); }
            _registry.Forget(entry);
        }
        _configuredSystems.Clear();
        _configuredModels.Clear();

        try { _events.Clear(); }
        catch (Exception exception) { AddFailure(failures, exception); }
        try { _registry.ClearUtilities(); }
        catch (Exception exception) { AddFailure(failures, exception); }

        try
        {
            if (_parent is not null) _parent._children.Remove(this);
            _parent = null;
            foreach (var child in _children) child._parent = null;
            _children.Clear();
        }
        catch (Exception exception) { AddFailure(failures, exception); }
        finally
        {
            _registry.ClearAllCandidates();
            _state = DomainState.Disposed;
            try { OnTerminalDisposed(); }
            catch (Exception exception) { AddFailure(failures, exception); }
        }
    }

    private protected virtual void OnTerminalDisposed() { }

    private static void ReleaseLifecycle(DomainComponentEntry entry)
    {
        entry.Context?.Invalidate();
        try
        {
            if (entry.Instance is IModelLifecycle model) model.Release();
            else ((ISystemLifecycle)entry.Instance).Release();
        }
        finally
        {
            LifecycleOwnershipTracker.DetachContext(entry.Instance, entry.Context);
        }
    }

    private static void CancelOwnedResources(DomainComponentEntry entry, List<Exception> failures)
    {
        foreach (var token in entry.TakeOwnedResourcesReverse())
        {
            try { token.UnRegister(); }
            catch (Exception exception) { AddFailure(failures, exception); }
        }
    }

    private T ResolvePublic<T>(ComponentCategory category) where T : class
    {
        EnsurePublicReadable();
        if (TryResolveCore(category, typeof(T), out var value)) return (T)value!;
        throw Missing(category, typeof(T));
    }

    private bool TryResolvePublic<T>(ComponentCategory category, out T? result) where T : class
    {
        EnsurePublicReadable();
        if (TryResolveCore(category, typeof(T), out var value))
        {
            result = (T)value!;
            return true;
        }
        result = null;
        return false;
    }

    private bool TryResolveCore(ComponentCategory category, Type requested, out object? value)
    {
        var domainName = GetType().FullName ?? GetType().Name;
        if (_registry.TryResolveLocal(category, requested, domainName, out value)) return true;
        return _parent is not null && _parent.TryResolveCore(category, requested, out value);
    }

    private KeyNotFoundException Missing(ComponentCategory category, Type requested) =>
        new($"Domain {GetType().FullName ?? GetType().Name} 及其父链中不存在 {category} {requested.FullName}。");

    private void EnsurePublicReadable()
    {
        if (_state == DomainState.Disposed) throw new ObjectDisposedException(GetType().FullName);
        if (_state != DomainState.Active) throw new InvalidOperationException($"Domain 在 {_state} 阶段不能执行该操作。");
        if (_treeState.IsTransitioning) throw new InvalidOperationException("Domain 树正在执行生命周期或结构转换。");
    }

    private void EnsureExecutable() => EnsurePublicReadable();

    private void EnsureRegistrationAllowed()
    {
        if (_state == DomainState.Disposed) throw new ObjectDisposedException(GetType().FullName);
        if (_state == DomainState.Starting)
        {
            if (!_isConfiguring) throw new InvalidOperationException("Starting 阶段只能在 Configure 中注册组件。");
            return;
        }
        if (_state != DomainState.Active) throw new InvalidOperationException($"Domain 在 {_state} 阶段不能注册组件。");
        if (_treeState.IsTransitioning || _treeState.ExecutionDepth > 0)
        {
            throw new InvalidOperationException("Domain 树正在执行或转换，不能注册组件。");
        }
    }

    private void EnsureTreeMutationAllowed()
    {
        if (_state == DomainState.Disposed) throw new ObjectDisposedException(GetType().FullName);
        if (_state != DomainState.Active) throw new InvalidOperationException("只有 Active Domain 可以修改树结构。");
        if (_treeState.IsTransitioning || _treeState.ExecutionDepth > 0)
        {
            throw new InvalidOperationException("Domain 树正在执行或转换，不能修改结构。");
        }
    }

    private void EnsureDisposalAllowed()
    {
        if (_state != DomainState.Active) throw new InvalidOperationException($"Domain 在 {_state} 阶段不能释放。");
        if (_treeState.IsTransitioning || _treeState.ExecutionDepth > 0)
        {
            throw new InvalidOperationException("Domain 树正在执行或转换，不能释放。");
        }
    }

    private void EnterExecution() => _treeState.ExecutionDepth++;
    private void ExitExecution() => _treeState.ExecutionDepth--;

    private void AdoptTreeState(DomainTreeState state)
    {
        _treeState = state;
        foreach (var child in _children) child.AdoptTreeState(state);
    }

    private static void ValidateRegistrationKey(Type key, object component, ComponentCategory category)
    {
        if (!key.IsInstanceOfType(component))
        {
            throw new ArgumentException($"{component.GetType().FullName} 不能赋值给 {category} 契约 {key.FullName}。", nameof(component));
        }
    }

    private static void ValidateCategory(object component, ComponentCategory category)
    {
        var isModel = component is IModel;
        var isSystem = component is ISystem;
        var isUtility = component is IUtility;
        var count = (isModel ? 1 : 0) + (isSystem ? 1 : 0) + (isUtility ? 1 : 0);
        if (count != 1)
        {
            throw new InvalidOperationException($"组件 {component.GetType().FullName} 同时属于多个分类或没有唯一分类。");
        }
        if (category == ComponentCategory.Model && component is not IModelLifecycle)
            throw new ArgumentException("Model 注册要求实例实现 IModelLifecycle。", nameof(component));
        if (category == ComponentCategory.System && component is not ISystemLifecycle)
            throw new ArgumentException("System 注册要求实例实现 ISystemLifecycle。", nameof(component));
    }

    private static void AddFailure(List<Exception> failures, Exception exception)
    {
        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions) AddFailure(failures, inner);
        }
        else failures.Add(exception);
    }

    private static void ThrowFailures(List<Exception> failures)
    {
        if (failures.Count == 0) return;
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        throw new AggregateException(failures);
    }
}

/// <summary>
/// 严格单例 Domain 基类。具体类型应以私有构造函数和静态 Instance 属性调用 <see cref="GetOrCreateInstance"/>。
/// </summary>
public abstract class AbstractSingletonDomain<TDomain> : AbstractDomain where TDomain : AbstractSingletonDomain<TDomain>
{
    /// <summary>严格单例的内部发布状态。</summary>
    private enum SingletonState
    {
        /// <summary>当前没有已发布或正在创建的实例。</summary>
        Empty,

        /// <summary>实例正在创建，禁止重入访问或销毁。</summary>
        Creating,

        /// <summary>实例已完成激活并对外发布。</summary>
        Published
    }
    private static SingletonState _singletonState;
    private static TDomain? _instance;

    /// <summary>获取或创建严格单例；创建完成前不会发布半初始化实例。</summary>
    protected static TDomain GetOrCreateInstance(Func<TDomain> factory)
    {
        if (_singletonState == SingletonState.Published) return _instance!;
        if (_singletonState == SingletonState.Creating) throw new InvalidOperationException($"{typeof(TDomain).FullName} 单例正在创建，禁止重入 Instance。");
        _singletonState = SingletonState.Creating;
        try
        {
            var created = CreateDomain(factory);
            _instance = created;
            _singletonState = SingletonState.Published;
            return created;
        }
        catch
        {
            _instance = null;
            _singletonState = SingletonState.Empty;
            throw;
        }
    }

    /// <summary>返回已发布单例；Empty 或 Creating 时返回 <see langword="null"/>。</summary>
    public static TDomain? GetInstance() => _singletonState == SingletonState.Published ? _instance : null;

    /// <summary>释放已发布单例；不存在时不创建，创建中调用会抛出异常。</summary>
    public static void DestroyInstance()
    {
        if (_singletonState == SingletonState.Creating) throw new InvalidOperationException($"{typeof(TDomain).FullName} 单例正在创建，不能销毁。");
        _instance?.Dispose();
    }

    private protected override void OnTerminalDisposed()
    {
        if (_singletonState == SingletonState.Published && ReferenceEquals(_instance, this))
        {
            _instance = null;
            _singletonState = SingletonState.Empty;
        }
    }
}
