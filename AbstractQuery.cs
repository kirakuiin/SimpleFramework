namespace SimpleFramework;

/// <summary>
/// 查询的抽象实现。
/// </summary>
public abstract class AbstractQuery<TResult> : IQuery<TResult>
{
    private bool _isExecuting;

    /// <inheritdoc />
    public IDomain Domain { get; private set; } = default!;

    /// <inheritdoc />
    public void SetDomain(IDomain domain)
    {
        ArgumentNullException.ThrowIfNull(domain);
        if (_isExecuting)
        {
            throw new InvalidOperationException("Cannot change Domain while this Query is executing.");
        }

        Domain = domain;
    }

    /// <inheritdoc />
    public TResult Execute()
    {
        if (_isExecuting)
        {
            throw new InvalidOperationException("Query instance is already executing.");
        }

        _isExecuting = true;
        try
        {
            return OnExecute();
        }
        finally
        {
            _isExecuting = false;
        }
    }

    /// <summary>
    /// 执行查询的具体逻辑。
    /// </summary>
    /// <returns>查询结果。</returns>
    protected abstract TResult OnExecute();
}
