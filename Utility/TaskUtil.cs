using System.Diagnostics;

namespace SimpleFramework.Utility;

public static class TaskUtil
{
    /// <summary>
    /// 等待谓词为真时返回。
    /// </summary>
    /// <param name="predict">判断函数。</param>
    /// <param name="timeout">超时时间（毫秒）。</param>
    /// <param name="interval">检查间隔（毫秒）。</param>
    public static async Task WaitUntil(Func<bool> predict, int timeout = 5000, int interval = 100)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!predict() && stopwatch.Elapsed.TotalMilliseconds < timeout)
        {
            await Task.Delay(interval);
        }
    }
}
