using System.Linq;
using NUnit.Framework;
using SimpleFramework.ECS;

namespace Test.ECS;

[TestFixture]
public class UnitTestWorld
{
    private World _world;

    [SetUp]
    public void Setup()
    {
        _world = new World("TestWorld");
    }

    [Test]
    public void TestWorldCreation()
    {
        Assert.IsNotNull(_world);
        Assert.AreEqual("TestWorld", _world.Name);
        Assert.AreEqual(0, _world.EntityCount);
    }

    [Test]
    public void TestCreateEmptyEntity()
    {
        var entity = _world.CreateEntity();
        Assert.IsNotNull(entity);
        Assert.AreEqual(_world, entity.World);
        Assert.AreEqual(1, _world.EntityCount);
    }

    [Test]
    public void TestCreateEntityWithComponents()
    {
        var entity = _world.CreateEntity(typeof(TestIntComponent), typeof(TestStringComponent));
        Assert.IsNotNull(entity);
        Assert.IsTrue(entity.Has<TestIntComponent>());
        Assert.IsTrue(entity.Has<TestStringComponent>());
        Assert.AreEqual(1, _world.EntityCount);
    }

    [Test]
    public void TestCreateQuery()
    {
        var query = _world.CreateQuery();
        Assert.IsNotNull(query);
        Assert.AreEqual(_world, query.World);
    }

    [Test]
    public void TestRemoveEntity()
    {
        var entity = _world.CreateEntity();
        Assert.AreEqual(1, _world.EntityCount);
        
        _world.RemoveEntity(entity);
        Assert.AreEqual(0, _world.EntityCount);
    }

    [Test]
    public void TestRemoveEntityId()
    {
        var entity = _world.CreateEntity<TestIntComponent>();
        Assert.AreEqual(1, _world.EntityCount);
        
        _world.RemoveEntity(entity.Id);
        Assert.AreEqual(0, _world.EntityCount);
    }
    
    [Test]
    public void TestGetEntityById()
    {
        var entity = _world.CreateEntity<TestIntComponent>();
        
        var entity1 = _world.GetEntity(entity.Id);
        
        Assert.AreEqual(entity, entity1);
    }

    [Test]
    public void TestGetEntities()
    {
        var entity1 = _world.CreateEntity();
        var entity2 = _world.CreateEntity(typeof(TestIntComponent));
        
        var entities = _world.GetEntities().ToList();
        Assert.AreEqual(2, entities.Count);
        Assert.Contains(entity1, entities);
        Assert.Contains(entity2, entities);
    }

    [Test]
    public void TestDestroy()
    {
        _world.CreateEntity();
        _world.CreateEntity(typeof(TestIntComponent));
        Assert.AreEqual(2, _world.EntityCount);
        
        _world.Destroy();
        Assert.AreEqual(0, _world.EntityCount);
    }

    [Test]
    public void TestToString()
    {
        var str = _world.ToString();
        Assert.IsTrue(str.Contains("TestWorld"));
        Assert.IsTrue(str.Contains("0 entities"));
        
        _world.CreateEntity();
        str = _world.ToString();
        Assert.IsTrue(str.Contains("1 entities"));
    }

    [Test]
    public void TestEquals()
    {
        var world2 = new World("TestWorld2");
        Assert.IsFalse(_world.Equals(world2));
        Assert.IsTrue(_world.Equals(_world));
    }

    [Test]
    public void TestGetEnumerator()
    {
        _world.CreateEntity();
        _world.CreateEntity(typeof(TestIntComponent));
        
        int count = 0;
        foreach (var archetype in _world)
        {
            count++;
            Assert.IsNotNull(archetype);
        }
        Assert.AreEqual(2, count);
    }

    // Template function tests
    [Test]
    public void TestCreateEntityWithOneComponent()
    {
        var entity = _world.CreateEntity<TestIntComponent>();
        Assert.IsNotNull(entity);
        Assert.IsTrue(entity.Has<TestIntComponent>());
    }

    [Test]
    public void TestCreateEntityWithTwoComponents()
    {
        var entity = _world.CreateEntity<TestIntComponent, TestStringComponent>();
        Assert.IsNotNull(entity);
        Assert.IsTrue(entity.Has<TestIntComponent>());
        Assert.IsTrue(entity.Has<TestStringComponent>());
    }

    [Test]
    public void TestCreateEntityWithThreeComponents()
    {
        var entity = _world.CreateEntity<TestIntComponent, TestStringComponent, TestDoubleComponent>();
        Assert.IsNotNull(entity);
        Assert.IsTrue(entity.Has<TestIntComponent>());
        Assert.IsTrue(entity.Has<TestStringComponent>());
        Assert.IsTrue(entity.Has<TestDoubleComponent>());
    }

