using System;
using System.IO;
using NUnit.Framework;
using SimpleFramework.Utility;

namespace Test.Utility;

[TestFixture]
public class TestDisposable
{
    [Test]
    public void TestCustomDisposableDisposeIsIdempotent()
    {
        var disposable = new CustomDisposable();

        disposable.Dispose();
        disposable.Dispose();

        Assert.IsTrue(disposable.IsDisposedCalled);
        Assert.AreEqual(1, disposable.DisposeCallCount);
    }

    [Test]
    public void TestDisposableGroupDisposesChildren()
    {
        var group = new DisposableGroup();
        var disposable1 = new CustomDisposable();
        var disposable2 = new CustomDisposable();

        group.Add(disposable1);
        group.Add(disposable2);
        group.Dispose();

        Assert.IsTrue(disposable1.IsDisposedCalled);
        Assert.IsTrue(disposable2.IsDisposedCalled);
    }

    [Test]
    public void TestDisposableGroupDisposeIsIdempotent()
    {
        var group = new DisposableGroup();
        var disposable = new CustomDisposable();
        group.Add(disposable);

        group.Dispose();
        group.Dispose();

        Assert.AreEqual(1, disposable.DisposeCallCount);
    }

    [Test]
    public void TestDisposableGroupAllowsNullAndMixedDisposables()
    {
        var group = new DisposableGroup();
        var disposable = new CustomDisposable();

        Assert.DoesNotThrow(() => group.Add(null!));
        group.Add(disposable);
        group.Add(new MemoryStream());

        Assert.DoesNotThrow(() => group.Dispose());
        Assert.IsTrue(disposable.IsDisposedCalled);
    }

    [Test]
    public void TestDisposableGroupAfterDisposeDoesNotThrow()
    {
        var group = new DisposableGroup();
        group.Dispose();

        Assert.DoesNotThrow(() => group.Add(new CustomDisposable()));
    }
}

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
