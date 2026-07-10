using SimpleFramework.FrameworkImpl;

namespace SimpleFramework;

/// <summary>
/// 域的抽象实现，提供父子域、组件注册和事件分发能力。
/// </summary>
public abstract class AbstractDomain<T> : IDomain where T : AbstractDomain<T>, new()
{
    private readonly EventBus _eventBus = new();

    private readonly Container _container = new();
    
    private static T? _domain;

    private WeakReference<IDomain>? _parent;
    
    private readonly List<IDomain> _children = new();

    private readonly HashSet<Type> _constructableKeys = new();

    private bool _isUninitializing;

    private bool _isReleasingComponent;
    
    /// <summary>
    /// 获取单例域实例；不存在时会创建并初始化。
    /// </summary>
    public static T Instance => _domain ??= Create();
    
    /// <summary>
    /// 获取当前单例域实例；不存在时返回 <see langword="null"/>。
    /// </summary>
    public static T? GetInstance() => _domain;
    
    /// <summary>
    /// 创建一个独立的域实例，并立即完成初始化。
    /// </summary>
    /// <returns>新的域实例。</returns>
    public static T Create()
    {
        var domain = new T();
        domain.Init();
        return domain;
    }

    /// <summary>
    /// 初始化新创建的域。
    /// </summary>
    protected abstract void Init();

