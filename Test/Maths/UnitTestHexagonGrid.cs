using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SimpleFramework.Maths;

namespace Test.Maths;


[TestFixture]
public class TestHexagonGrid
{
    [Test]
    public void TestHexArithmetic()
    {
        var hex1 = new Hex(1, -3, 2);
        var hex2 = new Hex(3, -7, 4);
        
        Assert.That(hex1 + hex2, Is.EqualTo(new Hex(4, -10, 6)));
        Assert.That(hex1 - hex2, Is.EqualTo(new Hex(-2, 4, -2)));
    }

    [Test]
    public void TestHexNeighbor()
    {
        var hex = new Hex(1, -2, 1);
        Assert.That(hex.GetNeighbor(HexDirection.SouthWest), Is.EqualTo(new Hex(1, -3, 2)));
    }

    [Test]
    public void TestHexDiagonal()
    {
        var hex = new Hex(1, -2, 1);
        Assert.That(hex.DiagonalNeighbor(HexDirection.West), Is.EqualTo(new Hex(-1, -1, 2)));
    }

    [Test]
    public void TestHexDistance()
    {
        var hex = new Hex(3, -7, 4);
        Assert.That(hex.Distance(new Hex(0, 0, 0)), Is.EqualTo(7));
    }

    [Test]
    public void TestHexRotation()
    {
        var hex = new Hex(1, -3, 2);
        Assert.That(hex.GetRotateRight(), Is.EqualTo(new Hex(3, -2, -1)));
        Assert.That(hex.GetRotateLeft(), Is.EqualTo(new Hex(-2, -1, 3)));
    }

    [Test]
    public void TestHexRound()
    {
        var a = new FractionalHex(0.0, 0.0, 0.0);
        var b = new FractionalHex(1.0, -1.0, 0.0);
        var c = new FractionalHex(0.0, -1.0, 1.0);

        // 测试插值和四舍五入
        var lerp = HexExtensions.HexLerp(a, new FractionalHex(10.0, -20.0, 10.0), 0.5);
        Assert.That(lerp.HexRound(), Is.EqualTo(new Hex(5, -10, 5)));

        // 测试边界情况
        Assert.That(HexExtensions.HexLerp(a, b, 0.499).HexRound(), Is.EqualTo(a.HexRound()));
        Assert.That(HexExtensions.HexLerp(a, b, 0.501).HexRound(), Is.EqualTo(b.HexRound()));

        // 测试三点插值
        var threeWayLerp = new FractionalHex(
            a.Q * 0.4 + b.Q * 0.3 + c.Q * 0.3,
            a.R * 0.4 + b.R * 0.3 + c.R * 0.3,
            a.S * 0.4 + b.S * 0.3 + c.S * 0.3);
        Assert.That(threeWayLerp.HexRound(), Is.EqualTo(a.HexRound()));
    }

    [Test]
    public void TestHexLineDraw()
    {
        var expected = new List<Hex> {
            new Hex(0, 0, 0), new Hex(0, -1, 1), new Hex(0, -2, 2),
            new Hex(1, -3, 2), new Hex(1, -4, 3), new Hex(1, -5, 4)
        };
        var actual = HexExtensions.HexLineDraw(new Hex(0, 0, 0), new Hex(1, -5, 4));
        
        Assert.That(actual.Count, Is.EqualTo(expected.Count));
        for (int i = 0; i < actual.Count; i++)
        {
            Assert.That(actual[i], Is.EqualTo(expected[i]));
        }
    }

    [Test]
    public void TestLayout()
    {
        var hex = new Hex(3, 4, -7);
        var size = new Point(10.0, 15.0);
        var origin = new Point(35.0, 71.0);

        // 测试平边朝上的布局
        var flat = new HexLayout(HexOrientation.Flat, size, origin);
        var pixel = flat.HexToPixel(hex);
        Assert.That(flat.PixelToHex(pixel), Is.EqualTo(hex));

        // 测试尖角朝上的布局
        var pointy = new HexLayout(HexOrientation.Pointy, size, origin);
        pixel = pointy.HexToPixel(hex);
        Assert.That(pointy.PixelToHex(pixel), Is.EqualTo(hex));
    }

