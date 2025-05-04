using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SimpleFramework.ECS;

namespace Test.ECS;

[TestFixture]
public class TestArchetype
{
    private World _world;
    private Archetype _archetype;
    private TypeSignature _signature;

    [SetUp]
    public void Setup()
    {
        _world = new World();
        _signature = new TypeSignature(typeof(TestIntComponent),
            typeof(TestStringComponent));
        _archetype = new Archetype(_world, _signature);
    }

    [Test]
    public void TestArchetypeCreation()
    {
        Assert.IsNotNull(_archetype);
        Assert.AreEqual(_world, _archetype.World);
        Assert.AreEqual(_signature, _archetype.TypeSignature);
        Assert.AreEqual(0, _archetype.EntityCount);
    }

    [Test]
    public void TestCreateEntity()
    {
        var entity = _archetype.CreateEntity();
        Assert.IsNotNull(entity);
        Assert.AreEqual(_archetype, entity.Archetype);
        Assert.AreEqual(1, _archetype.EntityCount);
    }

    [Test]
    public void TestAddEntity()
    {
        var entity = _world.CreateEntity<TestStringComponent, TestIntComponent>();
        Assert.AreEqual(1, _archetype.EntityCount);
        Assert.Contains(entity, _archetype.ToList());
    }

    [Test]
    public void TestRemoveEntity()
    {
        var entity = _archetype.CreateEntity();
        Assert.AreEqual(1, _archetype.EntityCount);
        
        _world.RemoveEntity(entity);
        Assert.AreEqual(0, _archetype.EntityCount);
        Assert.IsFalse(_archetype.Contains(entity));
    }

    [Test]
    public void TestHasComponent()
    {
        Assert.IsTrue(_archetype.Has<TestIntComponent>());
        Assert.IsTrue(_archetype.Has<TestStringComponent>());
        Assert.IsFalse(_archetype.Has<TestDoubleComponent>());
    }

    [Test]
    public void TestHasComponentWithType()
    {
        Assert.IsTrue(_archetype.Has(typeof(TestIntComponent)));
        Assert.IsTrue(_archetype.Has(typeof(TestStringComponent)));
        Assert.IsFalse(_archetype.Has(typeof(TestDoubleComponent)));
    }

    [Test]
    public void TestHasAll()
    {
        var matchingSignature = new TypeSignature(typeof(TestStringComponent), typeof(TestIntComponent));
        var matchingSignature1 = new TypeSignature(typeof(TestStringComponent));
        var nonMatchingSignature = new TypeSignature(typeof(List<int>), typeof(TestIntComponent));
        
        Assert.IsTrue(_archetype.HasAll(matchingSignature));
        Assert.IsTrue(_archetype.HasAll(matchingSignature1));
        Assert.IsFalse(_archetype.HasAll(nonMatchingSignature));
    }
    
    [Test]
    public void TestHasAny()
    {
        var matchingSignature = new TypeSignature(typeof(TestStringComponent), typeof(TestIntComponent));
        var matchingSignature1 = new TypeSignature(typeof(TestStringComponent), typeof(string));
        var nonMatchingSignature = new TypeSignature(typeof(List<int>));
        
        Assert.IsTrue(_archetype.HasAny(matchingSignature));
        Assert.IsTrue(_archetype.HasAny(matchingSignature1));
        Assert.IsFalse(_archetype.HasAny(nonMatchingSignature));
    }

    [Test]
    public void TestToString()
    {
        var str = _archetype.ToString();
        Assert.IsTrue(str.StartsWith("Archetype ["));
        Assert.IsTrue(str.Contains("TestIntComponent"));
        Assert.IsTrue(str.Contains("TestStringComponent"));
    }

    [Test]
    public void TestGetEnumerator()
    {
        var entity1 = _archetype.CreateEntity();
        var entity2 = _archetype.CreateEntity();
        
        int count = 0;
        foreach (var entity in _archetype)
        {
            count++;
            Assert.IsNotNull(entity);
        }
        Assert.AreEqual(2, count);
        Assert.AreEqual(2, _archetype.EntityCount);
    }

    [Test]
    public void TestEquals()
    {
        var sameArchetype = new Archetype(_world, _signature);
        var differentWorld = new World();
        var differentArchetype = new Archetype(differentWorld, _signature);
        var differentSignature = new TypeSignature(typeof(double));
        var differentSignatureArchetype = new Archetype(_world, differentSignature);
        
        Assert.IsTrue(_archetype.Equals(_archetype));
        Assert.IsTrue(_archetype.Equals(sameArchetype));
        Assert.IsFalse(_archetype.Equals(differentArchetype));
        Assert.IsFalse(_archetype.Equals(differentSignatureArchetype));
        Assert.IsFalse(_archetype.Equals(null));
    }
} 