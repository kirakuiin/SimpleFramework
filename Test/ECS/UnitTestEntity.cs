using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SimpleFramework.ECS;

namespace Test.ECS;

[TestFixture]
public class UnitTestEntity
{
    private World _world;
    private Entity _entity;

    [SetUp]
    public void Setup()
    {
        _world = new World();
        _entity = _world.CreateEntity();
    }

    [Test]
    public void TestEntityCreation()
    {
        Assert.IsNotNull(_entity);
        Assert.AreEqual(_world, _entity.World);
        Assert.AreEqual(1, _world.EntityCount);
    }

    [Test]
    public void TestAddComponent()
    {
        var intComp = new TestIntComponent { Value = 42 };
        var signature = new TypeSignature(typeof(TestIntComponent));
        _entity.Add(intComp);
        Assert.IsTrue(_entity.Has<TestIntComponent>());
        Assert.AreEqual(intComp, _entity.Get<TestIntComponent>());
        Assert.AreEqual(1, _world.GetArchetype(signature).EntityCount);
    }

    [Test]
    public void TestAddMultipleComponents()
    {
        var intComp = new TestIntComponent { Value = 42 };
        var stringComp = new TestStringComponent { Value = "test" };
        var doubleComp = new TestDoubleComponent { Value = 3.14 };
        var signature1 = new TypeSignature();
        var signature2 = new TypeSignature(typeof(TestIntComponent), typeof(TestStringComponent), typeof(TestDoubleComponent));

        _entity.Add(intComp);
        _entity.Add(stringComp);
        _entity.Add(doubleComp);

        Assert.IsTrue(_entity.Has<TestIntComponent>());
        Assert.IsTrue(_entity.Has<TestStringComponent>());
        Assert.IsTrue(_entity.Has<TestDoubleComponent>());
        Assert.AreEqual(intComp, _entity.Get<TestIntComponent>());
        Assert.AreEqual(stringComp, _entity.Get<TestStringComponent>());
        Assert.AreEqual(doubleComp, _entity.Get<TestDoubleComponent>());
        Assert.AreEqual(0, _world.GetArchetype(signature1).EntityCount);
        Assert.AreEqual(1, new Archetype(_world, signature2).EntityCount);
    }

    [Test]
    public void TestRemoveComponent()
    {
        var intComp = new TestIntComponent { Value = 42 };
        _entity.Add(intComp);
        Assert.IsTrue(_entity.Has<TestIntComponent>());

        _entity.Remove<TestIntComponent>();
        Assert.IsFalse(_entity.Has<TestIntComponent>());
    }

    [Test]
    public void TestGetNonExistentComponent()
    {
        Assert.Throws<KeyNotFoundException>(() => _entity.Get<TestIntComponent>());
    }

    [Test]
    public void TestAddTag()
    {
        _entity.AddTag("test");
        _entity.AddTag("help");
        _entity.AddTag("world");
        
        Assert.IsTrue(_entity.HasTags("test", "world"));
        Assert.IsFalse(_entity.HasTags("test", "word"));
    }

    [Test]
    public void TestRemoveTag()
    {
        _entity.AddTag("test");
        _entity.AddTag("world");
        Assert.IsTrue(_entity.HasTags("test"));

        _entity.RemoveTag("test");
        Assert.IsFalse(_entity.HasTags("test"));
        Assert.IsTrue(_entity.HasTags("world"));
    }

    [Test]
    public void TestToString()
    {
        var intComp = new TestIntComponent { Value = 42 };
        var stringComp = new TestStringComponent { Value = "test" };
        _entity.Add(intComp);
        _entity.Add(stringComp);
        _entity.AddTag("test");

        var str = _entity.ToString();
        Assert.IsTrue(str.Contains("TestIntComponent"));
        Assert.IsTrue(str.Contains("TestStringComponent"));
        Assert.IsTrue(str.Contains("test"));
    }

    [Test]
    public void TestEquals()
    {
        var entity2 = _world.CreateEntity();
        Assert.IsFalse(_entity.Equals(entity2));
    }

    [Test]
    public void TestGetHashCode()
    {
        var hash1 = _entity.GetHashCode();
        var hash2 = _entity.GetHashCode();
        Assert.AreEqual(hash1, hash2);
    }

    [Test]
    public void TestTryGet()
    {
        var intComp = new TestIntComponent { Value = 42 };
        _entity.Add(intComp);
        
        Assert.IsTrue(_entity.TryGet<TestIntComponent>(out var comp));
        Assert.AreEqual(intComp, comp);
        
        Assert.IsFalse(_entity.TryGet<TestStringComponent>(out var _));
    }

    [Test]
    public void TestGetByType()
    {
        var intComp = new TestIntComponent { Value = 42 };
        _entity.Add(intComp);
        
        var comp = _entity.Get(typeof(TestIntComponent));
        Assert.AreEqual(intComp, comp);
        
        Assert.Throws<KeyNotFoundException>(() => _entity.Get(typeof(TestStringComponent)));
    }

    [Test]
    public void TestComponentEnumeration()
    {
        var intComp = new TestIntComponent { Value = 42 };
        var stringComp = new TestStringComponent { Value = "test" };
        var doubleComp = new TestDoubleComponent { Value = 3.14 };
        
        _entity.Add(intComp);
        _entity.Add(stringComp);
        _entity.Add(doubleComp);
        
        var components = _entity.ToList();
        Assert.AreEqual(3, components.Count);
        Assert.Contains(intComp, components);
        Assert.Contains(stringComp, components);
        Assert.Contains(doubleComp, components);
    }

    [Test]
    public void TestComponentEnumerationAfterRemoval()
    {
        var intComp = new TestIntComponent { Value = 42 };
        var stringComp = new TestStringComponent { Value = "test" };
        
        _entity.Add(intComp);
        _entity.Add(stringComp);
        
        var components = _entity.ToList();
        Assert.AreEqual(2, components.Count);
        
        _entity.Remove<TestIntComponent>();
        components = _entity.ToList();
        Assert.AreEqual(1, components.Count);
        Assert.Contains(stringComp, components);
    }
} 