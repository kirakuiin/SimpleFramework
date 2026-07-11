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
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> 为 <see langword="null"/>。</exception>
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
    private sealed record Registration(Action<T> Handler, long Id);

    private sealed class DisposableSubscription(
        MessageChannel<T> channel,
        Action<T> handler,
        long registrationId) : IDisposable
    {
        private MessageChannel<T>? _channel = channel;

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _channel, null);
            owner?.Unsubscribe(handler, registrationId);
        }
    }

    private sealed class EmptySubscription : IDisposable
    {
        public static EmptySubscription Instance { get; } = new();

        public void Dispose()
        {
        }
    }

    private readonly List<Registration> _messageHandlers = new();

    private readonly Dictionary<Action<T>, Registration?> _pendingHandlers = new();
    private int _publishDepth;
    private long _nextRegistrationId;
    
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
        foreach (var (handler, registration) in _pendingHandlers)
        {
            _messageHandlers.RemoveAll(item => item.Handler == handler);
            if (registration is not null)
            {
                _messageHandlers.Add(registration);
            }
        }
        _pendingHandlers.Clear();
    }

    private void PublishMessage(T message)
    {
        foreach (var registration in _messageHandlers)
        {
            if (IsDisposed) break;
            if (IsSubscribed(registration))
            {
                registration.Handler(message);
            }
        }
    }

    /// <summary>订阅消息处理器；同一处理器重复订阅不会创建新的注册。</summary>
    /// <param name="handler">消息处理器。</param>
    /// <returns>新注册的显式取消句柄；处理器已订阅时返回无操作句柄。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> 为 <see langword="null"/>。</exception>
    /// <remarks>只有显式释放返回的句柄才会取消本次注册；句柄被垃圾回收不会改变订阅。</remarks>
    public virtual IDisposable Subscribe(Action<T> handler)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(handler);
        if (GetEffectiveRegistration(handler) is not null)
        {
            return EmptySubscription.Instance;
        }

        var registration = new Registration(handler, checked(++_nextRegistrationId));
        _pendingHandlers[handler] = registration;

        return new DisposableSubscription(this, handler, registration.Id);
    }

    private Registration? GetEffectiveRegistration(Action<T> handler)
    {
        if (_pendingHandlers.TryGetValue(handler, out var pendingRegistration))
        {
            return pendingRegistration;
        }

        return _messageHandlers.Find(registration => registration.Handler == handler);
    }

    private bool IsSubscribed(Registration registration) =>
        GetEffectiveRegistration(registration.Handler)?.Id == registration.Id;

    /// <inheritdoc />
    public void Unsubscribe(Action<T> handler)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(handler);
        if (GetEffectiveRegistration(handler) is null) return;

        _pendingHandlers[handler] = null;
    }

    private void Unsubscribe(Action<T> handler, long registrationId)
    {
        if (IsDisposed || GetEffectiveRegistration(handler)?.Id != registrationId) return;

        _pendingHandlers[handler] = null;
    }

    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
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
