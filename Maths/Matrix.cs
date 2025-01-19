namespace SimpleFramework.Maths;

/// <summary>
/// 表示2x2矩阵，用于坐标变换。
/// </summary>
public readonly struct Matrix2D
{
    /// <summary>
    /// 第一行，第一列的元素
    /// </summary>
    public readonly double M11;
    /// <summary>
    /// 第一行，第二列的元素
    /// </summary>
    public readonly double M12;
    /// <summary>
    /// 第二行，第一列的元素
    /// </summary>
    public readonly double M21;
    /// <summary>
    /// 第二行，第二列的元素
    /// </summary>
    public readonly double M22;

    /// <summary>
    /// 初始化一个2x2矩阵。
    /// </summary>
    /// <param name="m11">第一行，第一列的元素</param>
    /// <param name="m12">第一行，第二列的元素</param>
    /// <param name="m21">第二行，第一列的元素</param>
    /// <param name="m22">第二行，第二列的元素</param>
    public Matrix2D(double m11, double m12, double m21, double m22)
    {
        M11 = m11;
        M12 = m12;
        M21 = m21;
        M22 = m22;
    }

    /// <summary>
    /// 矩阵与点的乘法。
    /// </summary>
    /// <param name="p">要变换的点</param>
    /// <returns>变换后的点</returns>
    public Point Transform(Point p)
    {
        return new Point(
            M11 * p.X + M12 * p.Y,
            M21 * p.X + M22 * p.Y);
    }

    /// <summary>
    /// 计算矩阵的行列式。
    /// </summary>
    public double Determinant => M11 * M22 - M12 * M21;

    /// <summary>
    /// 计算矩阵的逆矩阵。
    /// </summary>
    /// <returns>当前矩阵的逆矩阵</returns>
    /// <exception cref="InvalidOperationException">当矩阵不可逆时抛出</exception>
    public Matrix2D Inverse()
    {
        var det = Determinant;
        if (Math.Abs(det) < double.Epsilon)
        {
            throw new InvalidOperationException("Matrix is not invertible");
        }

        return new Matrix2D(
            M22 / det, -M12 / det,
            -M21 / det, M11 / det);
    }

    /// <summary>
    /// 矩阵乘法。
    /// </summary>
    public static Matrix2D operator *(Matrix2D a, Matrix2D b)
    {
        return new Matrix2D(
            a.M11 * b.M11 + a.M12 * b.M21,
            a.M11 * b.M12 + a.M12 * b.M22,
            a.M21 * b.M11 + a.M22 * b.M21,
            a.M21 * b.M12 + a.M22 * b.M22);
    }

    /// <summary>
    /// 矩阵与标量的乘法。
    /// </summary>
    public static Matrix2D operator *(Matrix2D m, double s)
    {
        return new Matrix2D(
            m.M11 * s, m.M12 * s,
            m.M21 * s, m.M22 * s);
    }

    /// <summary>
    /// 矩阵与标量的乘法。
    /// </summary>
    public static Matrix2D operator *(double s, Matrix2D m) => m * s;

    /// <summary>
    /// 单位矩阵。
    /// </summary>
    public static Matrix2D Identity => new(1, 0, 0, 1);

    /// <summary>
    /// 计算两个矩阵的点乘（内积）。
    /// 点乘结果为两个矩阵对应位置的元素相乘后的和。
    /// </summary>
    /// <param name="other">另一个矩阵</param>
    /// <returns>点乘结果</returns>
    public double Dot(Matrix2D other)
    {
        return M11 * other.M11 + M12 * other.M12 + 
               M21 * other.M21 + M22 * other.M22;
    }

    /// <summary>
    /// 创建缩放矩阵。
    /// </summary>
    /// <param name="sx">X轴缩放比例</param>
    /// <param name="sy">Y轴缩放比例</param>
    /// <returns>缩放矩阵</returns>
    public static Matrix2D CreateScale(double sx, double sy)
    {
        return new Matrix2D(sx, 0, 0, sy);
    }

    /// <summary>
    /// 创建统一缩放矩阵。
    /// </summary>
    /// <param name="scale">缩放比例</param>
    /// <returns>缩放矩阵</returns>
    public static Matrix2D CreateScale(double scale)
    {
        return CreateScale(scale, scale);
    }
} 