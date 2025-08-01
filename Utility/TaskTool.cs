namespace SimpleFramework.Utility;

public static class TaskTool
{
    /// <summary>
    /// 等待谓词为真时返回
    /// </summary>
    /// <param name="predict">判断函数</param>
    /// <param name="interval">间隔（毫秒）</param>
    public static async Task WaitUntil(Func<bool> predict, int interval=100)
    {
        while (!predict())
        {
            await Task.Delay(interval);
        }
    }
}