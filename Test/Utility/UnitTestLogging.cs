using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleFramework.Utility;

namespace Test.Utility;

[TestFixture]
public class TestLogging
{
    private string _testLogFile = null!;

    [SetUp]
    public void Setup()
    {
        _testLogFile = Path.Combine(Path.GetTempPath(), $"test_log_{Guid.NewGuid()}.log");
    }

    [TearDown]
    public void TearDown()
    {
        Logger.Root.ClearHandlers();
        Logger.Root.Level = LogLevel.NoTest;

        if (File.Exists(_testLogFile))
        {
            File.Delete(_testLogFile);
        }
    }

    [Test]
    public void TestBasicLogging()
    {
        Logging.BasicConfig(LogLevel.Debug, _testLogFile);

        Logging.Debug("debug message");
        Logging.Info("info message");
        Logging.Warning("warning message");
        Logging.Error("error message");
        Logger.Root.ClearHandlers();

        var logLines = File.ReadAllLines(_testLogFile);

        Assert.AreEqual(4, logLines.Length);
        Assert.IsTrue(logLines[0].Contains("Debug") && logLines[0].Contains("debug message"));
        Assert.IsTrue(logLines[1].Contains("Info") && logLines[1].Contains("info message"));
        Assert.IsTrue(logLines[2].Contains("Warning") && logLines[2].Contains("warning message"));
        Assert.IsTrue(logLines[3].Contains("Error") && logLines[3].Contains("error message"));
    }

    [Test]
    public void TestCustomLogger()
    {
        var logger = Logger.GetLogger($"TestLogger-{Guid.NewGuid()}");
        var handler = new FileHandler(_testLogFile);
        logger.AddHandler(handler);

        logger.Info("custom logger message");
        logger.Error("custom logger error", new Exception("test exception"));
        logger.ClearHandlers();

        var logLines = File.ReadAllLines(_testLogFile);

        Assert.IsTrue(logLines[0].Contains(logger.Name));
        Assert.IsTrue(string.Join(Environment.NewLine, logLines).Contains("test exception"));
    }

    [Test]
    public void TestLogLevels()
    {
        var logger = Logger.GetLogger($"LevelTest-{Guid.NewGuid()}");
        logger.Level = LogLevel.Warning;
        logger.AddHandler(new FileHandler(_testLogFile));

        logger.Debug("debug skipped");
        logger.Info("info skipped");
        logger.Warning("warning written");
        logger.Error("error written");
        logger.ClearHandlers();

        var logLines = File.ReadAllLines(_testLogFile);

        Assert.AreEqual(2, logLines.Length);
        Assert.IsTrue(logLines[0].Contains("Warning"));
        Assert.IsTrue(logLines[1].Contains("Error"));
    }

    [Test]
    public void TestCustomFormatter()
    {
        var logger = Logger.GetLogger($"FormatterTest-{Guid.NewGuid()}");
        var handler = new FileHandler(_testLogFile)
        {
            Formatter = new CustomFormatter()
        };
        logger.AddHandler(handler);

        logger.Info("formatted message");
        logger.ClearHandlers();

        var logLines = File.ReadAllLines(_testLogFile);

        Assert.AreEqual($"Info::{logger.Name}::formatted message", logLines[0]);
    }

    [Test]
    public void TestHandlerMutationDuringEmitUsesSnapshot()
    {
        var logger = Logger.GetLogger($"MutationTest-{Guid.NewGuid()}");
        var records = new List<LogRecord>();
        logger.AddHandler(new ActionHandler(_ => logger.AddHandler(new ListHandler(records))));
        logger.AddHandler(new ListHandler(records));

        Assert.DoesNotThrow(() => logger.Info("hello"));
        logger.ClearHandlers();
    }

    [Test]
    public void TestClearHandlersWaitsForInFlightEmitBeforeDispose()
    {
        var logger = Logger.GetLogger($"LifetimeTest-{Guid.NewGuid()}");
        var enteredEmit = new ManualResetEventSlim(false);
        var allowEmitToFinish = new ManualResetEventSlim(false);
        var handler = new BlockingHandler(enteredEmit, allowEmitToFinish);
        logger.AddHandler(handler);

        var loggingTask = Task.Run(() => logger.Info("hello"));
        Assert.IsTrue(enteredEmit.Wait(TimeSpan.FromSeconds(2)));

        var clearTask = Task.Run(() => logger.ClearHandlers());
        Assert.IsFalse(clearTask.Wait(50));

        allowEmitToFinish.Set();

        Assert.DoesNotThrow(() => loggingTask.GetAwaiter().GetResult());
        Assert.DoesNotThrow(() => clearTask.GetAwaiter().GetResult());
        Assert.IsTrue(handler.DisposedAfterEmit);
    }

