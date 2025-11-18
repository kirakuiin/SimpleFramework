using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleFramework.Utility;

namespace Test.Utility;

[TestFixture]
public class TestTaskTool
{
    [Test]
    public async Task TestWaitUntilPredicateBecomesTrue()
    {
        var startTime = DateTime.Now;
        var predicateTriggered = false;

        // 启动一个任务在200ms后设置predicate为true
        Task.Run(async () =>
        {
            await Task.Delay(200);
            predicateTriggered = true;
        });

        // 等待谓词变为true
        await TaskTool.WaitUntil(() => predicateTriggered, 5000, 50);

        var elapsed = DateTime.Now - startTime;

        // 验证等待时间合理（应该在200ms左右）
        Assert.That(elapsed.TotalMilliseconds, Is.GreaterThanOrEqualTo(200).Within(50));
        Assert.IsTrue(predicateTriggered);
    }

    [Test]
    public async Task TestWaitUntilWithAlreadyTruePredicate()
    {
        var startTime = DateTime.Now;

        // 谓词已经为true，应该立即返回
        await TaskTool.WaitUntil(() => true, 5000, 100);

        var elapsed = DateTime.Now - startTime;

        // 验证几乎立即返回
        Assert.That(elapsed.TotalMilliseconds, Is.LessThan(50));
    }

    [Test]
    public async Task TestWaitUntilWithCustomInterval()
    {
        var startTime = DateTime.Now;
        var counter = 0;

        // 使用自定义间隔
        Task.Run(async () =>
        {
            await Task.Delay(500);
            counter = 10;
        });

        await TaskTool.WaitUntil(() => counter == 10, 5000, 50);

        var elapsed = DateTime.Now - startTime;

        // 验证等待时间合理
        Assert.That(elapsed.TotalMilliseconds, Is.GreaterThanOrEqualTo(500).Within(50));
        Assert.AreEqual(10, counter);
    }

    [Test]
    public async Task TestWaitUntilWithLongerInterval()
    {
        var startTime = DateTime.Now;
        var predicateTriggered = false;

        Task.Run(async () =>
        {
            await Task.Delay(300);
            predicateTriggered = true;
        });

        // 使用较长的检查间隔
        await TaskTool.WaitUntil(() => predicateTriggered, 5000, 150);

        var elapsed = DateTime.Now - startTime;

        // 验证等待时间合理，可能由于间隔原因会有一些延迟
        Assert.That(elapsed.TotalMilliseconds, Is.GreaterThanOrEqualTo(300).Within(100));
        Assert.IsTrue(predicateTriggered);
    }

    [Test]
    public async Task TestWaitUntilWithComplexPredicate()
    {
        var numbers = new int[5];
        var setCount = 0;

        Task.Run(async () =>
        {
            for (int i = 0; i < 5; i++)
            {
                await Task.Delay(100);
                numbers[i] = i + 1;
                setCount++;
            }
        });

        // 等待所有元素被设置
        await TaskTool.WaitUntil(() => setCount == 5, 5000, 50);

        // 验证所有数字都被正确设置
        for (int i = 0; i < 5; i++)
        {
            Assert.AreEqual(i + 1, numbers[i]);
        }
    }

    [Test]
    public async Task TestWaitUntilWithThrowingPredicate()
    {
        var attempts = 0;

        // 谓词在前几次会抛出异常，之后返回true
        Assert.ThrowsAsync<InvalidOperationException>(
        () => TaskTool.WaitUntil(() =>
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
        var startTime = DateTime.Now;

        // 创建一个永远不会为真的谓词，使用短超时
        await TaskTool.WaitUntil(() => false, 200, 50);

        var elapsed = DateTime.Now - startTime;

        // 验证超时机制工作正常，应该在超时时间左右返回
        Assert.That(elapsed.TotalMilliseconds, Is.GreaterThanOrEqualTo(200).Within(50));
    }

    [Test]
    public async Task TestWaitUntilMultipleCalls()
    {
        var state1 = false;
        var state2 = false;

        Task.Run(async () =>
        {
            await Task.Delay(100);
            state1 = true;
            await Task.Delay(100);
            state2 = true;
        });

        // 第一次等待
        await TaskTool.WaitUntil(() => state1, 5000, 30);
        Assert.IsTrue(state1);
        Assert.IsFalse(state2);

        // 第二次等待
        await TaskTool.WaitUntil(() => state2, 5000, 30);
        Assert.IsTrue(state1);
        Assert.IsTrue(state2);
    }

    [Test]
    public async Task TestWaitUntilConcurrentCalls()
    {
        var counter = 0;
        var tasks = new List<Task>();

        // 创建多个并发等待任务
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await TaskTool.WaitUntil(() => counter >= 3, 5000, 50);
            }));
        }

        // 在一段时间后满足条件
        await Task.Delay(200);
        Interlocked.Add(ref counter, 3);

        // 等待所有任务完成
        await Task.WhenAll(tasks);

        // 验证所有任务都成功完成
        Assert.AreEqual(3, counter);
    }
}