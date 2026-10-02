namespace SimpleFramework.FrameworkImpl;

/// <summary>不携带参数的可订阅事件抽象。</summary>
public interface IEvent
{
    /// <summary>注册回调。</summary>
    IUnRegister Register(Action onEvent);
}

/// <summary>供事件容器（<see cref="EventBus"/>）的所有者统一失效不同消息类型的事件订阅。</summary>
internal interface IClearableEvent
{
    /// <summary>失效并移除全部订阅。</summary>
    void Clear();
}

/// <summary>使用一次性回调实现的幂等取消注册句柄。</summary>
public sealed class CustomUnRegister : IUnRegister
{
    /// <summary>取消注册回调；执行后置空以保证幂等。</summary>
    private Action? _onUnRegister;

    /// <summary>创建句柄。</summary>
    public CustomUnRegister(Action onUnRegister) =>
        _onUnRegister = onUnRegister ?? throw new ArgumentNullException(nameof(onUnRegister));

    /// <inheritdoc />
    public void UnRegister()
    {
        var callback = _onUnRegister;
        _onUnRegister = null;
        callback?.Invoke();
    }
}

/// <summary>使用写时复制监听器数组的单参数事件。</summary>
public sealed class Event<T> : IEvent, IClearableEvent
{
    /// <summary>
    /// 一次具体注册。对象本身就是注册身份，避免相等委托之间的 token 误注销。
    /// <para>失效时置空 <see cref="Handler"/>：分发使用的是快照，单靠从数组移除无法阻止本轮继续调用它。</para>
    /// </summary>
    private sealed class Subscription(Action<T> handler)
    {
        /// <summary>回调；为 <see langword="null"/> 表示订阅已失效。</summary>
        public Action<T>? Handler { get; private set; } = handler;

        /// <summary>使订阅失效，同时释放对回调目标的引用。</summary>
        public void Deactivate() => Handler = null;
    }

    /// <summary>当前订阅数组；只整体替换，不原地修改，分发时可直接作为快照。</summary>
    private Subscription[] _subscriptions = [];

    /// <summary>事件是否没有监听器。</summary>
    public bool IsEmpty => _subscriptions.Length == 0;

    /// <summary>按注册顺序添加监听器。</summary>
    public IUnRegister Register(Action<T> onEvent)
    {
        ArgumentNullException.ThrowIfNull(onEvent);

        var subscription = new Subscription(onEvent);
        var previous = _subscriptions;
        var next = new Subscription[previous.Length + 1];
        Array.Copy(previous, next, previous.Length);
        next[^1] = subscription;
        _subscriptions = next;

        return new CustomUnRegister(() => UnRegister(subscription));
    }

    /// <summary>移除第一次匹配的监听器。</summary>
    public void UnRegister(Action<T> onEvent)
    {
        ArgumentNullException.ThrowIfNull(onEvent);

        var subscriptions = _subscriptions;
        for (var index = 0; index < subscriptions.Length; index++)
        {
            if (subscriptions[index].Handler != onEvent) continue;

            subscriptions[index].Deactivate();
            RemoveAt(subscriptions, index);
            return;
        }
    }

    /// <summary>捕获当前数组并同步触发；跳过已失效的订阅，首次异常会立即停止分发。</summary>
    public void Trigger(T value)
    {
        // 分发是热路径，直接遍历快照不分配；本轮新增的订阅不在快照中，本轮失效的订阅在此跳过。
        var snapshot = _subscriptions;
        for (var index = 0; index < snapshot.Length; index++)
        {
            snapshot[index].Handler?.Invoke(value);
        }
    }

    /// <inheritdoc />
    IUnRegister IEvent.Register(Action onEvent)
    {
        ArgumentNullException.ThrowIfNull(onEvent);
        return Register(_ => onEvent());
    }

    /// <inheritdoc />
    void IClearableEvent.Clear()
    {
        foreach (var subscription in _subscriptions) subscription.Deactivate();
        _subscriptions = [];
    }

    /// <summary>按注册身份移除；已移除时无效果。</summary>
    private void UnRegister(Subscription subscription)
    {
        subscription.Deactivate();

        var subscriptions = _subscriptions;
        var index = Array.IndexOf(subscriptions, subscription);
        if (index >= 0) RemoveAt(subscriptions, index);
    }

    /// <summary>生成移除指定位置后的新数组。</summary>
    private void RemoveAt(Subscription[] subscriptions, int index)
    {
        if (subscriptions.Length == 1)
        {
            _subscriptions = [];
            return;
        }

        var next = new Subscription[subscriptions.Length - 1];
        Array.Copy(subscriptions, 0, next, 0, index);
        Array.Copy(subscriptions, index + 1, next, index, next.Length - index);
        _subscriptions = next;
    }
}

/// <summary>
/// 按消息类型保存事件，供 Domain 本地事件与 <see cref="EventHub"/> 共用，使两者分发语义一致。
/// <para>由所有者在释放时调用 <see cref="Clear"/>；EventHub 在释放后先行拒绝访问，因此清理后的订阅异常只会由 Domain 触发。</para>
/// </summary>
internal sealed class EventBus
{
    /// <summary>按消息类型保存的事件。</summary>
    private readonly Dictionary<Type, IClearableEvent> _events = new();

    /// <summary>是否已由所有者清理；清理后不再接受订阅，发送变为无操作。</summary>
    private bool _cleared;

    /// <summary>订阅指定类型的消息。</summary>
    /// <exception cref="InvalidOperationException">所有者已清理（Domain 正在释放）。</exception>
    public IUnRegister Register<T>(Action<T> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (_cleared) throw new InvalidOperationException("Domain 正在释放，不能订阅事件。");

        if (!_events.TryGetValue(typeof(T), out var value))
        {
            value = new Event<T>();
            _events.Add(typeof(T), value);
        }

        return ((Event<T>)value).Register(handler);
    }

    /// <summary>同步发送消息；清理后没有任何订阅，因此不会调用任何处理器。</summary>
    public void Send<T>(T message)
    {
        if (_events.TryGetValue(typeof(T), out var value)) ((Event<T>)value).Trigger(message);
    }

    /// <summary>失效全部订阅；正在进行的分发会跳过剩余处理器。</summary>
    public void Clear()
    {
        _cleared = true;
        foreach (var @event in _events.Values) @event.Clear();
        _events.Clear();
    }
}
