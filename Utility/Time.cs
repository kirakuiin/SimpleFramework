namespace SimpleFramework.Utility;

public static class TimeUtil
{
    /// <summary>
    /// 毫秒
    /// </summary>
    private const int MsPerSecond = 1000;
    
    /// <summary>
    /// 将秒转为毫秒
    /// </summary>
    /// <param name="seconds"></param>
    /// <returns></returns>
    public static int ToMs(int seconds) => seconds * MsPerSecond;
}