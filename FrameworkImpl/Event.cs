namespace SimpleFramework.FrameworkImpl;

/// <summary>不携带参数的可订阅事件抽象。</summary>
public interface IEvent
{
    /// <summary>注册回调。</summary>
    IUnRegister Register(Action onEvent);
}

/// <summary>使用一次性回调实现的幂等取消注册句柄。</summary>
public sealed class CustomUnRegister : IUnRegister
{
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
public sealed class Event<T> : IEvent
{
    /// <summary>为一次具体注册提供独立身份，避免相等委托之间的 token 误注销。</summary>
    private sealed class Subscription(Action<T> handler)
    {
        public Action<T> Handler { get; } = handler;
    }

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
            RemoveAt(subscriptions, index);
            return;
        }
    }

    /// <summary>捕获当前数组并同步触发；首次异常会立即停止分发。</summary>
    public void Trigger(T value)
    {
        var snapshot = _subscriptions;
        for (var index = 0; index < snapshot.Length; index++) snapshot[index].Handler(value);
    }

    IUnRegister IEvent.Register(Action onEvent) => Register(_ => onEvent());

    private void UnRegister(Subscription subscription)
    {
        var subscriptions = _subscriptions;
        for (var index = 0; index < subscriptions.Length; index++)
        {
            if (!ReferenceEquals(subscriptions[index], subscription)) continue;
            RemoveAt(subscriptions, index);
            return;
        }
    }

    private void RemoveAt(Subscription[] subscriptions, int index)
    {
        if (subscriptions.Length == 1)
        {
            _subscriptions = [];
            return;
        }

        var next = new Subscription[subscriptions.Length - 1];
        if (index > 0) Array.Copy(subscriptions, 0, next, 0, index);
        if (index < subscriptions.Length - 1)
        {
            Array.Copy(subscriptions, index + 1, next, index, subscriptions.Length - index - 1);
        }
        _subscriptions = next;
    }
}

/// <summary>保存单个 Domain 的本地事件，并在 Domain 清理后使旧 token 安全失效。</summary>
internal sealed class DomainEventBus
{
    private readonly Dictionary<Type, object> _events = new();
    private bool _cleared;

    public IUnRegister Register<T>(Action<T> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (_cleared) throw new InvalidOperationException("Domain 事件容器已清理。");
        if (!_events.TryGetValue(typeof(T), out var value))
        {
            value = new Event<T>();
            _events.Add(typeof(T), value);
        }

        var @event = (Event<T>)value;
        var inner = @event.Register(handler);
        return new CustomUnRegister(() =>
        {
            inner.UnRegister();
            if (!_cleared && @event.IsEmpty && _events.TryGetValue(typeof(T), out var current) && ReferenceEquals(current, @event))
            {
                _events.Remove(typeof(T));
            }
        });
    }

    public void Send<T>(T message)
    {
        if (_cleared) throw new InvalidOperationException("Domain 事件容器已清理。");
        if (_events.TryGetValue(typeof(T), out var value)) ((Event<T>)value).Trigger(message);
    }

    public void Clear()
    {
        _cleared = true;
        _events.Clear();
    }
}
