namespace SimpleFramework.Extensions;
    
public static class RandomExtensions
{
    /// <summary>
    /// 在列表中随机挑选一个元素并返回
    /// </summary>
    /// <returns>被选中的元素</returns>
    public static T Choice<T>(this Random random, IList<T> list)
    {
        var index = random.Next(0, list.Count);
        return list[index];
    }

    /// <summary>
    /// 打乱列表中元素顺序。
    /// </summary>
    /// <typeparam name="T"></typeparam>
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
    /// <param name="random"></param>
    /// <param name="sequence">序列</param>
    /// <param name="k">提取的样本数</param>
    /// <typeparam name="T"></typeparam>
    public static IList<T> Sample<T>(this Random random, IEnumerable<T> sequence, int k)
    {
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