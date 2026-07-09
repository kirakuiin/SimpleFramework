#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimpleFramework.Patterns;

namespace Test.Patterns;

[TestFixture]
public class TestObjectPool
{
    private sealed class TestObject
    {
        public int Value { get; set; }

        public void Reset()
        {
            Value = 0;
        }
    }

    private sealed class PooledObject : IPoolCallbackListener
    {
        public bool OnGetCalled { get; private set; }
        public bool OnReturnCalled { get; private set; }

        public void OnGet()
        {
            OnGetCalled = true;
        }

        public void OnReturn()
        {
            OnReturnCalled = true;
        }
    }

    [Test]
    public void TestGetAndReturn()
    {
        using var pool = new ObjectPool<TestObject>(() => new TestObject());

        var obj1 = pool.Get();
        obj1.Value = 42;
        pool.Return(obj1);

        var obj2 = pool.Get();

        Assert.AreSame(obj1, obj2);
        Assert.AreEqual(42, obj2.Value);
        Assert.AreEqual(0, pool.Count);
    }

    [Test]
    public void TestCreateWhenEmpty()
    {
        var createCount = 0;
        using var pool = new ObjectPool<TestObject>(() =>
        {
            createCount++;
            return new TestObject();
        });

        var obj1 = pool.Get();
        var obj2 = pool.Get();

        Assert.AreEqual(2, createCount);
        Assert.AreNotSame(obj1, obj2);
        Assert.AreEqual(0, pool.Count);
    }

    [Test]
    public void TestReturnNull()
    {
        using var pool = new ObjectPool<TestObject>(() => new TestObject());

        Assert.Throws<ArgumentNullException>(() => pool.Return(null!));
    }

    [Test]
    public void TestPrewarm()
    {
        var createCount = 0;
        using var pool = new ObjectPool<TestObject>(() =>
        {
            createCount++;
            return new TestObject();
        });

        pool.Prewarm(5);
        var obj = pool.Get();

        Assert.IsNotNull(obj);
        Assert.AreEqual(5, createCount);
        Assert.AreEqual(4, pool.Count);
    }

    [Test]
    public void TestClearDestroysPooledObjects()
    {
        var destroyedObjects = new List<TestObject>();
        using var pool = new ObjectPool<TestObject>(
            () => new TestObject(),
            onDestroy: destroyedObjects.Add);

        var obj1 = new TestObject();
        var obj2 = new TestObject();
        pool.Return(obj1);
        pool.Return(obj2);

        pool.Clear();

        Assert.AreEqual(0, pool.Count);
        Assert.AreEqual(new[] { obj2, obj1 }, destroyedObjects);
    }

    [Test]
    public void TestOnGetAndOnReturnCallbacks()
    {
        var onGetCalledCount = 0;
        var onReturnCalledCount = 0;
        using var pool = new ObjectPool<TestObject>(
            () => new TestObject(),
            onGet: _ => onGetCalledCount++,
            onReturn: _ => onReturnCalledCount++);

        var obj = pool.Get();
        pool.Return(obj);
        _ = pool.Get();

        Assert.AreEqual(2, onGetCalledCount);
        Assert.AreEqual(1, onReturnCalledCount);
    }

    [Test]
    public void TestIPoolCallbackListener()
    {
        using var pool = new ObjectPool<PooledObject>(() => new PooledObject());

        var obj = pool.Get();
        pool.Return(obj);

        Assert.IsTrue(obj.OnGetCalled);
        Assert.IsTrue(obj.OnReturnCalled);
    }

    [Test]
    public void TestDisposePreventsUsage()
    {
        var pool = new ObjectPool<TestObject>(() => new TestObject());
        pool.Return(pool.Get());

        pool.Dispose();

        Assert.Throws<ObjectDisposedException>(() => pool.Get());
        Assert.Throws<ObjectDisposedException>(() => pool.Return(new TestObject()));
        Assert.Throws<ObjectDisposedException>(() => pool.Clear());
        Assert.Throws<ObjectDisposedException>(() => pool.Prewarm(5));
    }

    [Test]
    public void TestCountProperty()
    {
        using var pool = new ObjectPool<TestObject>(() => new TestObject());
        var obj1 = pool.Get();
        var obj2 = pool.Get();

        pool.Return(obj1);
        pool.Return(obj2);
        pool.Clear();

        Assert.AreEqual(0, pool.Count);
    }

    [Test]
    public void TestOnReturnCanResetObject()
    {
        using var pool = new ObjectPool<TestObject>(
            () => new TestObject(),
            onReturn: obj => obj.Reset());

        var obj = pool.Get();
        obj.Value = 3;

        pool.Return(obj);

        Assert.AreEqual(0, obj.Value);
    }

    [Test]
    public void TestReturnedObjectsAreReused()
    {
        using var pool = new ObjectPool<TestObject>(() => new TestObject());
        var obj1 = pool.Get();
        var obj2 = pool.Get();

        pool.Return(obj1);
        pool.Return(obj2);

        var reusedObj1 = pool.Get();
        var reusedObj2 = pool.Get();

        Assert.Contains(reusedObj1, new[] { obj1, obj2 });
        Assert.Contains(reusedObj2, new[] { obj1, obj2 });
        Assert.AreNotSame(reusedObj1, reusedObj2);
    }
}
