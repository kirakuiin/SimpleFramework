namespace SimpleFramework.FrameworkImpl;

/// <summary>
/// 代表一个基本的事件。
/// </summary>
public interface IEvent
{
    /// <summary>
    /// 注册一个回调。
    /// </summary>
    /// <param name="onEvent">回调</param>
    /// <returns><see cref="IUnRegister"/></returns>
    IUnRegister Register(Action onEvent);
}

/// <summary>
/// 能够响应一个全局事件。
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IOnGlobalEvent<in T>
{
    void OnEvent(T @event);
}

/// <summary>
/// 可配置的自定义取消注册。
/// </summary>
public class CustomUnRegister : IUnRegister
{
    private Action _onUnRegister;

    public CustomUnRegister(Action onUnRegister)
    {
        _onUnRegister = onUnRegister;
    }
    
    public void UnRegister()
    {
        _onUnRegister?.Invoke();
        _onUnRegister = null!;
    }
}

/// <summary>
/// 单参数的基本事件类型。
/// </summary>
public class Event<T> : IEvent
{
    private Action<T> _onEvent = _ => {};
    
    /// <summary>
    /// 注册一个单参数的回调。
    /// </summary>
    /// <param name="onEvent"><see cref="Action{T}"/></param>
    /// <returns><see cref="IUnRegister"/></returns>
    public IUnRegister Register(Action<T> onEvent)
    {
        _onEvent += onEvent;
        return new CustomUnRegister(() => UnRegister(onEvent));
    }
    
    /// <summary>
    /// 取消一个注册。
    /// </summary>
    /// <param name="onEvent"><see cref="Action"/></param>
    public void UnRegister(Action<T> onEvent) => _onEvent -= onEvent;
    
    /// <summary>
    /// 触发事件。
    /// </summary>
    /// <param name="t"></param>
    public void Trigger(T t) => _onEvent.Invoke(t);

    IUnRegister IEvent.Register(Action onEvent)
    {
        return Register(Replacement);
        void Replacement(T _) => onEvent();
    }
}

/// <summary>
/// 事件容器。
/// </summary>
public class EventContainer
{
    private readonly Dictionary<Type, IEvent> _events = new();
    
    /// <summary>
    /// 添加新的事件。
    /// </summary>
    /// <typeparam name="T"><see cref="IEvent"/></typeparam>
    public void AddEvent<T>() where T : IEvent, new() =>
        _events.Add(typeof(T), new T());

    /// <summary>
    /// 查询事件。
    /// </summary>
    /// <typeparam name="T">事件类型</typeparam>
    /// <returns><see cref="IEvent"/></returns>
    public T GetEvent<T>() where T : IEvent =>
        (_events.TryGetValue(typeof(T), out var @event) ? (T)@event : default)!;
}

/// <summary>
/// 事件总线，充当订阅者和发布者的中间件。
/// </summary>
public class EventBus
{
    private readonly EventContainer _container = new();
    
    public static readonly EventBus Global = new();

    /// <summary>
    /// 发送事件，将会触发事件。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public void Send<T>() where T : new() =>
        _container.GetEvent<Event<T>>()?.Trigger(new T());
    
    /// <summary>
    /// 发送事件，将会触发事件。
    /// </summary>
    /// <param name="e">事件对象</param>
    /// <typeparam name="T"></typeparam>
    public void Send<T>(T e) => _container.GetEvent<Event<T>>()?.Trigger(e);

    /// <summary>
    /// 查询事件是否被注册过。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns>如果注册过返回<c>true</c></returns>
    public bool Contains<T>() => _container.GetEvent<Event<T>>() != null;

    public IUnRegister Register<T>(Action<T> onEvent)
    {
        if (!Contains<T>())
        {
            _container.AddEvent<Event<T>>();
        }

        return _container.GetEvent<Event<T>>().Register(onEvent);
    }

    public void UnRegister<T>(Action<T> onEvent)
    {
        _container.GetEvent<Event<T>>()?.UnRegister(onEvent);
    }
}