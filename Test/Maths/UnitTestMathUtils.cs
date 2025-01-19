using NUnit.Framework;
using SimpleFramework.Maths;

namespace Test.Maths;

[TestFixture]
public class TestMathUtils
{
    [Test]
    public void TestToRadians()
    {
        Assert.That(MathUtils.ToRadians(180.0), Is.EqualTo(System.Math.PI).Within(1e-5));
        Assert.That(MathUtils.ToRadians(90.0), Is.EqualTo(System.Math.PI/2).Within(1e-5));
        Assert.That(MathUtils.ToRadians(360.0f), Is.EqualTo(2*System.Math.PI).Within(1e-5));
    }

    [Test]
    public void TestToDegrees()
    {
        Assert.That(MathUtils.ToDegrees(System.Math.PI), Is.EqualTo(180.0).Within(1e-10));
        Assert.That(MathUtils.ToDegrees(System.Math.PI/2), Is.EqualTo(90.0).Within(1e-10));
        Assert.That(MathUtils.ToDegrees(2*System.Math.PI), Is.EqualTo(360.0).Within(1e-10));
    }
} 