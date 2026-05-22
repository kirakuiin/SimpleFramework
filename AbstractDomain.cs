using SimpleFramework.FrameworkImpl;

namespace SimpleFramework;

/// <summary>
/// 实现了域大部分功能的抽象类。
/// </summary>
public abstract class AbstractDomain<T> : IDomain where T : AbstractDomain<T>, new()
{
    private readonly EventBus _eventBus = new();

    private readonly Container _container = new();
    
    private static T _domain;

    private WeakReference<IDomain> _parent;
    
    private readonly List<IDomain> _children = new();

    private readonly HashSet<Type> _constructableKeys = new();

    private bool _isUninitializing;
    
    /// <summary>
    /// 获取对象，如果对象不存在则创建
    /// </summary>
    public static T Instance => _domain ??= Create();
    
    /// <summary>
    /// 获取对象，如果对象不存在则返回null
    /// </summary>
    /// <returns></returns>
    public static T GetInstance() => _domain;
    
    /// <summary>
    /// 创建一个新的独立域实例，并立即初始化。
    /// </summary>
    /// <returns>新的域实例。</returns>
    public static T Create()
    {
        var domain = new T();
        domain.Init();
        return domain;
    }

    protected abstract void Init();

    public void UnInitialize()
    {
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
    
    protected virtual void UnInit() {}

    public IDomain Parent => _parent?.TryGetTarget(out var parent) == true ? parent : null;

    public void SetParent(IDomain parent)
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
    /// 检查参数是否有效
    /// </summary>
    /// <param name="domain"></param>
    /// <returns></returns>
    private bool _CheckCycle(IDomain domain)
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

    public void RegisterSystem<TSystem>(TSystem system) where TSystem : ISystem
        => RegisterSystemAs(system);

    public void RegisterSystemAs<TSystem>(TSystem system) where TSystem : ISystem
    {
        EnsureNotUninitializing();

        var hasSystem = IsConstructableRegistered(system);
        var wasLifecycleManagedKey = _constructableKeys.Contains(typeof(TSystem));
        var oldSystem = _container.Register(system);
        _constructableKeys.Add(typeof(TSystem));
        if (ReferenceEquals(oldSystem, system))
        {
            if (!hasSystem)
            {
                system.SetDomain(this);
                system.Initialize();
            }

            return;
        }

        if (wasLifecycleManagedKey && oldSystem != null && !IsConstructableRegistered(oldSystem))
        {
            oldSystem.UnInitialize();
        }

        if (!hasSystem)
        {
            system.SetDomain(this);
            system.Initialize();
        }
    }

    public void RegisterModel<TModel>(TModel model) where TModel : IModel
        => RegisterModelAs(model);

    public void RegisterModelAs<TModel>(TModel model) where TModel : IModel
    {
        EnsureNotUninitializing();

        var hasModel = IsConstructableRegistered(model);
        var wasLifecycleManagedKey = _constructableKeys.Contains(typeof(TModel));
        var oldModel = _container.Register(model);
        _constructableKeys.Add(typeof(TModel));
        if (ReferenceEquals(oldModel, model))
        {
            if (!hasModel)
            {
                model.SetDomain(this);
                model.Initialize();
            }

            return;
        }

        if (wasLifecycleManagedKey && oldModel != null && !IsConstructableRegistered(oldModel))
        {
            oldModel.UnInitialize();
        }

        if (!hasModel)
        {
            model.SetDomain(this);
            model.Initialize();
        }
    }

    public void RegisterUtility<TUtility>(TUtility utility) where TUtility : IUtility
        => RegisterUtilityAs(utility);

    public void RegisterUtilityAs<TUtility>(TUtility utility) where TUtility : IUtility
    {
        EnsureNotUninitializing();

        var wasLifecycleManagedKey = _constructableKeys.Remove(typeof(TUtility));
        var oldUtility = _container.Register(utility);
        if (wasLifecycleManagedKey && oldUtility is IConstructable oldConstructable &&
            !IsConstructableRegistered(oldConstructable))
        {
            oldConstructable.UnInitialize();
        }
    }

    private void EnsureNotUninitializing()
    {
        if (_isUninitializing)
        {
            throw new InvalidOperationException("Cannot register components while the domain is uninitializing.");
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

    private bool IsConstructableRegistered(IConstructable constructable)
    {
        return GetRegisteredConstructables().Any(component => ReferenceEquals(component, constructable));
    }

    public TSystem GetSystem<TSystem>() where TSystem : class, ISystem
    {
        var result = _container.Get<TSystem>();
        if (result != null) return result;

        return _parent?.TryGetTarget(out var parentDomain) == true ? parentDomain.GetSystem<TSystem>() : null;
    }

    public TModel GetModel<TModel>() where TModel : class, IModel
    {
        var result = _container.Get<TModel>();
        if (result != null) return result;

        return _parent?.TryGetTarget(out var parentDomain) == true ? parentDomain.GetModel<TModel>() : null;
    }

    public TUtility GetUtility<TUtility>() where TUtility : class, IUtility
    {
        var result = _container.Get<TUtility>();
        if (result != null) return result;

        return _parent?.TryGetTarget(out var parentDomain) == true ? parentDomain.GetUtility<TUtility>() : null;
    }

    public IUnRegister RegisterEvent<TEvent>(Action<TEvent> onEvent) => _eventBus.Register(onEvent);

    public void UnRegisterEvent<TEvent>(Action<TEvent> onEvent) => _eventBus.UnRegister(onEvent);

    public void SendEvent<TEvent>() where TEvent : new() => _eventBus.Send<TEvent>();

    public void SendEvent<TEvent>(TEvent @event) => _eventBus.Send(@event);

    public void SendCommand<TCommand>(TCommand command) where TCommand : ICommand =>
        ExecuteCommand(command);

    protected virtual void ExecuteCommand<TCommand>(TCommand command) where TCommand : ICommand
    {
        command.SetDomain(this);
        command.Execute();
    }

    public TResult SendCommand<TResult>(ICommand<TResult> command) => ExecuteCommand(command);
    
    protected virtual TResult ExecuteCommand<TResult>(ICommand<TResult> command)
    {
        command.SetDomain(this);
        return command.Execute();
    }

    public TResult SendQuery<TResult>(IQuery<TResult> query) => ExecuteQuery(query);
    
    protected virtual TResult ExecuteQuery<TResult>(IQuery<TResult> query)
    {
        query.SetDomain(this);
        return query.Execute();
    }

    public override string ToString() => _container.ToString();
}