    /// <inheritdoc />
    public void UnInitialize()
    {
        if (_isUninitializing || _isReleasingComponent)
        {
            return;
        }

        var exceptions = new List<Exception>();

        _isUninitializing = true;
        try
        {
            foreach (var child in _children.ToList())
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

            foreach (var constructable in GetRegisteredConstructables())
            {
                try
                {
                    constructable.UnInitialize();
                }
                catch (Exception exception)
                {
                    exceptions.Add(exception);
                }
            }

            _container.Clear();
            _constructableKeys.Clear();
            _eventBus.Clear();
            SetParent(null);
            if (ReferenceEquals(_domain, this))
            {
                _domain = null;
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
            _isUninitializing = false;
        }

        if (exceptions.Count > 0)
        {
            throw new AggregateException(exceptions);
        }
    }
    
    /// <summary>
    /// 在子域、生命周期组件和本地事件清理完成后释放域自身资源。
    /// </summary>
    protected virtual void UnInit() {}

    /// <inheritdoc />
    public IDomain? Parent => _parent?.TryGetTarget(out var parent) == true ? parent : null;

    /// <inheritdoc />
    public void SetParent(IDomain? parent)
    {
        if (ReferenceEquals(Parent, parent))
        {
            return;
        }
        if (_CheckCycle(parent))
        {
            var exception = new ArgumentException("Detect cycle reference!");
            Log.Error("ArgumentError!", exception);
            throw exception;
        }

        var oldParent = Parent;

        _parent = parent is null ? null : new WeakReference<IDomain>(parent);

        // 只有在真正改变父域且新父域不是旧父域时才调用 RemoveChild
        // RemoveChild 会检查到子域的父域已经不是自己，从而避免递归
        if (oldParent != null && !ReferenceEquals(oldParent, parent))
        {
            oldParent.RemoveChild(this);
        }
    }

    /// <summary>
    /// 检查设置父域后是否会形成循环引用。
    /// </summary>
    /// <param name="domain">候选父域。</param>
    /// <returns>会形成循环引用时返回 <see langword="true"/>。</returns>
    private bool _CheckCycle(IDomain? domain)
    {
        if (domain is null) return false;
        if (ReferenceEquals(this, domain)) return true;

        // 检查当前域是否已经是目标域的父级或祖先
        var current = domain.Parent;
        while (current != null)
        {
            if (ReferenceEquals(this, current))
                return true;
            current = current.Parent;
        }

        return false;
    }

    /// <inheritdoc />
    public void AddChild(IDomain child)
    {
        child.SetParent(this);
        
        // 防止重复添加
        if (_children.Any(existingChild => ReferenceEquals(existingChild, child)))
        {
            return;
        }
        
        _children.Add(child);
    }

    /// <inheritdoc />
    public void RemoveChild(IDomain child)
    {
        if (child == null) return;

        // 遍历子域列表，找到匹配的子域并移除
        for (var i = _children.Count - 1; i >= 0; i--)
        {
            var existingChild = _children[i];
            if (!ReferenceEquals(existingChild, child)) continue;
            _children.RemoveAt(i);

            // 只有当子域的父域确实是当前域时才清空，避免递归调用
            if (ReferenceEquals(existingChild.Parent, this))
            {
                existingChild.SetParent(null);
            }
            break;
        }
    }

    /// <inheritdoc />
    public void RegisterSystem<TSystem>(TSystem system) where TSystem : ISystem
        => RegisterSystemAs(system);

    /// <inheritdoc />
    public void RegisterSystemAs<TSystem>(TSystem system) where TSystem : ISystem
        => RegisterConstructable(system);

    /// <inheritdoc />
    public void RegisterModel<TModel>(TModel model) where TModel : IModel
        => RegisterModelAs(model);

    /// <inheritdoc />
    public void RegisterModelAs<TModel>(TModel model) where TModel : IModel
        => RegisterConstructable(model);

    private void RegisterConstructable<TComponent>(TComponent component)
        where TComponent : IDomainConfigurable, IConstructable
    {
        EnsureNotUninitializing();

        ArgumentNullException.ThrowIfNull(component);

        var key = typeof(TComponent);
        var isAlreadyManaged = IsConstructableRegistered(component);
        var wasLifecycleManagedKey = _constructableKeys.Contains(key);
        var previous = _container.Get(key) is TComponent registered ? registered : default;
        if (ReferenceEquals(previous, component))
        {
            _constructableKeys.Add(key);
            if (!isAlreadyManaged)
            {
                InitializeAndRollbackOnFailure(component, previous, wasLifecycleManagedKey, false);
            }

            return;
        }

        var releasedPrevious = wasLifecycleManagedKey && previous is not null &&
                               !IsConstructableRegistered(previous, key);
        if (releasedPrevious)
        {
            ReleaseComponent(previous!);
        }

        _container.Register(component);
        _constructableKeys.Add(key);
        if (!isAlreadyManaged)
        {
            InitializeAndRollbackOnFailure(component, previous, wasLifecycleManagedKey, releasedPrevious);
        }
    }

    private void InitializeAndRollbackOnFailure<TComponent>(
        TComponent component,
        TComponent? previous,
        bool wasLifecycleManagedKey,
        bool releasedPrevious)
        where TComponent : IDomainConfigurable, IConstructable
    {
        try
        {
            component.SetDomain(this);
            component.Initialize();
        }
        catch
        {
            var key = typeof(TComponent);
            if (previous is not null && !releasedPrevious)
            {
                _container.Register(previous);
            }
            else
            {
                _container.Remove<TComponent>();
            }

            if (!wasLifecycleManagedKey || releasedPrevious)
            {
                _constructableKeys.Remove(key);
            }

            throw;
        }
    }

    /// <inheritdoc />
    public void RegisterUtility<TUtility>(TUtility utility) where TUtility : IUtility
        => RegisterUtilityAs(utility);

    /// <inheritdoc />
    public void RegisterUtilityAs<TUtility>(TUtility utility) where TUtility : IUtility
    {
        EnsureNotUninitializing();
        ArgumentNullException.ThrowIfNull(utility);

        var key = typeof(TUtility);
        var wasLifecycleManagedKey = _constructableKeys.Contains(key);
        var oldUtility = _container.Get(key);
        if (wasLifecycleManagedKey && oldUtility is IConstructable oldConstructable &&
            !IsConstructableRegistered(oldConstructable, key))
        {
            ReleaseComponent(oldConstructable);
        }

        _container.Register(utility);
        _constructableKeys.Remove(key);
    }

    private void EnsureNotUninitializing()
    {
        if (_isUninitializing || _isReleasingComponent)
        {
            throw new InvalidOperationException("Cannot register components during lifecycle cleanup.");
        }
    }

    private void ReleaseComponent(IConstructable component)
    {
        _isReleasingComponent = true;
        try
        {
            component.UnInitialize();
        }
        finally
        {
            _isReleasingComponent = false;
        }
    }

    private List<IConstructable> GetRegisteredConstructables()
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var constructables = new List<IConstructable>();

        AddRegisteredConstructables(typeof(ISystem), constructables, visited);
        AddRegisteredConstructables(typeof(IModel), constructables, visited);

        return constructables;
    }

