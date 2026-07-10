using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace SimpleFramework.Collections;

/// <summary>
/// 带有默认值的字典。通过索引器读取缺失键时，会创建并保存默认值。
/// </summary>
/// <typeparam name="TK">键类型。</typeparam>
/// <typeparam name="TV">值类型。</typeparam>
public class DefaultDict<TK, TV> :
    IDictionary<TK, TV>,
    IReadOnlyDictionary<TK, TV>
    where TK : notnull
{
    private readonly Dictionary<TK, TV> _delegate;
    private readonly Func<TV> _initCb;

    /// <summary>
    /// 创建默认字典，并在索引器读取缺失键时使用回调创建默认值。
    /// </summary>
    /// <param name="initCallback">默认值工厂。</param>
    /// <param name="comparer">键比较器；为空时使用键类型的默认比较器。</param>
    /// <exception cref="ArgumentNullException"><paramref name="initCallback"/> 为空。</exception>
    public DefaultDict(Func<TV> initCallback, IEqualityComparer<TK>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(initCallback);
        _initCb = initCallback;
        _delegate = new Dictionary<TK, TV>(comparer);
    }

    /// <summary>
    /// 获取字典实际使用的键比较器。
    /// </summary>
    public IEqualityComparer<TK> Comparer => _delegate.Comparer;

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<TK, TV>> GetEnumerator()
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

    bool ICollection<KeyValuePair<TK, TV>>.Contains(KeyValuePair<TK, TV> item)
    {
        return ((ICollection<KeyValuePair<TK, TV>>)_delegate).Contains(item);
    }

    void ICollection<KeyValuePair<TK, TV>>.CopyTo(KeyValuePair<TK, TV>[] array, int arrayIndex)
    {
        ((ICollection<KeyValuePair<TK, TV>>)_delegate).CopyTo(array, arrayIndex);
    }

    void ICollection<KeyValuePair<TK, TV>>.Add(KeyValuePair<TK, TV> item)
    {
        _delegate.Add(item.Key, item.Value);
    }

    bool ICollection<KeyValuePair<TK, TV>>.Remove(KeyValuePair<TK, TV> item)
    {
        return ((ICollection<KeyValuePair<TK, TV>>)_delegate).Remove(item);
    }

    bool ICollection<KeyValuePair<TK, TV>>.IsReadOnly => false;

    /// <inheritdoc/>
    public int Count => _delegate.Count;

    /// <inheritdoc/>
    public void Add(TK key, TV value)
    {
        _delegate.Add(key, value);
    }

    /// <inheritdoc/>
    public bool Remove(TK key)
    {
        return _delegate.Remove(key);
    }

    /// <inheritdoc/>
    public bool ContainsKey(TK key)
    {
        return _delegate.ContainsKey(key);
    }

    /// <summary>
    /// 尝试获取已经存在的值。
    /// </summary>
    /// <remarks>
    /// 该方法保持 <see cref="Dictionary{TKey,TValue}.TryGetValue(TKey,out TValue)"/> 的探测语义：
    /// 键不存在时不会调用默认值工厂，也不会向字典插入新键。需要获取或创建默认值时请使用索引器。
    /// </remarks>
    /// <param name="key">要查找的键。</param>
    /// <param name="value">找到的值；缺失时为默认值。</param>
    /// <returns>找到键时为 <see langword="true"/>。</returns>
    public bool TryGetValue(TK key, [MaybeNullWhen(false)] out TV value)
    {
        return _delegate.TryGetValue(key, out value);
    }

    /// <summary>
    /// 获取或设置键值；读取缺失键时会调用默认值工厂并插入结果。
    /// </summary>
    public TV this[TK key]
    {
        get
        {
            if (!_delegate.TryGetValue(key, out var value))
            {
                value = _initCb();
                _delegate[key] = value;
            }

            return value;
        }
        set => _delegate[key] = value;
    }

    IEnumerable<TK> IReadOnlyDictionary<TK, TV>.Keys => _delegate.Keys;

    IEnumerable<TV> IReadOnlyDictionary<TK, TV>.Values => _delegate.Values;

    /// <inheritdoc/>
    public ICollection<TK> Keys => _delegate.Keys;

    /// <inheritdoc/>
    public ICollection<TV> Values => _delegate.Values;
}
