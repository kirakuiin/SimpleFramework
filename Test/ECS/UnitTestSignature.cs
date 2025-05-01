using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimpleFramework.ECS;

namespace Test.ECS;

[TestFixture]
public class TestTypeSignature
{
    [Test]
    public void TestSignatureCreate()
    {
        var sig = new TypeSignature(typeof(int), typeof(string));
        
        Assert.AreEqual(2, sig.Count);
    }
    
    [Test]
    public void TestEqual()
    {
        var sig1 = new TypeSignature(typeof(int), typeof(List<int>));
        var sig2 = new TypeSignature(typeof(int), typeof(List<int>));
        var sig3 = new TypeSignature(typeof(int), typeof(List<string>));
        
        Assert.IsTrue(sig1.Equals(sig2));
        Assert.IsFalse(sig1.Equals(sig3));
    }
    
    [Test]
    public void TestAdd()
    {
        var sig = new TypeSignature();
        
        sig.Add(typeof(int)).Add(typeof(string)).Add(typeof(List<int>));
        sig.Add<float>().Add<int>();
        
        Assert.AreEqual(5, sig.Count);
    }
    
    [Test]
    public void TestRemove()
    {
        var sig = new TypeSignature(typeof(int), typeof(List<string>), typeof(string));

        sig.Remove(typeof(int)).Remove(typeof(string));
        sig.Remove<List<string>>();
        
        Assert.AreEqual(0, sig.Count);
    }
    
    [Test]
    public void TestCopy()
    {
        var sig1 = new TypeSignature(typeof(int), typeof(List<int>));
        var sig2 = new TypeSignature(typeof(int));
        
        sig2.Copy(sig1);
        
        Assert.AreEqual(2, sig2.Count);
    }

    [Test]
    public void TestHasAny()
    {
        var sig1 = new TypeSignature(typeof(int), typeof(List<int>)); 
        var sig2 = new TypeSignature(typeof(string), typeof(List<int>)); 
        var sig3 = new TypeSignature(typeof(string), typeof(List<string>)); 
        
        Assert.IsTrue(sig1.HasAny(sig2));
        Assert.IsFalse(sig1.HasAny(sig3));
    }
    
    [Test]
    public void TestHasAll()
    {
        var sig1 = new TypeSignature(typeof(int), typeof(List<int>)); 
        var sig2 = new TypeSignature(typeof(string), typeof(List<int>)); 
        var sig3 = new TypeSignature(typeof(int)); 
        
        Assert.IsFalse(sig1.HasAll(sig2));
        Assert.IsTrue(sig1.HasAll(sig3));
    }
    
    [Test]
    public void TestEnumerate()
    {
        var types = new List<Type>(){typeof(int), typeof(float), typeof(string)};
        var sig1 = new TypeSignature(types);

        foreach (var type in sig1)
        {
            Assert.Contains(type, types);
        }
    }
    
    [Test]
    public void TestToString()
    {
        var sig1 = new TypeSignature(typeof(int), typeof(float), typeof(string));

        Assert.AreEqual("TypeSignature [Int32, Single, String, ]", sig1.ToString());
    }
    
    [Test]
    public void TestHash()
    {
        var sig1 = new TypeSignature(typeof(int), typeof(float), typeof(string));
        var sig2 = new TypeSignature(typeof(int), typeof(float), typeof(string));
        
        var set = new HashSet<TypeSignature>() {sig1, sig2};
        
        Assert.IsTrue(set.Contains(sig1));
        Assert.AreEqual(1, set.Count);
    }
    
    [Test]
    public void TestOrder()
    {
        var sig1 = new TypeSignature(typeof(int), typeof(float), typeof(string));
        var sig2 = new TypeSignature(typeof(int), typeof(string), typeof(float));
        
        Assert.AreNotEqual(sig1, sig2);
    }
    
    [Test]
    public void TestClear()
    {
        var sig1 = new TypeSignature(typeof(int), typeof(float), typeof(string));
        
        sig1.Clear();
        
        Assert.AreEqual(0, sig1.Count);
    }
}