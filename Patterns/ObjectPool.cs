namespace SimpleFramework.Patterns;

/// <summary>
/// 对象池接口
/// </summary>
/// <typeparam name="T">池中对象的类型。</typeparam>
public interface IObjectPool<T> : IDisposable
{
    /// <summary>
    /// 获取对象
    /// </summary>
    /// <returns>从池中复用或新建的对象。</returns>
    T Get();
    
    /// <summary>
    /// 归还对象
    /// </summary>
    /// <param name="obj">要归还的对象。</param>
    /// <exception cref="ArgumentNullException"><paramref name="obj"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="InvalidOperationException">同一对象已经位于池中。</exception>
    void Return(T obj);
}

/// <summary>
/// 监听对象创建归还接口
/// </summary>
public interface IPoolCallbackListener
{
    /// <summary>
    /// 创建或从池中取出对象时触发
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
/// <typeparam name="T">池中对象的引用类型。</typeparam>
public abstract class AbstractObjectPool<T> : IObjectPool<T>
    where T : class
{
    protected readonly Stack<T> Stack = new(32);
    private readonly HashSet<T> _pooledInstances = new(ReferenceEqualityComparer.Instance);

    private bool _isDisposed;

    /// <summary>创建一个新对象。</summary>
    /// <returns>新创建的对象。</returns>
    protected abstract T CreateInstance();

    /// <summary>销毁一个仍位于池中的对象。</summary>
    /// <param name="instance">要销毁的对象。</param>
    protected virtual void OnDestroy(T instance) { }

    /// <summary>对象借出前调用。</summary>
    /// <param name="instance">即将借出的对象。</param>
    protected virtual void OnGet(T instance) { }

    /// <summary>对象入池前调用。</summary>
    /// <param name="instance">即将归还的对象。</param>
    protected virtual void OnReturn(T instance) { }

    /// <inheritdoc />
    public T Get()
    {
        ThrowIfDisposed();
        if (!Stack.TryPop(out var obj))
        {
            obj = CreateInstance();
        }
        else
        {
            _pooledInstances.Remove(obj);
        }

        OnGet(obj);
        if (obj is IPoolCallbackListener receiver) receiver.OnGet();
        return obj;
    }

    /// <inheritdoc />
    public void Return(T obj)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(obj);
        if (!_pooledInstances.Add(obj))
        {
            throw new InvalidOperationException("同一对象不能重复归还到对象池。");
        }

        try
        {
            OnReturn(obj);
            if (obj is IPoolCallbackListener receiver) receiver.OnReturn();
            Stack.Push(obj);
        }
        catch
        {
            _pooledInstances.Remove(obj);
            throw;
        }
    }

    /// <summary>
    /// 销毁并移除池中当前闲置的所有对象。
    /// </summary>
    public void Clear()
    {
        ThrowIfDisposed();
        while (Stack.TryPop(out var obj))
        {
            _pooledInstances.Remove(obj);
            OnDestroy(obj);
        }
    }

    /// <summary>
    /// 预先创建对象
    /// </summary>
    /// <param name="count">要预创建的对象数量；负数等同于零。</param>
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
    /// 获取当前位于池中、可供复用的对象数量。
    /// </summary>
    public int Count => Stack.Count;

    /// <summary>
    /// 释放对象池并销毁池中当前闲置的对象。
    /// </summary>
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
/// <param name="onGet">创建或从池中取出对象时调用的函数</param>
/// <param name="onReturn">将对象归还到池子触发的函数</param>
/// <param name="onDestroy">销毁对象触发的函数</param>
/// <typeparam name="T">池中对象的引用类型。</typeparam>
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
