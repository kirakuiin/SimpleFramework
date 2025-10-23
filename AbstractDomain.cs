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
    
    private readonly List<WeakReference<IDomain>> _children = new();
    
    public static T Instance => _domain ??= BuildDomain();
    
    private static T BuildDomain()
    {
        var domain = new T();
        domain.Init();
        return domain;
    }

    protected abstract void Init();

    public void UnInitialize()
    {
        foreach (var child in _children)
        {
            if (child.TryGetTarget(out var domain))
            {
                domain.UnInitialize();
            }
        }
        _children.Clear();
        
        _container.GetComponents<ISystem>().ToList().ForEach(
            system => system.UnInitialize());
        _container.GetComponents<IModel>().ToList().ForEach(
            system => system.UnInitialize());
        _container.Clear();
        _domain = null;
        _parent = null;
        UnInit();
    }
    
    protected virtual void UnInit() {}

    public IDomain Parent => _parent?.TryGetTarget(out var parent) == true ? parent : null;

    public void SetParent(IDomain parent)
    {
        if (parent == null || ReferenceEquals(Parent, parent)) return;
        
        if (_parent?.TryGetTarget(out var prevParent) == true)
        {
            prevParent.RemoveChild(this);
        }
        _parent = new WeakReference<IDomain>(parent);
    }

    public void AddChild(IDomain child)
    {
        RemoveChild(child);
        _children.Add(new WeakReference<IDomain>(child));
        if (!ReferenceEquals(child.Parent, this))
        {
            child.SetParent(this);
        }
    }

    public void RemoveChild(IDomain child)
    {
        if (child == null) return;

        // 遍历子域列表，找到匹配的弱引用并移除
        for (var i = _children.Count - 1; i >= 0; i--)
        {
            if (!_children[i].TryGetTarget(out var existingChild) || !ReferenceEquals(existingChild, child)) continue;
            _children.RemoveAt(i);
            break;
        }
    }

    public void RegisterSystem<TSystem>(TSystem system) where TSystem : ISystem
    {
        system.SetDomain(this);
        _container.Register(system);
        system.Initialize();
    }

    public void RegisterModel<TModel>(TModel model) where TModel : IModel
    {
        model.SetDomain(this);
        _container.Register(model);
        model.Initialize();
    }

    public void RegisterUtility<TUtility>(TUtility utility) where TUtility : IUtility
        => _container.Register(utility);

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
