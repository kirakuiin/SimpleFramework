using System;
using NUnit.Framework;
using SimpleFramework.ECS;

namespace Test.ECS;

[TestFixture]
public class UnitTestEach
{
    [Test]
    public void EachWithOneComponentProvidesWritableReference()
    {
        var world = new World();
        var entity = world.CreateEntity(new TestPosition { X = 1 });

        world.Each<TestPosition>((Entity _, ref TestPosition position) =>
        {
            position.X += 10;
        });

        Assert.AreEqual(11, world.Get<TestPosition>(entity).X);
    }

    [Test]
    public void EachWithTwoComponentsProvidesWritableReferences()
    {
        var world = new World();
        var entity = world.CreateEntity(
            new TestPosition { X = 1 },
            new TestVelocity { X = 2 });

        world.Each<TestPosition, TestVelocity>((Entity _, ref TestPosition position, ref TestVelocity velocity) =>
        {
            position.X += velocity.X;
            velocity.X = 5;
        });

        Assert.AreEqual(3, world.Get<TestPosition>(entity).X);
        Assert.AreEqual(5, world.Get<TestVelocity>(entity).X);
    }

    [Test]
    public void EachThrowsWhenStructureChangesDuringTraversal()
    {
        var world = new World();
        world.CreateEntity(new TestPosition());

        Assert.Throws<InvalidOperationException>(() =>
        {
            world.Each<TestPosition>((Entity entity, ref TestPosition _) =>
            {
                world.Add(entity, new TestVelocity());
            });
        });
    }
}
