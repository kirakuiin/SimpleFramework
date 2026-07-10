using System.Collections.Generic;
using NUnit.Framework;
using SimpleFramework.Collections;

namespace Test.Collections;

[TestFixture]
public class TestCounter
{
    private Counter<string> _counter = default!;
    
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
    public void TestObjectEqualsUsesCounterValues()
    {
        object counter = new Counter<string>(_counter);

        Assert.IsTrue(counter.Equals(_counter));
    }

    [Test]
    public void TestHashCodeUsesCounterValues()
    {
        var counter = new Counter<string>(_counter);
        var set = new HashSet<Counter<string>> { counter };

        Assert.IsTrue(set.Contains(_counter));
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
    
    [Test]
    public void TestNull()
    {
        Assert.AreNotEqual(null, _counter);
    }

    [Test]
    public void TestEmptyCountersAreEqualButNotStrictlyOrdered()
    {
        var left = new Counter<string>();
        var right = new Counter<string>();

        Assert.IsFalse(left > right);
        Assert.IsFalse(left < right);
        Assert.IsTrue(left >= right);
        Assert.IsTrue(left <= right);
    }

    [Test]
    public void TestEqualCountersAreNotStrictlyOrdered()
    {
        var left = new Counter<string> { ["a"] = 1 };
        var right = new Counter<string> { ["a"] = 1 };

        Assert.IsFalse(left > right);
        Assert.IsFalse(left < right);
        Assert.IsTrue(left >= right);
        Assert.IsTrue(left <= right);
    }

    [Test]
    public void TestDisjointCountersAreNotComparable()
    {
        var left = new Counter<string> { ["a"] = 1 };
        var right = new Counter<string> { ["b"] = 1 };

        Assert.IsFalse(left > right);
        Assert.IsFalse(left < right);
        Assert.IsFalse(left >= right);
        Assert.IsFalse(left <= right);
    }

    [Test]
    public void TestSubtractIncludesRightOnlyKeys()
    {
        var left = new Counter<string> { ["left"] = 3 };
        var right = new Counter<string> { ["right"] = 2 };

        var result = left - right;

        Assert.AreEqual(3, result["left"]);
        Assert.AreEqual(-2, result["right"]);
    }

    [Test]
    public void TestCopyAndOperatorsPreserveComparer()
    {
        var left = new Counter<string>(StringComparer.OrdinalIgnoreCase);
        left["Alpha"] = 2;
        var copy = new Counter<string>(left);
        copy["ALPHA"]++;

        var right = new Counter<string>(StringComparer.OrdinalIgnoreCase);
        right["Beta"] = 4;
        var sum = left + right;
        var difference = left - right;
        sum["ALPHA"]++;
        difference["BETA"]--;

        Assert.Multiple(() =>
        {
            Assert.AreEqual(1, copy.Count);
            Assert.AreEqual(3, copy["alpha"]);
            Assert.AreSame(StringComparer.OrdinalIgnoreCase, copy.Comparer);
            Assert.AreEqual(2, sum.Count);
            Assert.AreEqual(3, sum["alpha"]);
            Assert.AreEqual(2, difference.Count);
            Assert.AreEqual(-5, difference["beta"]);
        });
    }
}
