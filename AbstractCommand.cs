namespace SimpleFramework;

/// <summary>
/// 命令的抽象实现。
/// </summary>
public abstract class AbstractCommand : ICommand
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
            throw new InvalidOperationException("Cannot change Domain while this Command is executing.");
        }

        Domain = domain;
    }

    /// <inheritdoc />
    public void Execute()
    {
        if (_isExecuting)
        {
            throw new InvalidOperationException("Command instance is already executing.");
        }

        _isExecuting = true;
        try
        {
            OnExecute();
        }
        finally
        {
            _isExecuting = false;
        }
    }

    /// <summary>
    /// 执行命令的具体逻辑。
    /// </summary>
    protected abstract void OnExecute();
}

/// <summary>
/// 带返回结果的命令抽象实现。
/// </summary>
/// <typeparam name="TResult">命令结果类型。</typeparam>
public abstract class AbstractCommand<TResult> : ICommand<TResult>
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
            throw new InvalidOperationException("Cannot change Domain while this Command is executing.");
        }

        Domain = domain;
    }

    /// <inheritdoc />
    public TResult Execute()
    {
        if (_isExecuting)
        {
            throw new InvalidOperationException("Command instance is already executing.");
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
    /// 执行命令的具体逻辑。
    /// </summary>
    /// <returns>命令执行结果。</returns>
    protected abstract TResult OnExecute();
}
