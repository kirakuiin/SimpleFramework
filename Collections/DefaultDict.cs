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
    private readonly Dictionary<TK, TV> _delegate = new();
    private readonly Func<TV> _initCb;

    /// <summary>
    /// 创建默认字典，并在索引器读取缺失键时使用回调创建默认值。
    /// </summary>
    /// <param name="initCallback">默认值工厂。</param>
    public DefaultDict(Func<TV> initCallback)
    {
        _initCb = initCallback;
    }

    public IEnumerator<KeyValuePair<TK, TV>> GetEnumerator()
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

    bool ICollection<KeyValuePair<TK, TV>>.Contains(KeyValuePair<TK, TV> item)
    {
        return _delegate.Contains(item);
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

    public int Count => _delegate.Count;

    public void Add(TK key, TV value)
    {
        _delegate.Add(key, value);
    }

    public bool Remove(TK key)
    {
        return _delegate.Remove(key);
    }

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
    public bool TryGetValue(TK key, [MaybeNullWhen(false)] out TV value)
    {
        return _delegate.TryGetValue(key, out value);
    }

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

    public ICollection<TK> Keys => _delegate.Keys;

    public ICollection<TV> Values => _delegate.Values;
}
