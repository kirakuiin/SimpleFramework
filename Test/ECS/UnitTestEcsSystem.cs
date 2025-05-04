using NUnit.Framework;
using SimpleFramework.ECS;

namespace Test.ECS;

[TestFixture]
public class TestEcsSystem
{
    private World _world;
    private TestSystem _system;

    [SetUp]
    public void Setup()
    {
        _world = new World();
        _system = new TestSystem(_world);
    }


    [Test]
    public void TestSystemUpdate()
    {
        var entity = _world.CreateEntity();
        entity.Add(new TestIntComponent { Value = 42 });
        entity.Add(new TestStringComponent { Value = "test" });

        _system.Update();
        Assert.AreEqual(1, _system.ProcessedEntities);
    }

    [Test]
    public void TestSystemQuery()
    {
        var entity1 = _world.CreateEntity();
        entity1.Add(new TestIntComponent { Value = 1 });
        entity1.Add(new TestStringComponent { Value = "test1" });

        var entity2 = _world.CreateEntity();
        entity2.Add(new TestIntComponent { Value = 2 });
        entity2.Add(new TestStringComponent { Value = "test2" });

        _system.Update();
        Assert.AreEqual(2, _system.ProcessedEntities);
    }

    [Test]
    public void TestSystemWithoutRequiredComponent()
    {
        var entity = _world.CreateEntity();
        entity.Add(new TestStringComponent { Value = "test" });

        _system.Update();
        Assert.AreEqual(0, _system.ProcessedEntities);
    }
}

// Test system implementation
public class TestSystem : EcsSystem
{
    public int ProcessedEntities { get; private set; }

    public TestSystem(World world) : base(world)
    {
        Query = world.CreateQuery<TestIntComponent, TestStringComponent>();
    }

    protected override void ProcessEntity(Entity entity)
    {
        ProcessedEntities++;
        var intComp = entity.Get<TestIntComponent>();
        var stringComp = entity.Get<TestStringComponent>();
        Assert.IsNotNull(intComp);
        Assert.IsNotNull(stringComp);
    }
} 