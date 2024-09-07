namespace SimpleFramework;

/// <summary>
/// 命令的抽象实现。
/// </summary>
public abstract class AbstractCommand : ICommand
{
    public IDomain Domain { get; private set; }

    public void SetDomain(IDomain domain) => Domain = domain;

    public void Execute() => OnExecute();
    
    protected abstract void OnExecute();
}

public abstract class AbstractCommand<TResult> : ICommand<TResult>
{
    public IDomain Domain { get; private set; } = null!;

    public void SetDomain(IDomain domain) => Domain = domain;

    public TResult Execute() => OnExecute();
    
    protected abstract TResult OnExecute();
}
