using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleFramework.Utility;

namespace Test.Utility;

[TestFixture]
public class TestTaskUtil
{
    [Test]
    public async Task TestWaitUntilPredicateBecomesTrue()
    {
        var stopwatch = Stopwatch.StartNew();
        var predicateTriggered = false;

        var producer = Task.Run(async () =>
        {
            await Task.Delay(200);
            predicateTriggered = true;
        });

        await TaskUtil.WaitUntil(() => predicateTriggered, 5000, 50);
        await producer;

        Assert.That(stopwatch.Elapsed.TotalMilliseconds, Is.GreaterThanOrEqualTo(200).Within(50));
        Assert.IsTrue(predicateTriggered);
    }

    [Test]
    public async Task TestWaitUntilWithAlreadyTruePredicate()
    {
        var stopwatch = Stopwatch.StartNew();

        await TaskUtil.WaitUntil(() => true, 5000, 100);

        Assert.That(stopwatch.Elapsed.TotalMilliseconds, Is.LessThan(50));
    }

    [Test]
    public async Task TestWaitUntilWithCustomInterval()
    {
        var stopwatch = Stopwatch.StartNew();
        var counter = 0;

        var producer = Task.Run(async () =>
        {
            await Task.Delay(500);
            counter = 10;
        });

        await TaskUtil.WaitUntil(() => counter == 10, 5000, 50);
        await producer;

        Assert.That(stopwatch.Elapsed.TotalMilliseconds, Is.GreaterThanOrEqualTo(500).Within(50));
        Assert.AreEqual(10, counter);
    }

    [Test]
    public async Task TestWaitUntilWithLongerInterval()
    {
        var stopwatch = Stopwatch.StartNew();
        var predicateTriggered = false;

        var producer = Task.Run(async () =>
        {
            await Task.Delay(300);
            predicateTriggered = true;
        });

        await TaskUtil.WaitUntil(() => predicateTriggered, 5000, 150);
        await producer;

        Assert.That(stopwatch.Elapsed.TotalMilliseconds, Is.GreaterThanOrEqualTo(300).Within(100));
        Assert.IsTrue(predicateTriggered);
    }

    [Test]
    public async Task TestWaitUntilWithComplexPredicate()
    {
        var numbers = new int[5];
        var setCount = 0;

        var producer = Task.Run(async () =>
        {
            for (var i = 0; i < 5; i++)
            {
                await Task.Delay(100);
                numbers[i] = i + 1;
                setCount++;
            }
        });

        await TaskUtil.WaitUntil(() => setCount == 5, 5000, 50);
        await producer;

        for (var i = 0; i < 5; i++)
        {
            Assert.AreEqual(i + 1, numbers[i]);
        }
    }

    [Test]
    public void TestWaitUntilWithThrowingPredicate()
    {
        var attempts = 0;

        Assert.ThrowsAsync<InvalidOperationException>(
            () => TaskUtil.WaitUntil(() =>
            {
                attempts++;
                if (attempts < 3)
                {
                    throw new InvalidOperationException("测试异常");
                }

                return true;
            }, 5000, 50));

        Assert.AreEqual(1, attempts);
    }

    [Test]
    public async Task TestWaitUntilWithTimeout()
    {
        var stopwatch = Stopwatch.StartNew();

        await TaskUtil.WaitUntil(() => false, 200, 50);

        Assert.That(stopwatch.Elapsed.TotalMilliseconds, Is.GreaterThanOrEqualTo(200).Within(50));
    }

    [Test]
    public async Task TestWaitUntilMultipleCalls()
    {
        var state1 = false;
        var state2 = false;

        var producer = Task.Run(async () =>
        {
            await Task.Delay(100);
            state1 = true;
            await Task.Delay(100);
            state2 = true;
        });

        await TaskUtil.WaitUntil(() => state1, 5000, 30);
        Assert.IsTrue(state1);
        Assert.IsFalse(state2);

        await TaskUtil.WaitUntil(() => state2, 5000, 30);
        await producer;

        Assert.IsTrue(state1);
        Assert.IsTrue(state2);
    }

    [Test]
    public async Task TestWaitUntilConcurrentCalls()
    {
        var counter = 0;
        var tasks = new List<Task>();

        for (var i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(() => TaskUtil.WaitUntil(() => counter >= 3, 5000, 50)));
        }

        await Task.Delay(200);
        Interlocked.Add(ref counter, 3);

        await Task.WhenAll(tasks);

        Assert.AreEqual(3, counter);
    }
}
