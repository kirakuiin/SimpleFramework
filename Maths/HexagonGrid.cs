// 本代码基于 Red Blob Games 的六边形网格实现
// 原始代码地址: http://www.redblobgames.com/grids/hexagons/

namespace SimpleFramework.Maths;

using System;
using System.Collections.Generic;


/// <summary>
/// 定义六边形网格的布局方向和变换矩阵。
/// </summary>
public readonly struct HexOrientation
{
    /// <summary>前向变换矩阵</summary>
    public Matrix2D Forward { get; }
    /// <summary>逆向变换矩阵</summary>
    public Matrix2D Inverse { get; }
    /// <summary>起始角度(degree)</summary>
    public double StartAngle { get; }

    /// <summary>使用前向矩阵元素和起始角度创建布局方向。</summary>
    /// <param name="f0">前向矩阵第一行第一列。</param>
    /// <param name="f1">前向矩阵第一行第二列。</param>
    /// <param name="f2">前向矩阵第二行第一列。</param>
    /// <param name="f3">前向矩阵第二行第二列。</param>
    /// <param name="startAngle">第一个顶点相对 X 轴的角度。</param>
    /// <exception cref="InvalidOperationException">前向矩阵不可逆。</exception>
    public HexOrientation(double f0, double f1, double f2, double f3, double startAngle)
    {
        Forward = new Matrix2D(f0, f1, f2, f3);
        Inverse = Forward.Inverse();
        StartAngle = startAngle;
    }
    
    /// <summary>尖角朝上的布局</summary>
    public static HexOrientation Pointy { get; } = new(
        Math.Sqrt(3.0), Math.Sqrt(3.0) / 2.0, 0.0, 3.0 / 2.0,
        30);

    /// <summary>平边朝上的布局</summary>
    public static HexOrientation Flat { get; } = new(
        3.0 / 2.0, 0.0, Math.Sqrt(3.0) / 2.0, Math.Sqrt(3.0),
        0);
}


/// <summary>
/// 六边形网格的方向枚举。
/// 从东方开始，按顺时针方向排列。
/// <para>
/// 在<see cref="HexOrientation.Pointy"/>朝向中，East指的是东。
/// </para>
/// 在<see cref="HexOrientation.Flat"/>朝向中，East指的是东南。
/// </summary>
public enum HexDirection
{
    /// <summary>东方(→)</summary>
    East = 0,
    /// <summary>东南方(↘)</summary>
    SouthEast = 1,
    /// <summary>西南方(↙)</summary>
    SouthWest = 2,
    /// <summary>西方(←)</summary>
    West = 3,
    /// <summary>西北方(↖)</summary>
    NorthWest = 4,
    /// <summary>东北方(↗)</summary>
    NorthEast = 5
}


/// <summary>
/// 表示六边形网格中的一个单元格，使用立方坐标系(q,r,s)。
/// 其中 q + r + s = 0。
/// </summary>
public readonly struct Hex
{
    /// <summary>获取 q 轴坐标。</summary>
    public int Q { get; }
    /// <summary>获取 r 轴坐标。</summary>
    public int R { get; }
    /// <summary>获取 s 轴坐标。</summary>
    public int S { get; }

    /// <summary>
    /// 使用立方坐标初始化一个六边形单元格。
    /// </summary>
    /// <param name="q">q轴坐标</param>
    /// <param name="r">r轴坐标</param>
    /// <param name="s">s轴坐标</param>
    /// <exception cref="ArgumentException">当q + r + s != 0时抛出</exception>
    public Hex(int q, int r, int s)
    {
        Q = q;
        R = r;
        S = s;
        if ((long)q + r + s != 0) throw new ArgumentException("q + r + s must be 0");
    }

    /// <summary>
    /// 将两个六边形坐标相加。
    /// </summary>
    /// <exception cref="OverflowException">任一坐标运算溢出。</exception>
    public static Hex operator +(Hex a, Hex b)
    {
        checked
        {
            return new Hex(a.Q + b.Q, a.R + b.R, a.S + b.S);
        }
    }

    /// <summary>
    /// 将两个六边形坐标相减。
    /// </summary>
    /// <exception cref="OverflowException">任一坐标运算溢出。</exception>
    public static Hex operator -(Hex a, Hex b)
    {
        checked
        {
            return new Hex(a.Q - b.Q, a.R - b.R, a.S - b.S);
        }
    }

    /// <summary>
    /// 将六边形坐标乘以一个系数。
    /// </summary>
    /// <exception cref="OverflowException">任一坐标运算溢出。</exception>
    public static Hex operator *(Hex a, int k)
    {
        checked
        {
            return new Hex(a.Q * k, a.R * k, a.S * k);
        }
    }

    /// <summary>
    /// 将六边形坐标乘以一个系数。
    /// </summary>
    /// <exception cref="OverflowException">任一坐标运算溢出。</exception>
    public static Hex operator *(int k, Hex a)
    {
        return a * k;
    }
    
    /// <summary>
    /// 计算从原点(0,0,0)到当前六边形的距离。
    /// </summary>
    /// <exception cref="OverflowException">距离超出 <see cref="int"/> 范围。</exception>
    public int Length()
    {
        return checked((int)((Math.Abs((long)Q) + Math.Abs((long)R) + Math.Abs((long)S)) / 2));
    }
}


