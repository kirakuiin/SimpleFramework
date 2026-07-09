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
        var predicateTriggered = false;
        var producer = Task.Run(async () =>
        {
            await Task.Delay(30);
            predicateTriggered = true;
        });

        await TaskUtil.WaitUntil(() => predicateTriggered, 1000, 5);
        await producer;

        Assert.IsTrue(predicateTriggered);
    }

    [Test]
    public async Task TestWaitUntilWithAlreadyTruePredicate()
    {
        var attempts = 0;

        await TaskUtil.WaitUntil(() => ++attempts == 1, 1000, 100);

        Assert.AreEqual(1, attempts);
    }

    [Test]
    public void TestWaitUntilWithThrowingPredicate()
    {
        var attempts = 0;

        Assert.ThrowsAsync<InvalidOperationException>(
            () => TaskUtil.WaitUntil(() =>
            {
                attempts++;
                throw new InvalidOperationException("测试异常");
            }, 1000, 5));

        Assert.AreEqual(1, attempts);
    }

    [Test]
    public async Task TestWaitUntilWithTimeout()
    {
        var stopwatch = Stopwatch.StartNew();

        await TaskUtil.WaitUntil(() => false, 30, 5);

        Assert.That(stopwatch.Elapsed.TotalMilliseconds, Is.GreaterThanOrEqualTo(30));
    }

    [Test]
    public async Task TestWaitUntilConcurrentCalls()
    {
        var counter = 0;
        var tasks = new List<Task>();

        for (var i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(() => TaskUtil.WaitUntil(() => counter >= 3, 1000, 5)));
        }

        await Task.Delay(30);
        Interlocked.Add(ref counter, 3);
        await Task.WhenAll(tasks);

        Assert.AreEqual(3, counter);
    }
}
