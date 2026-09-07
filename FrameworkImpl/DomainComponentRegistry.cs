using System.Runtime.CompilerServices;

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

/// <summary>描述一个组件的分类、唯一注册键、实例及生命周期资源。</summary>
internal sealed class DomainComponentEntry
{
    private readonly List<IUnRegister> _ownedResources = new();

    public DomainComponentEntry(ComponentCategory category, Type key, object instance)
    {
        Category = category;
        Key = key;
        Instance = instance;
    }

    public ComponentCategory Category { get; }
    public Type Key { get; }
    public object Instance { get; }
    public bool InitializationStarted { get; set; }
    public bool Published { get; set; }
    public ComponentContextBase? Context { get; set; }

    public IUnRegister Own(IUnRegister token)
    {
        IUnRegister? owned = null;
        owned = new CustomUnRegister(() =>
        {
            _ownedResources.Remove(owned!);
            token.UnRegister();
        });
        _ownedResources.Add(owned);
        return owned;
    }

    public IEnumerable<IUnRegister> TakeOwnedResourcesReverse()
    {
        // 先转移剩余 token，避免取消回调修改正在逆序遍历的归属列表。
        var resources = _ownedResources.ToArray();
        _ownedResources.Clear();
        for (var index = resources.Length - 1; index >= 0; index--) yield return resources[index];
    }
}

/// <summary>维护单个 Domain 的三类组件候选、精确键和可赋值解析。</summary>
internal sealed class DomainComponentRegistry
{
    private readonly Dictionary<ComponentCategory, Dictionary<Type, DomainComponentEntry>> _published = new()
    {
        [ComponentCategory.Model] = new(),
        [ComponentCategory.System] = new(),
        [ComponentCategory.Utility] = new()
    };

    private readonly Dictionary<object, DomainComponentEntry> _allByInstance = new(ReferenceEqualityComparer.Instance);

    public IReadOnlyCollection<DomainComponentEntry> Published(ComponentCategory category) => _published[category].Values;

    public bool HasKey(ComponentCategory category, Type key) =>
        _published[category].ContainsKey(key) || _allByInstance.Values.Any(entry => entry.Category == category && entry.Key == key);

    public DomainComponentEntry AddCandidate(ComponentCategory category, Type key, object instance)
    {
        if (HasKey(category, key))
        {
            throw new InvalidOperationException($"{category} 注册键 {key.FullName} 已存在，v2 不支持替换。");
        }

        if (_allByInstance.TryGetValue(instance, out var existing))
        {
            throw new InvalidOperationException(
                $"实例 {instance.GetType().FullName} 已在当前 Domain 以 {existing.Category}/{existing.Key.FullName} 注册，不能重复注册或跨分类注册。");
        }

        var entry = new DomainComponentEntry(category, key, instance);
        _allByInstance.Add(instance, entry);
        return entry;
    }

    public void Publish(DomainComponentEntry entry)
    {
        _published[entry.Category].Add(entry.Key, entry);
        entry.Published = true;
    }

    public void Unpublish(DomainComponentEntry entry)
    {
        if (entry.Published) _published[entry.Category].Remove(entry.Key);
        entry.Published = false;
    }

    public void Forget(DomainComponentEntry entry)
    {
        Unpublish(entry);
        _allByInstance.Remove(entry.Instance);
    }

    public bool TryResolveLocal(ComponentCategory category, Type requested, string domainName, out object? instance)
    {
        var entries = _published[category];
        if (entries.TryGetValue(requested, out var exact))
        {
            instance = exact.Instance;
            return true;
        }

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

    public void ClearUtilities()
    {
        foreach (var entry in _published[ComponentCategory.Utility].Values.ToArray()) Forget(entry);
    }

    public void ClearAllCandidates()
    {
        _allByInstance.Clear();
        foreach (var category in _published.Values) category.Clear();
    }
}

/// <summary>跨 Domain 跟踪 Model/System 实例的一次性独占生命周期所有权。</summary>
internal static class LifecycleOwnershipTracker
{
    private static readonly ConditionalWeakTable<object, Ownership> Records = new();

    public static void Reserve(object instance, object owner, ComponentCategory category, Type key)
    {
        if (Records.TryGetValue(instance, out var existing))
        {
            throw new InvalidOperationException(
                $"生命周期实例 {instance.GetType().FullName} 已被 {existing.Category}/{existing.Key.FullName} 消耗，不能再次注册。");
        }

        Records.Add(instance, new Ownership(owner, category, key));
    }

    public static void Begin(object instance, object owner)
    {
        var record = Get(instance, owner);
        record.Started = true;
    }

    public static void AttachContext(object instance, object owner, ComponentContextBase context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Get(instance, owner).Context = context;
    }

    public static ComponentContextBase GetContext(object instance)
    {
        if (!Records.TryGetValue(instance, out var record) || record.Context is null)
        {
            throw new InvalidOperationException($"生命周期组件 {instance.GetType().FullName} 当前没有可用的 Domain Context。");
        }

        return record.Context;
    }

    public static void DetachContext(object instance, ComponentContextBase? context)
    {
        if (context is null || !Records.TryGetValue(instance, out var record)) return;
        if (ReferenceEquals(record.Context, context)) record.Context = null;
    }

    public static void CancelUntouched(object instance, object owner)
    {
        var record = Get(instance, owner);
        if (record.Started) return;
        Records.Remove(instance);
    }

    private static Ownership Get(object instance, object owner)
    {
        if (!Records.TryGetValue(instance, out var record) || !ReferenceEquals(record.Owner, owner))
        {
            throw new InvalidOperationException("生命周期实例不属于当前 Domain。");
        }

        return record;
    }

    /// <summary>记录生命周期实例的唯一所有者、分类、键及是否已经开始初始化。</summary>
    private sealed class Ownership(object owner, ComponentCategory category, Type key)
    {
        public object Owner { get; } = owner;
        public ComponentCategory Category { get; } = category;
        public Type Key { get; } = key;
        public bool Started { get; set; }
        public ComponentContextBase? Context { get; set; }
    }
}
