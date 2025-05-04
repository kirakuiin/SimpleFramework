using System.Linq;
using NUnit.Framework;
using SimpleFramework.ECS;

namespace Test.ECS;

[TestFixture]
public class UnitTestQuery
{
    private World _world;
    private Query _query;

    [SetUp]
    public void Setup()
    {
        _world = new World();
        _query = _world.CreateQuery();
    }

    [Test]
    public void TestQueryCreation()
    {
        Assert.IsNotNull(_query);
        Assert.AreEqual(_world, _query.World);
    }

    [Test]
    public void TestHasComponent()
    {
        _query.Has<TestIntComponent>();
        var entity = _world.CreateEntity<TestIntComponent>();
        
        var archetypes = _query.GetArchetypes();
        Assert.AreEqual(1, archetypes.Count);
        Assert.IsTrue(archetypes[0].Has<TestIntComponent>());
    }

    [Test]
    public void TestNotComponent()
    {
        _query.Has<TestIntComponent>().Not<TestStringComponent>();
        
        var entity1 = _world.CreateEntity<TestIntComponent>();
        var entity2 = _world.CreateEntity<TestIntComponent, TestStringComponent>();
        
        var archetypes = _query.GetArchetypes();
        Assert.AreEqual(1, archetypes.Count);
        Assert.IsTrue(archetypes[0].Has<TestIntComponent>());
        Assert.IsFalse(archetypes[0].Has<TestStringComponent>());
    }

    [Test]
    public void TestHasAndNotComponent()
    {
        _query.Has<TestIntComponent>().Not<TestStringComponent>();
        
        _world.CreateEntity<TestIntComponent>();
        _world.CreateEntity<TestIntComponent>();
        _world.CreateEntity<TestIntComponent, TestStringComponent>();
        
        Assert.AreEqual(2, _query.ToList().Count);
    }

    [Test]
    public void TestHasTag()
    {
        _query.HasTag("test");
        
        var entity = _world.CreateEntity();
        entity.AddTag("test");
        
        var count = 0;
        _query.Foreach(e => count++);
        Assert.AreEqual(1, count);
    }

    [Test]
    public void TestNotTag()
    {
        _query.NotTag("test");
        
        var entity1 = _world.CreateEntity();
        entity1.AddTag("test");
        
        var entity2 = _world.CreateEntity();
        entity2.AddTag("other");
        
        var count = 0;
        _query.Foreach(e => count++);
        Assert.AreEqual(1, count);
    }

    [Test]
    public void TestClear()
    {
        _query.Has<TestIntComponent>().Has<TestStringComponent>();
        _query.Clear();
        Assert.AreEqual(0, _query.ToList().Count);
    }

    [Test]
    public void TestForeach()
    {
        var entity = _world.CreateEntity();
        entity.Add(new TestIntComponent { Value = 42 });
        _world.CreateEntity<TestIntComponent, TestDoubleComponent>();
        _query.Has<TestIntComponent>();
        
        int count = 0;
        _query.Foreach(e => count++);
        Assert.AreEqual(2, count);
    }

    [Test]
    public void TestToString()
    {
        _query.Has<TestIntComponent>().Not<TestStringComponent>().HasTag("test");
        var str = _query.ToString();
        Assert.IsTrue(str.Contains("Include:"));
        Assert.IsTrue(str.Contains("Exclude:"));
        Assert.IsTrue(str.Contains("IncludeTags:"));
        Assert.IsTrue(str.Contains("ExcludeTags:"));
    }

    [Test]
    public void TestComplexQuery()
    {
        _query.Has<TestIntComponent>()
              .Has<TestStringComponent>()
              .Not<TestDoubleComponent>()
              .HasTag("test")
              .NotTag("exclude");
        
        var entity1 = _world.CreateEntity<TestIntComponent, TestStringComponent>();
        entity1.AddTag("test");
        
        var entity2 = _world.CreateEntity<TestIntComponent, TestStringComponent, TestDoubleComponent>();
        entity2.AddTag("test");
        
        var entity3 = _world.CreateEntity<TestIntComponent, TestStringComponent>();
        entity3.AddTag("test");
        entity3.AddTag("exclude");
        
        var count = 0;
        _query.Foreach(e => count++);
        Assert.AreEqual(1, count);
    }

    [Test]
    public void TestGetArchetypes()
    {
        _query.Has<TestIntComponent>();
        
        _world.CreateEntity<TestIntComponent>();
        _world.CreateEntity<TestIntComponent, TestStringComponent>();
        
        var archetypes = _query.GetArchetypes();
        Assert.AreEqual(2, archetypes.Count);
        Assert.IsTrue(archetypes.All(a => a.Has<TestIntComponent>()));
    }

    [Test]
    public void TestQueryUpdateOnArchetypeChange()
    {
        _query.Has<TestIntComponent>();
        
        var entity = _world.CreateEntity<TestIntComponent>();
        Assert.AreEqual(1, _query.Count());
        
        entity.Remove<TestIntComponent>();
        Assert.AreEqual(0, _query.Count());
    }
} 