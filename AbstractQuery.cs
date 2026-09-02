namespace SimpleFramework;

/// <summary>查询的便利基类。</summary>
public abstract class AbstractQuery<TResult> : IQuery<TResult>
{
    /// <inheritdoc />
    public TResult Execute(QueryContext context) => OnExecute(context);

    /// <summary>执行查询逻辑。</summary>
    protected abstract TResult OnExecute(QueryContext context);
}
