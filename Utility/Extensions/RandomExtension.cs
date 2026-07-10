namespace SimpleFramework.Utility.Extensions;
    
/// <summary>
/// 提供基于 <see cref="Random"/> 的集合随机操作。
/// </summary>
public static class RandomExtensions
{
    /// <summary>
    /// 在列表中随机挑选一个元素并返回
    /// </summary>
    /// <param name="random">随机数生成器。</param>
    /// <param name="list">非空候选列表。</param>
    /// <typeparam name="T">元素类型。</typeparam>
    /// <returns>被选中的元素</returns>
    /// <exception cref="ArgumentException"><paramref name="list"/> 为空。</exception>
    public static T Choice<T>(this Random random, IList<T> list)
    {
        if (list.Count == 0)
        {
            throw new ArgumentException("候选列表不能为空。", nameof(list));
        }

        var index = random.Next(0, list.Count);
        return list[index];
    }

    /// <summary>
    /// 打乱列表中元素顺序。
    /// </summary>
    /// <param name="random">随机数生成器。</param>
    /// <param name="list">要原地打乱的列表。</param>
    /// <typeparam name="T">元素类型。</typeparam>
    public static void Shuffle<T>(this Random random, IList<T> list)
    {
        for (var i = list.Count - 1; i >= 0; --i)
        {
            var randIndex = random.Next(i+1);
            list.Swap(randIndex, i);
        }
    }
        
    /// <summary>
    /// 在序列中随机采样若干个样本。
    /// </summary>
    /// <remarks>使用水塘抽样算法实现。</remarks>
    /// <param name="random">随机数生成器。</param>
    /// <param name="sequence">输入序列。</param>
    /// <param name="k">最多提取的样本数；序列较短时返回全部元素。</param>
    /// <typeparam name="T">元素类型。</typeparam>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="k"/> 小于零。</exception>
    public static IList<T> Sample<T>(this Random random, IEnumerable<T> sequence, int k)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(k);
        var result = new List<T>();
        var i = 0;
        foreach (var elem in sequence)
        {
            i += 1;
            if (i <= k)
            {
                result.Add(elem);
            }
            else
            {
                var j = random.Next(0, i);
                if (j < k)
                {
                    result[j] = elem;
                }
            }
        }

        return result;
    }
}
