using NUnit.Framework;
using SimpleFramework.ECS;

namespace Test.ECS;

// Test components for unit testing
public class TestIntComponent : IComponent
{
    public int Value { get; set; }
}

public class TestStringComponent : IComponent
{
    public string Value { get; set; }
}

public class TestDoubleComponent : IComponent
{
    public double Value { get; set; }
}

public class TestBoolComponent : IComponent
{
    public bool Value { get; set; }
}

[TestFixture]
public class TestComponent
{
    [Test]
    public void TestComponentCreation()
    {
        var intComp = new TestIntComponent { Value = 42 };
        var stringComp = new TestStringComponent { Value = "test" };
        var doubleComp = new TestDoubleComponent { Value = 3.14 };
        var boolComp = new TestBoolComponent { Value = true };

        Assert.AreEqual(42, intComp.Value);
        Assert.AreEqual("test", stringComp.Value);
        Assert.AreEqual(3.14, doubleComp.Value);
        Assert.AreEqual(true, boolComp.Value);
    }
}
