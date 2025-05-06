using System.Text;

namespace SimpleFramework.Utility
{
    /// <summary>
    /// 日志级别枚举，类似Python的logging级别
    /// </summary>
    public enum LogLevel
    {
        NoTest = 0,
        Debug = 10,
        Info = 20,
        Warning = 30,
        Error = 40,
        Critical = 50,
    }

    /// <summary>
    /// 日志处理器接口
    /// </summary>
    public interface IHandler : IDisposable
    {
        /// <summary>
        /// 处理日志记录
        /// </summary>
        /// <param name="record">日志记录</param>
        void Emit(LogRecord record);
        
        /// <summary>
        /// 获取或设置日志级别
        /// </summary>
        LogLevel Level { get; set; }
        
        /// <summary>
        /// 获取或设置日志格式化器
        /// </summary>
        IFormatter Formatter { get; set; }
    }

    /// <summary>
    /// 日志格式化器接口
    /// </summary>
    public interface IFormatter
    {
        /// <summary>
        /// 格式化日志记录
        /// </summary>
        /// <param name="record">日志记录</param>
        /// <returns>格式化后的字符串</returns>
        string Format(LogRecord record);
    }

    /// <summary>
    /// 日志记录类
    /// </summary>
    public class LogRecord
    {
        /// <summary>
        /// 日志创建时间
        /// </summary>
        public DateTime Created { get; }
        
        /// <summary>
        /// 日志级别
        /// </summary>
        public LogLevel Level { get; }
        
        /// <summary>
        /// 日志级别名称
        /// </summary>
        public string LevelName => Level.ToString();
        
        /// <summary>
        /// 日志消息
        /// </summary>
        public string Message { get; }
        
        /// <summary>
        /// 日志来源
        /// </summary>
        public string LoggerName { get; }
        
        /// <summary>
        /// 异常信息
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// 创建日志记录
        /// </summary>
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
    /// 标准输出处理器
    /// </summary>
    public class ConsoleHandler : IHandler
    {
        /// <summary>
        /// 日志级别
        /// </summary>
        public LogLevel Level { get; set; } = LogLevel.NoTest;
        
        /// <summary>
        /// 日志格式化器
        /// </summary>
        public IFormatter Formatter { get; set; }

        /// <summary>
        /// 创建控制台处理器
        /// </summary>
        public ConsoleHandler()
        {
            Formatter = new StandardFormatter();
        }

        /// <summary>
        /// 处理日志记录
        /// </summary>
        public void Emit(LogRecord record)
        {
            if (record.Level < Level) return;
            
            var message = Formatter.Format(record);
            
            // 根据日志级别使用不同颜色
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = record.Level switch
            {
                LogLevel.Debug => ConsoleColor.Gray,
                LogLevel.Info => ConsoleColor.White,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error or LogLevel.Critical => ConsoleColor.Red,
                _ => Console.ForegroundColor
            };

            Console.WriteLine(message);
            Console.ForegroundColor = originalColor;
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// 文件处理器
    /// </summary>
    public class FileHandler : IHandler
    {
        private readonly StreamWriter _writer;
        private bool _disposed;

        /// <summary>
        /// 日志级别
        /// </summary>
        public LogLevel Level { get; set; } = LogLevel.NoTest;
        
        /// <summary>
        /// 日志格式化器
        /// </summary>
        public IFormatter Formatter { get; set; }

        /// <summary>
        /// 创建文件处理器
        /// </summary>
        /// <param name="filename">日志文件名</param>
        /// <param name="append">是否追加模式</param>
        public FileHandler(string filename, bool append = true)
        {
            _writer = new StreamWriter(filename, append, Encoding.UTF8) { AutoFlush = true };
            Formatter = new StandardFormatter();
        }

        ~FileHandler()
        {
            Dispose(false);
        }

        /// <summary>
        /// 处理日志记录
        /// </summary>
        public void Emit(LogRecord record)
        {
            if (_disposed) throw new ObjectDisposedException("FileHandler");
            if (record.Level < Level) return;
            
            var message = Formatter.Format(record);
            _writer.WriteLine(message);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        private void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                _writer.Dispose();
            }
            _disposed = true;
        }
    }

    /// <summary>
    /// 标准格式化器
    /// </summary>
    public class StandardFormatter : IFormatter
    {
        /// <summary>
        /// 格式化日志记录
        /// </summary>
        public string Format(LogRecord record)
        {
            var sb = new StringBuilder();
            
            // 格式: [时间] [级别] [来源] 消息
            sb.Append($"[{record.Created:yyyy-MM-dd HH:mm:ss.fff}] ");
            sb.Append($"[{record.LevelName,-8}] ");
            sb.Append($"[{record.LoggerName}] ");
            sb.Append(record.Message);
            
            // 添加异常信息
            if (record.Exception != null)
            {
                sb.AppendLine();
                sb.Append(record.Exception);
            }
            
            return sb.ToString();
        }
    }

