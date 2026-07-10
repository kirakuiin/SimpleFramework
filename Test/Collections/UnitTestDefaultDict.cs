using NUnit.Framework;
using System;
using System.Collections.Generic;
using SimpleFramework.Collections;

namespace Test.Collections;

[TestFixture]
public class TestDefaultDict
{
    private DefaultDict<string, int> _dict = default!;

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
    public void TestTryGetValueDoesNotInitializeMissingKey()
    {
        Assert.IsFalse(_dict.TryGetValue("missing", out var value));
        Assert.AreEqual(default(int), value);
        Assert.IsFalse(_dict.ContainsKey("missing"));
        Assert.AreEqual(0, _dict.Count);
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

    [Test]
    public void TestUsesSuppliedComparer()
    {
        var factoryCalls = 0;
        var dict = new DefaultDict<string, int>(() => ++factoryCalls, StringComparer.OrdinalIgnoreCase);

        Assert.AreEqual(1, dict["Key"]);
        Assert.AreEqual(1, dict["KEY"]);
        Assert.AreEqual(1, dict.Count);
        Assert.AreEqual(1, factoryCalls);
        Assert.AreSame(StringComparer.OrdinalIgnoreCase, dict.Comparer);
    }

    [Test]
    public void TestConstructorRejectsNullFactory()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => new DefaultDict<string, int>(null!));

        Assert.AreEqual("initCallback", exception!.ParamName);
    }

    [Test]
    public void TestPairContainsUsesConfiguredComparer()
    {
        var dict = new DefaultDict<string, int>(() => 0, StringComparer.OrdinalIgnoreCase)
        {
            ["Key"] = 7
        };
        ICollection<KeyValuePair<string, int>> pairs = dict;

        Assert.IsTrue(pairs.Contains(new KeyValuePair<string, int>("KEY", 7)));
        Assert.IsFalse(pairs.Contains(new KeyValuePair<string, int>("KEY", 8)));
    }
}
