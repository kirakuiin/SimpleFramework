namespace SimpleFramework.ECS;

/// <summary>
/// ECS 系统集合，负责按确定顺序统一更新系统。
/// </summary>
public sealed class SystemGroup
{
    private readonly List<Entry> _entries = new();
    private long _nextSequence;
    private bool _isUpdating;

    /// <summary>
    /// 添加系统，默认顺序为 0。
    /// </summary>
    /// <param name="system">要添加的系统。</param>
    public void Add(EcsSystem system)
    {
        Add(system, 0);
    }

    /// <summary>
    /// 添加系统并指定执行顺序。
    /// </summary>
    /// <param name="system">要添加的系统。</param>
    /// <param name="order">执行顺序，数值越小越先执行。</param>
    public void Add(EcsSystem system, int order)
    {
        ThrowIfUpdating();
        ArgumentNullException.ThrowIfNull(system);

        if (_entries.Any(entry => ReferenceEquals(entry.System, system)))
        {
            throw new ArgumentException("System is already added to this group.", nameof(system));
        }

        _entries.Add(new Entry(system, order, _nextSequence++));
    }

    /// <summary>
    /// 移除系统。
    /// </summary>
    /// <param name="system">要移除的系统。</param>
    /// <returns>如果找到并移除系统则为 true。</returns>
    public bool Remove(EcsSystem system)
    {
        ThrowIfUpdating();
        ArgumentNullException.ThrowIfNull(system);

        var index = _entries.FindIndex(entry => ReferenceEquals(entry.System, system));
        if (index < 0)
        {
            return false;
        }

        _entries.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// 按顺序更新全部系统。
    /// </summary>
    public void Update()
    {
        UpdateCore(system => system.Update());
    }

    /// <summary>
    /// 按顺序使用帧间隔时间更新全部系统。
    /// </summary>
    /// <param name="deltaTime">距离上次更新经过的时间。</param>
    public void Update(float deltaTime)
    {
        UpdateCore(system => system.Update(deltaTime));
    }

    private void UpdateCore(Action<EcsSystem> update)
    {
        _isUpdating = true;
        try
        {
            foreach (var entry in GetSortedEntries())
            {
                update(entry.System);
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private List<Entry> GetSortedEntries()
    {
        return _entries
            .OrderBy(entry => entry.Order)
            .ThenBy(entry => entry.Sequence)
            .ToList();
    }

    private void ThrowIfUpdating()
    {
        if (_isUpdating)
        {
            throw new InvalidOperationException("System group cannot be modified during update.");
        }
    }

    private sealed record Entry(EcsSystem System, int Order, long Sequence);
}
