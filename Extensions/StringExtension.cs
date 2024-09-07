using System.Text;

namespace SimpleFramework.Extensions;

public static class StringExtensions
{
    /// <summary>
    /// 生成重复字符串。
    /// </summary>
    /// <param name="text"></param>
    /// <param name="count"></param>
    /// <returns><see cref="string"/></returns>
    public static string Repeat(this string text, int count)
    {
        StringBuilder builder = new();
        while (count-- > 0)
        {
            builder.Append(text);
        }
        return builder.ToString();
    }
}