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

    [Test]
    public void PrefabRejectsNullReferenceComponentWithoutMutation()
    {
        var prefab = EntityPrefab.Create();

        var exception = Assert.Throws<ArgumentNullException>(() => prefab.With<TestName>(null!));
        Assert.AreEqual("component", exception!.ParamName);

        prefab.With(new TestName { Value = "valid" });
        var world = new World();
        var entity = world.Instantiate(prefab);
        Assert.AreEqual("valid", world.Get<TestName>(entity).Value);
    }

    [Test]
    public void PrefabRejectsWidenedDuplicateRuntimeTypeWithoutMutation()
    {
        var prefab = EntityPrefab.Create().With(new TestPosition { X = 1 });

        var exception = Assert.Throws<ArgumentException>(() =>
            prefab.With<IComponent>(new TestPosition { X = 2 }));

        Assert.AreEqual("component", exception!.ParamName);
        var world = new World();
        var entity = world.Instantiate(prefab);
        Assert.AreEqual(1, world.GetArchetype(entity).Signature.Count);
        Assert.AreEqual(1, world.Get<TestPosition>(entity).X);
    }

    [Test]
    public void PrefabKeepsDistinctWidenedRuntimeTypesWithIdenticalNames()
    {
        var firstType = TestTypeSignature.CreateDynamicComponentType("PrefabModuleA");
        var secondType = TestTypeSignature.CreateDynamicComponentType("PrefabModuleB");
        var first = (IComponent)Activator.CreateInstance(firstType)!;
        var second = (IComponent)Activator.CreateInstance(secondType)!;
        var prefab = EntityPrefab.Create()
            .With<IComponent>(first)
            .With<IComponent>(second);

        var world = new World();
        var entity = world.Instantiate(prefab);
        var archetype = world.GetArchetype(entity);

        Assert.AreNotEqual(firstType, secondType);
        Assert.AreEqual(firstType.AssemblyQualifiedName, secondType.AssemblyQualifiedName);
        Assert.IsTrue(archetype.Has(firstType));
        Assert.IsTrue(archetype.Has(secondType));
        Assert.AreSame(first, archetype.GetBoxed(0, firstType));
        Assert.AreSame(second, archetype.GetBoxed(0, secondType));
    }
}
