using System.Collections;

namespace SimpleFramework.Collections;

/// <summary>
/// 计数字典，用来统计各个元素的数量。
/// </summary>
/// <typeparam name="T">要计数的键类型。</typeparam>
public class Counter<T>
    : IDictionary<T, long>, IReadOnlyDictionary<T, long>, IEquatable<Counter<T>>
    where T : notnull
{
    private readonly DefaultDict<T, long> _delegate;

    /// <summary>
    /// 对序列里面的各个元素数量进行计数。
    /// </summary>
    /// <param name="sequence">用于初始化计数的元素序列。</param>
    /// <param name="comparer">键比较器；为空时使用键类型的默认比较器。</param>
    public Counter(IEnumerable<T> sequence, IEqualityComparer<T>? comparer = null)
        : this(comparer)
    {
        foreach (var elem in sequence)
        {
            _delegate[elem] += 1;
        }
    }

    /// <summary>
    /// 复制另一个计数字典，并保留其键比较器。
    /// </summary>
    /// <param name="other">要复制的计数字典。</param>
    public Counter(Counter<T> other)
        : this(other.Comparer)
    {
        foreach (var pair in other)
        {
            _delegate[pair.Key] = pair.Value;
        }
    }

    /// <summary>
    /// 使用键类型的默认比较器创建空计数字典。
    /// </summary>
    public Counter()
        : this((IEqualityComparer<T>?)null)
    {
    }

    /// <summary>
    /// 使用指定键比较器创建空计数字典。
    /// </summary>
    /// <param name="comparer">键比较器；为空时使用键类型的默认比较器。</param>
    public Counter(IEqualityComparer<T>? comparer)
    {
        _delegate = new DefaultDict<T, long>(() => 0, comparer);
    }

    /// <summary>
    /// 获取计数字典实际使用的键比较器。
    /// </summary>
    public IEqualityComparer<T> Comparer => _delegate.Comparer;

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<T, long>> GetEnumerator()
    {
        return _delegate.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <inheritdoc/>
    public void Clear()
    {
        _delegate.Clear();
    }

    bool ICollection<KeyValuePair<T, long>>.Contains(KeyValuePair<T, long> item)
    {
        ICollection<KeyValuePair<T, long>> collection = _delegate;
        return collection.Contains(item);
    }

    void ICollection<KeyValuePair<T, long>>.CopyTo(KeyValuePair<T, long>[] array, int arrayIndex)
    {
        ICollection<KeyValuePair<T, long>> collection = _delegate;
        collection.CopyTo(array, arrayIndex);
    }

    void ICollection<KeyValuePair<T, long>>.Add(KeyValuePair<T, long> item)
    {
        ICollection<KeyValuePair<T, long>> collection = _delegate;
        collection.Add(item);
    }

    bool ICollection<KeyValuePair<T, long>>.Remove(KeyValuePair<T, long> item)
    {
        ICollection<KeyValuePair<T, long>> collection = _delegate;
        return collection.Remove(item);
    }

    bool ICollection<KeyValuePair<T, long>>.IsReadOnly 
    {
        get
        {
            ICollection<KeyValuePair<T, long>> collection = _delegate;
            return collection.IsReadOnly;
        }
    }

    /// <inheritdoc/>
    public int Count => _delegate.Count;

    /// <inheritdoc/>
    public void Add(T key, long value)
    {
        _delegate.Add(key, value);
    }

    /// <inheritdoc/>
    public bool Remove(T key)
    {
        return _delegate.Remove(key);
    }

    /// <inheritdoc/>
    public bool ContainsKey(T key)
    {
        return _delegate.ContainsKey(key);
    }

    /// <inheritdoc/>
    public bool TryGetValue(T key, out long value)
    {
        return _delegate.TryGetValue(key, out value);
    }

    /// <summary>
    /// 获取或设置键的计数；读取缺失键时会插入计数零。
    /// </summary>
    public long this[T key]
    {
        get => _delegate[key];
        set => _delegate[key] = value;
    }

    IEnumerable<T> IReadOnlyDictionary<T, long>.Keys => _delegate.Keys;

    IEnumerable<long> IReadOnlyDictionary<T, long>.Values => _delegate.Values;

    /// <inheritdoc/>
    public ICollection<T> Keys => _delegate.Keys;

    /// <inheritdoc/>
    public ICollection<long> Values => _delegate.Values;

    /// <summary>
    /// 列举计数字典中最多的元素，返回前<c>n</c>多的。
    /// </summary>
    /// <remarks>默认降序返回全部键值</remarks>
    /// <param name="n">最多返回的元素数。</param>
    /// <returns>按计数降序排列的键值对。</returns>
    public IEnumerable<KeyValuePair<T, long>> MostCommon(ulong n = ulong.MaxValue)
    {
        var commonList = from pair in _delegate
            group pair by pair.Value
            into result
            orderby result.Key descending
            select result;

        foreach (var pair in MostCommonForN(commonList, n))
        {
            yield return pair;
        }
    }

    private IEnumerable<KeyValuePair<T, long>> MostCommonForN(IOrderedEnumerable<IGrouping<long, KeyValuePair<T, long>>> result, ulong n)
    {
        ulong i = 0;
        foreach (var group in result)
        {
            foreach (var pair in group)
            {
                if (i < n)
                {
                    yield return pair;
                }
                else
                {
                    yield break;
                }

                i++;
            }
        }
    }

    /// <summary>
    /// 获得所有值的总数。
    /// </summary>
    /// <returns>全部计数之和。</returns>
    public long Total()
    {
        return _delegate.Values.Sum();
    }

    /// <summary>
    /// 将所有的键依据其数量填充到一个序列中。
    /// </summary>
    /// <returns>按正计数次数重复每个键的延迟序列。</returns>
    public IEnumerable<T> Elements()
    {
        foreach (var pair in MostCommon())
        {
            for (var i = 0; i < pair.Value; ++i)
            {
                yield return pair.Key;
            }
        }
    }

    /// <summary>
    /// 将另一个计数器的内容合并到自身。
    /// </summary>
    /// <param name="other">要合并的计数字典。</param>
    public void Update(Counter<T> other)
    {
        foreach (var pair in other)
        {
            _delegate[pair.Key] += pair.Value;
        }
    }

    /// <summary>
    /// 减去另一个计数器内的数值。
    /// </summary>
    /// <remarks>如果本身不存在某个键则会出现负数。</remarks>
    /// <param name="other">要减去的计数字典。</param>
    public void Subtract(Counter<T> other)
    {
        foreach (var pair in other)
        {
            _delegate[pair.Key] -= pair.Value;
        }
    }

    /// <summary>
    /// 返回每个计数取反的新计数字典，并保留键比较器。
    /// </summary>
    public static Counter<T> operator -(Counter<T> origin)
    {
        var result = new Counter<T>(origin.Comparer);
        foreach (var pair in origin)
        {
            result[pair.Key] = -pair.Value;
        }

        return result;
    }

    /// <summary>
    /// 作为集合的减法。
    /// </summary>
    /// <param name="a">左操作数，其键比较器由结果保留。</param>
    /// <param name="b">右操作数。</param>
    /// <returns>包含双方全部键的计数差。</returns>
    public static Counter<T> operator -(Counter<T> a, Counter<T> b)
    {
        var result = new Counter<T>(a.Comparer);
        foreach (var key in GetAllKeys(a, b))
        {
            result[key] = a.GetValueOrDefault(key, 0) - b.GetValueOrDefault(key, 0);
        }

        return result;
    }

    /// <summary>
    /// 作为集合的加法。
    /// </summary>
    /// <param name="a">左操作数，其键比较器由结果保留。</param>
    /// <param name="b">右操作数。</param>
    /// <returns>包含双方全部键的计数和。</returns>
    public static Counter<T> operator +(Counter<T> a, Counter<T> b)
    {
        var result = new Counter<T>(a.Comparer);
        foreach (var key in GetAllKeys(a, b))
        {
            result[key] = a.GetValueOrDefault(key, 0) + b.GetValueOrDefault(key, 0);
        }

        return result;
    }

    /// <summary>判断左侧所有计数是否逐项大于或等于右侧，且两者不相等。</summary>
    public static bool operator >(Counter<T> a, Counter<T> b)
    {
        return a >= b && !a.Equals(b);
    }
        
    /// <summary>判断左侧所有计数是否逐项小于或等于右侧，且两者不相等。</summary>
    public static bool operator <(Counter<T> a, Counter<T> b)
    {
        return a <= b && !a.Equals(b);
    }
        
    /// <summary>判断左侧所有计数是否逐项大于或等于右侧。</summary>
    public static bool operator >=(Counter<T> a, Counter<T> b)
    {
        return CompareAllKeys(a, b, static (left, right) => left >= right);
    }
        
    /// <summary>判断左侧所有计数是否逐项小于或等于右侧。</summary>
    public static bool operator <=(Counter<T> a, Counter<T> b)
    {
        return CompareAllKeys(a, b, static (left, right) => left <= right);
    }

    private static bool CompareAllKeys(Counter<T> a, Counter<T> b, Func<long, long, bool> comparer)
    {
        return GetAllKeys(a, b).All(key => comparer(a.GetValueOrDefault(key, 0), b.GetValueOrDefault(key, 0)));
    }

    private static HashSet<T> GetAllKeys(Counter<T> a, Counter<T> b)
    {
        var keys = new HashSet<T>(a.Keys, a.Comparer);
        keys.UnionWith(b.Keys);
        return keys;
    }

    /// <inheritdoc/>
    public bool Equals(Counter<T>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (!Comparer.Equals(other.Comparer)) return false;
        return GetAllKeys(this, other).All(key => this.GetValueOrDefault(key, 0) == other.GetValueOrDefault(key, 0));
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is Counter<T> other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = 0;
        foreach (var pair in this)
        {
            if (pair.Value == 0) continue;
            hash ^= HashCode.Combine(Comparer.GetHashCode(pair.Key), pair.Value);
        }

        return hash;
    }
}
