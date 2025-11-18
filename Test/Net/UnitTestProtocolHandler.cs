using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleFramework.Net.Connection;
using SimpleFramework.Utility;

namespace Test.Net;

/// <summary>
/// ProtocolHandler测试的辅助类和工具方法
/// </summary>
public static class ProtocolHandlerTestHelper
{
    /// <summary>
    /// 创建测试用的ProtocolHandler实例
    /// </summary>
    public static ProtocolHandler CreateHandlerWithTestProtocols()
    {
        var handler = new ProtocolHandler();
        handler.RegisterCallingProtocol();
        return handler;
    }

    /// <summary>
    /// 验证数据包格式的辅助方法
    /// </summary>
    public static bool ValidatePacketFormat(byte[] data, out ushort mainId, out ushort subId, out int dataLength, out ushort beforeGuard, out ushort afterGuard)
    {
        mainId = 0;
        subId = 0;
        dataLength = 0;
        beforeGuard = 0;
        afterGuard = 0;

        if (data == null || data.Length < 12) // 最小包大小检查
            return false;

        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);

        try
        {
            mainId = br.ReadUInt16();
            subId = br.ReadUInt16();
            dataLength = br.ReadInt32();
            beforeGuard = br.ReadUInt16();

            if (data.Length < 12 + dataLength)
                return false;

            br.ReadBytes(dataLength);
            afterGuard = br.ReadUInt16();

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 生成边界值测试数据
    /// </summary>
    public static IEnumerable<object[]> GetBoundaryTestData()
    {
        yield return new object[] { new TestProtocol1 { Id = 0, Name = "" } }; // 最小值
        yield return new object[] { new TestProtocol1 { Id = int.MaxValue, Name = new string('A', 1000) } }; // 最大值
        yield return new object[] { new TestProtocol1 { Id = -1, Name = "Negative ID" } }; // 负数
        yield return new object[] { new TestProtocol1 { Id = 42, Name = "测试中文字符" } }; // Unicode字符
        yield return new object[] { new TestProtocol1 { Id = 123, Name = "Special chars: !@#$%^&*()" } }; // 特殊字符
    }
}

/// <summary>
/// 用于测试重复协议的冲突协议
/// </summary>
[Protocol(1, 1)] // 与TestProtocol1相同ID
public struct DuplicateProtocol
{
    public string Message;
}

/// <summary>
/// 测试用的简单协议结构体
/// </summary>
[Protocol(1, 1)]
public struct TestProtocol1
{
    public int Id;
    public string Name;
}

/// <summary>
/// 测试用的复杂数据协议结构体
/// </summary>
[Protocol(1, 2)]
public struct TestProtocol2
{
    public bool Flag;
    public float Value;
    public long Timestamp;
}

/// <summary>
/// 测试用的边界值协议结构体
/// </summary>
[Protocol(2, 1)]
public struct TestProtocol3
{
    public byte SmallValue;
    public double LargeValue;
    public decimal DecimalValue;
}

/// <summary>
/// 不带ProtocolAttribute的结构体，用于测试错误处理
/// </summary>
public struct InvalidProtocol
{
    public int Value;
}

/// <summary>
/// 测试ProtocolHandler基础功能
/// </summary>
[TestFixture]
public class TestProtocolHandlerBasics
{
    private ProtocolHandler _handler;

    [SetUp]
    public void Setup()
    {
        _handler = new ProtocolHandler();
    }

    [TearDown]
    public void TearDown()
    {
        _handler.Clear();
    }

    [Test]
    public void TestRegisterProtocolFromAssembly()
    {
        // Act
        _handler.RegisterProtocol(Assembly.GetExecutingAssembly());

        // Assert - 由于我们无法直接访问内部字典，这里主要通过PackData来验证
        var result = _handler.PackData(new TestProtocol1 { Id = 1, Name = "Test" }, out var data);

        Assert.That(result, Is.True);
        Assert.That(data, Is.Not.Null);
        Assert.That(data.Length, Is.GreaterThan(0));
    }

