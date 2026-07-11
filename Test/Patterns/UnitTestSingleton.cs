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

        var first = Task.Run(() => ConcurrentSingletonExample.Instance);
        Assert.That(ConcurrentSingletonExample.WaitUntilConstructionStarts(), Is.True);
        var remaining = Enumerable.Range(0, 15)
            .Select(_ => Task.Run(() => ConcurrentSingletonExample.Instance))
            .ToArray();
        ConcurrentSingletonExample.AllowConstructionToComplete();
        var instances = remaining.Prepend(first).ToArray();

        Task.WaitAll(instances);

        Assert.AreEqual(1, ConcurrentSingletonExample.CreateCount);
        Assert.IsTrue(instances.All(task => ReferenceEquals(instances[0].Result, task.Result)));
    }

    [Test]
    public void TestInitializationFailureDoesNotPublishInstance()
    {
        FailingInitializationSingleton.Destroy();
        FailingInitializationSingleton.Reset();

        Assert.Throws<InvalidOperationException>(() => _ = FailingInitializationSingleton.Instance);

        var instance = FailingInitializationSingleton.Instance;

        Assert.That(instance.IsInitialized(), Is.True);
        Assert.That(FailingInitializationSingleton.CreateCount, Is.EqualTo(2));
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
    private static ManualResetEventSlim _constructionStarted = new(false);
    private static ManualResetEventSlim _allowConstruction = new(false);

    public ConcurrentSingletonExample()
    {
        Interlocked.Increment(ref _createCount);
        _constructionStarted.Set();
        _allowConstruction.Wait();
    }

    public static int CreateCount => _createCount;

    public static void Reset()
    {
        _createCount = 0;
        _constructionStarted.Dispose();
        _allowConstruction.Dispose();
        _constructionStarted = new ManualResetEventSlim(false);
        _allowConstruction = new ManualResetEventSlim(false);
    }

    public static bool WaitUntilConstructionStarts() => _constructionStarted.Wait(TimeSpan.FromSeconds(5));

    public static void AllowConstructionToComplete() => _allowConstruction.Set();
}

public class FailingInitializationSingleton : Singleton<FailingInitializationSingleton>
{
    private static int _createCount;
    private static int _failNextInitialization;

    public FailingInitializationSingleton()
    {
        Interlocked.Increment(ref _createCount);
    }

    public static int CreateCount => _createCount;

    public static void Reset()
    {
        _createCount = 0;
        _failNextInitialization = 1;
    }

    public override void Initialize()
    {
        if (Interlocked.Exchange(ref _failNextInitialization, 0) == 1)
        {
            throw new InvalidOperationException("Initialization failed.");
        }

        base.Initialize();
    }
}
