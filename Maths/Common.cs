namespace SimpleFramework.Maths;

/// <summary>
/// 表示二维平面上的点。
/// </summary>
public struct Point
{
    /// <summary>获取 X 坐标。</summary>
    public double X { get; }
    /// <summary>获取 Y 坐标。</summary>
    public double Y { get; }

    /// <summary>创建二维点。</summary>
    /// <param name="x">X 坐标。</param>
    /// <param name="y">Y 坐标。</param>
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
