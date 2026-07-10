namespace SimpleFramework.Utility.Extensions;

/// <summary>
/// 提供列表原地操作的扩展方法。
/// </summary>
public static class ListExtensions
{
    /// <summary>
    /// 交换列表中两个元素的位置
    /// </summary>
    /// <param name="list">列表对象。</param>
    /// <param name="i">第一个下标。</param>
    /// <param name="j">第二个下标。</param>
    /// <typeparam name="T">元素类型。</typeparam>
    public static void Swap<T>(this IList<T> list, int i, int j)
    {
        (list[i], list[j]) = (list[j], list[i]);
    }
}
