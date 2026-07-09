using SimpleFramework.Utility;

namespace SimpleFramework.FrameworkImpl;

/// <summary>
/// 框架模块的logger
/// </summary>
public static class Log
{
    private static Logger Logger { get; } = Logging.GetLogger("Framework");
    
    public static void Debug(string message)
    {
        Logger.Debug(message);
    }
    
    public static void Info(string message)
    {
        Logger.Info(message);
    }
    
    public static void Error(string message, Exception exception = null)
    {
        Logger.Error(message, exception);
    }
}