    [Test]
    public void TestLayoutUsesPerAxisScaleAfterOrientation()
    {
        var hex = new Hex(3, 4, -7);
        var size = new Point(10.0, 15.0);
        var origin = new Point(35.0, 71.0);
        var layout = new HexLayout(HexOrientation.Pointy, size, origin);
        var expected = new Point(
            (Math.Sqrt(3.0) * hex.Q + Math.Sqrt(3.0) / 2.0 * hex.R) * size.X + origin.X,
            (3.0 / 2.0 * hex.R) * size.Y + origin.Y);

        var pixel = layout.HexToPixel(hex);

        Assert.That(pixel.X, Is.EqualTo(expected.X).Within(1e-5));
        Assert.That(pixel.Y, Is.EqualTo(expected.Y).Within(1e-5));
        Assert.That(layout.PixelToHex(expected), Is.EqualTo(hex));
    }

    [Test]
    public void TestFractionalHexRejectsCoordinatesThatDoNotSumToZero()
    {
        Assert.Throws<ArgumentException>(() => new FractionalHex(0.2, 0.2, 0.09));
    }

    [Test]
    public void TestHexCornerOffset()
    {
        var layout = new HexLayout(HexOrientation.Flat, new Point(10.0, 10.0), new Point(0, 0));
        
        // 测试东方方向的偏移
        var offset = layout.HexCornerOffset(HexDirection.East);
        Assert.That(offset.X, Is.EqualTo(10).Within(1e-5));
        Assert.That(offset.Y, Is.EqualTo(0).Within(1e-5));
    }

    [Test]
    public void TestPolygonCorners()
    {
        var size = new Point(10.0, 10.0);
        var origin = new Point(0.0, 0.0);
        var hex = new Hex(0, 0, 0);

        // 测试尖角朝上布局
        var pointyLayout = new HexLayout(HexOrientation.Pointy, size, origin);
        var pointyCorners = pointyLayout.PolygonCorners(hex);
        
        Assert.That(pointyCorners.Count, Is.EqualTo(6));
        // 验证上部顶点
        Assert.That(pointyCorners[5].Y, Is.EqualTo(size.Y).Within(1e-5));
        Assert.That(pointyCorners[5].X, Is.EqualTo(0).Within(1e-5));

        // 测试平边朝上布局
        var flatLayout = new HexLayout(HexOrientation.Flat, size, origin);
        var flatCorners = flatLayout.PolygonCorners(hex);
        
        // 验证右上角顶点（右上方的尖角）
        Assert.That(flatCorners[5].X, Is.EqualTo(size.X/2).Within(1e-5));
        Assert.That(flatCorners[5].Y, Is.EqualTo(size.Y*Math.Sqrt(3)/2).Within(1e-5));
    }

    [Test]
    public void TestHexRejectsOverflowInvalidCoordinates()
    {
        Assert.Throws<ArgumentException>(() => new Hex(int.MaxValue, int.MaxValue, 2));
    }

    [Test]
    public void TestHexArithmeticThrowsOnOverflow()
    {
        var left = new Hex(int.MaxValue, -int.MaxValue, 0);
        var right = new Hex(1, -1, 0);

        Assert.Throws<OverflowException>(() => _ = left + right);
    }

    [Test]
    public void TestFractionalHexRejectsNonFiniteCoordinates()
    {
        Assert.Multiple(() =>
        {
            var nan = Assert.Throws<ArgumentOutOfRangeException>(() => new FractionalHex(double.NaN, 0, 0));
            var infinity = Assert.Throws<ArgumentOutOfRangeException>(
                () => new FractionalHex(0, double.PositiveInfinity, double.NegativeInfinity));
            Assert.AreEqual("q", nan?.ParamName);
            Assert.AreEqual("r", infinity?.ParamName);
        });
    }

    [Test]
    public void TestOrientationPresetsAreReadOnlyProperties()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;