    [Test]
    public void TestRegisterHandler()
    {
        // Arrange
        _handler.RegisterProtocol(Assembly.GetExecutingAssembly());
        var receivedProtocols = new List<TestProtocol1>();

        // Act
        _handler.RegisterHandler<TestProtocol1>(protocol => receivedProtocols.Add(protocol));

        // Assert
        Assert.That(receivedProtocols.Count, Is.EqualTo(0)); // 初始应该为空

        // 通过打包和数据处理来验证注册是否成功
        var testData = new TestProtocol1 { Id = 123, Name = "Test Handler" };
        var packResult = _handler.PackData(testData, out var data);
        var handleResult = _handler.HandleData(data);

        Assert.That(packResult, Is.True);
        Assert.That(handleResult, Is.True);
        Assert.That(receivedProtocols.Count, Is.EqualTo(1));
        Assert.That(receivedProtocols[0].Id, Is.EqualTo(123));
        Assert.That(receivedProtocols[0].Name, Is.EqualTo("Test Handler"));
    }

    [Test]
    public void TestRegisterMultipleHandlers()
    {
        // Arrange
        _handler.RegisterProtocol(Assembly.GetExecutingAssembly());
        var protocol1Received = new List<TestProtocol1>();
        var protocol2Received = new List<TestProtocol2>();

        // Act
        _handler.RegisterHandler<TestProtocol1>(protocol => protocol1Received.Add(protocol));
        _handler.RegisterHandler<TestProtocol2>(protocol => protocol2Received.Add(protocol));

        // 测试协议1
        var testData1 = new TestProtocol1 { Id = 1, Name = "Protocol1" };
        var packResult1 = _handler.PackData(testData1, out var data1);
        var handleResult1 = _handler.HandleData(data1);

        // 测试协议2
        var testData2 = new TestProtocol2 { Flag = true, Value = 3.14f, Timestamp = DateTime.Now.Ticks };
        var packResult2 = _handler.PackData(testData2, out var data2);
        var handleResult2 = _handler.HandleData(data2);

        // Assert
        Assert.That(packResult1, Is.True);
        Assert.That(handleResult1, Is.True);
        Assert.That(protocol1Received.Count, Is.EqualTo(1));
        Assert.That(protocol1Received[0].Name, Is.EqualTo("Protocol1"));

        Assert.That(packResult2, Is.True);
        Assert.That(handleResult2, Is.True);
        Assert.That(protocol2Received.Count, Is.EqualTo(1));
        Assert.That(protocol2Received[0].Flag, Is.EqualTo(true));
    }

    [Test]
    public void TestRegisterHandlerWithoutProtocolRegistration()
    {
        // Arrange
        var receivedProtocols = new List<TestProtocol1>();

        // Act - 尝试注册处理函数但未注册协议类型
        _handler.RegisterHandler<TestProtocol1>(protocol => receivedProtocols.Add(protocol));

        // 测试打包和处理
        var testData = new TestProtocol1 { Id = 1, Name = "Test" };
        var packResult = _handler.PackData(testData, out var data);
        var handleResult = _handler.HandleData(data);

        // Assert - 打包应该成功，但处理应该失败
        Assert.That(packResult, Is.True);
        Assert.That(handleResult, Is.False); // 因为协议类型未注册，处理失败
        Assert.That(receivedProtocols.Count, Is.EqualTo(0));
    }

    [Test]
    public void TestClear()
    {
        // Arrange
        _handler.RegisterProtocol(Assembly.GetExecutingAssembly());
        _handler.RegisterHandler<TestProtocol1>(protocol => { });

        // Act
        _handler.Clear();

        // Assert - 清理后重新注册协议应该成功
        _handler.RegisterProtocol(Assembly.GetExecutingAssembly());
        var packResult = _handler.PackData(new TestProtocol1 { Id = 1, Name = "After Clear" }, out var data);

        Assert.That(packResult, Is.True);
        Assert.That(data, Is.Not.Null);
    }
}

/// <summary>
/// 测试ProtocolHandler的数据打包功能
/// </summary>
[TestFixture]
public class TestProtocolHandlerPacking
{
    private ProtocolHandler _handler;

    [SetUp]
    public void Setup()
    {
        _handler = new ProtocolHandler();
    }

