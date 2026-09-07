using SimpleFramework.FrameworkImpl;

namespace SimpleFramework;

/// <summary>
/// 只读的可绑定属性。
/// <para>一个可绑定属性代表着当它的属性变化时可以进行监听。</para>
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IReadonlyBindableProperty<out T> : IEvent
{
    /// <summary>
    /// 获取存储的值。
    /// </summary>
    T Value { get; }
    
    /// <summary>
    /// 注册回调，并且在注册时就会触发一次回调；首次通知同样禁止重入写入不同值。
    /// </summary>
    /// <param name="onValueChanged"></param>
    /// <returns></returns>
    IUnRegister RegisterWithNotify(Action<T, T> onValueChanged);
    
    /// <summary>
    /// 仅注册回调，不会触发。
    /// </summary>
    /// <param name="onValueChanged"></param>
    /// <returns></returns>
    IUnRegister Register(Action<T, T> onValueChanged);

    /// <summary>
    /// 取消最后一次匹配的注册；不会影响其他注册所对应的 token。
    /// </summary>
    /// <param name="onValueChanged"></param>
    void UnRegister(Action<T, T> onValueChanged);
}

/// <summary>
/// 可写的绑定属性。
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IBindableProperty<T> : IReadonlyBindableProperty<T>
{
    /// <summary>
    /// 获取或设置存储的值；值发生变化时通知监听器。
    /// <para>同步通知期间写入不同值会抛出 <see cref="InvalidOperationException"/>；比较器判定相等的写入仍为无操作。</para>
    /// </summary>
    new T Value { get; set; }
    
    /// <summary>
    /// 设置新的值并且不触发事件。
    /// <para>仍使用当前比较器，并在当前属性正在通知监听器时拒绝不同值写入。</para>
    /// </summary>
    /// <param name="value"></param>
    void SetValueWithoutNotify(T value);
}

/// <summary>
/// 可读写绑定属性的具体实现。
/// </summary>
/// <typeparam name="T"></typeparam>
public class BindableProperty<T> : IBindableProperty<T>
{
    private T _value;
    private bool _isNotifying;

    private Func<T, T, bool> _comparer = EqualityComparer<T>.Default.Equals;

    /// <summary>一次独立的属性订阅，使相等委托的 token 仍具有不同身份。</summary>
    private sealed class Subscription(Action<T, T> handler)
    {
        public Action<T, T> Handler { get; } = handler;
    }

    private Subscription[] _subscriptions = [];

    /// <summary>
    /// 使用指定初始值创建可绑定属性。
    /// </summary>
    /// <param name="initialValue">初始值。</param>
    public BindableProperty(T initialValue = default!) => _value = initialValue;

    /// <summary>
    /// 设置当前实例用于判断值是否相等的比较器。
    /// </summary>
    /// <param name="comparer">相等比较器；为 <see langword="null"/> 时恢复默认比较器。</param>
    /// <returns>当前可绑定属性。</returns>
    public BindableProperty<T> WithComparer(Func<T, T, bool>? comparer)
    {
        _comparer = comparer ?? EqualityComparer<T>.Default.Equals;
        return this;
    }

    /// <inheritdoc />
    public T Value
    {
        get => GetValue();
        set
        {
            if (!CanWriteValue(value, out var prev)) return;

            SetValue(value);
            _isNotifying = true;
            try
            {
                var snapshot = _subscriptions;
                var current = Value;
                foreach (var subscription in snapshot) subscription.Handler(prev, current);
            }
            finally
            {
                _isNotifying = false;
            }
        }
    }
    
    /// <summary>
    /// 写入底层值。
    /// </summary>
    /// <param name="value">新值。</param>
    protected virtual void SetValue(T value) => _value = value;

    /// <summary>
    /// 读取底层值。
    /// </summary>
    /// <returns>当前值。</returns>
    protected virtual T GetValue() => _value;
    
    IUnRegister IEvent.Register(Action onEvent)
    {
        ArgumentNullException.ThrowIfNull(onEvent);
        return Register(Replace);
        void Replace(T prev, T curr) => onEvent();
    }

    /// <inheritdoc />
    public void SetValueWithoutNotify(T value)
    {
        if (!CanWriteValue(value, out _)) return;
        SetValue(value);
    }

    private bool CanWriteValue(T value, out T previous)
    {
        previous = GetValue();
        if (_comparer(previous, value)) return false;
        if (_isNotifying)
        {
            throw new InvalidOperationException("Cannot change BindableProperty while notifying listeners.");
        }

        return true;
    }

    /// <inheritdoc />
    public IUnRegister RegisterWithNotify(Action<T, T> onValueChanged)
    {
        ArgumentNullException.ThrowIfNull(onValueChanged);
        var val = Value;
        var wasNotifying = _isNotifying;
        _isNotifying = true;
        try { onValueChanged.Invoke(val, val); }
        // 首次通知也可能嵌套在普通通知中，必须保留外层的写入保护。
        finally { _isNotifying = wasNotifying; }
        return Register(onValueChanged);
    }

    /// <inheritdoc />
    public IUnRegister Register(Action<T, T> onValueChanged)
    {
        ArgumentNullException.ThrowIfNull(onValueChanged);
        var subscription = new Subscription(onValueChanged);
        var next = new Subscription[_subscriptions.Length + 1];
        Array.Copy(_subscriptions, next, _subscriptions.Length);
        next[^1] = subscription;
        _subscriptions = next;
        return new CustomUnRegister(() => UnRegister(subscription));
    }

    /// <inheritdoc />
    public void UnRegister(Action<T, T> onValueChanged)
    {
        for (var index = _subscriptions.Length - 1; index >= 0; index--)
        {
            if (_subscriptions[index].Handler != onValueChanged) continue;
            RemoveAt(index);
            return;
        }
    }

    private void UnRegister(Subscription subscription)
    {
        var index = Array.IndexOf(_subscriptions, subscription);
        if (index >= 0) RemoveAt(index);
    }

    private void RemoveAt(int index)
    {
        var next = new Subscription[_subscriptions.Length - 1];
        Array.Copy(_subscriptions, 0, next, 0, index);
        Array.Copy(_subscriptions, index + 1, next, index, next.Length - index);
        _subscriptions = next;
    }

    /// <summary>
    /// 返回当前值的字符串表示；值为 <see langword="null"/> 时返回空字符串。
    /// </summary>
    /// <returns>当前值的字符串表示。</returns>
    public override string ToString() => Value?.ToString() ?? string.Empty;
}
