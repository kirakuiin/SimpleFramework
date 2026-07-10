using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SimpleFramework.Utility;

namespace Test.Utility;

[TestFixture]
public class TestSerializeUtil
{
    [Test]
    public void TestStrSerialize()
    {
        var msg = new Message{Value = 1, Name = "Test", IsTrue = false};
        
        var result = SerializeUtil.Serialize(msg);
        var finalRes = SerializeUtil.Deserialize<Message>(result);
        
        Assert.AreEqual(msg.Value, finalRes.Value);
        Assert.AreEqual(msg.Name, finalRes.Name);
        Assert.IsFalse(finalRes.IsTrue);
    }
    
    [Test]
    public void TestBinarySerialize()
    {
        var msg = new Message{Value = 1, Name = "Test", IsTrue = true};
        var result = SerializeUtil.SerializeBytes(msg);
        
        var finalRes = SerializeUtil.Deserialize<Message>(result);
        Assert.AreEqual(msg.Value, finalRes.Value);
        Assert.AreEqual(msg.Name, finalRes.Name);
        Assert.IsTrue(finalRes.IsTrue);
    }
    
    [Test]
    public void TestRuntimeSerialize()
    {
        var msg = new Message{Value = 1, Name = "Test", IsTrue = true};
        var result = SerializeUtil.SerializeBytes(msg);
        
        var finalRes = (Message)SerializeUtil.Deserialize(result, typeof(Message))!;
        Assert.AreEqual(msg.Value, finalRes.Value);
        Assert.AreEqual(msg.Name, finalRes.Name);
        Assert.IsTrue(finalRes.IsTrue);
    }
    
    [Test]
    public void TestPureStrSerialize()
    {
        const string msg = "hello world";
        var result = SerializeUtil.SerializeBytes(msg);
        
        var finalRes = SerializeUtil.Deserialize<string>(result);
        Assert.AreEqual(msg, finalRes);
    }
    
    [Test]
    public void TestDictAndList()
    {
        Dictionary<int, string> dict = new() { { 1, "hello" }, { 2, "world" } };
        List<int> list = [1, 2, 3];
        var msg = new SpecialMsg(dict, list);
        var result = SerializeUtil.SerializeBytes(msg);
        
        var finalRes = SerializeUtil.Deserialize<SpecialMsg>(result);
        
        Assert.AreEqual(msg.Dict[1], finalRes!.Dict[1]);
        Assert.AreEqual(msg.List.Count, finalRes.List.Count);
    }

    [Test]
    public void TestDeserializeDeclaresNullableResult()
    {
        var context = new NullabilityInfoContext();
        var methods = typeof(SerializeUtil).GetMethods()
            .Where(method => method.Name == nameof(SerializeUtil.Deserialize))
            .ToArray();

        Assert.AreEqual(4, methods.Length);
        Assert.That(methods.Select(method => context.Create(method.ReturnParameter).ReadState),
            Is.All.EqualTo(NullabilityState.Nullable));
        Assert.IsNull(SerializeUtil.Deserialize<string>("null"));
        Assert.IsNull(SerializeUtil.Deserialize("null", typeof(string)));
    }

    [Test]
    public void TestSerializeBytesAvoidsIntermediateStringAllocation()
    {
        var payload = new string('a', 16_384);
        _ = SerializeUtil.Serialize(payload);
        _ = SerializeUtil.SerializeBytes(payload);

        var stringAllocation = MeasureAllocation(() => SerializeUtil.Serialize(payload).Length);
        var byteAllocation = MeasureAllocation(() => SerializeUtil.SerializeBytes(payload).Length);

        Assert.That(byteAllocation, Is.LessThan(stringAllocation),
            $"UTF-8 序列化不应先分配 JSON 字符串；字符串={stringAllocation}，字节={byteAllocation}。");
    }

    private static long MeasureAllocation(Func<int> operation)
    {
        const int iterations = 20;
        var checksum = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
        {
            checksum += operation();
        }

        GC.KeepAlive(checksum);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}

public struct Message
{
    public int Value { init; get; }
    public string Name;
    public bool IsTrue;
}

public class SpecialMsg(Dictionary<int, string> dict, List<int> list)
{
    public Dictionary<int, string> Dict => dict;
    
    public List<int> List = list;
}
