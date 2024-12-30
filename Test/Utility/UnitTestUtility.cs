using NUnit.Framework;
using SimpleFramework.Utility;

namespace Test.Utility;

[TestFixture]
public class TestSerializeTool
{
    [Test]
    public void TestStrSerialize()
    {
        var msg = new Message{Value = 1, Name = "Test", IsTrue = false};
        
        var result = SerializeTool.Serialize(msg);
        var finalRes = SerializeTool.Deserialize<Message>(result);
        
        Assert.AreEqual(msg.Value, finalRes.Value);
        Assert.AreEqual(msg.Name, finalRes.Name);
        Assert.IsFalse(finalRes.IsTrue);
    }
    
    [Test]
    public void TestBinarySerialize()
    {
        var msg = new Message{Value = 1, Name = "Test", IsTrue = true};
        var result = SerializeTool.SerializeBytes(msg);
        
        var finalRes = SerializeTool.Deserialize<Message>(result);
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