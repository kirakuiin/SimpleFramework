using System.Collections;

namespace SimpleFramework.Collections;

/// <summary>
/// 计数字典，用来统计各个元素的数量。
/// </summary>
/// <typeparam name="T"></typeparam>
public class Counter<T>
    : IDictionary<T, long>, IReadOnlyDictionary<T, long>, IEquatable<Counter<T>>
{
    private readonly DefaultDict<T, long> _delegate;

    /// <summary>
    /// 对序列里面的各个元素数量进行计数。
    /// </summary>
    /// <param name="sequence"></param>
    public Counter(IEnumerable<T> sequence)
        : this()
    {
        foreach (var elem in sequence)
        {
            _delegate[elem] += 1;
        }
    }

    public Counter(Counter<T> other)
        : this()
    {
        foreach (var pair in other)
        {
            _delegate[pair.Key] = pair.Value;
        }
    }

    public Counter()
    {
        _delegate = new DefaultDict<T, long>(() => 0);
    }

    public IEnumerator<KeyValuePair<T, long>> GetEnumerator()
    {
        return _delegate.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

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

    public int Count => _delegate.Count;

    public void Add(T key, long value)
    {
        _delegate.Add(key, value);
    }

    public bool Remove(T key)
    {
        return _delegate.Remove(key);
    }

    public bool ContainsKey(T key)
    {
        return _delegate.ContainsKey(key);
    }

    public bool TryGetValue(T key, out long value)
    {
        return _delegate.TryGetValue(key, out value);
    }

    public long this[T key]
    {
        get => _delegate[key];
        set => _delegate[key] = value;
    }

    IEnumerable<T> IReadOnlyDictionary<T, long>.Keys => _delegate.Keys;

    IEnumerable<long> IReadOnlyDictionary<T, long>.Values => _delegate.Values;

    public ICollection<T> Keys => _delegate.Keys;

    public ICollection<long> Values => _delegate.Values;

    /// <summary>
    /// 列举计数字典中最多的元素，返回前<c>n</c>多的。
    /// </summary>
    /// <remarks>默认降序返回全部键值</remarks>
    /// <param name="n"></param>
    /// <returns></returns>
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
    /// <returns></returns>
    public long Total()
    {
        return _delegate.Values.Sum();
    }

    /// <summary>
    /// 将所有的键依据其数量填充到一个序列中。
    /// </summary>
    /// <returns></returns>
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
    /// <param name="other"></param>
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
    public void Subtract(Counter<T> other)
    {
        foreach (var pair in other)
        {
            _delegate[pair.Key] -= pair.Value;
        }
    }

    public static Counter<T> operator -(Counter<T> origin)
    {
        var result = new Counter<T>();
        foreach (var pair in origin)
        {
            result[pair.Key] = -pair.Value;
        }

        return result;
    }

    /// <summary>
    /// 作为集合的减法。
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static Counter<T> operator -(Counter<T> a, Counter<T> b)
    {
        var result = new Counter<T>();
        foreach (var pair in a)
        {
            result[pair.Key] = pair.Value - b.GetValueOrDefault(pair.Key, 0);
        }

        return result;
    }

    /// <summary>
    /// 作为集合的加法。
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static Counter<T> operator +(Counter<T> a, Counter<T> b)
    {
        var result = new Counter<T>();
        foreach (var key in new HashSet<T>(a.Keys.Concat(b.Keys)))
        {
            result[key] = a.GetValueOrDefault(key, 0) + b.GetValueOrDefault(key, 0);
        }

        return result;
    }

    public static bool operator >(Counter<T> a, Counter<T> b)
    {
        var allKey = new HashSet<T>(a.Keys.Concat(b.Keys));
        return allKey.All(key => a.GetValueOrDefault(key, 0) > b.GetValueOrDefault(key, 0));
    }
        
    public static bool operator <(Counter<T> a, Counter<T> b)
    {
        var allKey = new HashSet<T>(a.Keys.Concat(b.Keys));
        return allKey.All(key => a.GetValueOrDefault(key, 0) < b.GetValueOrDefault(key, 0));
    }
        
    public static bool operator >=(Counter<T> a, Counter<T> b)
    {
        return !(a < b);
    }
        
    public static bool operator <=(Counter<T> a, Counter<T> b)
    {
        return !(a > b);
    }

    public bool Equals(Counter<T> other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        var allKey = new HashSet<T>(Keys.Concat(other.Keys));
        return allKey.All(key => this.GetValueOrDefault(key, 0) == other.GetValueOrDefault(key, 0));
    }
}