namespace SimpleFramework.Maths;

/// <summary>
/// 表示二维平面上的点。
/// </summary>
public struct Point
{
    public double X { get; }
    public double Y { get; }

    public Point(double x, double y)
    {
        X = x;
        Y = y;
    }
}

/// <summary>
/// 数学计算的常用工具类。
/// </summary>
public static class MathUtils
{
    /// <summary>
    /// 将角度转换为弧度。
    /// </summary>
    /// <param name="degrees">角度值</param>
    /// <returns>对应的弧度值</returns>
    public static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    /// <summary>
    /// 将弧度转换为角度。
    /// </summary>
    /// <param name="radians">弧度值</param>
    /// <returns>对应的角度值</returns>
    public static double ToDegrees(double radians)
    {
        return radians * 180.0 / Math.PI;
    }

    /// <summary>
    /// 将角度转换为弧度。
    /// </summary>
    /// <param name="degrees">角度值</param>
    /// <returns>对应的弧度值</returns>
    public static float ToRadians(float degrees)
    {
        return degrees * MathF.PI / 180.0f;
    }

    /// <summary>
    /// 将弧度转换为角度。
    /// </summary>
    /// <param name="radians">弧度值</param>
    /// <returns>对应的角度值</returns>
    public static float ToDegrees(float radians)
    {
        return radians * 180.0f / MathF.PI;
    }
}