    [Test]
    public void TestPackBasicProtocol()
    {
        // Arrange
        var testProtocol = new TestProtocol1 { Id = 42, Name = "Basic Test" };

        // Act
        var result = _handler.PackData(testProtocol, out var data);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(data, Is.Not.Null);
        Assert.That(data.Length, Is.GreaterThan(10)); // 至少包含协议头和数据

        // 验证数据包结构
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);
        var mainId = br.ReadUInt16();
        var subId = br.ReadUInt16();
        var length = br.ReadInt32();
        var guard1 = br.ReadUInt16();

        Assert.That(mainId, Is.EqualTo(1));
        Assert.That(subId, Is.EqualTo(1));
        Assert.That(length, Is.GreaterThan(0));
        Assert.That(guard1, Is.EqualTo(0xCafe));
    }

    [Test]
    public void TestPackComplexProtocol()
    {
        // Arrange
        var testProtocol = new TestProtocol2
        {
            Flag = true,
            Value = 3.14159f,
            Timestamp = DateTime.Now.Ticks
        };

        // Act
        var result = _handler.PackData(testProtocol, out var data);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(data, Is.Not.Null);

        // 验证协议ID
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);
        var mainId = br.ReadUInt16();
        var subId = br.ReadUInt16();

        Assert.That(mainId, Is.EqualTo(1));
        Assert.That(subId, Is.EqualTo(2));
    }

    [Test]
    public void TestPackEdgeValuesProtocol()
    {
        // Arrange
        var testProtocol = new TestProtocol3
        {
            SmallValue = byte.MaxValue,
            LargeValue = double.MaxValue,
            DecimalValue = decimal.MinValue
        };

        // Act
        var result = _handler.PackData(testProtocol, out var data);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(data, Is.Not.Null);

        // 验证协议ID
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);
        var mainId = br.ReadUInt16();
        var subId = br.ReadUInt16();

        Assert.That(mainId, Is.EqualTo(2));
        Assert.That(subId, Is.EqualTo(1));
    }

    [Test]
    public void TestPackInvalidProtocol()
    {
        // Arrange
        var invalidProtocol = new InvalidProtocol { Value = 123 };

        // Act
        var result = _handler.PackData(invalidProtocol, out var data);

        // Assert
        Assert.That(result, Is.False);
        Assert.That(data, Is.Empty);
    }

    [Test]
    public void TestGuardValues()
    {
        // Arrange
        var testProtocol = new TestProtocol1 { Id = 1, Name = "Guard Test" };

        // Act
        var result = _handler.PackData(testProtocol, out var data);

        // Assert
        Assert.That(result, Is.True);

        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);
        br.ReadUInt16(); // mainId
        br.ReadUInt16(); // subId
        var length = br.ReadInt32();
        var beforeGuard = br.ReadUInt16();
        br.ReadBytes(length); // data
        var afterGuard = br.ReadUInt16();

        Assert.That(beforeGuard, Is.EqualTo(0xCafe));
        Assert.That(afterGuard, Is.EqualTo(0xCafe));
    }
}

/// <summary>
/// 测试ProtocolHandler的数据处理功能
/// </summary>
[TestFixture]
public class TestProtocolHandlerHandling
{
    private ProtocolHandler _handler;

    [SetUp]
    public void Setup()
    {
        _handler = new ProtocolHandler();
        _handler.RegisterProtocol(Assembly.GetExecutingAssembly());
    }

    [Test]
    public void TestHandleValidData()
    {
        // Arrange
        var receivedProtocols = new List<TestProtocol1>();
        _handler.RegisterHandler<TestProtocol1>(protocol => receivedProtocols.Add(protocol));

        var testProtocol = new TestProtocol1 { Id = 999, Name = "Handle Test" };
        var packResult = _handler.PackData(testProtocol, out var data);

        // Act
        var handleResult = _handler.HandleData(data);

        // Assert
        Assert.That(packResult, Is.True);
        Assert.That(handleResult, Is.True);
        Assert.That(receivedProtocols.Count, Is.EqualTo(1));
        Assert.That(receivedProtocols[0].Id, Is.EqualTo(999));
        Assert.That(receivedProtocols[0].Name, Is.EqualTo("Handle Test"));
    }

