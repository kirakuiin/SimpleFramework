using NUnit.Framework;
using SimpleFramework.ECS;

namespace Test.ECS;

[TestFixture]
public class UnitTestEntity
{
    [Test]
    public void EntityStoresWorldIdIdAndVersion()
    {
        var entity = new Entity(worldId: 1, id: 2, version: 3);

        Assert.AreEqual(1, entity.WorldId);
        Assert.AreEqual(2, entity.Id);
        Assert.AreEqual(3, entity.Version);
    }

    [Test]
    public void EntitiesCompareByWorldIdIdAndVersion()
    {
        var entity = new Entity(1, 2, 3);
        var same = new Entity(1, 2, 3);
        var differentWorld = new Entity(2, 2, 3);
        var differentId = new Entity(1, 3, 3);
        var differentVersion = new Entity(1, 2, 4);

        Assert.AreEqual(entity, same);
        Assert.IsTrue(entity == same);
        Assert.IsFalse(entity != same);
        Assert.AreNotEqual(entity, differentWorld);
        Assert.AreNotEqual(entity, differentId);
        Assert.AreNotEqual(entity, differentVersion);
    }

    [Test]
    public void EntityToStringContainsHandleParts()
    {
        var entity = new Entity(10, 20, 30);

        var text = entity.ToString();

        StringAssert.Contains("10", text);
        StringAssert.Contains("20", text);
        StringAssert.Contains("30", text);
    }
}
