using System.Text;

namespace SimpleFramework.Utility;

/// <summary>
/// 日志级别枚举，数值越大表示级别越高。
/// </summary>
public enum LogLevel
{
    /// <summary>不筛选任何日志。</summary>
    NoTest = 0,
    /// <summary>调试信息。</summary>
    Debug = 10,
    /// <summary>普通信息。</summary>
    Info = 20,
    /// <summary>警告信息。</summary>
    Warning = 30,
    /// <summary>错误信息。</summary>
    Error = 40,
    /// <summary>严重错误信息。</summary>
    Critical = 50,
}

/// <summary>
/// 日志处理器接口。
/// </summary>
public interface IHandler : IDisposable
{
    /// <summary>
    /// 处理一条日志记录。
    /// </summary>
    /// <param name="record">日志记录。</param>
    /// <remarks>实现可以报告发送失败；通过 <see cref="Logger"/> 分发时，该异常会被隔离且不会阻止其他处理器。</remarks>
    void Emit(LogRecord record);

    /// <summary>
    /// 获取或设置处理器接收的最低日志级别。
    /// </summary>
    LogLevel Level { get; set; }

    /// <summary>
    /// 获取或设置日志格式化器。
    /// </summary>
    IFormatter Formatter { get; set; }
}

/// <summary>
/// 日志格式化器接口。
/// </summary>
public interface IFormatter
{
    /// <summary>
    /// 将日志记录格式化为字符串。
    /// </summary>
    /// <param name="record">日志记录。</param>
    /// <returns>格式化后的日志文本。</returns>
    string Format(LogRecord record);
}

/// <summary>
/// 一条日志记录。
/// </summary>
public class LogRecord
{
    /// <summary>
    /// 日志创建时间。
    /// </summary>
    public DateTime Created { get; }

    /// <summary>
    /// 日志级别。
    /// </summary>
    public LogLevel Level { get; }

    /// <summary>
    /// 日志级别名称。
    /// </summary>
    public string LevelName => Level.ToString();

    /// <summary>
    /// 日志消息。
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 日志来源名称。
    /// </summary>
    public string LoggerName { get; }

    /// <summary>
    /// 关联的异常信息。
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// 创建日志记录。
    /// </summary>
    /// <param name="created">日志创建时间。</param>
    /// <param name="level">日志级别。</param>
    /// <param name="message">日志消息。</param>
    /// <param name="loggerName">日志来源名称。</param>
    /// <param name="exception">关联的异常信息。</param>
    public LogRecord(DateTime created, LogLevel level, string message, string loggerName, Exception? exception = null)
    {
        Created = created;
        Level = level;
        Message = message;
        LoggerName = loggerName;
        Exception = exception;
    }
}

/// <summary>
/// 将日志写入控制台的处理器。
/// </summary>
public class ConsoleHandler : IHandler
{
    /// <summary>
    /// 获取或设置处理器接收的最低日志级别。
    /// </summary>
    public LogLevel Level { get; set; } = LogLevel.NoTest;

    /// <summary>
    /// 获取或设置日志格式化器。
    /// </summary>
    public IFormatter Formatter { get; set; } = new StandardFormatter();

    /// <summary>
    /// 将日志记录写入控制台。
    /// </summary>
    public void Emit(LogRecord record)
    {
        if (record.Level < Level) return;

        var originalColor = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = record.Level switch
            {
                LogLevel.Debug => ConsoleColor.Gray,
                LogLevel.Info => ConsoleColor.White,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error or LogLevel.Critical => ConsoleColor.Red,
                _ => originalColor
            };

            Console.WriteLine(Formatter.Format(record));
        }
        finally
        {
            Console.ForegroundColor = originalColor;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}

/// <summary>
/// 将日志写入文件的处理器。
/// </summary>
public class FileHandler : IHandler
{
    private readonly object _lock = new();
    private readonly StreamWriter _writer;
    private bool _disposed;

    /// <summary>
    /// 获取或设置处理器接收的最低日志级别。
    /// </summary>
    public LogLevel Level { get; set; } = LogLevel.NoTest;

    /// <summary>
    /// 获取或设置日志格式化器。
    /// </summary>
    public IFormatter Formatter { get; set; } = new StandardFormatter();

    /// <summary>
    /// 创建文件处理器。
    /// </summary>
    /// <param name="filename">日志文件名。</param>
    /// <param name="append">是否追加到已有文件。</param>
    public FileHandler(string filename, bool append = true)
    {
        _writer = new StreamWriter(filename, append, Encoding.UTF8) { AutoFlush = true };
    }

