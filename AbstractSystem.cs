namespace SimpleFramework;

/// <summary>可选的 System 生命周期基类，安全保存框架 Context。</summary>
public abstract class AbstractSystem : ISystemLifecycle
{
    private ISystemContext? _context;

    /// <summary>获取当前 System Context；初始化前或释放完成后访问会失败。</summary>
    protected ISystemContext Context => _context ?? throw new InvalidOperationException("System Context 当前不可用。");

    void ISystemLifecycle.Initialize(ISystemContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_context is not null) throw new InvalidOperationException("System 不能重复初始化。");
        _context = context;
        OnInitialize();
    }

    void ISystemLifecycle.Release()
    {
        try { OnRelease(); }
        finally { _context = null; }
    }

    /// <summary>初始化 System。</summary>
    protected abstract void OnInitialize();

    /// <summary>释放 System 自身资源；初始化部分失败后也可能调用。</summary>
    protected virtual void OnRelease() { }
}
