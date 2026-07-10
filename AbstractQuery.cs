namespace SimpleFramework;

/// <summary>
/// 查询的抽象实现。
/// </summary>
public abstract class AbstractQuery<TResult> : IQuery<TResult>
{
    /// <inheritdoc />
    public IDomain Domain { get; private set; } = default!;

    /// <inheritdoc />
    public void SetDomain(IDomain domain) => Domain = domain;

    /// <inheritdoc />
    public TResult Execute() => OnExecute();

    /// <summary>
    /// 执行查询的具体逻辑。
    /// </summary>
    /// <returns>查询结果。</returns>
    protected abstract TResult OnExecute();
}
