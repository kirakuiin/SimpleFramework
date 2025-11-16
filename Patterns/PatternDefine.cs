using SimpleFramework.Utility;

namespace SimpleFramework.Patterns;

/// <summary>
/// 模式模块的logger
/// </summary>
public class PatternLogger
{
    public static Logger Logger { get; } = Logging.GetLogger("Pattern");
    
    public static void Debug(string message)
    {
        Logger.Debug(message);
    }
    
    public static void Info(string message)
    {
        Logger.Info(message);
    }
    
    public static void Warning(string message)
    {
        Logger.Warning(message);
    }
    
    public static void Error(string message, Exception? exception = null)
    {
        Logger.Error(message, exception);
    }
}
