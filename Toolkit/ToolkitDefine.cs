using SimpleFramework.Utility;

namespace SimpleFramework.Toolkit;

/// <summary>
/// toolkit模块的logger
/// </summary>
public static class ToolkitLog
{
    /// <summary>获取 Toolkit 模块日志记录器。</summary>
    public static Logger Logger { get; } = Logging.GetLogger("Toolkit");

    /// <summary>记录调试信息。</summary>
    /// <param name="message">日志消息。</param>
    public static void Debug(string message)
    {
        Logger.Debug(message);
    }
    
    /// <summary>记录普通信息。</summary>
    /// <param name="message">日志消息。</param>
    public static void Info(string message)
    {
        Logger.Info(message);
    }
    
    /// <summary>记录警告信息。</summary>
    /// <param name="message">日志消息。</param>
    public static void Warning(string message)
    {
        Logger.Warning(message);
    }
    
    /// <summary>记录错误信息。</summary>
    /// <param name="message">日志消息。</param>
    /// <param name="exception">关联异常。</param>
    public static void Error(string message, Exception? exception = null)
    {
        Logger.Error(message, exception);
    }
}
