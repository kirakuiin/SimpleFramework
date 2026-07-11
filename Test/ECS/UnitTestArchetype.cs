using NUnit.Framework;
using SimpleFramework.ECS;

namespace Test.ECS;

[TestFixture]
public class UnitTestArchetype
{
    [Test]
    public void ArchetypeStoresTypedComponentsByAlignedRow()
    {
        var signature = new TypeSignature(typeof(TestPosition), typeof(TestVelocity));
        var archetype = new Archetype(signature);
        var entity = new Entity(1, 1, 1);

        var row = archetype.Add(entity, new IComponent[]
        {
            new TestPosition { X = 1, Y = 2 },
            new TestVelocity { X = 3, Y = 4 }
        });

        Assert.AreEqual(0, row);
        Assert.AreEqual(entity, archetype.GetEntity(0));
        Assert.AreEqual(1, archetype.Get<TestPosition>(0).X);
        Assert.AreEqual(4, archetype.Get<TestVelocity>(0).Y);
    }

    [Test]
    public void ArchetypeSetUpdatesStoredComponent()
    {
        var signature = new TypeSignature(typeof(TestPosition));
        var archetype = new Archetype(signature);
        archetype.Add(new Entity(1, 1, 1), new IComponent[] { new TestPosition { X = 1 } });

        archetype.Set(0, new TestPosition { X = 5, Y = 6 });

        Assert.AreEqual(5, archetype.Get<TestPosition>(0).X);
        Assert.AreEqual(6, archetype.Get<TestPosition>(0).Y);
    }

    [Test]
    public void ArchetypeGetReturnsWritableReference()
    {
        var signature = new TypeSignature(typeof(TestPosition));
        var archetype = new Archetype(signature);
        archetype.Add(new Entity(1, 1, 1), new IComponent[] { new TestPosition { X = 1 } });

        ref var position = ref archetype.Get<TestPosition>(0);
        position.X = 9;

        Assert.AreEqual(9, archetype.Get<TestPosition>(0).X);
    }

    [Test]
    public void ArchetypeGetAllBoxedReturnsRowValues()
    {
        var signature = new TypeSignature(typeof(TestPosition), typeof(TestName));
        var archetype = new Archetype(signature);
        var name = new TestName { Value = "player" };
        archetype.Add(new Entity(1, 1, 1), new IComponent[]
        {
            new TestPosition { X = 1 },
            name
        });

        var values = archetype.GetAllBoxed(0);

        Assert.AreEqual(1, ((TestPosition)values[typeof(TestPosition)]).X);
        Assert.AreSame(name, values[typeof(TestName)]);
    }

    [Test]
    public void ArchetypeRemoveAtSwapBackReturnsMovedEntity()
    {
        var signature = new TypeSignature(typeof(TestPosition));
        var archetype = new Archetype(signature);
        var first = new Entity(1, 1, 1);
        var second = new Entity(1, 2, 1);
        archetype.Add(first, new IComponent[] { new TestPosition { X = 1 } });
        archetype.Add(second, new IComponent[] { new TestPosition { X = 2 } });

        var moved = archetype.RemoveAtSwapBack(0);

        Assert.AreEqual(second, moved);
        Assert.AreEqual(second, archetype.GetEntity(0));
        Assert.AreEqual(2, archetype.Get<TestPosition>(0).X);
        Assert.AreEqual(1, archetype.EntityCount);
    }

    [Test]
    public void ArchetypeRemoveAtSwapBackLastRowReturnsNull()
    {
        var signature = new TypeSignature(typeof(TestPosition));
        var archetype = new Archetype(signature);
        archetype.Add(new Entity(1, 1, 1), new IComponent[] { new TestPosition { X = 1 } });

        var moved = archetype.RemoveAtSwapBack(0);

        Assert.IsNull(moved);
        Assert.AreEqual(0, archetype.EntityCount);
    }

    [Test]
    public void ArchetypeRejectedRowDoesNotCorruptAlignedStorage()
    {
        var signature = new TypeSignature(typeof(TestPosition), typeof(TestVelocity));
        var archetype = new Archetype(signature);
        var rejected = new Entity(1, 1, 1);

        Assert.Throws<InvalidOperationException>(() => archetype.Add(rejected,
            new Dictionary<Type, IComponent>
            {
                [typeof(TestPosition)] = new TestPosition { X = 10 }
            }));
        Assert.AreEqual(0, archetype.EntityCount);

        var accepted = new Entity(1, 2, 1);
        var row = archetype.Add(accepted, new IComponent[]
        {
            new TestPosition { X = 20 },
            new TestVelocity { X = 30 }
        });

        Assert.AreEqual(0, row);
        Assert.AreEqual(accepted, archetype.GetEntity(0));
        Assert.AreEqual(20, archetype.Get<TestPosition>(0).X);
        Assert.AreEqual(30, archetype.Get<TestVelocity>(0).X);
    }
}
