using System;
using System.IO;
using System.Reflection;
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

    [Test]
    public void TestDisposableGroupDisposesAllChildrenWhenOneThrows()
    {
        var group = new DisposableGroup();
        var throwing = new ThrowingDisposable();
        var tracking = new CustomDisposable();
        group.Add(throwing);
        group.Add(tracking);

        var exception = Assert.Throws<AggregateException>(() => group.Dispose());

        Assert.Multiple(() =>
        {
            Assert.AreEqual(1, exception!.InnerExceptions.Count);
            Assert.That(exception.InnerExceptions[0], Is.TypeOf<InvalidOperationException>());
            Assert.AreEqual(1, throwing.DisposeCallCount);
            Assert.AreEqual(1, tracking.DisposeCallCount);
        });
        Assert.DoesNotThrow(() => group.Dispose());
    }

    [Test]
    public void TestDisposableGroupDisposesItemsAddedAfterDisposal()
    {
        var group = new DisposableGroup();
        var disposable = new CustomDisposable();
        group.Dispose();

        group.Add(disposable);

        Assert.AreEqual(1, disposable.DisposeCallCount);
    }

    [Test]
    public void TestDisposableGroupAddDeclaresNullableParameter()
    {
        var parameter = typeof(DisposableGroup).GetMethod(nameof(DisposableGroup.Add))!.GetParameters().Single();
        var nullability = new NullabilityInfoContext().Create(parameter);

        Assert.AreEqual(NullabilityState.Nullable, nullability.ReadState);
    }

    private sealed class ThrowingDisposable : IDisposable
    {
        public int DisposeCallCount { get; private set; }

        public void Dispose()
        {
            DisposeCallCount++;
            throw new InvalidOperationException("expected disposal failure");
        }
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