    /// <summary>
    /// 日志记录器，类似Python的Logger类
    /// </summary>
    public class Logger
    {
        private static readonly Dictionary<string, Logger> Loggers = new ();
        private readonly List<IHandler> _handlers = new ();
        
        /// <summary>
        /// 记录器名称
        /// </summary>
        public string Name { get; }
        
        /// <summary>
        /// 日志级别
        /// </summary>
        public LogLevel Level { get; set; } = LogLevel.NoTest;

        /// <summary>
        /// 获取根日志记录器
        /// </summary>
        public static Logger Root => GetLogger("root");

        /// <summary>
        /// 私有构造函数
        /// </summary>
        private Logger(string name)
        {
            Name = name;
        }

        /// <summary>
        /// 获取指定名称的日志记录器
        /// </summary>
        /// <param name="name">日志记录器名称</param>
        public static Logger GetLogger(string name)
        {
            if (Loggers.TryGetValue(name, out var logger)) return logger;
            logger = new Logger(name);
            Loggers[name] = logger;

            return logger;
        }

        /// <summary>
        /// 添加处理器
        /// </summary>
        /// <param name="handler">处理器</param>
        public void AddHandler(IHandler handler)
        {
            if (!_handlers.Contains(handler))
            {
                _handlers.Add(handler);
            }
        }

        /// <summary>
        /// 移除处理器
        /// </summary>
        /// <param name="handler">处理器</param>
        public void RemoveHandler(IHandler handler)
        {
            _handlers.Remove(handler);
            handler.Dispose();
        }

        /// <summary>
        /// 去除所有处理器
        /// </summary>
        public void ClearHandlers()
        {
            foreach (var handler in _handlers)
            {
                handler.Dispose();
            }
            _handlers.Clear();
        }

        /// <summary>
        /// 记录日志
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="message">日志消息</param>
        /// <param name="exception">异常信息</param>
        private void Log(LogLevel level, string message, Exception? exception = null)
        {
            if (level < Level) return;
            
            var record = new LogRecord(DateTime.Now, level, message, Name, exception);
            
            // 如果没有处理器且不是根日志器，使用根日志器的处理器
            if (_handlers.Count == 0 && Name != "root")
            {
                Root.Log(level, message, exception);
                return;
            }
            
            foreach (var handler in _handlers)
            {
                handler.Emit(record);
            }
        }

        /// <summary>
        /// 记录调试日志
        /// </summary>
        public void Debug(string message, Exception? exception = null)
        {
            Log(LogLevel.Debug, message, exception);
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        public void Info(string message, Exception? exception = null)
        {
            Log(LogLevel.Info, message, exception);
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        public void Warning(string message, Exception? exception = null)
        {
            Log(LogLevel.Warning, message, exception);
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        public void Error(string message, Exception? exception = null)
        {
            Log(LogLevel.Error, message, exception);
        }

        /// <summary>
        /// 记录严重错误日志
        /// </summary>
        public void Critical(string message, Exception? exception = null)
        {
            Log(LogLevel.Critical, message, exception);
        }
    }

    /// <summary>
    /// 提供类似Python logging模块的静态方法
    /// </summary>
    public static class Logging
    {
        static Logging()
        {
            // 默认配置：添加一个控制台处理器到根日志器
            var handler = new ConsoleHandler { Level = LogLevel.Info };
            Logger.Root.AddHandler(handler);
        }

        /// <summary>
        /// 获取指定名称的日志记录器
        /// </summary>
        public static Logger GetLogger(string name) => Logger.GetLogger(name);

        /// <summary>
        /// 基础日志配置
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="filename">日志文件名，如果不为null则添加文件处理器</param>
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
        /// 记录调试日志
        /// </summary>
        public static void Debug(string message, Exception? exception = null)
        {
            Logger.Root.Debug(message, exception);
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        public static void Info(string message, Exception? exception = null)
        {
            Logger.Root.Info(message, exception);
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        public static void Warning(string message, Exception? exception = null)
        {
            Logger.Root.Warning(message, exception);
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        public static void Error(string message, Exception? exception = null)
        {
            Logger.Root.Error(message, exception);
        }

        /// <summary>
        /// 记录严重错误日志
        /// </summary>
        public static void Critical(string message, Exception? exception = null)
        {
            Logger.Root.Critical(message, exception);
        }
    }
} 