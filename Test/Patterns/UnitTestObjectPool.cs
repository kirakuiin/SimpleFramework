#nullable enable
using NUnit.Framework;
using SimpleFramework.Patterns;
using System;

namespace Test.Patterns;

[TestFixture]
public class TestObjectPool
{
    private class TestObject
    {
        public int Value { get; set; }
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }

        public void Reset()
        {
            Value = 0;
        }
    }

    private class PooledObject : IPoolCallbackListener
    {
        public int Value { get; set; }
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

        public void Reset()
        {
            Value = 0;
            OnGetCalled = false;
            OnReturnCalled = false;
        }
    }

    [Test]
    public void TestGetAndReturn()
    {
        using var pool = new ObjectPool<TestObject>(() => new TestObject());

        var obj1 = pool.Get();
        Assert.IsNotNull(obj1);
        Assert.AreEqual(0, pool.Count);

        obj1.Value = 42;
        pool.Return(obj1);
        Assert.AreEqual(1, pool.Count);

        var obj2 = pool.Get();
        Assert.AreEqual(0, pool.Count);
        Assert.AreSame(obj1, obj2); // 应该返回同一个对象
        Assert.AreEqual(42, obj2.Value); // 值应该保持不变
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
        Assert.AreEqual(1, createCount);
        Assert.AreEqual(0, pool.Count);

        var obj2 = pool.Get();
        Assert.AreEqual(2, createCount);
        Assert.AreEqual(0, pool.Count);
        Assert.AreNotSame(obj1, obj2);
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
        Assert.AreEqual(5, createCount);
        Assert.AreEqual(5, pool.Count);

        // 获取对象应该使用预热的对象，不会创建新的
        var obj = pool.Get();
        Assert.AreEqual(5, createCount); // 创建次数不变
        Assert.AreEqual(4, pool.Count);
    }

    [Test]
    public void TestClear()
    {
        using var pool = new ObjectPool<TestObject>(() => new TestObject());

        // 添加一些对象到池中
        for (var i = 0; i < 3; i++)
        {
            var obj = pool.Get();
            pool.Return(obj);
        }

        Assert.AreEqual(1, pool.Count);

        pool.Clear();
        Assert.AreEqual(0, pool.Count);
    }

    [Test]
    public void TestOnDestroyCallback()
    {
        var disposedObjects = new System.Collections.Generic.List<TestObject>();

        using var pool = new ObjectPool<TestObject>(
            () => new TestObject(),
            onDestroy: obj => disposedObjects.Add(obj)
        );

        var obj1 = new TestObject();
        var obj2 = new TestObject();

        pool.Return(obj1);
        pool.Return(obj2);

        Assert.AreEqual(2, pool.Count);
        Assert.AreEqual(0, disposedObjects.Count);

        pool.Clear();
        Assert.AreEqual(0, pool.Count);
        Assert.AreEqual(2, disposedObjects.Count);
        Assert.IsTrue(disposedObjects.Contains(obj1));
        Assert.IsTrue(disposedObjects.Contains(obj2));
    }

    [Test]
    public void TestOnGetAndOnReturnCallbacks()
    {
        var onGetCalledCount = 0;
        var onReturnCalledCount = 0;

        using var pool = new ObjectPool<TestObject>(
            () => new TestObject(),
            onGet: obj => onGetCalledCount++,
            onReturn: obj => onReturnCalledCount++
        );

        var obj = pool.Get();
        Assert.AreEqual(0, onGetCalledCount);
        Assert.AreEqual(0, onReturnCalledCount);

        pool.Return(obj);
        Assert.AreEqual(0, onGetCalledCount);
        Assert.AreEqual(1, onReturnCalledCount);
        
        obj = pool.Get();
        Assert.AreEqual(1, onGetCalledCount);
    }

    [Test]
    public void TestIPoolCallbackListener()
    {
        using var pool = new ObjectPool<PooledObject>(() => new PooledObject());

        var obj = pool.Get();
        
        Assert.IsFalse(obj.OnReturnCalled);
        
        obj.OnReturn();
        obj.OnGet();
        
        Assert.IsTrue(obj.OnGetCalled);
        Assert.IsTrue(obj.OnReturnCalled);
    }

    [Test]
    public void TestDisposePreventsUsage()
    {
        var pool = new ObjectPool<TestObject>(() => new TestObject());

        var obj = pool.Get();
        pool.Return(obj);

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

        Assert.AreEqual(0, pool.Count);

        var obj1 = pool.Get();
        Assert.AreEqual(0, pool.Count);

        var obj2 = pool.Get();
        Assert.AreEqual(0, pool.Count);

        pool.Return(obj1);
        Assert.AreEqual(1, pool.Count);

        pool.Return(obj2);
        Assert.AreEqual(2, pool.Count);

        pool.Clear();
        Assert.AreEqual(0, pool.Count);
    }

    [Test]
    public void TestComplexWorkflow()
    {
        var resetCount = 0;
        using var pool = new ObjectPool<TestObject>(
            () => new TestObject(),
            onReturn: obj =>
            {
                obj.Reset();
                resetCount++;
            }
        );

        // 获取多个对象
        var obj1 = pool.Get();
        var obj2 = pool.Get();
        var obj3 = pool.Get();

        obj1.Value = 1;
        obj2.Value = 2;
        obj3.Value = 3;

        Assert.AreEqual(0, pool.Count);
        Assert.AreEqual(0, resetCount);

        // 归还对象
        pool.Return(obj1);
        Assert.AreEqual(1, pool.Count);
        Assert.AreEqual(1, resetCount);
        Assert.AreEqual(0, obj1.Value); // 应该被重置

        pool.Return(obj2);
        Assert.AreEqual(2, pool.Count);
        Assert.AreEqual(2, resetCount);
        Assert.AreEqual(0, obj2.Value); // 应该被重置

        // 重新获取对象，应该复用之前归还的对象
        var reusedObj1 = pool.Get();
        var reusedObj2 = pool.Get();

        Assert.AreEqual(0, pool.Count);
        Assert.AreEqual(2, resetCount);

        // 应该复用之前的对象（顺序可能不同）
        Assert.IsTrue(reusedObj1 == obj1 || reusedObj1 == obj2);
        Assert.IsTrue(reusedObj2 == obj1 || reusedObj2 == obj2);
        Assert.AreNotSame(reusedObj1, reusedObj2);
    }
}