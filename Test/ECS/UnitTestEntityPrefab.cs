using System;
using NUnit.Framework;
using SimpleFramework.ECS;

namespace Test.ECS;

[TestFixture]
public class UnitTestEntityPrefab
{
    [Test]
    public void InstantiateCreatesEntityWithPrefabComponentValues()
    {
        var world = new World();
        var prefab = EntityPrefab.Create()
            .With(new TestPosition { X = 1, Y = 2 })
            .With(new TestHealth { Current = 100, Max = 100 });

        var entity = world.Instantiate(prefab);

        Assert.IsTrue(world.Has<TestPosition>(entity));
        Assert.IsTrue(world.Has<TestHealth>(entity));
        Assert.AreEqual(1, world.Get<TestPosition>(entity).X);
        Assert.AreEqual(100, world.Get<TestHealth>(entity).Current);
    }

    [Test]
    public void PrefabRejectsDuplicateComponentTypes()
    {
        var prefab = EntityPrefab.Create()
            .With(new TestPosition { X = 1 });

        var ex = Assert.Throws<ArgumentException>(() => prefab.With(new TestPosition { X = 2 }));

        StringAssert.Contains(nameof(TestPosition), ex!.Message);
    }

    [Test]
    public void InstantiateUsesSameReferenceForClassComponents()
    {
        var world = new World();
        var name = new TestName { Value = "shared" };
        var prefab = EntityPrefab.Create().With(name);

        var entity = world.Instantiate(prefab);

        Assert.AreSame(name, world.Get<TestName>(entity));
    }

    [Test]
    public void InstantiateRejectsNullPrefab()
    {
        var world = new World();

        Assert.Throws<ArgumentNullException>(() => world.Instantiate(null!));
    }
}
