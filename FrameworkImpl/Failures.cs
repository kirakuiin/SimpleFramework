using System.Runtime.ExceptionServices;

namespace SimpleFramework.FrameworkImpl;

/// <summary>
/// 生命周期清理中的异常收集与重抛。清理需要执行到底，不能被第一个异常打断，
/// 因此先收集，最后统一抛出。
/// </summary>
internal static class Failures
{
    /// <summary>收集异常；展开非空的 AggregateException，使调用方看到的是扁平的失败列表。</summary>
    public static void Add(List<Exception> failures, Exception exception)
    {
        if (exception is AggregateException { InnerExceptions.Count: > 0 } aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions) Add(failures, inner);
        }
        else
        {
            failures.Add(exception);
        }
    }

    /// <summary>单个异常保留原始堆栈重抛，多个异常包装为 <see cref="AggregateException"/>；没有异常时直接返回。</summary>
    public static void ThrowIfAny(List<Exception> failures)
    {
        if (failures.Count == 0) return;
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        throw new AggregateException(failures);
    }
}
