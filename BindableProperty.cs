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
    /// 注册回调，并且在注册时就会触发一次回调。
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
    /// 取消注册。
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
    new T Value { get; set; }
    
    /// <summary>
    /// 设置新的值并且不触发事件。
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

    public static Func<T, T, bool> Comparer { get; set; } = (a, b) => a.Equals(b);

    private Action<T, T> OnValueChanged { get; set; } = (_, _) => {};

    public BindableProperty(T initialValue = default) => _value = initialValue;

    public BindableProperty<T> WithComparer(Func<T, T, bool> comparer)
    {
        Comparer = comparer;
        return this;
    }

    public T Value
    {
        get => GetValue();
        set
        {
            if (_value == null && value == null) return;
            if (_value != null && Comparer(_value, value)) return;

            var prev = GetValue();
            SetValue(value);
            OnValueChanged.Invoke(prev, Value);
        }
    }
    
    protected virtual void SetValue(T value) => _value = value;

    protected virtual T GetValue() => _value;
    
    IUnRegister IEvent.Register(Action onEvent)
    {
        return Register(Replace);
        void Replace(T prev, T curr) => onEvent();
    }

    public void SetValueWithoutNotify(T value) => SetValue(value);

    public IUnRegister RegisterWithNotify(Action<T, T> onValueChanged)
    {
        var val = Value;
        onValueChanged.Invoke(val, val);
        return Register(onValueChanged);
    }

    public IUnRegister Register(Action<T, T> onValueChanged)
    {
        OnValueChanged += onValueChanged;
        return new BindablePropertyUnRegister<T>(this, onValueChanged);
    }

    public void UnRegister(Action<T, T> onValueChanged)
    {
        OnValueChanged -= onValueChanged;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>
/// 用于解除可绑定属性的注册关系的类。
/// </summary>
/// <typeparam name="T"></typeparam>
internal class BindablePropertyUnRegister<T> : IUnRegister
{
    private IReadonlyBindableProperty<T> _property;
   
    private Action<T, T> _onValueChanged;
    
    public BindablePropertyUnRegister(IReadonlyBindableProperty<T> property, Action<T, T> onValueChanged)
    {
        _property = property;
        _onValueChanged = onValueChanged;
    }
    
    public void UnRegister()
    {
        _property.UnRegister(_onValueChanged);
        _property = null;
        _onValueChanged = null;
    }
}