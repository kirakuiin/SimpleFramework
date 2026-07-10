using System.Text;

namespace SimpleFramework.Utility.Extensions;

/// <summary>
/// 提供字符串构造辅助方法。
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// 生成重复字符串。
    /// </summary>
    /// <param name="text">要重复的文本。</param>
    /// <param name="count">重复次数。</param>
    /// <returns><see cref="string"/></returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> 小于零。</exception>
    public static string Repeat(this string text, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        StringBuilder builder = new();
        while (count-- > 0)
        {
            builder.Append(text);
        }
        return builder.ToString();
    }
}
