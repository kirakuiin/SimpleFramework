namespace SimpleFramework.Utility;

/// <summary>
/// 通用资源管理抽象类，实现基础的资源管理。
/// </summary>
public abstract class Disposable : IDisposable
{
    /// <summary>
    /// 指示资源是否已经释放，供派生类实现幂等释放。
    /// </summary>
    protected bool IsDisposed = false;

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 实现释放资源接口。
    /// </summary>
    /// <param name="isDisposing">是否由用户主动调用。</param>
    protected abstract void Dispose(bool isDisposing);
}


/// <summary>
/// 管理多个可释放对象的组。
/// </summary>
public class DisposableGroup : Disposable
{
    private readonly List<IDisposable> _container = new();

    /// <summary>
    /// 添加一个可处理对象。
    /// </summary>
    /// <remarks>组已释放时，传入的非空对象会被立即释放；空值会被忽略。</remarks>
    /// <param name="disposable">要纳入组生命周期的对象。</param>
    public void Add(IDisposable? disposable)
    {
        if (disposable is null) return;
        if (IsDisposed)
        {
            disposable.Dispose();
            return;
        }

        _container.Add(disposable);
    }
    
    /// <inheritdoc/>
    /// <exception cref="AggregateException">一个或多个组内对象释放失败；所有对象仍会被尝试释放。</exception>
    protected override void Dispose(bool isDisposing)
    {
        if (IsDisposed) return;
        IsDisposed = true;

        if (isDisposing)
        {
            List<Exception>? exceptions = null;
            foreach (var element in _container)
            {
                try
                {
                    element?.Dispose();
                }
                catch (Exception exception)
                {
                    (exceptions ??= []).Add(exception);
                }
            }

            _container.Clear();
            if (exceptions is not null)
            {
                throw new AggregateException("释放组内资源时发生一个或多个错误。", exceptions);
            }
        }
    }
}
