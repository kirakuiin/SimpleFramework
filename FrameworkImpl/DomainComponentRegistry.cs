namespace SimpleFramework.FrameworkImpl;

/// <summary>Domain 注册表中的组件分类。</summary>
internal enum ComponentCategory
{
    /// <summary>由 Domain 管理生命周期的 Model。</summary>
    Model,

    /// <summary>由 Domain 管理生命周期的 System。</summary>
    System,

    /// <summary>由调用方管理生命周期的 Utility。</summary>
    Utility
}

/// <summary>一条注册记录：分类、唯一注册键和实例。</summary>
/// <param name="Category">组件分类。</param>
/// <param name="Key">注册键，即查找时的精确匹配类型。</param>
/// <param name="Instance">组件实例。</param>
internal sealed record DomainComponentEntry(ComponentCategory Category, Type Key, object Instance)
{
    /// <summary>用于日志的组件描述。</summary>
    public override string ToString() =>
        $"{Category}（契约 {Key.FullName ?? Key.Name}，实现 {Instance.GetType().FullName ?? Instance.GetType().Name}）";
}

/// <summary>维护单个 Domain 的三类组件，提供精确键与唯一可赋值查找。</summary>
internal sealed class DomainComponentRegistry
{
    /// <summary>按分类保存的注册键索引。</summary>
    private readonly Dictionary<ComponentCategory, Dictionary<Type, DomainComponentEntry>> _entries = new()
    {
        [ComponentCategory.Model] = new(),
        [ComponentCategory.System] = new(),
        [ComponentCategory.Utility] = new()
    };

    /// <summary>已注册实例集合，按引用判等，用于拒绝同一实例占用多个键或分类。</summary>
    private readonly HashSet<object> _instances = new(ReferenceEqualityComparer.Instance);

    /// <summary>添加一条注册记录。</summary>
    /// <exception cref="InvalidOperationException">注册键已存在，或实例已经注册过。</exception>
    public DomainComponentEntry Add(ComponentCategory category, Type key, object instance, string domainName)
    {
        var entries = _entries[category];
        if (entries.ContainsKey(key))
        {
            throw new InvalidOperationException($"Domain {domainName} 的 {category} 注册键 {key.FullName} 已存在，不支持替换。");
        }

        if (!_instances.Add(instance))
        {
            throw new InvalidOperationException($"实例 {instance.GetType().FullName} 已在 Domain {domainName} 中注册，不能重复注册。");
        }

        var entry = new DomainComponentEntry(category, key, instance);
        entries.Add(key, entry);
        return entry;
    }

    /// <summary>在本 Domain 内查找：先精确键，再唯一可赋值实例。</summary>
    /// <exception cref="InvalidOperationException">存在多个可赋值候选。</exception>
    public bool TryResolveLocal(ComponentCategory category, Type requested, string domainName, out object? instance)
    {
        var entries = _entries[category];
        if (entries.TryGetValue(requested, out var exact))
        {
            instance = exact.Instance;
            return true;
        }

        // 查找处于热路径，用循环而不是 LINQ，命中唯一候选时不分配内存；只有歧义时才构造候选列表。
        DomainComponentEntry? match = null;
        List<DomainComponentEntry>? candidates = null;
        foreach (var entry in entries.Values)
        {
            if (!requested.IsInstanceOfType(entry.Instance)) continue;

            if (match is null)
            {
                match = entry;
                continue;
            }

            candidates ??= [match];
            candidates.Add(entry);
        }

        if (candidates is not null)
        {
            var details = string.Join(", ", candidates.Select(entry =>
                $"键={entry.Key.FullName}, 运行时类型={entry.Instance.GetType().FullName}"));
            throw new InvalidOperationException(
                $"Domain {domainName} 解析 {category} {requested.FullName} 时存在多个本地候选：{details}。");
        }

        instance = match?.Instance;
        return match is not null;
    }

    /// <summary>清空全部注册；不会释放 Utility，它们由调用方管理。</summary>
    public void Clear()
    {
        foreach (var entries in _entries.Values) entries.Clear();
        _instances.Clear();
    }
}
