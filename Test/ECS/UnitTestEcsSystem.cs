using NUnit.Framework;
using SimpleFramework.ECS;

namespace Test.ECS;

[TestFixture]
public class TestEcsSystem
{
    [Test]
    public void SystemProcessesMatchingQueryEntities()
    {
        var world = new World();
        var system = new MovementTestSystem(world);
        var entity = world.CreateEntity(
            new TestPosition { X = 1 },
            new TestVelocity { X = 2 });
        world.CreateEntity(new TestPosition { X = 10 });

        system.Update();

        Assert.AreEqual(3, world.Get<TestPosition>(entity).X);
    }

    [Test]
    public void DeltaTimeUpdateFallsBackToParameterlessUpdate()
    {
        var world = new World();
        var system = new CountingSystem(world);

        system.Update(0.25f);

        Assert.AreEqual(1, system.UpdateCount);
    }

    [Test]
    public void SystemConstructorRejectsNullWorld()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new CountingSystem(null!));

        Assert.AreEqual("world", exception!.ParamName);
    }

    private sealed class MovementTestSystem : EcsSystem
    {
        private readonly Query _query;

        public MovementTestSystem(World world) : base(world)
        {
            _query = world.Query<TestPosition, TestVelocity>();
        }

        public override void Update()
        {
            foreach (var entity in _query)
            {
                ref var position = ref World.Get<TestPosition>(entity);
                ref var velocity = ref World.Get<TestVelocity>(entity);
                position.X += velocity.X;
                position.Y += velocity.Y;
            }
        }
    }

    private sealed class CountingSystem : EcsSystem
    {
        public CountingSystem(World world) : base(world)
        {
        }

        public int UpdateCount { get; private set; }

        public override void Update()
        {
            UpdateCount++;
        }
    }
}
