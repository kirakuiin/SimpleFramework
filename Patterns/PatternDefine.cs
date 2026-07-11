using SimpleFramework.Utility;

namespace SimpleFramework.Patterns;

/// <summary>
/// 模式模块的logger
/// </summary>
public class PatternLogger
{
    /// <summary>获取 Patterns 模块使用的日志记录器。</summary>
    public static Logger Logger { get; } = Logging.GetLogger("Pattern");

    /// <summary>记录调试消息。</summary>
    /// <param name="message">消息文本。</param>
    public static void Debug(string message)
    {
        Logger.Debug(message);
    }
    
    /// <summary>记录一般信息。</summary>
    /// <param name="message">消息文本。</param>
    public static void Info(string message)
    {
        Logger.Info(message);
    }
    
    /// <summary>记录警告消息。</summary>
    /// <param name="message">消息文本。</param>
    public static void Warning(string message)
    {
        Logger.Warning(message);
    }
    
    /// <summary>记录错误消息及可选异常。</summary>
    /// <param name="message">消息文本。</param>
    /// <param name="exception">与错误关联的异常。</param>
    public static void Error(string message, Exception? exception = null)
    {
        Logger.Error(message, exception);
    }
}
