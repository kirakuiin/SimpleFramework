namespace SimpleFramework.Utility;

/// <summary>
/// 提供 Unix 时间与常用时间单位转换方法。
/// </summary>
public static class TimeUtil
{
    /// <summary>
    /// 将秒转为毫秒
    /// </summary>
    /// <param name="seconds">秒数。</param>
    /// <returns>对应的毫秒数。</returns>
    /// <exception cref="OverflowException">转换结果超出 <see cref="int"/> 范围。</exception>
    public static int ToMs(int seconds)
    {
        return checked(seconds * 1000);
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
