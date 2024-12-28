using System.Linq;
using NUnit.Framework;
using SimpleFramework;

namespace Test.Framework;

[TestFixture]
public class TestEc
{
    private Entity _entity;
    
    [SetUp]
    public void Setup()
    {
        _entity = new Entity();
    }

    [Test]
    public void TestHas()
    {
        _entity.AddComponent(new CompB());
        
        Assert.IsFalse(_entity.HasComponent<CompA>());
        Assert.IsTrue(_entity.HasComponent<CompB>());
    }
    
    [Test]
    public void TestRemove()
    {
        _entity.AddComponent(new CompA());
        var b = new CompB();
        _entity.AddComponent(b);
        
        _entity.RemoveComponent(b);
        _entity.RemoveComponent(new CompC());
        
        Assert.IsTrue(_entity.HasComponent<CompA>());
        Assert.IsFalse(_entity.HasComponent<CompB>());
    }
    
    [Test]
    public void TestRemoveList()
    {
        _entity.AddComponent(new CompA());
        _entity.AddComponent(new CompA());
        _entity.AddComponent(new CompA());
        
        _entity.RemoveAllComponents<CompA>();
        _entity.RemoveAllComponents<CompB>();
        
        Assert.AreEqual(0, _entity.GetComponents<CompA>().ToList().Count);
        Assert.AreEqual(0, _entity.GetComponents<CompB>().ToList().Count);
    }
    
    [Test]
    public void TestGet()
    {
        _entity.AddComponent(new CompA());
        _entity.AddComponent(new CompB());
        
        Assert.IsTrue(_entity.GetComponent<CompA>().Name == "CompA");
        Assert.IsTrue(_entity.GetComponent<CompB>().Domain == "CompB");
        Assert.IsNull(_entity.GetComponent<CompC>());
    }
    
    [Test]
    public void TestGetList()
    {
        _entity.AddComponent(new CompA());
        _entity.AddComponent(new CompA());
        _entity.AddComponent(new CompA());
        
        Assert.AreEqual(3, _entity.GetComponents<CompA>().ToList().Count);
    }
    
    [Test]
    public void TestClear()
    {
        _entity.AddComponent(new CompA());
        _entity.AddComponent(new CompA());
        _entity.AddComponent(new CompA());
        _entity.AddComponent(new CompC());
        
        Assert.AreEqual(3, _entity.GetComponents<CompA>().ToList().Count);
        Assert.AreEqual(1, _entity.GetComponents<CompC>().ToList().Count);
        
        _entity.Clear();
        
        Assert.AreEqual(0, _entity.GetComponents<CompA>().ToList().Count);
        Assert.AreEqual(0, _entity.GetComponents<CompC>().ToList().Count);
    }
}


public class CompA : IComponent
{
    public string Name => "CompA";
}


public class CompB : IComponent
{
    public string Domain => "CompB";
}


public class CompC : IComponent
{
}