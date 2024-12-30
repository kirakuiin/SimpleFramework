using NUnit.Framework;
using SimpleFramework.Patterns;

namespace Test.Patterns;

[TestFixture]
public class TestSingleton
{
    [Test]
    public void TestUse()
    {
        Assert.AreEqual(SingletonExample.Init, SingletonExample.Instance.Value);
    }
    
    [Test]
    public void TestDestroy()
    {
        SingletonExample.Instance.Value = "abc";
        
        SingletonExample.Destroy();
        
        Assert.AreEqual(SingletonExample.Init, SingletonExample.Instance.Value);
    }
}

public class SingletonExample : Singleton<SingletonExample>
{
    public const string Init = "Hello";

    public string Value { get; set; } = Init;
}