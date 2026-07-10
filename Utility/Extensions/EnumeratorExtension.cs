namespace SimpleFramework.Utility.Extensions;

/// <summary>
/// 提供对序列逐项执行操作的扩展方法。
/// </summary>
public static class EnumeratorExtensions
{
    /// <summary>
    /// 在原始序列的每个对象上执行操作。
    /// </summary>
    /// <param name="enumerable">要遍历的序列。</param>
    /// <param name="action">对每个元素执行的操作。</param>
    /// <typeparam name="T">元素类型。</typeparam>
    public static void Apply<T>(this IEnumerable<T> enumerable, Action<T> action)
    {
        foreach (var elem in enumerable)
        {
            action(elem);
        }
    }

    /// <summary>
    /// 在原始序列的每个对象上执行操作。
    /// </summary>
    /// <param name="enumerable">要遍历的序列。</param>
    /// <param name="func">对每个元素调用并忽略返回值的函数。</param>
    /// <typeparam name="T">元素类型。</typeparam>
    /// <typeparam name="TR">函数返回类型。</typeparam>
    public static void Apply<T, TR>(this IEnumerable<T> enumerable, Func<T, TR> func)
    {
        foreach (var elem in enumerable)
        {
            func(elem);
        }
    }
}
