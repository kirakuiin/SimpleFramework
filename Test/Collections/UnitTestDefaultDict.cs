using NUnit.Framework;
using SimpleFramework.Collections;

namespace Test.Collections;

[TestFixture]
public class TestDefaultDict
{
    private DefaultDict<string, int> _dict;

    private const int InitialVal = 10;
    
    [SetUp]
    public void Setup()
    {
        _dict = new DefaultDict<string, int>(() => InitialVal);
    }

    [Test]
    public void TestConstruct()
    {
        Assert.AreEqual(InitialVal, _dict["hello"]);
    }
    
    [Test]
    public void TestCount()
    {
        Assert.AreEqual(0, _dict.Count);
        _dict["hello"] = _dict["nico"];
        Assert.AreEqual(2, _dict.Count);
    }
    
    [Test]
    public void TestContains()
    {
        _dict["hello"] = 1;
        
        Assert.IsTrue(_dict.ContainsKey("hello"));
    }
    
    [Test]
    public void TestRemove()
    {
        var a = _dict["hello"];
        _dict.Remove("hello");
        
        Assert.IsFalse(_dict.ContainsKey("hello"));
    }
    
    
    [Test]
    public void TestEnumerator()
    {
        var (a, b, c) = (_dict["a"], _dict["b"], _dict["c"]);
        foreach (var pair in _dict)
        {
            Assert.AreEqual(pair.Value, _dict[pair.Key]);
        }
    }
    
    [Test]
    public void TestClear()
    {
        var a = _dict["what the fuck"];
        Assert.AreEqual(1, _dict.Count);
        
        _dict.Clear();
        Assert.AreEqual(0, _dict.Count);
    }
}