    [Test]
    public void TestHandleInvalidGuardValues()
    {
        // Arrange
        var testProtocol = new TestProtocol1 { Id = 1, Name = "Invalid Guard" };
        var packResult = _handler.PackData(testProtocol, out var data);

        // 修改守卫值使其无效
        data[8] = 0x00; // 修改第一个守卫值

        // Act
        var handleResult = _handler.HandleData(data);

        // Assert
        Assert.That(packResult, Is.True);
        Assert.That(handleResult, Is.False);
    }

    [Test]
    public void TestHandleIncompleteData()
    {
        // Arrange
        var incompleteData = new byte[] { 1, 0, 1, 0 }; // 只有mainId和subId，缺少长度和守卫值

        // Act
        var handleResult = _handler.HandleData(incompleteData);

        // Assert
        Assert.That(handleResult, Is.False);
    }

    [Test]
    public void TestHandleUnregisteredProtocol()
    {
        // Arrange
        var testProtocol = new TestProtocol1 { Id = 1, Name = "Unregistered" };
        var packResult = _handler.PackData(testProtocol, out var data);

        // 不注册处理函数

        // Act
        var handleResult = _handler.HandleData(data);

        // Assert
        Assert.That(packResult, Is.True);
        Assert.That(handleResult, Is.False); // HandleData返回true，但不会有处理函数被调用
    }

    [Test]
    public void TestHandleEmptyData()
    {
        // Act
        var handleResult = _handler.HandleData(Array.Empty<byte>());

        // Assert
        Assert.That(handleResult, Is.False);
    }

    [Test]
    public void TestHandleNullData()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _handler.HandleData(null));
    }
}

/// <summary>
/// 测试ProtocolHandler的扩展方法
/// </summary>
[TestFixture]
public class TestProtocolHandlerExtensions
{
    private ProtocolHandler _handler;

    [SetUp]
    public void Setup()
    {
        _handler = new ProtocolHandler();
    }

    [Test]
    public void TestRegisterExecutingProtocol()
    {
        // Act
        _handler.RegisterExecutingProtocol();

        // Assert - 通过打包测试来验证
        var result = _handler.PackData(new TestProtocol1 { Id = 1, Name = "Executing Assembly" }, out var data);

        Assert.That(result, Is.True);
        Assert.That(data, Is.Not.Null);
    }

    [Test]
    public void TestRegisterCallingProtocol()
    {
        // Act
        _handler.RegisterCallingProtocol();

        // Assert - 通过打包测试来验证
        var result = _handler.PackData(new TestProtocol1 { Id = 1, Name = "Calling Assembly" }, out var data);

        Assert.That(result, Is.True);
        Assert.That(data, Is.Not.Null);
    }
}

/// <summary>
/// 测试ProtocolHandler的集成功能
/// </summary>
[TestFixture]
public class TestProtocolHandlerIntegration
{
    private ProtocolHandler _handler;

    [SetUp]
    public void Setup()
    {
        _handler = new ProtocolHandler();
        _handler.RegisterCallingProtocol();
    }

    [TearDown]
    public void TearDown()
    {
        _handler.Clear();
    }

    [Test]
    public void TestCompleteWorkflow()
    {
        // Arrange
        var receivedProtocols = new List<TestProtocol1>();
        var receivedProtocols2 = new List<TestProtocol2>();

        _handler.RegisterHandler<TestProtocol1>(protocol => receivedProtocols.Add(protocol));
        _handler.RegisterHandler<TestProtocol2>(protocol => receivedProtocols2.Add(protocol));

        // Act & Assert - 完整的工作流程
        var testProtocol1 = new TestProtocol1 { Id = 1, Name = "Integration Test 1" };
        var testProtocol2 = new TestProtocol2 { Flag = false, Value = 2.71f, Timestamp = 1234567890 };

        // 打包数据
        var packResult1 = _handler.PackData(testProtocol1, out var data1);
        var packResult2 = _handler.PackData(testProtocol2, out var data2);

        Assert.That(packResult1, Is.True);
        Assert.That(packResult2, Is.True);

        // 处理数据
        var handleResult1 = _handler.HandleData(data1);
        var handleResult2 = _handler.HandleData(data2);

        Assert.That(handleResult1, Is.True);
        Assert.That(handleResult2, Is.True);

        // 验证结果
        Assert.That(receivedProtocols.Count, Is.EqualTo(1));
        Assert.That(receivedProtocols[0].Name, Is.EqualTo("Integration Test 1"));

        Assert.That(receivedProtocols2.Count, Is.EqualTo(1));
        Assert.That(receivedProtocols2[0].Flag, Is.EqualTo(false));
        Assert.That(receivedProtocols2[0].Value, Is.EqualTo(2.71f));
    }

