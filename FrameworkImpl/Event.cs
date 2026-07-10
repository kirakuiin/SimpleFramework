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
    /// <summary>
    /// 处理收到的全局事件。
    /// </summary>
    /// <param name="event">事件数据。</param>
    void OnEvent(T @event);
}

/// <summary>
/// 可配置的自定义取消注册。
/// </summary>
public class CustomUnRegister : IUnRegister
{
    private Action? _onUnRegister;

    /// <summary>
    /// 使用指定的取消注册回调创建实例。
    /// </summary>
    /// <param name="onUnRegister">首次取消注册时执行的回调。</param>
    public CustomUnRegister(Action onUnRegister)
    {
        _onUnRegister = onUnRegister;
    }
    
    /// <summary>
    /// 执行取消注册回调；无论回调是否抛出异常，该回调都最多执行一次。
    /// </summary>
    /// <exception cref="Exception">取消注册回调抛出的原始异常会直接传播。</exception>
    public void UnRegister()
    {
        var onUnRegister = _onUnRegister;
        _onUnRegister = null;
        onUnRegister?.Invoke();
    }
}

/// <summary>
/// 单参数的基本事件类型。
/// </summary>
public class Event<T> : IEvent
{
    private readonly List<Action<T>> _listeners = new();

    /// <summary>
    /// 获取当前是否没有监听器。
    /// </summary>
    public bool IsEmpty => _listeners.Count == 0;
    
    /// <summary>
    /// 注册一个单参数的回调。
    /// </summary>
    /// <param name="onEvent"><see cref="Action{T}"/></param>
    /// <returns><see cref="IUnRegister"/></returns>
    public IUnRegister Register(Action<T> onEvent)
    {
        _listeners.Add(onEvent);
        return new CustomUnRegister(() => UnRegister(onEvent));
    }
    
    /// <summary>
    /// 取消一个注册。
    /// </summary>
    /// <param name="onEvent"><see cref="Action"/></param>
    public void UnRegister(Action<T> onEvent) => _listeners.Remove(onEvent);
    
    /// <summary>
    /// 触发事件。
    /// </summary>
    /// <param name="t"></param>
    public void Trigger(T t)
    {
        foreach (var listener in _listeners.ToArray())
        {
            listener.Invoke(t);
        }
    }

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
    /// <typeparam name="T">要创建的事件类型。</typeparam>
    /// <returns>新建并存储的事件。</returns>
    public T AddEvent<T>() where T : IEvent, new()
    {
        var @event = new T();
        _events.Add(typeof(T), @event);
        return @event;
    }

    /// <summary>
    /// 查询事件。
    /// </summary>
    /// <typeparam name="T">事件类型</typeparam>
    /// <returns>已注册的事件；不存在时返回 <c>default(T)</c>，仅当 <typeparamref name="T"/> 为引用类型时该值为 <see langword="null"/>。</returns>
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    public T GetEvent<T>() where T : IEvent =>
        _events.TryGetValue(typeof(T), out var @event) && @event is T result ? result : default;

    /// <summary>
    /// 移除事件。
    /// </summary>
    /// <typeparam name="T">事件类型</typeparam>
    public void RemoveEvent<T>() where T : IEvent =>
        _events.Remove(typeof(T));

    /// <summary>
    /// 清空事件。
    /// </summary>
    public void Clear() => _events.Clear();
}

/// <summary>
/// 事件总线，充当订阅者和发布者的中间件。
/// </summary>
public class EventBus
{
    private readonly EventContainer _container = new();
    
    /// <summary>
    /// 全局事件
    /// </summary>
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

    /// <summary>
    /// 注册事件
    /// </summary>
    /// <param name="onEvent"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public IUnRegister Register<T>(Action<T> onEvent)
    {
        var @event = _container.GetEvent<Event<T>>() ?? _container.AddEvent<Event<T>>();
        @event.Register(onEvent);
        return new CustomUnRegister(() =>
        {
            @event.UnRegister(onEvent);
            if (@event.IsEmpty && ReferenceEquals(_container.GetEvent<Event<T>>(), @event))
            {
                _container.RemoveEvent<Event<T>>();
            }
        });
    }

    /// <summary>
    /// 取消注册
    /// </summary>
    /// <param name="onEvent"></param>
    /// <typeparam name="T"></typeparam>
    public void UnRegister<T>(Action<T> onEvent)
    {
        var @event = _container.GetEvent<Event<T>>();
        if (@event == null)
        {
            return;
        }

        @event.UnRegister(onEvent);
        if (@event.IsEmpty)
        {
            _container.RemoveEvent<Event<T>>();
        }
    }

    /// <summary>
    /// 清空事件。
    /// </summary>
    public void Clear() => _container.Clear();
}
