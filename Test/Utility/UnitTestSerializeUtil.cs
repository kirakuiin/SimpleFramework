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
}

public struct Message
{
    public int Value { init; get; }
    public string Name;
    public bool IsTrue;
}