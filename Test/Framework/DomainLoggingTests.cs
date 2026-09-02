using System.Text.RegularExpressions;
using NUnit.Framework;
using SimpleFramework.Utility;

namespace Test.Framework;

/// <summary>验证 Framework 日志不会改变 Domain 行为，并提供稳定的生命周期关联信息。</summary>
[TestFixture]
[NonParallelizable]
public sealed class DomainLoggingTests
{
    private Logger _logger = null!;
    private CaptureHandler _capture = null!;
    private LogLevel _originalLevel;

    [SetUp]
    public void SetUp()
    {
        _logger = Logger.GetLogger("Framework");
        _originalLevel = _logger.Level;
        _logger.Level = LogLevel.NoTest;
        _capture = new CaptureHandler();
        _logger.AddHandler(_capture);
    }

    [TearDown]
    public void TearDown()
    {
        _logger.RemoveHandler(_capture);
        _logger.Level = _originalLevel;
    }

    [Test]
    public void LifecycleAndTreeLogsShareStableDomainIdentity()
    {
        var parent = ProbeDomain.Create(configure: domain =>
        {
            domain.RegisterModel(new ProbeModel());
            domain.RegisterSystem(new ProbeSystem());
        });
        var child = ProbeDomain.Create("child");

        parent.AddChild(child);
        parent.RemoveChild(child);
        parent.AddChild(child);
        parent.DisposeSelfOnly();
        child.Dispose();

        var modelRegistration = _capture.Records.Single(record =>
            record.Message.Contains("Model（契约") && record.Message.Contains("注册完成"));
        var parentIdentity = ExtractDomainIdentity(modelRegistration.Message);
        var parentMessages = _capture.Records
            .Where(record => record.Message.Contains(parentIdentity, StringComparison.Ordinal))
            .Select(record => record.Message)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(_capture.Records, Has.All.Property(nameof(LogRecord.LoggerName)).EqualTo("Framework"));
            Assert.That(parentMessages, Has.Some.Contains("System（契约").And.Contains("注册完成"));
            Assert.That(parentMessages, Has.Some.EqualTo($"Domain {parentIdentity} 已激活。"));
            Assert.That(parentMessages, Has.Some.Contains("已挂载子 Domain"));
            Assert.That(parentMessages, Has.Some.Contains("已移除子 Domain"));
            Assert.That(parentMessages, Has.Some.Contains("已分离子 Domain"));
            Assert.That(parentMessages, Has.Some.Contains("System（契约").And.Contains("释放完成"));
            Assert.That(parentMessages, Has.Some.Contains("Model（契约").And.Contains("释放完成"));
            Assert.That(parentMessages, Has.Some.EqualTo($"Domain {parentIdentity} 已释放。"));
        });

        var systemReleaseIndex = parentMessages.FindIndex(message =>
            message.Contains("System（契约") && message.Contains("释放完成"));
        var modelReleaseIndex = parentMessages.FindIndex(message =>
            message.Contains("Model（契约") && message.Contains("释放完成"));
        var domainReleaseIndex = parentMessages.FindIndex(message => message == $"Domain {parentIdentity} 已释放。");
        Assert.That(systemReleaseIndex, Is.LessThan(modelReleaseIndex));
        Assert.That(modelReleaseIndex, Is.LessThan(domainReleaseIndex));
    }

    [Test]
    public void LoggingHandlerFailureDoesNotMaskLifecycleFailure()
    {
        var throwingHandler = new ThrowingHandler();
        _logger.AddHandler(throwingHandler);
        var expected = new ApplicationException("initialize");

        try
        {
            var actual = Assert.Throws<ApplicationException>(() => ProbeDomain.Create(configure: domain =>
                domain.RegisterModel(new ProbeModel(_ => throw expected))));

            Assert.That(actual, Is.SameAs(expected));
            Assert.That(_capture.Records, Has.Some.Matches<LogRecord>(record =>
                record.Level == LogLevel.Error && ReferenceEquals(record.Exception, expected)));
        }
        finally
        {
            _logger.RemoveHandler(throwingHandler);
        }
    }

    [Test]
    public void ActivationLogCannotDisposeCandidateBeforeCreateReturns()
    {
        ProbeDomain? candidate = null;
        var handler = new ActionHandler(record =>
        {
            if (record.Message.EndsWith("已激活。", StringComparison.Ordinal)) candidate!.Dispose();
        });
        _logger.AddHandler(handler);

        try
        {
            var domain = ProbeDomain.Create(configure: value => candidate = value);

            Assert.DoesNotThrow(() => domain.RegisterUtility(new ClockUtility()));
            domain.Dispose();
        }
        finally
        {
            _logger.RemoveHandler(handler);
            candidate?.Dispose();
        }
    }

    [Test]
    public void AddChildLogCannotRemoveChildBeforeAddReturns()
    {
        var parent = ProbeDomain.Create("parent");
        var child = ProbeDomain.Create("child");
        var handler = new ActionHandler(record =>
        {
            if (record.Message.Contains("已挂载子 Domain", StringComparison.Ordinal)) parent.RemoveChild(child);
        });
        _logger.AddHandler(handler);

        try
        {
            parent.AddChild(child);

            Assert.That(child.Parent, Is.SameAs(parent));
        }
        finally
        {
            _logger.RemoveHandler(handler);
            parent.Dispose();
            child.Dispose();
        }
    }

    [Test]
    public void DisposeSelfOnlyLogCannotDisposeDetachedChildBeforeReturn()
    {
        var parent = ProbeDomain.Create("parent");
        var child = ProbeDomain.Create("child");
        parent.AddChild(child);
        var handler = new ActionHandler(record =>
        {
            if (record.Message.Contains("已分离子 Domain", StringComparison.Ordinal)) child.Dispose();
        });
        _logger.AddHandler(handler);

        try
        {
            parent.DisposeSelfOnly();

            Assert.That(child.Parent, Is.Null);
        }
        finally
        {
            _logger.RemoveHandler(handler);
            parent.Dispose();
            child.Dispose();
        }
    }

    private static string ExtractDomainIdentity(string message)
    {
        var match = Regex.Match(message, @"Domain (?<identity>[^ ]+#\d+)");
        Assert.That(match.Success, Is.True, $"日志中缺少 Domain 诊断标识：{message}");
        return match.Groups["identity"].Value;
    }

    /// <summary>收集 Framework 日志记录供测试断言。</summary>
    private sealed class CaptureHandler : IHandler
    {
        public List<LogRecord> Records { get; } = [];
        public LogLevel Level { get; set; } = LogLevel.NoTest;
        public IFormatter Formatter { get; set; } = new StandardFormatter();
        public void Emit(LogRecord record) => Records.Add(record);
        public void Dispose() { }
    }

    /// <summary>模拟日志输出目标发送失败。</summary>
    private sealed class ThrowingHandler : IHandler
    {
        public LogLevel Level { get; set; } = LogLevel.NoTest;
        public IFormatter Formatter { get; set; } = new StandardFormatter();
        public void Emit(LogRecord record) => throw new InvalidOperationException("handler failure");
        public void Dispose() { }
    }

    /// <summary>将日志记录转发给测试回调。</summary>
    private sealed class ActionHandler(Action<LogRecord> emit) : IHandler
    {
        public LogLevel Level { get; set; } = LogLevel.NoTest;
        public IFormatter Formatter { get; set; } = new StandardFormatter();
        public void Emit(LogRecord record) => emit(record);
        public void Dispose() { }
    }
}
