using SimpleFramework.FrameworkImpl;

namespace SimpleFramework;

/// <summary>
/// 实现了系统大部分功能的抽象类。
/// </summary>
public abstract class AbstractSystem : ISystem
{
    /// <inheritdoc />
    public IDomain Domain { get; private set; } = default!;

    /// <inheritdoc />
    public void SetDomain(IDomain domain)
    {
        Domain = domain;
    }

    void IConstructable.Initialize() => OnInitialize();
    
    void IConstructable.UnInitialize() => OnUninitialize();

    /// <summary>
    /// 初始化系统。
    /// </summary>
    protected abstract void OnInitialize();

    /// <summary>
    /// 释放系统持有的资源。
    /// </summary>
    protected virtual void OnUninitialize() {}
}