/// <summary>
/// 表示具有浮点数坐标的六边形，用于插值计算。
/// </summary>
public readonly struct FractionalHex
{
    /// <summary>获取 q 轴坐标。</summary>
    public double Q { get; }
    /// <summary>获取 r 轴坐标。</summary>
    public double R { get; }
    /// <summary>获取 s 轴坐标。</summary>
    public double S { get; }

    /// <summary>
    /// 使用浮点坐标初始化一个六边形单元格。
    /// </summary>
    /// <param name="q">q 轴坐标。</param>
    /// <param name="r">r 轴坐标。</param>
    /// <param name="s">s 轴坐标。</param>
    /// <exception cref="ArgumentOutOfRangeException">任一坐标不是有限数值。</exception>
    /// <exception cref="ArgumentException">三个有限坐标之和不在零的允许误差内。</exception>
    public FractionalHex(double q, double r, double s)
    {
        if (!double.IsFinite(q)) throw new ArgumentOutOfRangeException(nameof(q), "坐标必须是有限数值。");
        if (!double.IsFinite(r)) throw new ArgumentOutOfRangeException(nameof(r), "坐标必须是有限数值。");
        if (!double.IsFinite(s)) throw new ArgumentOutOfRangeException(nameof(s), "坐标必须是有限数值。");
        Q = q;
        R = r;
        S = s;
        if (Math.Abs(q + r + s) > 1e-6) throw new ArgumentException("q + r + s must be 0");
    }
}


