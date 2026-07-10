using System.Diagnostics;

namespace SimpleFramework.Utility;

/// <summary>
/// 提供轻量的异步任务辅助方法。
/// </summary>
public static class TaskUtil
{
    /// <summary>
    /// 等待谓词为真时返回。
    /// </summary>
    /// <remarks>达到超时时间时正常返回，不抛出超时异常。</remarks>
    /// <param name="predict">判断函数。</param>
    /// <param name="timeout">超时时间（毫秒）。</param>
    /// <param name="interval">检查间隔（毫秒）。</param>
    /// <param name="cancellationToken">用于取消等待的令牌。</param>
    /// <exception cref="OperationCanceledException">等待已被取消。</exception>
    public static async Task WaitUntil(
        Func<bool> predict,
        int timeout = 5000,
        int interval = 100,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();
        while (!predict() && stopwatch.Elapsed.TotalMilliseconds < timeout)
        {
            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
        }
    }
}
