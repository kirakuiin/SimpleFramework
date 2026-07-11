namespace SimpleFramework.ECS;

/// <summary>
/// ECS 系统集合，负责按确定顺序统一更新系统。
/// </summary>
public sealed class SystemGroup
{
    private readonly List<Entry> _entries = new();
    private List<Entry>? _sortedEntries;
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
        InvalidateSortedEntries();
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
        InvalidateSortedEntries();
        return true;
    }

    /// <summary>
    /// 验证系统依赖并缓存排序结果，不执行系统更新。
    /// </summary>
    public void Validate()
    {
        GetSortedEntries();
    }

    /// <summary>
    /// 按顺序更新全部系统。
    /// </summary>
    /// <exception cref="InvalidOperationException">系统依赖成环，或当前系统组正在更新。</exception>
    public void Update()
    {
        UpdateCore(system => system.Update());
    }

    /// <summary>
    /// 按顺序使用帧间隔时间更新全部系统。
    /// </summary>
    /// <param name="deltaTime">距离上次更新经过的时间。</param>
    /// <exception cref="InvalidOperationException">系统依赖成环，或当前系统组正在更新。</exception>
    public void Update(float deltaTime)
    {
        UpdateCore(system => system.Update(deltaTime));
    }

    private void UpdateCore(Action<EcsSystem> update)
    {
        if (_isUpdating)
        {
            throw new InvalidOperationException("System group is already updating and cannot be updated reentrantly.");
        }

        _isUpdating = true;
        try
        {
            foreach (var entry in GetSortedEntries())
            {
                if (!entry.System.Enabled)
                {
                    continue;
                }

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
        if (_sortedEntries != null)
        {
            return _sortedEntries;
        }

        var baseline = _entries
            .OrderBy(entry => entry.Order)
            .ThenBy(entry => entry.Sequence)
            .ToList();

        _sortedEntries = StableTopologicalSort(baseline);
        return _sortedEntries;
    }

    private void InvalidateSortedEntries()
    {
        _sortedEntries = null;
    }

    private static List<Entry> StableTopologicalSort(List<Entry> baseline)
    {
        var outgoing = baseline.ToDictionary(entry => entry, _ => new List<Entry>());
        var indegree = baseline.ToDictionary(entry => entry, _ => 0);

        foreach (var entry in baseline)
        {
            foreach (var attribute in entry.System.GetType().GetCustomAttributes(typeof(RunAfterAttribute), true).Cast<RunAfterAttribute>())
            {
                foreach (var dependency in FindMatchingEntries(baseline, entry, attribute.SystemType))
                {
                    AddEdge(dependency, entry, outgoing, indegree);
                }
            }

            foreach (var attribute in entry.System.GetType().GetCustomAttributes(typeof(RunBeforeAttribute), true).Cast<RunBeforeAttribute>())
            {
                foreach (var target in FindMatchingEntries(baseline, entry, attribute.SystemType))
                {
                    AddEdge(entry, target, outgoing, indegree);
                }
            }
        }

        var sorted = new List<Entry>(baseline.Count);
        var emitted = new HashSet<Entry>();

        while (sorted.Count < baseline.Count)
        {
            var next = baseline.FirstOrDefault(entry => !emitted.Contains(entry) && indegree[entry] == 0);
            if (next == null)
            {
                throw CreateCycleException(baseline, outgoing, emitted);
            }

            sorted.Add(next);
            emitted.Add(next);

            foreach (var target in outgoing[next])
            {
                indegree[target]--;
            }
        }

        return sorted;
    }

    private static InvalidOperationException CreateCycleException(
        List<Entry> baseline,
        Dictionary<Entry, List<Entry>> outgoing,
        HashSet<Entry> emitted)
    {
        var cycle = FindCycle(baseline, outgoing, emitted);
        var chain = string.Join(" -> ", cycle.Select(entry => entry.System.GetType().Name));
        return new InvalidOperationException($"SystemGroup dependency cycle detected: {chain}");
    }

    private static List<Entry> FindCycle(
        List<Entry> baseline,
        Dictionary<Entry, List<Entry>> outgoing,
        HashSet<Entry> emitted)
    {
        var remaining = baseline.Where(entry => !emitted.Contains(entry)).ToHashSet();
        var state = remaining.ToDictionary(entry => entry, _ => 0);
        var path = new List<Entry>();
        var pathIndex = new Dictionary<Entry, int>();

        foreach (var entry in baseline)
        {
            if (!remaining.Contains(entry) || state[entry] != 0)
            {
                continue;
            }

            var cycle = Visit(entry, outgoing, remaining, state, path, pathIndex);
            if (cycle.Count > 0)
            {
                return cycle;
            }
        }

        return remaining.Take(1).ToList();
    }

    private static List<Entry> Visit(
        Entry entry,
        Dictionary<Entry, List<Entry>> outgoing,
        HashSet<Entry> remaining,
        Dictionary<Entry, int> state,
        List<Entry> path,
        Dictionary<Entry, int> pathIndex)
    {
        state[entry] = 1;
        pathIndex[entry] = path.Count;
        path.Add(entry);

        foreach (var target in outgoing[entry])
        {
            if (!remaining.Contains(target))
            {
                continue;
            }

            if (state[target] == 0)
            {
                var cycle = Visit(target, outgoing, remaining, state, path, pathIndex);
                if (cycle.Count > 0)
                {
                    return cycle;
                }
            }
            else if (state[target] == 1)
            {
                var cycle = path.Skip(pathIndex[target]).ToList();
                cycle.Add(target);
                return cycle;
            }
        }

        path.RemoveAt(path.Count - 1);
        pathIndex.Remove(entry);
        state[entry] = 2;

        return new List<Entry>();
    }

    private static IEnumerable<Entry> FindMatchingEntries(List<Entry> baseline, Entry source, Type systemType)
    {
        return baseline.Where(entry =>
            !ReferenceEquals(entry, source) &&
            entry.Order == source.Order &&
            systemType.IsAssignableFrom(entry.System.GetType()));
    }

    private static void AddEdge(
        Entry before,
        Entry after,
        Dictionary<Entry, List<Entry>> outgoing,
        Dictionary<Entry, int> indegree)
    {
        if (outgoing[before].Contains(after))
        {
            return;
        }

        outgoing[before].Add(after);
        indegree[after]++;
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
