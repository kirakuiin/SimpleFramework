namespace SimpleFramework.Patterns;

/// <summary>
/// 发布者接口，实现此接口具有发布消息功能
/// </summary>
/// <typeparam name="T">消息类型。</typeparam>
public interface IPublisher<in T> : IGameService
{
    /// <summary>
    /// 发布消息
    /// </summary>
    /// <param name="message">要发布的消息。</param>
    public void Publish(T message);
}

/// <summary>
/// 订阅者接口，实现此接口具有订阅功能
/// </summary>
/// <typeparam name="T">消息类型。</typeparam>
public interface ISubscriber<out T> : IGameService
{
    /// <summary>
    /// 使用处理函数订阅此接口
    /// </summary>
    /// <param name="handler">处理函数</param>
    /// <returns>用于取消本次订阅的一次性句柄。</returns>
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
/// <typeparam name="T">消息类型。</typeparam>
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
/// <typeparam name="T">消息类型。</typeparam>
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
/// <typeparam name="T">消息类型。</typeparam>
public class MessageChannel<T> : IMessageChannel<T>
{
    private readonly List<Action<T>> _messageHandlers = new();

    private readonly Dictionary<Action<T>, bool> _pendingHandlers = new();
    private int _publishDepth;
    
    /// <inheritdoc />
    public bool IsDisposed { get; private set; }

    /// <summary>释放通道并移除所有订阅。</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>执行释放逻辑。</summary>
    /// <param name="isDisposing">是否由显式释放调用。</param>
    protected virtual void Dispose(bool isDisposing)
    {
        if (IsDisposed) return;

        IsDisposed = true;
        _pendingHandlers.Clear();
        if (_publishDepth == 0)
        {
            _messageHandlers.Clear();
        }
    }

    ~MessageChannel()
    {
        Dispose(false);
    }

    /// <inheritdoc />
    public virtual void Publish(T message)
    {
        ThrowIfDisposed();
        if (_publishDepth == 0)
        {
            ClearPendingHandlers();
        }

        _publishDepth++;
        try
        {
            PublishMessage(message);
        }
        finally
        {
            _publishDepth--;
            if (_publishDepth == 0)
            {
                if (IsDisposed)
                {
                    _messageHandlers.Clear();
                    _pendingHandlers.Clear();
                }
                else
                {
                    ClearPendingHandlers();
                }
            }
        }
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
            if (IsDisposed) break;
            if (IsSubscribed(handler))
            {
                handler(message);
            }
        }
    }

    /// <inheritdoc />
    public virtual IDisposable Subscribe(Action<T> handler)
    {
        ThrowIfDisposed();
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

    /// <inheritdoc />
    public void Unsubscribe(Action<T> handler)
    {
        ThrowIfDisposed();
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

    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
    }
}

/// <summary>
/// 处理激活的信道订阅和取消订阅相关问题
/// </summary>
/// <typeparam name="T">消息类型。</typeparam>
public sealed class DisposableSubscription<T> : IDisposable
{
    private Action<T>? _handler;
    private IMessageChannel<T>? _channel;
    private bool _isDisposed;

    /// <summary>创建一个绑定到指定订阅的取消句柄。</summary>
    /// <param name="messageChannel">订阅所属的通道。</param>
    /// <param name="handler">要在释放时取消的处理器。</param>
    public DisposableSubscription(IMessageChannel<T> messageChannel, Action<T> handler)
    {
        _channel = messageChannel;
        _handler = handler;
    }

    ~DisposableSubscription()
    {
        Dispose(false);
    }

    /// <summary>取消绑定的订阅；重复调用不执行操作。</summary>
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
/// <typeparam name="T">消息类型。</typeparam>
public class BufferedMessageChannel<T> : MessageChannel<T>, IBufferedMessageChannel<T>
{
    /// <inheritdoc />
    public override void Publish(T message)
    {
        ThrowIfDisposed();
        HasBufferedMessage = true;
        BufferedMessage = message;
        base.Publish(message);
    }

    /// <summary>
    /// 订阅消息；已有缓存时立即向新处理器重放最后一条消息。
    /// </summary>
    /// <param name="handler">消息处理器。</param>
    /// <returns>用于取消订阅的一次性句柄。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> 为 <see langword="null"/>。</exception>
    /// <remarks>重放处理器抛出异常时会取消本次订阅，并原样传播该异常。</remarks>
    public override IDisposable Subscribe(Action<T> handler)
    {
        var subscription = base.Subscribe(handler);

        if (HasBufferedMessage)
        {
            try
            {
                handler(BufferedMessage);
            }
            catch
            {
                subscription.Dispose();
                throw;
            }
        }

        return subscription;
    }

    /// <inheritdoc />
    public bool HasBufferedMessage { get; private set; } = false;

    /// <inheritdoc />
    public T BufferedMessage { get; private set; } = default!;
}
