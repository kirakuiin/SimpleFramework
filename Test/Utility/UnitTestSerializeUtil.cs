using System;
using System.Collections.Generic;
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
        
        var finalRes = (Message)SerializeUtil.Deserialize(result, typeof(Message));
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
        
        Assert.AreEqual(msg.Dict[1], finalRes.Dict[1]);
        Assert.AreEqual(msg.List.Count, finalRes.List.Count);
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