/// <summary>
/// 定义六边形网格的布局，包括方向、大小和原点。
/// <para>
/// 通过这个类，六边形不再是抽象的数学模型，而是和现实世界的坐标相连。
/// </para>
/// </summary>
public readonly struct HexLayout
{
    private static readonly List<HexDirection> Directions = new()
    {
        HexDirection.East, HexDirection.SouthEast, HexDirection.SouthWest,
        HexDirection.West, HexDirection.NorthWest, HexDirection.NorthEast,
    };
    
    /// <summary>
    /// 六边形的朝向
    /// </summary>
    public HexOrientation HexOrientation { get; }

    /// <summary>
    /// 原点
    /// </summary>
    public Point Origin { get; }
    
    /// <summary>
    /// 六边形的大小
    /// </summary>
    public Point Size { get; }

    /// <summary>
    /// 完整的变换矩阵，包含了朝向、缩放和平移
    /// </summary>
    private readonly Matrix2D _transformMatrix;
    
    /// <summary>
    /// 逆变换矩阵
    /// </summary>
    private readonly Matrix2D _inverseMatrix;

    /// <summary>
    /// 初始化一个六边形网格布局。
    /// </summary>
    /// <param name="hexOrientation">六边形的朝向（尖角朝上或平边朝上）</param>
    /// <param name="size">
    /// 六边形的大小。
    /// <para>
    /// 对于尖角朝上(Pointy)布局：
    /// size.x = W/√3，其中W是六边形的宽度
    /// size.y = H/2，其中H是六边形的高度
    /// 对于平边朝上(Flat)布局：
    /// size.x = W/2，其中W是六边形的宽度
    /// size.y = H/√3，其中H是六边形的高度
    /// </para>
    /// </param>
    /// <param name="origin">布局的原点（屏幕坐标）</param>
    /// <exception cref="ArgumentOutOfRangeException">大小含零或非有限轴，或原点含非有限坐标。</exception>
    public HexLayout(HexOrientation hexOrientation, Point size, Point origin)
    {
        if (!double.IsFinite(size.X) || !double.IsFinite(size.Y) || size.X == 0 || size.Y == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "六边形大小的两个轴必须是有限非零数值。");
        }

        if (!double.IsFinite(origin.X) || !double.IsFinite(origin.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(origin), "布局原点必须使用有限数值。");
        }

        HexOrientation = hexOrientation;
        Origin = origin;
        Size = size;
        
        // 构建完整的变换矩阵：缩放 * 基础变换
        _transformMatrix = Matrix2D.CreateScale(size.X, size.Y) * hexOrientation.Forward;
        _inverseMatrix = hexOrientation.Inverse * Matrix2D.CreateScale(1/size.X, 1/size.Y);
    }

    /// <summary>
    /// 将六边形坐标转换为屏幕坐标。
    /// </summary>
    /// <param name="h">六边形坐标。</param>
    /// <returns>对应屏幕坐标。</returns>
    public Point HexToPixel(Hex h)
    {
        var point = _transformMatrix.Transform(new Point(h.Q, h.R));
        return new Point(point.X + Origin.X, point.Y + Origin.Y);
    }

    /// <summary>
    /// 将屏幕坐标转换为六边形坐标。
    /// </summary>
    /// <param name="p">屏幕坐标。</param>
    /// <returns>最近的六边形坐标。</returns>
    public Hex PixelToHex(Point p)
    {
        var pt = new Point(p.X - Origin.X, p.Y - Origin.Y);
        var transformed = _inverseMatrix.Transform(pt);
        var frac = new FractionalHex(transformed.X, transformed.Y, -transformed.X - transformed.Y);
        return frac.HexRound();
    }

    /// <summary>
    /// 计算六边形顶点相对于中心的偏移。
    /// </summary>
    /// <param name="direction">顶点方向。</param>
    /// <returns>顶点偏移。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> 不是有效方向。</exception>
    public Point HexCornerOffset(HexDirection direction)
    {
        ValidateDirection(direction);
        var angle = MathUtils.ToRadians(HexOrientation.StartAngle - 60 * (int)direction);
        return new Point(Math.Cos(angle)*Size.X, Math.Sin(angle)*Size.Y);
    }

    /// <summary>
    /// 获取六边形的所有顶点坐标。
    /// </summary>
    /// <param name="h">六边形坐标</param>
    /// <returns>顶点坐标列表</returns>
    public List<Point> PolygonCorners(Hex h)
    {
        var corners = new List<Point>();
        var center = HexToPixel(h);
        foreach (var dir in Directions)
        {
            var offset = HexCornerOffset(dir);
            corners.Add(new Point(center.X + offset.X, center.Y + offset.Y));
        }
        return corners;
    }

    private static void ValidateDirection(HexDirection direction)
    {
        if ((uint)direction >= (uint)Directions.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }
    }
}


/// <summary>
/// 六边形网格的扩展方法。
/// 提供了旋转、获取邻居和距离计算等功能。
/// </summary>
public static class HexExtensions
{
    /// <summary>
    /// 六个方向的单位向量，从东方开始顺时针排列。
    /// </summary>
    private static readonly List<Hex> Directions = new() {
        new Hex(1, 0, -1), new Hex(1, -1, 0), new Hex(0, -1, 1), 
        new Hex(-1, 0, 1), new Hex(-1, 1, 0), new Hex(0, 1, -1)
    };

    /// <summary>
    /// 对角线方向的向量，从东北方开始顺时针排列。
    /// </summary>
    private static readonly List<Hex> Diagonals = new() {
        new Hex(2, -1, -1), new Hex(1, -2, 1), new Hex(-1, -1, 2), 
        new Hex(-2, 1, 1), new Hex(-1, 2, -1), new Hex(1, 1, -2)
    };
    
