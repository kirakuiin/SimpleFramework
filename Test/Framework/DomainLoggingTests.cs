using System.Text.RegularExpressions;
using NUnit.Framework;
using SimpleFramework.Utility;

namespace Test.Framework;

/// <summary>验证 Framework 日志不会改变 Domain 行为，并提供稳定的生命周期关联信息。</summary>
[TestFixture]
[NonParallelizable]
public sealed class DomainLoggingTests
{
    /// <summary>Framework 日志器。</summary>
    private Logger _logger = null!;

    /// <summary>收集日志的处理器。</summary>
    private CaptureHandler _capture = null!;

    /// <summary>测试前的日志级别，测试后恢复。</summary>
    private LogLevel _originalLevel;

    /// <summary>接入日志收集处理器。</summary>
    [SetUp]
    public void SetUp()
    {
        _logger = Logger.GetLogger("Framework");
        _originalLevel = _logger.Level;
        _logger.Level = LogLevel.NoTest;
        _capture = new CaptureHandler();
        _logger.AddHandler(_capture);
    }

    /// <summary>移除日志收集处理器并恢复日志级别。</summary>
    [TearDown]
    public void TearDown()
    {
        _logger.RemoveHandler(_capture);
        _logger.Level = _originalLevel;
    }

    /// <summary>测试生命周期与树操作日志使用稳定的 Domain 诊断标识。</summary>
    [Test]
    public void LifecycleAndTreeLogsShareStableDomainIdentity()
    {
        var parent = ProbeDomain.Create(configure: domain =>
        {
            domain.AddModel(new ProbeModel());
            domain.AddSystem(new ProbeSystem());
        });
        var child = ProbeDomain.Create("child");

        parent.AddChild(child);
        parent.RemoveChild(child);
        parent.Dispose();
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
            Assert.That(parentMessages, Has.Some.Contains("System（契约").And.Contains("释放完成"));
            Assert.That(parentMessages, Has.Some.Contains("Model（契约").And.Contains("释放完成"));
            Assert.That(parentMessages, Has.Some.EqualTo($"Domain {parentIdentity} 已释放。"));
        });

        var systemReleaseIndex = parentMessages.FindIndex(message => message.Contains("System（契约") && message.Contains("释放完成"));
        var modelReleaseIndex = parentMessages.FindIndex(message => message.Contains("Model（契约") && message.Contains("释放完成"));
        var domainReleaseIndex = parentMessages.FindIndex(message => message == $"Domain {parentIdentity} 已释放。");

        Assert.That(systemReleaseIndex, Is.LessThan(modelReleaseIndex));
        Assert.That(modelReleaseIndex, Is.LessThan(domainReleaseIndex));
    }

    /// <summary>测试日志处理器失败不会掩盖生命周期异常。</summary>
    [Test]
    public void LoggingHandlerFailureDoesNotMaskLifecycleFailure()
    {
        var throwingHandler = new ThrowingHandler();
        _logger.AddHandler(throwingHandler);
        var expected = new ApplicationException("initialize");

        try
        {
            var actual = Assert.Throws<ApplicationException>(() => ProbeDomain.Create(configure: domain =>
                domain.AddModel(new ProbeModel(_ => throw expected))));

            Assert.That(actual, Is.SameAs(expected));
            Assert.That(_capture.Records, Has.Some.Matches<LogRecord>(record =>
                record.Level == LogLevel.Error && ReferenceEquals(record.Exception, expected)));
        }
        finally
        {
            _logger.RemoveHandler(throwingHandler);
        }
    }

    /// <summary>从日志消息中提取 Domain 诊断标识。</summary>
    private static string ExtractDomainIdentity(string message)
    {
        var match = Regex.Match(message, @"Domain (?<identity>[^ ]+#\d+)");
        Assert.That(match.Success, Is.True, $"日志中缺少 Domain 诊断标识：{message}");
        return match.Groups["identity"].Value;
    }

    /// <summary>收集 Framework 日志记录供测试断言。</summary>
    private sealed class CaptureHandler : IHandler
    {
        /// <summary>收集到的日志记录。</summary>
        public List<LogRecord> Records { get; } = [];

        /// <inheritdoc />
        public LogLevel Level { get; set; } = LogLevel.NoTest;

        /// <inheritdoc />
        public IFormatter Formatter { get; set; } = new StandardFormatter();

        /// <inheritdoc />
        public void Emit(LogRecord record) => Records.Add(record);

        /// <inheritdoc />
        public void Dispose() { }
    }

    /// <summary>模拟日志输出目标发送失败。</summary>
    private sealed class ThrowingHandler : IHandler
    {
        /// <inheritdoc />
        public LogLevel Level { get; set; } = LogLevel.NoTest;

        /// <inheritdoc />
        public IFormatter Formatter { get; set; } = new StandardFormatter();

        /// <inheritdoc />
        public void Emit(LogRecord record) => throw new InvalidOperationException("handler failure");

        /// <inheritdoc />
        public void Dispose() { }
    }
}
