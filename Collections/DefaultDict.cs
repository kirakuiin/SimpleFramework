using System.Collections;

namespace SimpleFramework.Collections;

/// <summary>
/// 带有默认值的字典，直接获取键的值不会导致错误，而是返回默认值。
/// </summary>
/// <typeparam name="TK"></typeparam>
/// <typeparam name="TV"></typeparam>
public class DefaultDict<TK, TV> :
    IDictionary<TK, TV>,
    IReadOnlyDictionary<TK, TV>
{
    private readonly Dictionary<TK, TV> _delegate;

    private readonly Func<TV> _initCb;

    /// <summary>
    /// 当字典内不包含此键时，使用一个回调函数进行初始化。
    /// </summary>
    /// <param name="initCallback"></param>
    public DefaultDict(Func<TV> initCallback)
    {
        _initCb = initCallback;
        _delegate = new Dictionary<TK, TV>();
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
        ICollection<KeyValuePair<TK, TV>> collection = _delegate;
        return collection.Contains(item);
    }

    void ICollection<KeyValuePair<TK, TV>>.CopyTo(KeyValuePair<TK, TV>[] array, int arrayIndex)
    {
        ICollection<KeyValuePair<TK, TV>> collection = _delegate;
        collection.CopyTo(array, arrayIndex);
    }

    void ICollection<KeyValuePair<TK, TV>>.Add(KeyValuePair<TK, TV> item)
    {
        ICollection<KeyValuePair<TK, TV>> collection = _delegate;
        collection.Add(item);
    }

    bool ICollection<KeyValuePair<TK, TV>>.Remove(KeyValuePair<TK, TV> item)
    {
        ICollection<KeyValuePair<TK, TV>> collection = _delegate;
        return collection.Remove(item);
    }

    bool ICollection<KeyValuePair<TK, TV>>.IsReadOnly 
    {
        get
        {
            ICollection<KeyValuePair<TK, TV>> collection = _delegate;
            return collection.IsReadOnly;
        }
    }

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

    public bool TryGetValue(TK key, out TV value)
    {
        return _delegate.TryGetValue(key, out value);
    }

    public TV this[TK key]
    {
        get
        {
            if (!_delegate.ContainsKey(key))
            {
                _delegate[key] = _initCb();
            }
            return _delegate[key];
        }
        set => _delegate[key] = value;
    }

    IEnumerable<TK> IReadOnlyDictionary<TK, TV>.Keys => _delegate.Keys;

    IEnumerable<TV> IReadOnlyDictionary<TK, TV>.Values => _delegate.Values;

    public ICollection<TK> Keys => _delegate.Keys;

    public ICollection<TV> Values => _delegate.Values;
}