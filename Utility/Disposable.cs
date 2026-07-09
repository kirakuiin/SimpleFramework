namespace SimpleFramework.Utility;

/// <summary>
/// 通用资源管理抽象类，实现基础的资源管理。
/// </summary>
public abstract class Disposable : IDisposable
{
    protected bool IsDisposed = false;
    
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
    /// <param name="disposable"></param>
    public void Add(IDisposable disposable)
    {
        _container.Add(disposable);
    }
    
    protected override void Dispose(bool isDisposing)
    {
        if (IsDisposed) return;
        IsDisposed = true;

        if (isDisposing)
        {
            foreach (var element in _container)
            {
                element?.Dispose();
            }

            _container.Clear();
        }
    }
}
