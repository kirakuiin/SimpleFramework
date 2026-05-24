using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

    [Test]
    public void TestConcurrentFirstAccessCreatesSingleInstance()
    {
        ConcurrentSingletonExample.Destroy();
        ConcurrentSingletonExample.Reset();

        var instances = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => ConcurrentSingletonExample.Instance))
            .ToArray();

        Task.WaitAll(instances);

        Assert.AreEqual(1, ConcurrentSingletonExample.CreateCount);
        Assert.IsTrue(instances.All(task => ReferenceEquals(instances[0].Result, task.Result)));
    }
}

public class SingletonExample : Singleton<SingletonExample>
{
    public const string Init = "Hello";

    public string Value { get; set; } = Init;
}

public class ConcurrentSingletonExample : Singleton<ConcurrentSingletonExample>
{
    private static int _createCount;

    public ConcurrentSingletonExample()
    {
        Interlocked.Increment(ref _createCount);
        Thread.Sleep(20);
    }

    public static int CreateCount => _createCount;

    public static void Reset()
    {
        _createCount = 0;
    }
}
