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
        _query = new Query(_world);
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
        _world.CreateEntity<TestStringComponent>();
        
        _query.Has<TestIntComponent>();
        
        Assert.AreEqual(0, _query.GetArchetypes().Count);
    }

    [Test]
    public void TestNotComponent()
    {
        _query.Not<TestIntComponent>();
        Assert.AreEqual(0, _query.ToList().Count);
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
        var entity = _world.CreateEntity<TestIntComponent>();
        entity.AddTag("test", "help");
        var entity1 = _world.CreateEntity();
        entity1.AddTag("test", "nop");

        _query.HasTag("help");
        
        Assert.AreEqual(1, _query.ToList().Count);
    }

    [Test]
    public void TestNotTag()
    {
        var entity = _world.CreateEntity<TestIntComponent>();
        entity.AddTag("test", "help");
        var entity1 = _world.CreateEntity();
        entity1.AddTag("world", "nop");
        
        _query.NotTag("test").NotTag("nop");
        
        Assert.AreEqual(0, _query.ToList().Count);
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
} 