    private void AddRegisteredConstructables(
        Type lifecycleKeyType,
        List<IConstructable> constructables,
        HashSet<object> visited)
    {
        foreach (var key in _constructableKeys.Where(lifecycleKeyType.IsAssignableFrom))
        {
            if (_container.Get(key) is IConstructable constructable && visited.Add(constructable))
            {
                constructables.Add(constructable);
            }
        }
    }

    private bool IsConstructableRegistered(IConstructable constructable, Type? excludedKey = null)
    {
        return _constructableKeys
            .Where(key => key != excludedKey)
            .Select(_container.Get)
            .Any(component => ReferenceEquals(component, constructable));
    }

    /// <inheritdoc />
    public TSystem? GetSystem<TSystem>() where TSystem : class, ISystem
    {
        var result = _container.Get<TSystem>();
        if (result != null) return result;

        return _parent?.TryGetTarget(out var parentDomain) == true ? parentDomain.GetSystem<TSystem>() : null;
    }

    /// <summary>
    /// 尝试在域中获取系统。
    /// </summary>
    /// <param name="system">找到的系统；未找到时为 <see langword="null"/>。</param>
    /// <typeparam name="TSystem">系统类型。</typeparam>
    /// <returns>找到系统时返回 true。</returns>
    public bool TryGetSystem<TSystem>(out TSystem? system) where TSystem : class, ISystem
    {
        system = GetSystem<TSystem>();
        return system != null;
    }

    /// <summary>
    /// 在域中获取系统，未找到时抛出异常。
    /// </summary>
    /// <typeparam name="TSystem">系统类型。</typeparam>
    /// <returns><see cref="ISystem"/></returns>
    /// <exception cref="InvalidOperationException">当前域及其父域中不存在指定系统。</exception>
    public TSystem RequireSystem<TSystem>() where TSystem : class, ISystem
    {
        var system = GetSystem<TSystem>();
        if (system != null) return system;
        throw new InvalidOperationException($"System not found: {typeof(TSystem).FullName} in {GetType().FullName}.");
    }

    /// <inheritdoc />
    public TModel? GetModel<TModel>() where TModel : class, IModel
    {
        var result = _container.Get<TModel>();
        if (result != null) return result;

        return _parent?.TryGetTarget(out var parentDomain) == true ? parentDomain.GetModel<TModel>() : null;
    }

    /// <summary>
    /// 尝试在域中获取模型。
    /// </summary>
    /// <param name="model">找到的模型；未找到时为 <see langword="null"/>。</param>
    /// <typeparam name="TModel">模型类型。</typeparam>
    /// <returns>找到模型时返回 true。</returns>
    public bool TryGetModel<TModel>(out TModel? model) where TModel : class, IModel
    {
        model = GetModel<TModel>();
        return model != null;
    }

    /// <summary>
    /// 在域中获取模型，未找到时抛出异常。
    /// </summary>
    /// <typeparam name="TModel">模型类型。</typeparam>
    /// <returns><see cref="IModel"/></returns>
    /// <exception cref="InvalidOperationException">当前域及其父域中不存在指定模型。</exception>
    public TModel RequireModel<TModel>() where TModel : class, IModel
    {
        var model = GetModel<TModel>();
        if (model != null) return model;
        throw new InvalidOperationException($"Model not found: {typeof(TModel).FullName} in {GetType().FullName}.");
    }

