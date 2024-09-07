using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimpleFramework.Extensions;

namespace Test.Extensions;

[TestFixture]
public class TestString
{
    [Test]
    public void TestRepeat()
    {
        var str = "mi";
        Assert.AreEqual("mimi", str.Repeat(2));
    }
}

[TestFixture]
public class TestEnumerable
{
    [Test]
    public void TestApply()
    {
        var arr = new List<int>() {1, 2, 3};
        var result = new List<int>();
        
        arr.Apply(v => result.Add(v));
        
        Assert.AreEqual(result, arr);
    }
}

[TestFixture]
public class TestList
{
    [Test]
    public void TestSwap()
    {
        var arr = new List<int>() {1, 2};
        
        arr.Swap(0, 1);
        
        Assert.AreEqual(2, arr[0]);
        Assert.AreEqual(1, arr[1]);
    }
}

[TestFixture]
public class TestRandom
{
    [Test]
    public void TestChoice()
    {
        var random = new Random();
        var arr = new List<int> {1, 2, 3};
        var result = new HashSet<int>();
        
        var tryTime = 0;
        while (result.Count < arr.Count)
        {
            result.Add(random.Choice(arr));
            ++tryTime;
        }
        
        Assert.GreaterOrEqual(tryTime, arr.Count);
    }
    
    [Test]
    public void TestShuffle()
    {
        var random = new Random();
        var arr = new List<int> {1, 2, 3, 4, 5, 6};
        var result = new List<int> {1, 2, 3, 4, 5, 6};
        
        random.Shuffle(arr);
        
        Assert.AreNotEqual(result, arr);
    }
    
    [Test]
    public void TestSample()
    {
        var random = new Random();
        var arr = new List<int> {1, 2, 3, 4, 5, 6};
        var result = new List<int>();
        
        result.AddRange(random.Sample(arr, 3));
        
        Assert.AreNotEqual(result[0], result[1]);
        Assert.AreNotEqual(result[1], result[2]);
    }
}
