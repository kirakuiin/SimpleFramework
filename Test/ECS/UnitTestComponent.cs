using NUnit.Framework;
using SimpleFramework.ECS;

namespace Test.ECS;

public struct TestPosition : IComponent
{
    public float X;
    public float Y;
}

public struct TestVelocity : IComponent
{
    public float X;
    public float Y;
}

public struct TestHealth : IComponent
{
    public int Current;
    public int Max;
}

public struct TestDeadTag : IComponent
{
}

public sealed class TestName : IComponent
{
    public string Value { get; set; } = string.Empty;
}

[TestFixture]
public class TestComponent
{
    [Test]
    public void ComponentTypesCanBeStructsOrClasses()
    {
        IComponent position = new TestPosition { X = 1, Y = 2 };
        IComponent name = new TestName { Value = "player" };

        Assert.IsInstanceOf<TestPosition>(position);
        Assert.IsInstanceOf<TestName>(name);
    }
}
