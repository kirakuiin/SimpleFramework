namespace SimpleFramework.Utility;

public static class TimeUtil
{
    /// <summary>
    /// 将秒转为毫秒
    /// </summary>
    /// <param name="seconds"></param>
    /// <returns></returns>
    public static int ToMs(int seconds)
    {
        var span = new TimeSpan(0, 0, seconds);
        return (int)span.TotalMilliseconds;
    }

    /// <summary>
    /// 获得从unix起始时间到utc now经过的毫秒数
    /// </summary>
    /// <returns></returns>
    public static long GetUtcMilliseconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// 获得当前时间减去指定时间的差值
    /// </summary>
    /// <param name="milliseconds"></param>
    /// <returns></returns>
    public static TimeSpan GetUtcTimeSpanByNow(long milliseconds)
    {
        return DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
    }
}