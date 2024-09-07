namespace SimpleFramework;

/// <summary>
/// 查询的抽象实现。
/// </summary>
public abstract class AbstractQuery<TResult> : IQuery<TResult>
{
    public IDomain Domain { get; private set; } = default!;

    public void SetDomain(IDomain domain) => Domain = domain;

    public TResult Execute() => OnExecute();
    
    protected abstract TResult OnExecute();
}
