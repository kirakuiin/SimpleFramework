using SimpleFramework.FrameworkImpl;
namespace SimpleFramework;

/// <summary>
/// 实现了模型大部分功能的抽象类。
/// </summary>
public abstract class AbstractModel : IModel
{
    public IDomain Domain { get; private set; } = default!;

    public void SetDomain(IDomain domain)
    {
        Domain = domain;
    }

    void IConstructable.Initialize() => OnInitialize();
    
    void IConstructable.UnInitialize() => OnUninitialize();

    protected abstract void OnInitialize();
    
    protected virtual void OnUninitialize() {}
}