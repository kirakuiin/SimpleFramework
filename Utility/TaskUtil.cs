namespace SimpleFramework.Utility;

public static class TaskUtil
{
    /// <summary>
    /// 等待谓词为真时返回
    /// </summary>
    /// <param name="predict">判断函数</param>
    /// <param name="timeout">超时事件(ms)</param>
    /// <param name="interval">间隔（毫秒）</param>
    public static async Task WaitUntil(Func<bool> predict, int timeout=5000, int interval=100)
    {
        var start = DateTime.Now;
        while (!predict() && DateTime.Now.Subtract(start).TotalMilliseconds < timeout)
        {
            await Task.Delay(interval);
        }
    }
}