using System;
using NUnit.Framework;
using SimpleFramework.Maths;

namespace Test.Maths;

[TestFixture]
public class TestMatrix2D
{
    [Test]
    public void TestCreateScale()
    {
        var scale = Matrix2D.CreateScale(2.0, 3.0);
        Assert.That(scale.M11, Is.EqualTo(2.0));
        Assert.That(scale.M22, Is.EqualTo(3.0));
        Assert.That(scale.M12, Is.EqualTo(0.0));
        Assert.That(scale.M21, Is.EqualTo(0.0));
    }

    [Test]
    public void TestUniformScale()
    {
        var scale = Matrix2D.CreateScale(2.0);
        Assert.That(scale.M11, Is.EqualTo(2.0));
        Assert.That(scale.M22, Is.EqualTo(2.0));
        Assert.That(scale.M12, Is.EqualTo(0.0));
        Assert.That(scale.M21, Is.EqualTo(0.0));
    }

    [Test]
    public void TestDotProduct()
    {
        var m1 = new Matrix2D(1, 2, 3, 4);
        var m2 = new Matrix2D(5, 6, 7, 8);
        Assert.That(m1.Dot(m2), Is.EqualTo(70));
    }

    [Test]
    public void TestInverse()
    {
        var matrix = new Matrix2D(4, 7, 2, 6);
        var inverse = matrix.Inverse();
        var identity = matrix * inverse;

        Assert.That(identity.M11, Is.EqualTo(1).Within(1e-12));
        Assert.That(identity.M12, Is.EqualTo(0).Within(1e-12));
        Assert.That(identity.M21, Is.EqualTo(0).Within(1e-12));
        Assert.That(identity.M22, Is.EqualTo(1).Within(1e-12));
    }

    [Test]
    public void TestNearSingularMatrixThrows()
    {
        var matrix = new Matrix2D(1, 1, 1, 1 + 1e-14);

        Assert.Throws<InvalidOperationException>(() => matrix.Inverse());
    }

    [Test]
    public void TestNonFiniteMatrixThrows()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(
                () => new Matrix2D(double.NaN, 0, 0, 1).Inverse());
            Assert.Throws<InvalidOperationException>(
                () => new Matrix2D(double.PositiveInfinity, 0, 0, 1).Inverse());
        });
    }
}
