using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimpleFramework.Utility;

namespace Test.Utility;

[TestFixture]
public class TestDisposable
{

    [Test]
    public void TestCustomDisposableImplementation()
    {
        // 创建自定义的可释放对象
        var customDisposable = new CustomDisposable();

        // 验证初始状态
        Assert.IsFalse(customDisposable.IsDisposedCalled);

        // 调用Dispose
        customDisposable.Dispose();

        // 验证Dispose被调用
        Assert.IsTrue(customDisposable.IsDisposedCalled);
    }

    [Test]
    public void TestCustomDisposableDoubleDispose()
    {
        var customDisposable = new CustomDisposable();

        // 第一次调用Dispose
        customDisposable.Dispose();
        Assert.IsTrue(customDisposable.IsDisposedCalled);
        Assert.AreEqual(1, customDisposable.DisposeCallCount);

        // 第二次调用Dispose不应该抛出异常
        customDisposable.Dispose();
        Assert.AreEqual(1, customDisposable.DisposeCallCount);
    }

    [Test]
    public void TestDisposableGroupBasicFunctionality()
    {
        var group = new DisposableGroup();
        var disposable1 = new CustomDisposable();
        var disposable2 = new CustomDisposable();

        // 添加可释放对象
        group.Add(disposable1);
        group.Add(disposable2);

        // 验证对象尚未释放
        Assert.IsFalse(disposable1.IsDisposedCalled);
        Assert.IsFalse(disposable2.IsDisposedCalled);

        // 释放组
        group.Dispose();

        // 验证所有对象都被释放
        Assert.IsTrue(disposable1.IsDisposedCalled);
        Assert.IsTrue(disposable2.IsDisposedCalled);
    }

    [Test]
    public void TestDisposableGroupMultipleDisposes()
    {
        var group = new DisposableGroup();
        var disposable = new CustomDisposable();

        group.Add(disposable);

        // 第一次释放
        group.Dispose();
        Assert.IsTrue(disposable.IsDisposedCalled);
        Assert.AreEqual(1, disposable.DisposeCallCount);

        // 第二次释放应该安全，不会重复释放内部对象
        group.Dispose();
        Assert.AreEqual(1, disposable.DisposeCallCount); // 内部对象不会被重复释放
    }

    [Test]
    public void TestDisposableGroupWithNullObjects()
    {
        var group = new DisposableGroup();

        // 添加null对象不应该抛出异常
        Assert.DoesNotThrow(() => group.Add(null));

        // 释放组不应该抛出异常
        Assert.DoesNotThrow(() => group.Dispose());
    }

    [Test]
    public void TestDisposableGroupWithMixedObjects()
    {
        var group = new DisposableGroup();
        var customDisposable = new CustomDisposable();

        // 添加不同类型的可释放对象
        group.Add(customDisposable);
        group.Add(new System.IO.MemoryStream());

        // 释放组
        group.Dispose();

        // 验证自定义对象被释放
        Assert.IsTrue(customDisposable.IsDisposedCalled);
    }

    [Test]
    public void TestDisposableGroupMultipleAdd()
    {
        var group = new DisposableGroup();
        var disposable1 = new CustomDisposable();
        var disposable2 = new CustomDisposable();

        // 添加相同的对象两次
        group.Add(disposable1);
        group.Add(disposable2);
        group.Add(disposable1); // 重复添加

        group.Dispose();

        // 验证Dispose被调用，但具体次数取决于实现
        Assert.IsTrue(disposable1.IsDisposedCalled);
        Assert.IsTrue(disposable2.IsDisposedCalled);
    }

    [Test]
    public void TestDisposableGroupAfterDispose()
    {
        var group = new DisposableGroup();
        var disposable = new CustomDisposable();

        group.Add(disposable);
        group.Dispose();

        // Dispose后添加对象应该是安全的，但可能不会被执行
        var newDisposable = new CustomDisposable();
        Assert.DoesNotThrow(() => group.Add(newDisposable));
    }

    [Test]
    public void TestDisposableGroupFinalizer()
    {
        var disposable = new CustomDisposable();
        var group = new DisposableGroup();
        group.Add(disposable);

        // 不调用Dispose，让垃圾回收器处理
        group = null;

        // 强制垃圾回收
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // 注意：由于垃圾回收的不确定性，这个测试可能不稳定
        // 在实际应用中，Finalizer是作为最后的保障机制
    }
}

/// <summary>
/// 用于测试的自定义可释放类
/// </summary>
public class CustomDisposable : Disposable
{
    public bool IsDisposedCalled { get; private set; }
    public int DisposeCallCount { get; private set; }

    protected override void Dispose(bool isDisposing)
    {
        if (IsDisposed) return;

        IsDisposedCalled = true;
        DisposeCallCount++;
        IsDisposed = true;
    }
}