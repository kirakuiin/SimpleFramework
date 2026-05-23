using System;
using System.Linq;
using NUnit.Framework;
using SimpleFramework.ECS;

namespace Test.ECS;

[TestFixture]
public class UnitTestQuery
{
    [Test]
    public void QueryIncludesAndExcludesByComponent()
    {
        var world = new World();
        var query = world.Query<TestPosition>().Not<TestDeadTag>();
        var alive = world.CreateEntity(new TestPosition { X = 1 });
        world.CreateEntity(new TestPosition { X = 2 }, new TestDeadTag());

        CollectionAssert.AreEquivalent(new[] { alive }, query.ToList());
    }

    [Test]
    public void QueryLazyRefreshSeesNewArchetype()
    {
        var world = new World();
        var query = world.Query<TestPosition, TestVelocity>();

        Assert.AreEqual(0, query.Count());

        var entity = world.CreateEntity(new TestPosition(), new TestVelocity());

        CollectionAssert.AreEqual(new[] { entity }, query.ToList());
    }

    [Test]
    public void QueryUsesExistingMatchingArchetypes()
    {
        var world = new World();
        var entity = world.CreateEntity(new TestPosition(), new TestVelocity());

        var query = world.Query<TestPosition>();

        CollectionAssert.AreEqual(new[] { entity }, query.ToList());
    }

    [Test]
    public void QueryUpdatesWhenEntityChangesArchetype()
    {
        var world = new World();
        var entity = world.CreateEntity(new TestPosition());
        var query = world.Query<TestPosition, TestVelocity>();

        Assert.IsEmpty(query.ToList());

        world.Add(entity, new TestVelocity());

        CollectionAssert.AreEqual(new[] { entity }, query.ToList());
    }

    [Test]
    public void QueryStopsReturningDestroyedEntities()
    {
        var world = new World();
        var entity = world.CreateEntity(new TestPosition());
        var query = world.Query<TestPosition>();

        world.DestroyEntity(entity);

        Assert.IsEmpty(query.ToList());
    }

    [Test]
    public void QueryThrowsWhenStructureChangesDuringEnumeration()
    {
        var world = new World();
        world.CreateEntity(new TestPosition());
        var query = world.Query<TestPosition>();

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (var entity in query)
            {
                world.DestroyEntity(entity);
            }
        });
    }

    [Test]
    public void QueryClearRemovesFilters()
    {
        var world = new World();
        var first = world.CreateEntity(new TestPosition());
        var second = world.CreateEntity(new TestVelocity());
        var query = world.Query<TestPosition>();

        query.Clear();

        CollectionAssert.AreEquivalent(new[] { first, second }, query.ToList());
    }

    [Test]
    public void QueryForeachVisitsMatchingEntities()
    {
        var world = new World();
        world.CreateEntity(new TestPosition());
        world.CreateEntity(new TestPosition(), new TestVelocity());
        world.CreateEntity(new TestVelocity());
        var query = world.Query<TestPosition>();
        var count = 0;

        query.Foreach(_ => count++);

        Assert.AreEqual(2, count);
    }

    [Test]
    public void GetArchetypesReturnsMatchingArchetypes()
    {
        var world = new World();
        world.CreateEntity(new TestPosition());
        world.CreateEntity(new TestPosition(), new TestVelocity());
        world.CreateEntity(new TestVelocity());
        var query = world.Query<TestPosition>();

        var archetypes = query.GetArchetypes();

        Assert.AreEqual(2, archetypes.Count);
        Assert.IsTrue(archetypes.All(archetype => archetype.Signature.Has<TestPosition>()));
    }
}