namespace SimpleFramework.Patterns;

/// <summary>
/// 黑板模式实现，提供中心化的数据存储和访问机制。
/// </summary>
/// <remarks>
/// 黑板模式是一种中心化的数据存储机制，允许多个组件通过键值对的形式共享和访问数据。
/// 该实现具有以下特性：
/// 1. 类型安全的数据存储
/// 2. 线程安全
/// 3. 支持数据变更通知
/// 4. 支持层级访问控制
/// </remarks>
public class BlackBoard
{
    private readonly Dictionary<string, object> _data = new();
    private readonly ReaderWriterLockSlim _lock = new();

    /// <summary>
    /// 当黑板中的数据发生变更时触发的事件。
    /// </summary>
    public event EventHandler<BlackBoardEventArgs>? OnDataChanged;

    /// <summary>
    /// 初始化黑板的新实例。
    /// </summary>
    /// <param name="name">黑板的名称</param>
    /// <param name="parent">父级黑板，用于实现层级访问控制</param>
    public BlackBoard(string name = "BlackBoard", BlackBoard? parent = null)
    {
        Parent = parent;
        Name = name;
    }

    /// <summary>
    /// 获取黑板的名称。
    /// </summary>
    public string Name { init; get; }

    /// <summary>
    /// 获取父级黑板。
    /// </summary>
    public BlackBoard? Parent { get; }

    /// <summary>
    /// 设置指定键的值。
    /// </summary>
    /// <typeparam name="T">值的类型</typeparam>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    public void Set<T>(string key, T value)
    {
        _lock.EnterWriteLock();
        try
        {
            var oldValue = GetInternal<T>(key);
            _data[key] = value!;
            NotifyDataChanged(key, oldValue, value);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// 获取指定键的值。
    /// </summary>
    /// <typeparam name="T">值的类型</typeparam>
    /// <param name="key">键</param>
    /// <returns>如果找到则返回对应的值，否则返回默认值</returns>
    public T? Get<T>(string key)
    {
        _lock.EnterReadLock();
        try
        {
            return GetInternal<T>(key);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// 内部获取方法，假设已经获取了读锁。
    /// </summary>
    private T? GetInternal<T>(string key)
    {
        if (_data.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return Parent == null ? default : Parent.Get<T>(key);
    }

    /// <summary>
    /// 尝试获取指定键的值。
    /// </summary>
    /// <typeparam name="T">值的类型</typeparam>
    /// <param name="key">键</param>
    /// <param name="value">如果找到则返回对应的值</param>
    /// <returns>如果找到则返回true，否则返回false</returns>
    public bool TryGet<T>(string key, out T? value)
    {
        _lock.EnterReadLock();
        try
        {
            if (_data.TryGetValue(key, out var obj) && obj is T typedValue)
            {
                value = typedValue;
                return true;
            }
            return Parent?.TryGet(key, out value) ?? (value = default, false).Item2;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// 检查是否包含指定的键。
    /// </summary>
    /// <param name="key">要检查的键</param>
    /// <returns>如果包含则返回true，否则返回false</returns>
    public bool Contains(string key)
    {
        _lock.EnterReadLock();
        try
        {
            return _data.ContainsKey(key) || (Parent?.Contains(key) ?? false);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// 移除指定键的值。
    /// </summary>
    /// <param name="key">要移除的键</param>
    /// <returns>如果成功移除则返回true，否则返回false</returns>
    public bool Remove(string key)
    {
        _lock.EnterWriteLock();
        try
        {
            return RemoveWithoutLock(key);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
    
    /// <summary>
    /// 清除所有数据。
    /// </summary>
    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            foreach (var key in _data.Keys)
            {
                RemoveWithoutLock(key);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private bool RemoveWithoutLock(string key)
    {
        if (!_data.Remove(key, out var oldValue)) return false;
        NotifyDataChanged(key, oldValue, null);
        return true;
    }

    /// <summary>
    /// 通知数据变更。
    /// </summary>
    /// <param name="key">变更的键</param>
    /// <param name="oldValue">旧值</param>
    /// <param name="newValue">新值</param>
    private void NotifyDataChanged(string key, object? oldValue, object? newValue)
    {
        OnDataChanged?.Invoke(this, new BlackBoardEventArgs(key, oldValue, newValue));
    }

    /// <summary>
    /// 获取所有键的列表。
    /// </summary>
    /// <returns>所有键的列表</returns>
    public IReadOnlyList<string> GetKeys()
    {
        _lock.EnterReadLock();
        try
        {
            return _data.Keys.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// 获取所有值的列表。
    /// </summary>
    /// <returns>所有值的列表</returns>
    public IReadOnlyList<object> GetValues()
    {
        _lock.EnterReadLock();
        try
        {
            return _data.Values.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// 获取所有键值对的列表。
    /// </summary>
    /// <returns>所有键值对的列表</returns>
    public IReadOnlyList<KeyValuePair<string, object>> GetEntries()
    {
        _lock.EnterReadLock();
        try
        {
            return _data.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
}

/// <summary>
/// 黑板数据变更事件的参数。
/// </summary>
public class BlackBoardEventArgs : EventArgs
{
    /// <summary>
    /// 获取变更的键。
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 获取旧值。
    /// </summary>
    public object? OldValue { get; }

    /// <summary>
    /// 获取新值。
    /// </summary>
    public object? NewValue { get; }

    /// <summary>
    /// 初始化黑板数据变更事件参数的新实例。
    /// </summary>
    /// <param name="key">变更的键</param>
    /// <param name="oldValue">旧值</param>
    /// <param name="newValue">新值</param>
    public BlackBoardEventArgs(string key, object? oldValue, object? newValue)
    {
        Key = key;
        OldValue = oldValue;
        NewValue = newValue;
    }
}