using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleFramework.Net;
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
    /// 验证数据包格式的辅助方法 - 基于类型哈希的新格式
    /// </summary>
    public static bool ValidatePacketFormat(byte[] data, out ulong typeHash, out int dataLength, out ushort beforeGuard, out ushort afterGuard)
    {
        typeHash = 0;
        dataLength = 0;
        beforeGuard = 0;
        afterGuard = 0;

        if (data == null || data.Length < 16) // 最小包大小检查：2+8+4+2 = 16 bytes
            return false;

        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);

        try
        {
            beforeGuard = br.ReadUInt16();
            typeHash = br.ReadUInt64();  // 读取类型哈希 (8 bytes)
            dataLength = br.ReadInt32();

            if (data.Length < 16 + dataLength)
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
    /// 验证数据包并返回类型哈希的十六进制字符串表示
    /// </summary>
    public static bool ValidatePacketFormatWithTypeHash(byte[] data, out string typeHashHex, out int dataLength, out ushort beforeGuard, out ushort afterGuard)
    {
        if (ValidatePacketFormat(data, out var typeHash, out dataLength, out beforeGuard, out afterGuard))
        {
            typeHashHex = $"0x{typeHash:X16}";
            return true;
        }
        typeHashHex = null;
        return false;
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
[Protocol] // 与TestProtocol1相同ID
public struct DuplicateProtocol
{
    public string Message;
}

/// <summary>
/// 测试用的简单协议结构体
/// </summary>
[Protocol]
public struct TestProtocol1
{
    public int Id;
    public string Name;
}

/// <summary>
/// 测试用的复杂数据协议结构体
/// </summary>
[Protocol]
public struct TestProtocol2
{
    public bool Flag;
    public float Value;
    public long Timestamp;
}

/// <summary>
/// 测试用的边界值协议结构体
/// </summary>
[Protocol]
public struct TestProtocol3
{
    public byte SmallValue;
    public double LargeValue;
    public decimal DecimalValue;
}

[Protocol]
public struct TestRequest(long id, string name)
{
    public readonly long Id = id;
    public readonly string Name = name;
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
        var expectedTypeHash = MiscUtil.TypeHash<TestProtocol1>();

        // Act
        var result = _handler.PackData(testProtocol, out var data);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(data, Is.Not.Null);
        Assert.That(data.Length, Is.GreaterThan(16)); // 至少包含协议头和数据：2+8+4+2=16

        // 验证数据包结构
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);
        var guard1 = br.ReadUInt16();
        var typeHash = br.ReadUInt64();  // 读取类型哈希
        var length = br.ReadInt32();

        Assert.That(guard1, Is.EqualTo(0xCafe));
        Assert.That(typeHash, Is.EqualTo(expectedTypeHash));
        Assert.That(length, Is.GreaterThan(0));

        Console.WriteLine($"TestProtocol1 TypeHash: 0x{typeHash:X16}");
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
        var expectedTypeHash = MiscUtil.TypeHash<TestProtocol2>();

        // Act
        var result = _handler.PackData(testProtocol, out var data);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(data, Is.Not.Null);

        // 验证类型哈希
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);
        br.ReadUInt16(); // 跳过守卫值
        var typeHash = br.ReadUInt64(); // 读取类型哈希

        Assert.That(typeHash, Is.EqualTo(expectedTypeHash));
        Console.WriteLine($"TestProtocol2 TypeHash: 0x{typeHash:X16}");
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
        var expectedTypeHash = MiscUtil.TypeHash<TestProtocol3>();

        // Act
        var result = _handler.PackData(testProtocol, out var data);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(data, Is.Not.Null);

        // 验证类型哈希
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);
        br.ReadUInt16(); // 跳过守卫值
        var typeHash = br.ReadUInt64(); // 读取类型哈希

        Assert.That(typeHash, Is.EqualTo(expectedTypeHash));
        Console.WriteLine($"TestProtocol3 TypeHash: 0x{typeHash:X16}");
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
        var beforeGuard = br.ReadUInt16();
        br.ReadUInt64(); // typeHash (8 bytes)
        var length = br.ReadInt32();
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

        // 修改守卫值使其无效 - 修改前守卫值（前2字节）
        data[0] = 0x00;
        data[1] = 0x00;

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
        var incompleteData = new byte[] { 0xFE, 0xCA }; // 只有前守卫值，缺少类型哈希、长度和后守卫值

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
    public void TestHandleUnregisterProtocol()
    {
        // Arrange
        var testProtocol = new TestProtocol1 { Id = 1, Name = "Unregistered" };
        var packResult = _handler.PackData(testProtocol, out var data);

        _handler.RegisterHandler<TestProtocol1>(protocol => protocol.Id = 1);
        _handler.UnRegisterHandler<TestProtocol1>();

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
        // 测试ProtocolAttribute的正确使用 - 现在只检查是否存在属性
        var testProtocolType = typeof(TestProtocol1);
        var attribute = testProtocolType.GetCustomAttribute<ProtocolAttribute>();

        Assert.That(attribute, Is.Not.Null, "TestProtocol1 应该有 ProtocolAttribute");

        // 测试另一个协议
        var testProtocol2Type = typeof(TestProtocol2);
        var attribute2 = testProtocol2Type.GetCustomAttribute<ProtocolAttribute>();

        Assert.That(attribute2, Is.Not.Null, "TestProtocol2 应该有 ProtocolAttribute");

        // 测试更多协议类型
        var testProtocol3Type = typeof(TestProtocol3);
        var attribute3 = testProtocol3Type.GetCustomAttribute<ProtocolAttribute>();
        Assert.That(attribute3, Is.Not.Null, "TestProtocol3 应该有 ProtocolAttribute");

        var testRequestType = typeof(TestRequest);
        var attribute4 = testRequestType.GetCustomAttribute<ProtocolAttribute>();
        Assert.That(attribute4, Is.Not.Null, "TestRequest 应该有 ProtocolAttribute");

        // 测试重复协议类型 - 它们应该都有 ProtocolAttribute
        var duplicateProtocolType = typeof(DuplicateProtocol);
        var duplicateAttribute = duplicateProtocolType.GetCustomAttribute<ProtocolAttribute>();
        Assert.That(duplicateAttribute, Is.Not.Null, "DuplicateProtocol 应该有 ProtocolAttribute");
    }

    [Test]
    public void TestTypeHashUniqueness()
    {
        // 测试不同协议类型的哈希值应该是唯一的
        var hash1 = MiscUtil.TypeHash<TestProtocol1>();
        var hash2 = MiscUtil.TypeHash<TestProtocol2>();
        var hash3 = MiscUtil.TypeHash<TestProtocol3>();
        var hash4 = MiscUtil.TypeHash<TestRequest>();
        var hash5 = MiscUtil.TypeHash<DuplicateProtocol>();

        Console.WriteLine($"TestProtocol1 Hash: 0x{hash1:X16}");
        Console.WriteLine($"TestProtocol2 Hash: 0x{hash2:X16}");
        Console.WriteLine($"TestProtocol3 Hash: 0x{hash3:X16}");
        Console.WriteLine($"TestRequest Hash: 0x{hash4:X16}");
        Console.WriteLine($"DuplicateProtocol Hash: 0x{hash5:X16}");

        // 所有哈希值都不应为零
        Assert.That(hash1, Is.Not.EqualTo(0UL), "TestProtocol1 哈希值不应为零");
        Assert.That(hash2, Is.Not.EqualTo(0UL), "TestProtocol2 哈希值不应为零");
        Assert.That(hash3, Is.Not.EqualTo(0UL), "TestProtocol3 哈希值不应为零");
        Assert.That(hash4, Is.Not.EqualTo(0UL), "TestRequest 哈希值不应为零");
        Assert.That(hash5, Is.Not.EqualTo(0UL), "DuplicateProtocol 哈希值不应为零");

        // 所有哈希值应该互不相同
        var hashes = new[] { hash1, hash2, hash3, hash4, hash5 };
        var distinctHashes = hashes.Distinct().ToArray();
        Assert.That(distinctHashes.Length, Is.EqualTo(hashes.Length), "所有协议类型的哈希值应该互不相同");
    }

    [Test]
    public void TestTypeHashConsistency()
    {
        // 测试相同类型的哈希值应该一致
        var hash1 = MiscUtil.TypeHash<TestProtocol1>();
        var hash2 = MiscUtil.TypeHash<TestProtocol1>();
        var hash3 = MiscUtil.TypeHash<TestProtocol1>();

        Assert.That(hash2, Is.EqualTo(hash1), "相同类型的不同调用应返回相同哈希值");
        Assert.That(hash3, Is.EqualTo(hash1), "相同类型的不同调用应返回相同哈希值");
    }

    [Test]
    public void TestCustom()
    {
        var data = new TestRequest(1231231231, "123");
        _handler.RegisterHandler<TestRequest>(proto =>
        {
            Console.WriteLine(proto.Id);
            Console.WriteLine(proto.Name);
        });
        _handler.PackData(data, out var bytes);
        _handler.HandleData(bytes);
    }

    [Test]
    public void TestGetAllRegisteredProtocols()
    {
        // Arrange & Act
        var protocols = _handler.GetAllRegisteredProtocols();

        // Assert
        Assert.That(protocols, Is.Not.Null, "协议列表不应为null");

        // 验证已注册的协议类型
        var protocol1 = protocols.FirstOrDefault(p => p.TypeName.Contains("TestProtocol1"));
        var protocol2 = protocols.FirstOrDefault(p => p.TypeName.Contains("TestProtocol2"));
        var protocol3 = protocols.FirstOrDefault(p => p.TypeName.Contains("TestProtocol3"));

        Assert.That(protocol1.TypeName, Is.Not.Null, "应包含 TestProtocol1");
        Assert.That(protocol2.TypeName, Is.Not.Null, "应包含 TestProtocol2");
        Assert.That(protocol3.TypeName, Is.Not.Null, "应包含 TestProtocol3");

        // 验证哈希值不为零
        Assert.That(protocol1.Hash, Is.Not.EqualTo(0UL), "TestProtocol1 哈希值不应为零");
        Assert.That(protocol2.Hash, Is.Not.EqualTo(0UL), "TestProtocol2 哈希值不应为零");
        Assert.That(protocol3.Hash, Is.Not.EqualTo(0UL), "TestProtocol3 哈希值不应为零");

        // 验证程序集名称
        Assert.That(protocol1.AssemblyName, Does.Contain("Test"), "程序集名称应包含 Test");

        Console.WriteLine($"总协议数: {protocols.Count}");
        foreach (var protocol in protocols)
        {
            Console.WriteLine($"{protocol}");
        }
    }

    [Test]
    public void TestGetProtocolInfoWithRegisteredType()
    {
        // Arrange
        _handler.RegisterHandler<TestProtocol1>(p => { });

        // Act
        var info = _handler.GetProtocolInfo<TestProtocol1>();

        // Assert
        Assert.That(info.HasValue, Is.True, "已注册的协议应返回信息");

        var protocolInfo = info.Value;
        Assert.That(protocolInfo.TypeName, Does.Contain("TestProtocol1"));
        Assert.That(protocolInfo.HasHandler, Is.True, "已注册处理函数的协议 HasHandler 应为 true");
        Assert.That(protocolInfo.Hash, Is.Not.EqualTo(0UL));

        Console.WriteLine($"TestProtocol1 Info: {protocolInfo}");
    }

    [Test]
    public void TestGetProtocolInfoWithUnregisteredHandler()
    {
        // Arrange - 不注册处理函数

        // Act
        var info = _handler.GetProtocolInfo<TestProtocol1>();

        // Assert
        Assert.That(info.HasValue, Is.True, "已注册协议类型应返回信息");

        var protocolInfo = info.Value;
        Assert.That(protocolInfo.TypeName, Does.Contain("TestProtocol1"));
        Assert.That(protocolInfo.HasHandler, Is.False, "未注册处理函数的协议 HasHandler 应为 false");

        Console.WriteLine($"TestProtocol1 Info (无处理函数): {protocolInfo}");
    }

    [Test]
    public void TestGetProtocolInfoWithUnregisteredType()
    {
        // Arrange - 使用未注册的类型

        // Act
        var info = _handler.GetProtocolInfo<InvalidProtocol>();

        // Assert
        Assert.That(info.HasValue, Is.False, "未注册的协议类型应返回 null");
    }

    [Test]
    public void TestPrintDebugInfo()
    {
        // Arrange
        _handler.RegisterHandler<TestProtocol1>(p => { });
        _handler.RegisterHandler<TestProtocol2>(p => { });
        // TestProtocol3 不注册处理函数

        // Act & Assert - 确保不会抛出异常
        Assert.DoesNotThrow(() => _handler.PrintDebugInfo(), "PrintDebugInfo 不应抛出异常");

        Console.WriteLine("=== PrintDebugInfo 测试输出 ===");
        _handler.PrintDebugInfo();
        Console.WriteLine("=== 输出结束 ===");
    }

    [Test]
    public void TestDebugInfoAfterClear()
    {
        // Arrange - 先注册协议
        _handler.RegisterHandler<TestProtocol1>(p => { });
        var protocolsBefore = _handler.GetAllRegisteredProtocols();

        // Act - 清理所有注册信息
        _handler.Clear();

        // Assert
        var protocolsAfter = _handler.GetAllRegisteredProtocols();
        Assert.That(protocolsBefore.Count, Is.GreaterThan(0), "清理前应有协议");
        Assert.That(protocolsAfter.Count, Is.EqualTo(0), "清理后不应有任何协议");

        var info = _handler.GetProtocolInfo<TestProtocol1>();
        Assert.That(info.HasValue, Is.False, "清理后查询协议应返回 null");
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
        var isValidFormat = ProtocolHandlerTestHelper.ValidatePacketFormat(data, out var typeHash, out var dataLength, out var beforeGuard, out var afterGuard);
        Assert.That(isValidFormat, Is.True, "数据包格式应该有效");

        var expectedTypeHash = MiscUtil.TypeHash<TestProtocol1>();
        Assert.That(typeHash, Is.EqualTo(expectedTypeHash), $"类型哈希应该正确，期望:0x{expectedTypeHash:X16}，实际:0x{typeHash:X16}");
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

        // 修改前守卫值
        if (data.Length >= 2)
        {
            data[0] = 0x00;
            data[1] = 0x00;
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