namespace SimpleFramework;

/// <summary>
/// 命令的抽象实现。
/// </summary>
public abstract class AbstractCommand : ICommand
{
    /// <inheritdoc />
    public IDomain Domain { get; private set; } = default!;

    /// <inheritdoc />
    public void SetDomain(IDomain domain) => Domain = domain;

    /// <inheritdoc />
    public void Execute() => OnExecute();

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
    /// <inheritdoc />
    public IDomain Domain { get; private set; } = default!;

    /// <inheritdoc />
    public void SetDomain(IDomain domain) => Domain = domain;

    /// <inheritdoc />
    public TResult Execute() => OnExecute();

    /// <summary>
    /// 执行命令的具体逻辑。
    /// </summary>
    /// <returns>命令执行结果。</returns>
    protected abstract TResult OnExecute();
}
