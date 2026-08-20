using SimpleFramework.FrameworkImpl;
namespace SimpleFramework;

/// <summary>
/// 实现了模型大部分功能的抽象类。
/// </summary>
public abstract class AbstractModel : IModel
{
    private IDomain? _domain;

    /// <inheritdoc />
    public IDomain Domain => _domain ?? throw new InvalidOperationException("Model has not been bound to a Domain.");

    /// <inheritdoc />
    void IDomainBindable.BindDomain(IDomain domain)
    {
        ArgumentNullException.ThrowIfNull(domain);
        if (_domain is not null)
        {
            throw new InvalidOperationException("Model is already bound to a Domain.");
        }

        _domain = domain;
    }

    void IConstructable.Initialize() => OnInitialize();

    void IConstructable.UnInitialize() => OnUninitialize();

    /// <summary>
    /// 初始化模型。
    /// </summary>
    protected abstract void OnInitialize();

    /// <summary>
    /// 释放模型持有的资源；<see cref="OnInitialize"/> 抛出后也可能调用，因此必须能处理部分初始化状态。
    /// </summary>
    protected virtual void OnUninitialize() { }
}