    [Test]
    public void TestMultipleDataPackets()
    {
        // Arrange
        var receivedProtocols = new List<TestProtocol1>();
        _handler.RegisterHandler<TestProtocol1>(protocol => receivedProtocols.Add(protocol));

        // Act - 发送多个数据包
        for (int i = 0; i < 5; i++)
        {
            var protocol = new TestProtocol1 { Id = i, Name = $"Packet {i}" };
            var packResult = _handler.PackData(protocol, out var data);
            var handleResult = _handler.HandleData(data);

            Assert.That(packResult, Is.True);
            Assert.That(handleResult, Is.True);
        }

        // Assert
        Assert.That(receivedProtocols.Count, Is.EqualTo(5));
        for (int i = 0; i < 5; i++)
        {
            Assert.That(receivedProtocols[i].Id, Is.EqualTo(i));
            Assert.That(receivedProtocols[i].Name, Is.EqualTo($"Packet {i}"));
        }
    }

    [Test]
    public void TestConcurrentHandling()
    {
        // Arrange
        var receivedProtocols = new List<TestProtocol1>();
        var lockObject = new object();

        _handler.RegisterHandler<TestProtocol1>(protocol =>
        {
            lock (lockObject)
            {
                receivedProtocols.Add(protocol);
            }
        });

        // Act - 并发发送数据包
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                var protocol = new TestProtocol1 { Id = index, Name = $"Concurrent {index}" };
                var packResult = _handler.PackData(protocol, out var data);
                var handleResult = _handler.HandleData(data);

                Assert.That(packResult, Is.True);
                Assert.That(handleResult, Is.True);
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        Assert.That(receivedProtocols.Count, Is.EqualTo(10));
    }

    [Test]
    public void TestProtocolAttributeUsage()
    {
        // 测试ProtocolAttribute的正确使用
        var testProtocolType = typeof(TestProtocol1);
        var attribute = testProtocolType.GetCustomAttribute<ProtocolAttribute>();

        Assert.That(attribute, Is.Not.Null);
        Assert.That(attribute.MainId, Is.EqualTo(1));
        Assert.That(attribute.SubId, Is.EqualTo(1));

        // 测试另一个协议
        var testProtocol2Type = typeof(TestProtocol2);
        var attribute2 = testProtocol2Type.GetCustomAttribute<ProtocolAttribute>();

        Assert.That(attribute2, Is.Not.Null);
        Assert.That(attribute2.MainId, Is.EqualTo(1));
        Assert.That(attribute2.SubId, Is.EqualTo(2));
    }
}

/// <summary>
/// 测试ProtocolHandler的边界值和性能
/// </summary>
[TestFixture]
public class TestProtocolHandlerBoundaryAndPerformance
{
    private ProtocolHandler _handler;

    [SetUp]
    public void Setup()
    {
        _handler = ProtocolHandlerTestHelper.CreateHandlerWithTestProtocols();
    }

    [TearDown]
    public void TearDown()
    {
        _handler.Clear();
    }

    [Test]
    [TestCaseSource(typeof(ProtocolHandlerTestHelper), nameof(ProtocolHandlerTestHelper.GetBoundaryTestData))]
    public void TestPackDataWithBoundaryValuesShouldSucceed(TestProtocol1 testData)
    {
        // Arrange & Act
        var result = _handler.PackData(testData, out var data);

        // Assert
        Assert.That(result, Is.True, $"边界值测试失败: Id={testData.Id}, Name={testData.Name}");
        Assert.That(data, Is.Not.Null, "打包后的数据不应为空");
        Assert.That(data.Length, Is.GreaterThan(0), "打包后的数据长度应大于0");

        // 验证数据包格式
        var isValidFormat = ProtocolHandlerTestHelper.ValidatePacketFormat(data, out var mainId, out var subId, out var dataLength, out var beforeGuard, out var afterGuard);
        Assert.That(isValidFormat, Is.True, "数据包格式应该有效");
        Assert.That(mainId, Is.EqualTo(1), "主协议ID应该正确");
        Assert.That(subId, Is.EqualTo(1), "子协议ID应该正确");
        Assert.That(beforeGuard, Is.EqualTo(0xCafe), "前守卫值应该正确");
        Assert.That(afterGuard, Is.EqualTo(0xCafe), "后守卫值应该正确");
    }