    [Test]
    public void TestClearHandlersDisposesEveryHandlerAndAggregatesFailures()
    {
        var logger = Logger.GetLogger($"FailureTest-{Guid.NewGuid()}");
        var disposalOrder = new List<string>();
        logger.AddHandler(new DisposeActionHandler(() =>
        {
            disposalOrder.Add("first");
            throw new InvalidOperationException("first failure");
        }));
        logger.AddHandler(new DisposeActionHandler(() => disposalOrder.Add("middle")));
        logger.AddHandler(new DisposeActionHandler(() =>
        {
            disposalOrder.Add("last");
            throw new ApplicationException("last failure");
        }));

        var exception = Assert.Throws<AggregateException>(() => logger.ClearHandlers());

        CollectionAssert.AreEqual(new[] { "first", "middle", "last" }, disposalOrder);
        CollectionAssert.AreEqual(
            new[] { "first failure", "last failure" },
            exception?.InnerExceptions.Select(item => item.Message));
        Assert.DoesNotThrow(() => logger.ClearHandlers());
    }

    [Test]
    public void TestRemoveHandlerDisposesOutsideLoggerLock()
    {
        var logger = Logger.GetLogger($"RemoveLifetimeTest-{Guid.NewGuid()}");
        using var competingTaskStarted = new ManualResetEventSlim(false);
        var probe = new DisposeActionHandler(() => { });
        Task? competingTask = null;
        var acquiredDuringDispose = false;
        var removed = new DisposeActionHandler(() =>
        {
            competingTask = Task.Run(() =>
            {
                competingTaskStarted.Set();
                logger.AddHandler(probe);
            });
            Assert.IsTrue(competingTaskStarted.Wait(TimeSpan.FromSeconds(2)));
            acquiredDuringDispose = competingTask.Wait(TimeSpan.FromSeconds(2));
        });
        logger.AddHandler(removed);

        logger.RemoveHandler(removed);

        Assert.IsNotNull(competingTask);
        var completedAfterDispose = competingTask?.Wait(TimeSpan.FromSeconds(2)) ?? false;
        Assert.Multiple(() =>
        {
            Assert.IsTrue(completedAfterDispose);
            Assert.IsTrue(acquiredDuringDispose);
        });
        logger.RemoveHandler(probe);
    }

    [Test]
    public void TestRemoveHandlerPropagatesDisposalFailureAndIgnoresMissingHandler()
    {
        var logger = Logger.GetLogger($"RemoveFailureTest-{Guid.NewGuid()}");
        var expected = new InvalidOperationException("dispose failure");
        var handler = new DisposeActionHandler(() => throw expected);
        logger.AddHandler(handler);

        Assert.AreSame(expected, Assert.Throws<InvalidOperationException>(() => logger.RemoveHandler(handler)));
        Assert.DoesNotThrow(() => logger.RemoveHandler(handler));
    }

    private sealed class ListHandler(List<LogRecord> records) : IHandler
    {
        public LogLevel Level { get; set; } = LogLevel.NoTest;
        public IFormatter Formatter { get; set; } = new StandardFormatter();

        public void Emit(LogRecord record)
        {
            records.Add(record);
        }

        public void Dispose()
        {
        }
    }

    private sealed class ActionHandler(Action<LogRecord> onEmit) : IHandler
    {
        public LogLevel Level { get; set; } = LogLevel.NoTest;
        public IFormatter Formatter { get; set; } = new StandardFormatter();

        public void Emit(LogRecord record)
        {
            onEmit(record);
        }

        public void Dispose()
        {
        }
    }

    private sealed class BlockingHandler(
        ManualResetEventSlim enteredEmit,
        ManualResetEventSlim allowEmitToFinish) : IHandler
    {
        private volatile bool _emitFinished;

        public bool DisposedAfterEmit { get; private set; }
        public LogLevel Level { get; set; } = LogLevel.NoTest;
        public IFormatter Formatter { get; set; } = new StandardFormatter();

        public void Emit(LogRecord record)
        {
            enteredEmit.Set();
            allowEmitToFinish.Wait();
            _emitFinished = true;
        }

        public void Dispose()
        {
            DisposedAfterEmit = _emitFinished;
        }
    }

    private sealed class DisposeActionHandler(Action dispose) : IHandler
    {
        public LogLevel Level { get; set; }
        public IFormatter Formatter { get; set; } = new StandardFormatter();

        public void Emit(LogRecord record)
        {
        }

        public void Dispose() => dispose();
    }

    private sealed class CustomFormatter : IFormatter
    {
        public string Format(LogRecord record)
        {
            return $"{record.LevelName}::{record.LoggerName}::{record.Message}";
        }
    }
}
