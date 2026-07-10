using SimpleFramework.Utility;

namespace SimpleFramework.FrameworkImpl;

/// <summary>
/// 框架模块的logger
/// </summary>
public static class Log
{
    private static Logger Logger { get; } = Logging.GetLogger("Framework");
    
    /// <summary>
    /// 记录调试信息。
    /// </summary>
    /// <param name="message">日志内容。</param>
    public static void Debug(string message)
    {
        Logger.Debug(message);
    }
    
    /// <summary>
    /// 记录一般信息。
    /// </summary>
    /// <param name="message">日志内容。</param>
    public static void Info(string message)
    {
        Logger.Info(message);
    }
    
    /// <summary>
    /// 记录错误信息及可选异常。
    /// </summary>
    /// <param name="message">日志内容。</param>
    /// <param name="exception">关联异常。</param>
    public static void Error(string message, Exception? exception = null)
    {
        Logger.Error(message, exception);
    }
}