    /// <inheritdoc />
    public TUtility? GetUtility<TUtility>() where TUtility : class, IUtility
    {
        var result = _container.Get<TUtility>();
        if (result != null) return result;

        return _parent?.TryGetTarget(out var parentDomain) == true ? parentDomain.GetUtility<TUtility>() : null;
    }

    /// <summary>
    /// 尝试在域中获取功能组件。
    /// </summary>
    /// <param name="utility">找到的功能组件；未找到时为 <see langword="null"/>。</param>
    /// <typeparam name="TUtility">功能组件类型。</typeparam>
    /// <returns>找到功能组件时返回 true。</returns>
    public bool TryGetUtility<TUtility>(out TUtility? utility) where TUtility : class, IUtility
    {
        utility = GetUtility<TUtility>();
        return utility != null;
    }

    /// <summary>
    /// 在域中获取功能组件，未找到时抛出异常。
    /// </summary>
    /// <typeparam name="TUtility">功能组件类型。</typeparam>
    /// <returns><see cref="IUtility"/></returns>
    /// <exception cref="InvalidOperationException">当前域及其父域中不存在指定功能组件。</exception>
    public TUtility RequireUtility<TUtility>() where TUtility : class, IUtility
    {
        var utility = GetUtility<TUtility>();
        if (utility != null) return utility;
        throw new InvalidOperationException($"Utility not found: {typeof(TUtility).FullName} in {GetType().FullName}.");
    }

    /// <inheritdoc />
    public IUnRegister RegisterEvent<TEvent>(Action<TEvent> onEvent) => _eventBus.Register(onEvent);

    /// <inheritdoc />
    public void UnRegisterEvent<TEvent>(Action<TEvent> onEvent) => _eventBus.UnRegister(onEvent);

    /// <inheritdoc />
    public void SendEvent<TEvent>() where TEvent : new() => _eventBus.Send<TEvent>();

    /// <inheritdoc />
    public void SendEvent<TEvent>(TEvent @event) => _eventBus.Send(@event);

    /// <inheritdoc />
    public void SendCommand<TCommand>(TCommand command) where TCommand : ICommand =>
        ExecuteCommand(command);

    /// <summary>
    /// 注入当前域并执行无返回值命令；派生域可重写调度过程。
    /// </summary>
    /// <param name="command">要执行的命令。</param>
    /// <typeparam name="TCommand">命令类型。</typeparam>
    protected virtual void ExecuteCommand<TCommand>(TCommand command) where TCommand : ICommand
    {
        command.SetDomain(this);
        command.Execute();
    }

    /// <inheritdoc />
    public TResult SendCommand<TResult>(ICommand<TResult> command) => ExecuteCommand(command);

    /// <summary>
    /// 注入当前域并执行带返回值命令；派生域可重写调度过程。
    /// </summary>
    /// <param name="command">要执行的命令。</param>
    /// <typeparam name="TResult">命令结果类型。</typeparam>
    /// <returns>命令执行结果。</returns>
    protected virtual TResult ExecuteCommand<TResult>(ICommand<TResult> command)
    {
        command.SetDomain(this);
        return command.Execute();
    }

    /// <inheritdoc />
    public TResult SendQuery<TResult>(IQuery<TResult> query) => ExecuteQuery(query);

    /// <summary>
    /// 注入当前域并执行查询；派生域可重写调度过程。
    /// </summary>
    /// <param name="query">要执行的查询。</param>
    /// <typeparam name="TResult">查询结果类型。</typeparam>
    /// <returns>查询结果。</returns>
    protected virtual TResult ExecuteQuery<TResult>(IQuery<TResult> query)
    {
        query.SetDomain(this);
        return query.Execute();
    }

    /// <inheritdoc />
    public override string ToString() => _container.ToString();
}