        Assert.Multiple(() =>
        {
            Assert.IsNull(typeof(HexOrientation).GetField(nameof(HexOrientation.Pointy), flags));
            Assert.IsNull(typeof(HexOrientation).GetField(nameof(HexOrientation.Flat), flags));
            Assert.IsFalse(typeof(HexOrientation).GetProperty(nameof(HexOrientation.Pointy), flags)?.CanWrite);
            Assert.IsFalse(typeof(HexOrientation).GetProperty(nameof(HexOrientation.Flat), flags)?.CanWrite);
        });
    }

    [Test]
    public void TestLayoutRejectsZeroOrNonFiniteSize()
    {
        Assert.Multiple(() =>
        {
            var zero = Assert.Throws<ArgumentOutOfRangeException>(() => new HexLayout(
                HexOrientation.Pointy, new Point(0, 1), new Point(0, 0)));
            var nan = Assert.Throws<ArgumentOutOfRangeException>(() => new HexLayout(
                HexOrientation.Pointy, new Point(1, double.NaN), new Point(0, 0)));
            Assert.AreEqual("size", zero?.ParamName);
            Assert.AreEqual("size", nan?.ParamName);
        });
    }

    [Test]
    public void TestLayoutRejectsNonFiniteOrigin()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new HexLayout(
            HexOrientation.Pointy,
            new Point(1, 1),
            new Point(double.NaN, double.PositiveInfinity)));

        Assert.AreEqual("origin", exception!.ParamName);
    }

    [Test]
    public void TestDirectionMethodsNameInvalidDirection()
    {
        var invalid = (HexDirection)6;
        var hex = new Hex(0, 0, 0);
        var layout = new HexLayout(HexOrientation.Pointy, new Point(1, 1), new Point(0, 0));

        Assert.Multiple(() =>
        {
            Assert.AreEqual("direction",
                Assert.Throws<ArgumentOutOfRangeException>(() => hex.GetNeighbor(invalid))?.ParamName);
            Assert.AreEqual("direction",
                Assert.Throws<ArgumentOutOfRangeException>(() => hex.DiagonalNeighbor(invalid))?.ParamName);
            Assert.AreEqual("direction",
                Assert.Throws<ArgumentOutOfRangeException>(() => layout.HexCornerOffset(invalid))?.ParamName);
        });
    }

    [Test]
    public void TestOrientationRejectsNonFiniteAngle()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new HexOrientation(1, 0, 0, 1, double.NaN));

        Assert.AreEqual("startAngle", exception?.ParamName);
    }

    [Test]
    public void TestLayoutRejectsDefaultOrNonFiniteOrientation()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new HexLayout(default, new Point(1, 1), new Point(0, 0)));

        Assert.AreEqual("hexOrientation", exception?.ParamName);
    }

    [Test]
    public void TestLayoutRejectsNonRepresentableReciprocal()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new HexLayout(
            HexOrientation.Pointy,
            new Point(double.Epsilon, 1),
            new Point(0, 0)));

        Assert.AreEqual("size", exception?.ParamName);
    }

    [Test]
    public void TestNegativeScaleMirrorsAndRoundTrips()
    {
        var hex = new Hex(3, 4, -7);
        var positive = new HexLayout(HexOrientation.Pointy, new Point(10, 15), new Point(0, 0));
        var mirrored = new HexLayout(HexOrientation.Pointy, new Point(-10, 15), new Point(0, 0));
        var positivePixel = positive.HexToPixel(hex);
        var mirroredPixel = mirrored.HexToPixel(hex);

        Assert.Multiple(() =>
        {
            Assert.AreEqual(-positivePixel.X, mirroredPixel.X, 1e-10);
            Assert.AreEqual(positivePixel.Y, mirroredPixel.Y, 1e-10);
            Assert.AreEqual(hex, mirrored.PixelToHex(mirroredPixel));
        });
    }

    [Test]
    public void TestHexRoundRejectsUnrepresentableFiniteCoordinates()
    {
        var fractional = new FractionalHex(double.MaxValue, -double.MaxValue, 0);

        Assert.Throws<OverflowException>(() => fractional.HexRound());
    }
} 
