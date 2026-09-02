namespace SimpleFramework;

/// <summary>无返回值命令的便利基类。</summary>
public abstract class AbstractCommand : ICommand
{
    /// <inheritdoc />
    public void Execute(CommandContext context) => OnExecute(context);

    /// <summary>执行命令逻辑。</summary>
    protected abstract void OnExecute(CommandContext context);
}

/// <summary>带返回值命令的便利基类。</summary>
public abstract class AbstractCommand<TResult> : ICommand<TResult>
{
    /// <inheritdoc />
    public TResult Execute(CommandContext context) => OnExecute(context);

    /// <summary>执行命令逻辑。</summary>
    protected abstract TResult OnExecute(CommandContext context);
}