    /// <summary>
    /// 将六边形逆时针旋转60度。
    /// </summary>
    /// <param name="h">要旋转的六边形</param>
    /// <returns>旋转后的六边形</returns>
    public static Hex GetRotateLeft(this Hex h)
    {
        checked
        {
            return new Hex(-h.S, -h.Q, -h.R);
        }
    }

    /// <summary>
    /// 将六边形顺时针旋转60度。
    /// </summary>
    /// <param name="h">要旋转的六边形</param>
    /// <returns>旋转后的六边形</returns>
    public static Hex GetRotateRight(this Hex h)
    {
        checked
        {
            return new Hex(-h.R, -h.S, -h.Q);
        }
    }

    /// <summary>
    /// 获取指定方向上相邻的六边形。
    /// </summary>
    /// <param name="h">当前六边形</param>
    /// <param name="direction">方向</param>
    /// <returns>相邻的六边形</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> 不是有效方向。</exception>
    public static Hex GetNeighbor(this Hex h, HexDirection direction)
    {
        return h + Directions[GetDirectionIndex(direction)];
    }

    /// <summary>
    /// 获取指定对角线方向上的六边形。
    /// </summary>
    /// <param name="h">当前六边形</param>
    /// <param name="direction">方向</param>
    /// <returns>对角线方向的六边形</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> 不是有效方向。</exception>
    public static Hex DiagonalNeighbor(this Hex h, HexDirection direction)
    {
        return h + Diagonals[GetDirectionIndex(direction)];
    }

    /// <summary>
    /// 计算两个六边形之间的距离。
    /// </summary>
    /// <param name="a">起始六边形</param>
    /// <param name="b">目标六边形</param>
    /// <returns>两个六边形之间的最短距离</returns>
    public static int Distance(this Hex a, Hex b)
    {
        return (a-b).Length();
    }
    
    /// <summary>
    /// 将浮点坐标四舍五入为最近的整数坐标。
    /// </summary>
    public static Hex HexRound(this FractionalHex h)
    {
        var q = (int)(Math.Round(h.Q));
        var r = (int)(Math.Round(h.R));
        var s = (int)(Math.Round(h.S));
        var qDiff = Math.Abs(q - h.Q);
        var rDiff = Math.Abs(r - h.R);
        var sDiff = Math.Abs(s - h.S);
        if (qDiff > rDiff && qDiff > sDiff)
        {
            q = -r - s;
        }
        else if (rDiff > sDiff)
        {
            r = -q - s;
        }
        else
        {
            s = -q - r;
        }
        return new Hex(q, r, s);
    }
    
    /// <summary>
    /// 在两个六边形之间绘制一条线。
    /// </summary>
    /// <param name="a">起始六边形</param>
    /// <param name="b">结束六边形</param>
    /// <returns>路径上的所有六边形列表</returns>
    public static List<Hex> HexLineDraw(Hex a, Hex b)
    {
        var n = a.Distance(b);
        // 添加微小偏移以避免边界情况
        var aNudge = new FractionalHex(a.Q + 1e-06, a.R + 1e-06, a.S - 2e-06);
        var bNudge = new FractionalHex(b.Q + 1e-06, b.R + 1e-06, b.S - 2e-06);
        var results = new List<Hex>();
        var step = 1.0 / Math.Max(n, 1);
        for (var i = 0; i <= n; ++i)
        {
            results.Add(HexExtensions.HexLerp(aNudge, bNudge, i * step).HexRound());
        }
        return results;
    }
    
    /// <summary>
    /// 在两个六边形之间进行线性插值。
    /// </summary>
    public static FractionalHex HexLerp(FractionalHex l, FractionalHex r, double t)
    {
        return new FractionalHex(
            l.Q * (1.0 - t) + r.Q * t,
            l.R * (1.0 - t) + r.R * t,
            l.S * (1.0 - t) + r.S * t);
    }

    private static int GetDirectionIndex(HexDirection direction)
    {
        if ((uint)direction >= (uint)Directions.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }

        return (int)direction;
    }
}
