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

    [Test]
    public void TestWaitUntilHonorsCancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(
            () => TaskUtil.WaitUntil(() => false, 1000, 10, source.Token));
    }

    [Test]
    public void TestWaitUntilRejectsInvalidArguments()
    {
        var attempts = 0;
        Exception? nullPredicate = null;
        Exception? negativeTimeout = null;
        Exception? zeroInterval = null;
        Exception? infiniteInterval = null;

        Assert.Multiple(() =>
        {
            nullPredicate = Assert.CatchAsync<Exception>(() => TaskUtil.WaitUntil(null!));
            negativeTimeout = Assert.CatchAsync<Exception>(
                () => TaskUtil.WaitUntil(() => ++attempts > 0, -1, 1));
            zeroInterval = Assert.CatchAsync<Exception>(
                () => TaskUtil.WaitUntil(() => ++attempts > 0, 1, 0));
            infiniteInterval = Assert.CatchAsync<Exception>(
                () => TaskUtil.WaitUntil(() => ++attempts > 0, 1, -1));

            Assert.That(nullPredicate, Is.TypeOf<ArgumentNullException>());
            Assert.AreEqual("predict", (nullPredicate as ArgumentNullException)?.ParamName);
            Assert.That(negativeTimeout, Is.TypeOf<ArgumentOutOfRangeException>());
            Assert.AreEqual("timeout", (negativeTimeout as ArgumentOutOfRangeException)?.ParamName);
            Assert.That(zeroInterval, Is.TypeOf<ArgumentOutOfRangeException>());
            Assert.AreEqual("interval", (zeroInterval as ArgumentOutOfRangeException)?.ParamName);
            Assert.That(infiniteInterval, Is.TypeOf<ArgumentOutOfRangeException>());
            Assert.AreEqual("interval", (infiniteInterval as ArgumentOutOfRangeException)?.ParamName);
        });
        Assert.AreEqual(0, attempts);
    }

    [Test]
    [Timeout(2000)]
    public async Task TestWaitUntilCapsDelayToRemainingTimeout()
    {
        using var safety = new CancellationTokenSource(500);
        var stopwatch = Stopwatch.StartNew();

        await TaskUtil.WaitUntil(() => false, 30, int.MaxValue, safety.Token);

        Assert.That(stopwatch.ElapsedMilliseconds, Is.InRange(20, 400));
    }

    [Test]
    [Timeout(2000)]
    public async Task TestWaitUntilCancellationInterruptsActiveDelay()
    {
        using var source = new CancellationTokenSource();
        var enteredPolling = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = TaskUtil.WaitUntil(() =>
        {
            enteredPolling.TrySetResult();
            return false;
        }, 5000, 5000, source.Token);

        await enteredPolling.Task;
        source.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () => await waiting);
    }
}