    ~FileHandler()
    {
        Dispose(false);
    }

    /// <summary>
    /// 将日志记录写入文件。
    /// </summary>
    /// <param name="record">日志记录。</param>
    public void Emit(LogRecord record)
    {
        if (record.Level < Level) return;

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _writer.WriteLine(Formatter.Format(record));
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        lock (_lock)
        {
            if (_disposed) return;

            if (disposing)
            {
                _writer.Flush();
                _writer.Dispose();
            }

            _disposed = true;
        }
    }
}

/// <summary>
/// 标准日志格式化器。
/// </summary>
public class StandardFormatter : IFormatter
{
    /// <summary>
    /// 将日志记录格式化为默认文本。
    /// </summary>
    /// <param name="record">日志记录。</param>
    /// <returns>包含时间、级别、来源、消息和可选异常的文本。</returns>
    public string Format(LogRecord record)
    {
        var sb = new StringBuilder();
        sb.Append($"[{record.Created:yyyy-MM-dd HH:mm:ss.fff}] ");
        sb.Append($"[{record.LevelName,-8}] ");
        sb.Append($"[{record.LoggerName}] ");
        sb.Append(record.Message);

        if (record.Exception != null)
        {
            sb.AppendLine();
            sb.Append(record.Exception);
        }

        return sb.ToString();
    }
}

/// <summary>
/// 日志记录器，负责按级别分发日志记录。
/// </summary>
public class Logger
{
    private static readonly object LoggersLock = new();
    private static readonly Dictionary<string, Logger> Loggers = new();

    private readonly object _handlersLock = new();
    private readonly List<IHandler> _handlers = new();

    private Logger(string name)
    {
        Name = name;
    }

    /// <summary>
    /// 日志记录器名称。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 获取或设置记录器接收的最低日志级别。
    /// </summary>
    public LogLevel Level { get; set; } = LogLevel.NoTest;

    /// <summary>
    /// 根日志记录器。
    /// </summary>
    public static Logger Root => GetLogger("root");

    /// <summary>
    /// 获取指定名称的日志记录器。
    /// </summary>
    /// <param name="name">日志记录器名称。</param>
    /// <returns>同名共享日志记录器。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> 为空。</exception>
    public static Logger GetLogger(string name)
    {
        lock (LoggersLock)
        {
            if (Loggers.TryGetValue(name, out var logger)) return logger;
            logger = new Logger(name);
            Loggers[name] = logger;
            return logger;
        }
    }

    /// <summary>
    /// 添加日志处理器。
    /// </summary>
    /// <param name="handler">日志处理器。</param>
    public void AddHandler(IHandler handler)
    {
        lock (_handlersLock)
        {
            if (!_handlers.Contains(handler))
            {
                _handlers.Add(handler);
            }
        }
    }

    /// <summary>
    /// 移除日志处理器并释放它。
    /// </summary>
    /// <param name="handler">日志处理器。</param>
    /// <exception cref="Exception">处理器已移除后，其释放回调失败。</exception>
    public void RemoveHandler(IHandler handler)
    {
        bool removed;
        lock (_handlersLock)
        {
            removed = _handlers.Remove(handler);
        }

        if (removed)
        {
            handler.Dispose();
        }
    }

    /// <summary>
    /// 移除并释放所有日志处理器。
    /// </summary>
    /// <exception cref="AggregateException">一个或多个处理器释放失败；所有处理器仍会被尝试释放。</exception>
    public void ClearHandlers()
    {
        IHandler[] handlers;
        lock (_handlersLock)
        {
            handlers = _handlers.ToArray();
            _handlers.Clear();
        }

        List<Exception>? exceptions = null;
        foreach (var handler in handlers)
        {
            try
            {
                handler.Dispose();
            }
            catch (Exception exception)
            {
                (exceptions ??= []).Add(exception);
            }
        }

        if (exceptions is not null)
        {
            throw new AggregateException("释放日志处理器时发生一个或多个错误。", exceptions);
        }
    }

    /// <summary>
    /// 记录调试日志。
    /// </summary>
    /// <param name="message">日志消息。</param>
    /// <param name="exception">关联异常。</param>
    /// <remarks>单个日志处理器发送失败不会向调用方传播，也不会阻止后续处理器。</remarks>
    public void Debug(string message, Exception? exception = null)
    {
        Log(LogLevel.Debug, message, exception);
    }

    /// <summary>
    /// 记录信息日志。
    /// </summary>
    /// <param name="message">日志消息。</param>
    /// <param name="exception">关联异常。</param>
    /// <remarks>单个日志处理器发送失败不会向调用方传播，也不会阻止后续处理器。</remarks>
    public void Info(string message, Exception? exception = null)
    {
        Log(LogLevel.Info, message, exception);
    }

