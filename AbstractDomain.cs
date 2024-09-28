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

    private IDomain _parent;
    
    
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
        _container.GetComponents<ISystem>().ToList().ForEach(
            system => system.UnInitialize());
        _container.GetComponents<IModel>().ToList().ForEach(
            system => system.UnInitialize());
        _container.Clear();
        _domain = null;
    }

    public void SetParent(IDomain parent)
    {
        _parent = parent;
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
        return _container.Get<TSystem>() ?? _parent?.GetSystem<TSystem>();
    }

    public TModel GetModel<TModel>() where TModel : class, IModel
    {
        return _container.Get<TModel>() ?? _parent?.GetModel<TModel>();
    }

    public TUtility GetUtility<TUtility>() where TUtility : class, IUtility
    {
        return _container.Get<TUtility>() ?? _parent?.GetUtility<TUtility>();
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
