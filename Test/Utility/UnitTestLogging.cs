using System;
using System.IO;
using NUnit.Framework;
using SimpleFramework.Utility;

namespace Test.Utility
{
    [TestFixture]
    public class TestLogging
    {
        private string _testLogFile;
        
        [SetUp]
        public void Setup()
        {
            // 创建临时日志文件
            _testLogFile = Path.Combine(Path.GetTempPath(), $"test_log_{Guid.NewGuid()}.log");
        }
        
        [TearDown]
        public void TearDown()
        {
            // 清理日志文件
            if (File.Exists(_testLogFile))
            {
                File.Delete(_testLogFile);
            }
        }

        [Test]
        public void TestBasicLogging()
        {
            // 配置根日志
            Logging.BasicConfig(LogLevel.Debug, _testLogFile);
            
            // 使用静态方法记录日志
            Logging.Debug("这是一条调试信息");
            Logging.Info("这是一条普通信息");
            Logging.Warning("这是一条警告信息");
            Logging.Error("这是一条错误信息");
            
            // 验证日志文件存在
            Assert.IsTrue(File.Exists(_testLogFile));
            
            // 读取日志内容
            var root = Logging.GetLogger("root");
            root.ClearHandlers();
            var logLines = File.ReadAllLines(_testLogFile);
            
            // 验证日志行数
            Assert.AreEqual(4, logLines.Length);
            
            // 验证日志内容包含期望的消息
            Assert.IsTrue(logLines[0].Contains("Debug") && logLines[0].Contains("这是一条调试信息"));
            Assert.IsTrue(logLines[1].Contains("Info") && logLines[1].Contains("这是一条普通信息"));
            Assert.IsTrue(logLines[2].Contains("Warning") && logLines[2].Contains("这是一条警告信息"));
            Assert.IsTrue(logLines[3].Contains("Error") && logLines[3].Contains("这是一条错误信息"));
        }

        [Test]
        public void TestCustomLogger()
        {
            // 获取自定义日志记录器
            var logger = Logger.GetLogger("TestLogger");
            
            // 添加文件处理器
            var handler = new FileHandler(_testLogFile);
            logger.AddHandler(handler);
            
            // 记录日志
            logger.Info("来自自定义日志记录器的消息");
            logger.Error("发生了一个错误", new Exception("测试异常"));
            
            // 验证日志文件存在
            Assert.IsTrue(File.Exists(_testLogFile));
            
            // 读取日志内容
            handler.Dispose();
            var logLines = File.ReadAllLines(_testLogFile);
            
            // 验证日志包含日志器名称
            Assert.IsTrue(logLines[0].Contains("[TestLogger]"));
            
            // 验证异常信息被记录
            Assert.IsTrue(logLines[2].Contains("测试异常"));
        }

        [Test]
        public void TestLogLevels()
        {
            // 获取自定义日志记录器并设置级别为WARNING
            var logger = Logger.GetLogger("LevelTest");
            logger.Level = LogLevel.Warning;
            var handler = new FileHandler(_testLogFile);
            
            // 添加文件处理器
            logger.AddHandler(handler);
            
            // 尝试记录不同级别的日志
            logger.Debug("这条调试信息不应该被记录");
            logger.Info("这条信息不应该被记录");
            logger.Warning("这条警告信息应该被记录");
            logger.Error("这条错误信息应该被记录");
            
            // 验证日志文件存在
            Assert.IsTrue(File.Exists(_testLogFile));
            
            // 读取日志内容
            handler.Dispose();
            var logLines = File.ReadAllLines(_testLogFile);
            
            // 验证只有警告和错误被记录
            Assert.AreEqual(2, logLines.Length);
            Assert.IsTrue(logLines[0].Contains("Warning"));
            Assert.IsTrue(logLines[1].Contains("Error"));
        }
        
        [Test]
        public void TestCustomFormatter()
        {
            // 创建自定义格式化器
            var formatter = new CustomFormatter();
            
            // 获取自定义日志记录器
            var logger = Logger.GetLogger("FormatterTest");
            
            // 创建并配置处理器
            var handler = new FileHandler(_testLogFile);
            handler.Formatter = formatter;
            
            // 添加处理器
            logger.AddHandler(handler);
            
            // 记录日志
            logger.Info("测试自定义格式化器");
            
            // 关闭文件句柄
            handler.Dispose();
            
            // 验证日志文件存在
            Assert.IsTrue(File.Exists(_testLogFile));
            
            // 读取日志内容
            var logLines = File.ReadAllLines(_testLogFile);
            
            // 验证自定义格式
            Assert.IsTrue(logLines[0].StartsWith("Info::FormatterTest::"));
        }
    }
    
    // 自定义格式化器示例
    public class CustomFormatter : IFormatter
    {
        public string Format(LogRecord record)
        {
            return $"{record.LevelName}::{record.LoggerName}::{record.Message}";
        }
    }
} 