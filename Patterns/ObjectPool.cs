namespace SimpleFramework.Patterns;

/// <summary>
/// 对象池接口
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IObjectPool<T> : IDisposable
{
    /// <summary>
    /// 获取对象
    /// </summary>
    /// <returns></returns>
    T Get();
    
    /// <summary>
    /// 归还对象
    /// </summary>
    /// <param name="obj"></param>
    void Return(T obj);
}

/// <summary>
/// 监听对象创建归还接口
/// </summary>
public interface IPoolCallbackListener
{
    /// <summary>
    /// 从池中获取对象时触发
    /// </summary>
    void OnGet();
    
    /// <summary>
    /// 归还对象时触发
    /// </summary>
    void OnReturn();
}


/// <summary>
/// 对象池抽象类
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class AbstractObjectPool<T> : IObjectPool<T>
    where T : class
{
    protected readonly Stack<T> Stack = new(32);

    private bool _isDisposed;

    protected abstract T CreateInstance();
    protected virtual void OnDestroy(T instance) { }

    protected virtual void OnGet(T instance) { }
    protected virtual void OnReturn(T instance) { }

    public T Get()
    {
        ThrowIfDisposed();
        if (!Stack.TryPop(out var obj)) return CreateInstance();
        OnGet(obj);
        if (obj is IPoolCallbackListener receiver) receiver.OnGet();
        return obj;
    }

    public void Return(T obj)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(obj);

        OnReturn(obj);
        if (obj is IPoolCallbackListener receiver) receiver.OnReturn();
        Stack.Push(obj);
    }

    /// <summary>
    /// 清理对象池
    /// </summary>
    public void Clear()
    {
        ThrowIfDisposed();
        while (Stack.TryPop(out var obj))
        {
            OnDestroy(obj);
        }
    }

    /// <summary>
    /// 预先创建对象
    /// </summary>
    /// <param name="count"></param>
    public void Prewarm(int count)
    {
        ThrowIfDisposed();
        for (var i = 0; i < count; i++)
        {
            var instance = CreateInstance();
            Return(instance);
        }
    }

    /// <summary>
    /// 对象池数量
    /// </summary>
    public int Count => Stack.Count;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放对象池资源
    /// </summary>
    /// <param name="disposing">是否正在释放托管资源</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed) return;

        if (disposing)
        {
            Clear();
        }

        _isDisposed = true;
    }

    /// <summary>
    /// 析构函数
    /// </summary>
    ~AbstractObjectPool()
    {
        Dispose(false);
    }

    void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }
}


/// <summary>
/// 一般对象池
/// </summary>
/// <param name="createFunc">创建时调用的函数</param>
/// <param name="onGet">从池子中拿去时调用的函数</param>
/// <param name="onReturn">将对象归还到池子触发的函数</param>
/// <param name="onDestroy">销毁对象触发的函数</param>
/// <typeparam name="T"></typeparam>
public sealed class ObjectPool<T>(
    Func<T> createFunc,
    Action<T>? onGet = null,
    Action<T>? onReturn = null,
    Action<T>? onDestroy = null)
    : AbstractObjectPool<T>
    where T : class
{
    private readonly Func<T> _createFunc = createFunc ?? throw new ArgumentException(null, nameof(createFunc));

    protected override T CreateInstance()
    {
        return _createFunc();
    }

    protected override void OnDestroy(T instance)
    {
        onDestroy?.Invoke(instance);
    }

    protected override void OnGet(T instance)
    {
        onGet?.Invoke(instance);
    }

    protected override void OnReturn(T instance)
    {
        onReturn?.Invoke(instance);
    }
}
