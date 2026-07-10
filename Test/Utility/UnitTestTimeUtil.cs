using System;
using NUnit.Framework;
using SimpleFramework.Utility;

namespace Test.Utility;

[TestFixture]
public class TestTimeUtil
{
    [Test]
    public void TestGetUtcMilliseconds()
    {
        // 获取当前UTC毫秒数
        long utcMilliseconds = TimeUtil.GetUtcMilliseconds();

        // 验证返回的毫秒数是合理的（应该大于2020年1月1日的Unix时间戳）
        Assert.Greater(utcMilliseconds, 1577836800000); // 2020-01-01 00:00:00 UTC

        // 再次调用，时间应该是递增的（允许微小误差）
        System.Threading.Thread.Sleep(10);
        long utcMilliseconds2 = TimeUtil.GetUtcMilliseconds();
        Assert.GreaterOrEqual(utcMilliseconds2, utcMilliseconds);
    }

    [Test]
    public void TestGetUtcTimeSpanByNow()
    {
        // 获取当前时间戳
        long currentTimestamp = TimeUtil.GetUtcMilliseconds();

        // 测试过去的时间（1秒前）
        long pastTimestamp = currentTimestamp - 1000;
        TimeSpan pastTimeSpan = TimeUtil.GetUtcTimeSpanByNow(pastTimestamp);
        Assert.Greater(pastTimeSpan.TotalMilliseconds, 900); // 允许一些误差
        Assert.Less(pastTimeSpan.TotalMilliseconds, 1100);
        Assert.IsTrue(pastTimeSpan.TotalMilliseconds > 0);

        // 测试未来的时间（1秒后）
        long futureTimestamp = currentTimestamp + 1000;
        TimeSpan futureTimeSpan = TimeUtil.GetUtcTimeSpanByNow(futureTimestamp);
        Assert.Greater(futureTimeSpan.TotalMilliseconds, -1100);
        Assert.Less(futureTimeSpan.TotalMilliseconds, -900);
        Assert.IsTrue(futureTimeSpan.TotalMilliseconds < 0);

        // 测试当前时间（误差在合理范围内）
        TimeSpan currentTimeSpan = TimeUtil.GetUtcTimeSpanByNow(currentTimestamp);
        Assert.Less(Math.Abs(currentTimeSpan.TotalMilliseconds), 100); // 允许100ms误差
    }

    [Test]
    public void TestGetUtcTimeSpanByNowWithKnownValue()
    {
        // 使用一个已知的时间戳进行测试
        // 2023-01-01 00:00:00 UTC的Unix时间戳
        long knownTimestamp = 1672531200000;

        TimeSpan timeSpan = TimeUtil.GetUtcTimeSpanByNow(knownTimestamp);

        // 验证时间差是正数且合理（这个时间在过去）
        Assert.Greater(timeSpan.TotalMilliseconds, 0);
        Assert.Greater(timeSpan.TotalDays, 365); // 至少过了一年多
    }

    [Test]
    public void TestToMsThrowsOnOverflow()
    {
        Assert.Throws<OverflowException>(() => TimeUtil.ToMs(int.MaxValue));
    }
}
