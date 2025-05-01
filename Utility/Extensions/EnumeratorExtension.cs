namespace SimpleFramework.Utility.Extensions;

public static class EnumeratorExtensions
{
    /// <summary>
    /// 在原始序列的每个对象上执行操作。
    /// </summary>
    /// <param name="enumerable"></param>
    /// <param name="action"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
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
    /// <param name="enumerable"></param>
    /// <param name="func"></param>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TR"></typeparam>
    /// <returns></returns>
    public static void Apply<T, TR>(this IEnumerable<T> enumerable, Func<T, TR> func)
    {
        foreach (var elem in enumerable)
        {
            func(elem);
        }
    }
}