    [Test]
    public void TestCreateEntityWithFourComponents()
    {
        var entity = _world.CreateEntity<TestIntComponent, TestStringComponent, TestDoubleComponent, TestBoolComponent>();
        Assert.IsNotNull(entity);
        Assert.IsTrue(entity.Has<TestIntComponent>());
        Assert.IsTrue(entity.Has<TestStringComponent>());
        Assert.IsTrue(entity.Has<TestDoubleComponent>());
        Assert.IsTrue(entity.Has<TestBoolComponent>());
    }

    [Test]
    public void TestCreateEntityWithOneComponentInstance()
    {
        var comp = new TestIntComponent { Value = 42 };
        var entity = _world.CreateEntity(comp);
        Assert.IsNotNull(entity);
        Assert.IsTrue(entity.Has<TestIntComponent>());
        Assert.AreEqual(comp, entity.Get<TestIntComponent>());
    }

    [Test]
    public void TestCreateEntityWithTwoComponentInstances()
    {
        var comp1 = new TestIntComponent { Value = 42 };
        var comp2 = new TestStringComponent { Value = "test" };
        var entity = _world.CreateEntity(comp1, comp2);
        Assert.IsNotNull(entity);
        Assert.IsTrue(entity.Has<TestIntComponent>());
        Assert.IsTrue(entity.Has<TestStringComponent>());
        Assert.AreEqual(comp1, entity.Get<TestIntComponent>());
        Assert.AreEqual(comp2, entity.Get<TestStringComponent>());
    }

    [Test]
    public void TestCreateEntityWithThreeComponentInstances()
    {
        var comp1 = new TestIntComponent { Value = 42 };
        var comp2 = new TestStringComponent { Value = "test" };
        var comp3 = new TestDoubleComponent { Value = 3.14 };
        var entity = _world.CreateEntity(comp1, comp2, comp3);
        Assert.IsNotNull(entity);
        Assert.IsTrue(entity.Has<TestIntComponent>());
        Assert.IsTrue(entity.Has<TestStringComponent>());
        Assert.IsTrue(entity.Has<TestDoubleComponent>());
        Assert.AreEqual(comp1, entity.Get<TestIntComponent>());
        Assert.AreEqual(comp2, entity.Get<TestStringComponent>());
        Assert.AreEqual(comp3, entity.Get<TestDoubleComponent>());
    }

    [Test]
    public void TestCreateEntityWithFourComponentInstances()
    {
        var comp1 = new TestIntComponent { Value = 42 };
        var comp2 = new TestStringComponent { Value = "test" };
        var comp3 = new TestDoubleComponent { Value = 3.14 };
        var comp4 = new TestBoolComponent { Value = true };
        var entity = _world.CreateEntity(comp1, comp2, comp3, comp4);
        Assert.IsNotNull(entity);
        Assert.IsTrue(entity.Has<TestIntComponent>());
        Assert.IsTrue(entity.Has<TestStringComponent>());
        Assert.IsTrue(entity.Has<TestDoubleComponent>());
        Assert.IsTrue(entity.Has<TestBoolComponent>());
        Assert.AreEqual(comp1, entity.Get<TestIntComponent>());
        Assert.AreEqual(comp2, entity.Get<TestStringComponent>());
        Assert.AreEqual(comp3, entity.Get<TestDoubleComponent>());
        Assert.AreEqual(comp4, entity.Get<TestBoolComponent>());
    }

    [Test]
    public void TestCreateQueryWithOneComponent()
    {
        var query = _world.CreateQuery<TestIntComponent>();
        _world.CreateEntity<TestIntComponent>();
        Assert.IsNotNull(query);
        Assert.AreEqual(1, query.GetArchetypes().Count);
    }

    [Test]
    public void TestCreateQueryWithTwoComponents()
    {
        var query = _world.CreateQuery<TestIntComponent, TestStringComponent>();
        _world.CreateEntity<TestIntComponent, TestStringComponent>();
        Assert.IsNotNull(query);
        Assert.AreEqual(1, query.GetArchetypes().Count);
    }

    [Test]
    public void TestCreateQueryWithThreeComponents()
    {
        var query = _world.CreateQuery<TestIntComponent, TestStringComponent, TestDoubleComponent>();
        _world.CreateEntity<TestIntComponent, TestStringComponent, TestDoubleComponent>();
        Assert.IsNotNull(query);
        Assert.AreEqual(1, query.GetArchetypes().Count);
    }

    [Test]
    public void TestCreateQueryWithFourComponents()
    {
        var query = _world.CreateQuery<TestIntComponent, TestStringComponent, TestDoubleComponent, TestBoolComponent>();
        _world.CreateEntity<TestIntComponent, TestStringComponent, TestDoubleComponent, TestBoolComponent>();
        Assert.IsNotNull(query);
        Assert.AreEqual(1, query.GetArchetypes().Count);
    }
} 