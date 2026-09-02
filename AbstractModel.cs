namespace SimpleFramework;

/// <summary>可选的 Model 生命周期基类，安全保存框架 Context。</summary>
public abstract class AbstractModel : IModelLifecycle
{
    private IModelContext? _context;

    /// <summary>获取当前 Model Context；初始化前或释放完成后访问会失败。</summary>
    protected IModelContext Context => _context ?? throw new InvalidOperationException("Model Context 当前不可用。");

    void IModelLifecycle.Initialize(IModelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_context is not null) throw new InvalidOperationException("Model 不能重复初始化。");
        _context = context;
        OnInitialize();
    }

    void IModelLifecycle.Release()
    {
        try { OnRelease(); }
        finally { _context = null; }
    }

    /// <summary>初始化 Model。</summary>
    protected abstract void OnInitialize();

    /// <summary>释放 Model 自身资源；初始化部分失败后也可能调用。</summary>
    protected virtual void OnRelease() { }
}