    /// <summary>
    /// 记录警告日志。
    /// </summary>
    /// <param name="message">日志消息。</param>
    /// <param name="exception">关联异常。</param>
    /// <remarks>单个日志处理器发送失败不会向调用方传播，也不会阻止后续处理器。</remarks>
    public void Warning(string message, Exception? exception = null)
    {
        Log(LogLevel.Warning, message, exception);
    }

    /// <summary>
    /// 记录错误日志。
    /// </summary>
    /// <param name="message">日志消息。</param>
    /// <param name="exception">关联异常。</param>
    /// <remarks>单个日志处理器发送失败不会向调用方传播，也不会阻止后续处理器。</remarks>
    public void Error(string message, Exception? exception = null)
    {
        Log(LogLevel.Error, message, exception);
    }

    /// <summary>
    /// 记录严重错误日志。
    /// </summary>
    /// <param name="message">日志消息。</param>
    /// <param name="exception">关联异常。</param>
    /// <remarks>单个日志处理器发送失败不会向调用方传播，也不会阻止后续处理器。</remarks>
    public void Critical(string message, Exception? exception = null)
    {
        Log(LogLevel.Critical, message, exception);
    }

    private void Log(LogLevel level, string message, Exception? exception = null)
    {
        if (level < Level) return;

        lock (_handlersLock)
        {
            if (_handlers.Count == 0 && Name != "root")
            {
                Root.Log(level, message, exception);
                return;
            }

            var record = new LogRecord(DateTime.Now, level, message, Name, exception);
            foreach (var handler in _handlers.ToArray())
            {
                try
                {
                    handler.Emit(record);
                }
                catch (Exception)
                {
                    // 日志是诊断旁路，单个输出目标失败不能改变调用方行为或阻止其他目标。
                }
            }
        }
    }
}

/// <summary>
/// 提供类似 Python logging 模块的静态方法。
/// </summary>
public static class Logging
{
    static Logging()
    {
        Logger.Root.AddHandler(new ConsoleHandler { Level = LogLevel.Info });
    }

    /// <summary>
    /// 获取指定名称的日志记录器。
    /// </summary>
    /// <param name="name">日志记录器名称。</param>
    /// <returns>同名共享日志记录器。</returns>
    public static Logger GetLogger(string name) => Logger.GetLogger(name);

    /// <summary>
    /// 配置根日志记录器。
    /// </summary>
    /// <param name="level">最低日志级别。</param>
    /// <param name="filename">日志文件名；为空时只输出到控制台。</param>
    /// <exception cref="AggregateException">原根处理器中有一个或多个释放失败。</exception>
    public static void BasicConfig(LogLevel level = LogLevel.Info, string filename = "")
    {
        Logger.Root.Level = level;
        Logger.Root.ClearHandlers();
        Logger.Root.AddHandler(new ConsoleHandler { Level = level });

        if (!string.IsNullOrEmpty(filename))
        {
            Logger.Root.AddHandler(new FileHandler(filename) { Level = level });
        }
    }

    /// <summary>
    /// 记录调试日志。
    /// </summary>
    /// <param name="message">日志消息。</param>
    /// <param name="exception">关联异常。</param>
    public static void Debug(string message, Exception? exception = null)
    {
        Logger.Root.Debug(message, exception);
    }

    /// <summary>
    /// 记录信息日志。
    /// </summary>
    /// <param name="message">日志消息。</param>
    /// <param name="exception">关联异常。</param>
    public static void Info(string message, Exception? exception = null)
    {
        Logger.Root.Info(message, exception);
    }

    /// <summary>
    /// 记录警告日志。
    /// </summary>
    /// <param name="message">日志消息。</param>
    /// <param name="exception">关联异常。</param>
    public static void Warning(string message, Exception? exception = null)
    {
        Logger.Root.Warning(message, exception);
    }

    /// <summary>
    /// 记录错误日志。
    /// </summary>
    /// <param name="message">日志消息。</param>
    /// <param name="exception">关联异常。</param>
    public static void Error(string message, Exception? exception = null)
    {
        Logger.Root.Error(message, exception);
    }

    /// <summary>
    /// 记录严重错误日志。
    /// </summary>
    /// <param name="message">日志消息。</param>
    /// <param name="exception">关联异常。</param>
    public static void Critical(string message, Exception? exception = null)
    {
        Logger.Root.Critical(message, exception);
    }
}
