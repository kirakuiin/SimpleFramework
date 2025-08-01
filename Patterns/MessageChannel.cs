namespace SimpleFramework.Patterns;

/// <summary>
/// 发布者接口，实现此接口具有发布消息功能
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IPublisher<in T> : IGameService
{
    /// <summary>
    /// 发布消息
    /// </summary>
    /// <param name="message"></param>
    public void Publish(T message);
}

/// <summary>
/// 订阅者接口，实现此接口具有订阅功能
/// </summary>
/// <typeparam name="T"></typeparam>
public interface ISubscriber<out T> : IGameService
{
    /// <summary>
    /// 使用处理函数订阅此接口
    /// </summary>
    /// <param name="handler">处理函数</param>
    /// <returns></returns>
    public IDisposable Subscribe(Action<T> handler);

    /// <summary>
    /// 取消订阅
    /// </summary>
    /// <param name="handler">处理函数</param>
    public void Unsubscribe(Action<T> handler);
}

/// <summary>
/// 信息通道，同时支持订阅和发布功能。
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IMessageChannel<T> : IPublisher<T>, ISubscriber<T>, IDisposable
{
    /// <summary>
    /// 是否已经释放完毕
    /// </summary>
    public bool IsDisposed { get; }
}

/// <summary>
/// 支持缓存功能的信息通道。
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IBufferedMessageChannel<T> : IMessageChannel<T>
{
    /// <summary>
    /// 是否存在缓存的消息。
    /// </summary>
    bool HasBufferedMessage { get; }
    
    /// <summary>
    /// 被缓存的消息。
    /// </summary>
    T BufferedMessage { get; }
}

/// <summary>
/// 基础版本的信道
/// </summary>
/// <typeparam name="T"></typeparam>
public class MessageChannel<T> : IMessageChannel<T>
{
    private readonly List<Action<T>> _messageHandlers = new();

    private readonly Dictionary<Action<T>, bool> _pendingHandlers = new();
    
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool isDisposing)
    {
        if (IsDisposed) return;

        IsDisposed = true;
        _messageHandlers.Clear();
        _pendingHandlers.Clear();
    }

    ~MessageChannel()
    {
        Dispose(false);
    }

    public virtual void Publish(T message)
    {
        ClearPendingHandlers();
        PublishMessage(message);
    }

    private void ClearPendingHandlers()
    {
        foreach (var handler in _pendingHandlers.Keys)
        {
            var shouldBeAdded = _pendingHandlers[handler];
            if (shouldBeAdded)
            {
                _messageHandlers.Add(handler);
            }
            else
            {
                _messageHandlers.Remove(handler);
            }
        }
        _pendingHandlers.Clear();
    }

    private void PublishMessage(T message)
    {
        foreach (var handler in _messageHandlers)
        {
            handler?.Invoke(message);
        }
    }

    public virtual IDisposable Subscribe(Action<T> handler)
    {
        if (!_pendingHandlers.TryAdd(handler, true))
        {
            var shouldBeRemove = !_pendingHandlers[handler];
            if (shouldBeRemove)
            {
                _pendingHandlers.Remove(handler);
            }
        }

        return new DisposableSubscription<T>(this, handler);
    }

    private bool IsSubscribed(Action<T> handler)
    {
        var isPendingRemoval = _pendingHandlers.ContainsKey(handler) && !_pendingHandlers[handler];
        var isPendingAdding = _pendingHandlers.ContainsKey(handler) && _pendingHandlers[handler];
        return (_messageHandlers.Contains(handler) && !isPendingRemoval) || isPendingAdding;
    }

    public void Unsubscribe(Action<T> handler)
    {
        if (!IsSubscribed(handler)) return;

        if (!_pendingHandlers.TryAdd(handler, false))
        {
            var shouldBeAdded = _pendingHandlers[handler];
            if (shouldBeAdded)
            {
                _pendingHandlers.Remove(handler);
            }
        }
    }
}

/// <summary>
/// 处理激活的信道订阅和取消订阅相关问题
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class DisposableSubscription<T> : IDisposable
{
    private Action<T>? _handler;
    private IMessageChannel<T>? _channel;
    private bool _isDisposed;

    public DisposableSubscription(IMessageChannel<T> messageChannel, Action<T> handler)
    {
        _channel = messageChannel;
        _handler = handler;
    }

    ~DisposableSubscription()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool isDisposing)
    {
        if (_isDisposed) return;
        
        _isDisposed = true;
        if (_channel is { IsDisposed: false })
        {
            if (_handler != null)
            {
                _channel.Unsubscribe(_handler);
            }
        }
        _handler = null;
        _channel = null;
    }
}

/// <summary>
/// 缓存上次发布的消息，有新的订阅时会自动向新订阅发送之前的消息。
/// </summary>
/// <typeparam name="T"></typeparam>
public class BufferedMessageChannel<T> : MessageChannel<T>, IBufferedMessageChannel<T>
{
    public override void Publish(T message)
    {
        HasBufferedMessage = true;
        BufferedMessage = message;
        base.Publish(message);
    }

    public override IDisposable Subscribe(Action<T> handler)
    {
        var subscription = base.Subscribe(handler);

        if (HasBufferedMessage)
        {
            handler?.Invoke(BufferedMessage);
        }

        return subscription;
    }

    public bool HasBufferedMessage { get; private set; } = false;

    public T BufferedMessage { get; private set; } = default!;
}
