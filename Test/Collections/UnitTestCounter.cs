using System.Collections.Generic;
using NUnit.Framework;
using SimpleFramework.Collections;

namespace Test.Collections;

[TestFixture]
public class TestCounter
{
    private Counter<string> _counter;
    
    [SetUp]
    public void Setup()
    {
        var arr = new List<string>() { "one", "two", "two", "three", "three" };
        _counter = new Counter<string>(arr);
    }

    [Test]
    public void TestConstruct()
    {
        Assert.AreEqual(1, _counter["one"]);
        Assert.AreEqual(2, _counter["two"]);
        Assert.AreEqual(2, _counter["three"]);
    }
    
    [Test]
    public void TestCopy()
    {
        var counter = new Counter<string>(_counter);
        
        Assert.AreEqual(counter, _counter);
    }

    [Test]
    public void TestCount()
    {
        Assert.AreEqual(3, _counter.Count);
    }
    
    [Test]
    public void TestAdd()
    {
        _counter.Add("four", 4);
        
        Assert.AreEqual(4, _counter["four"]);
    }
    
    [Test]
    public void TestRemove()
    {
        _counter.Remove("two");
        
        Assert.AreEqual(0, _counter["two"]);
    }
    
    [Test]
    public void TestIndex()
    {
        _counter["one"] -= 1;
        _counter["three"] += 1;
        
        Assert.AreEqual(0, _counter["one"]);
        Assert.AreEqual(3, _counter["three"]);
    }
    
    [Test]
    public void TestEnumerator()
    {
        foreach (var pair in _counter)
        {
            Assert.AreEqual(pair.Value, _counter[pair.Key]);
        }
    }
    
    [Test]
    public void TestNotExists()
    {
        Assert.AreEqual(0, _counter["four"]);
    }
    
    [Test]
    public void TestMostCommon()
    {
        _counter["three"] += 1;
        var target = 3;
        foreach (var pair in _counter.MostCommon())
        {
            Assert.AreEqual(pair.Value, target--);
        }
    }
    
    [Test]
    public void TestClear()
    {
        _counter.Clear();
        
        Assert.AreEqual(0, _counter.Count);
    }
}