    [Test]
    public void TestPackDataPerformanceTestShouldCompleteWithinTimeLimit()
    {
        // Arrange
        var testData = new TestProtocol1 { Id = 1, Name = "Performance Test" };
        const int iterations = 10000;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var result = _handler.PackData(testData, out var data);
            Assert.That(result, Is.True, $"第{i}次打包应该成功");
        }

        stopwatch.Stop();

        // Assert
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000), $"{iterations}次打包操作应在1秒内完成");
        Assert.That(stopwatch.ElapsedMilliseconds / (double)iterations, Is.LessThan(0.1), "平均每次打包操作应少于0.1毫秒");
    }

    [Test]
    public void TestHandleDataPerformanceTestShouldCompleteWithinTimeLimit()
    {
        // Arrange
        var receivedProtocols = new List<TestProtocol1>();
        var lockObject = new object();
        _handler.RegisterHandler<TestProtocol1>(protocol =>
        {
            lock (lockObject)
            {
                receivedProtocols.Add(protocol);
            }
        });

        var testData = new TestProtocol1 { Id = 1, Name = "Performance Test" };
        var packResult = _handler.PackData(testData, out var data);
        Assert.That(packResult, Is.True, "数据打包应该成功");

        const int iterations = 5000;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var handleResult = _handler.HandleData(data);
            Assert.That(handleResult, Is.True, $"第{i}次处理应该成功");
        }

        stopwatch.Stop();

        // Assert
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000), $"{iterations}次处理操作应在1秒内完成");
        Assert.That(receivedProtocols.Count, Is.EqualTo(iterations), "应该接收到所有协议数据");
        Assert.That(stopwatch.ElapsedMilliseconds / (double)iterations, Is.LessThan(0.2), "平均每次处理操作应少于0.2毫秒");
    }

    [Test]
    public void TestPackDataWithExtremelyLargeStringShouldHandleGracefully()
    {
        // Arrange
        var largeString = new string('X', 100000); // 100K字符
        var testData = new TestProtocol1 { Id = 1, Name = largeString };

        // Act & Assert - 这可能会因为内存限制而失败，但应该优雅处理
        Assert.DoesNotThrow(() =>
        {
            var result = _handler.PackData(testData, out var data);
            // 如果成功，验证数据完整性
            if (result)
            {
                Assert.That(data, Is.Not.Null);
                Assert.That(data.Length, Is.GreaterThan(largeString.Length));
            }
        }, "处理大数据时不应抛出异常");
    }

    [Test]
    public void TestHandleDataWithCorruptedGuardValuesShouldReturnFalse()
    {
        // Arrange
        var testData = new TestProtocol1 { Id = 1, Name = "Corruption Test" };
        var packResult = _handler.PackData(testData, out var data);
        Assert.That(packResult, Is.True, "数据打包应该成功");

        // 修改守卫值
        if (data.Length >= 12)
        {
            // 修改第一个守卫值
            data[8] = 0x00;
            data[9] = 0x00;
        }

        // Act
        var handleResult = _handler.HandleData(data);

        // Assert
        Assert.That(handleResult, Is.False, "守卫值损坏时数据处理应该失败");
    }

    [Test]
    public void TestHandleDataWithTruncatedDataShouldReturnFalse()
    {
        // Arrange
        var testData = new TestProtocol1 { Id = 1, Name = "Truncated Test" };
        var packResult = _handler.PackData(testData, out var fullData);
        Assert.That(packResult, Is.True, "完整数据打包应该成功");

        // 创建截断的数据
        var truncatedData = new byte[fullData.Length / 2];
        Array.Copy(fullData, truncatedData, truncatedData.Length);

        // Act
        var handleResult = _handler.HandleData(truncatedData);

        // Assert
        Assert.That(handleResult, Is.False, "截断数据处理应该失败");
    }
}