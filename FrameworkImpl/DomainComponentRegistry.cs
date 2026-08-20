using System.Runtime.CompilerServices;
using System.Text;

namespace SimpleFramework.FrameworkImpl;

internal enum ComponentCategory
{
    System,
    Model,
    Utility
}

internal sealed class DomainComponentEntry
{
    private readonly List<IUnRegister> _ownedResources = new();

    public DomainComponentEntry(ComponentCategory category, Type primaryKey, object instance)
    {
        Category = category;
        PrimaryKey = primaryKey;
        Instance = instance;
    }

    public ComponentCategory Category { get; }

    public Type PrimaryKey { get; }

    public object Instance { get; }

    public bool IsLifecycleManaged => Category is ComponentCategory.System or ComponentCategory.Model;

    public bool IsActive { get; set; }

    public void OwnResource(IUnRegister resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        _ownedResources.Add(resource);
    }

    public IReadOnlyList<IUnRegister> TakeOwnedResourcesReverse()
    {
        var resources = _ownedResources.ToArray();
        Array.Reverse(resources);
        _ownedResources.Clear();
        return resources;
    }
}

internal sealed class DomainComponentRegistry
{
    private readonly Dictionary<ComponentCategory, Dictionary<Type, DomainComponentEntry>> _entries = new()
    {
        [ComponentCategory.System] = new(),
        [ComponentCategory.Model] = new(),
        [ComponentCategory.Utility] = new()
    };

    private readonly Dictionary<object, DomainComponentEntry> _entriesByInstance =
        new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<ComponentCategory, List<DomainComponentEntry>> _activationOrder = new()
    {
        [ComponentCategory.System] = new(),
        [ComponentCategory.Model] = new()
    };

    public object OwnershipToken { get; } = new();

    public DomainComponentEntry? GetExact(ComponentCategory category, Type key)
    {
        return _entries[category].GetValueOrDefault(key);
    }

    public DomainComponentEntry? GetByInstance(object instance)
    {
        return _entriesByInstance.GetValueOrDefault(instance);
    }

    public bool IsPublished(DomainComponentEntry entry)
    {
        return _entries[entry.Category].TryGetValue(entry.PrimaryKey, out var current) &&
               ReferenceEquals(current, entry);
    }

    public object? Resolve(ComponentCategory category, Type requestedType)
    {
        if (_entries[category].TryGetValue(requestedType, out var exact))
        {
            return exact.Instance;
        }

        var candidates = _entries[category].Values
            .Where(entry => requestedType.IsInstanceOfType(entry.Instance))
            .ToArray();

        return candidates.Length switch
        {
            0 => null,
            1 => candidates[0].Instance,
            _ => throw new AmbiguousComponentException(
                requestedType,
                candidates.Select(entry => entry.PrimaryKey),
                category.ToString())
        };
    }

    public DomainComponentEntry Publish(ComponentCategory category, Type key, object instance)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(instance);

        if (!key.IsInstanceOfType(instance))
        {
            throw new ArgumentException(
                $"Component {instance.GetType().FullName} is not assignable to registration key {key.FullName}.",
                nameof(instance));
        }

        if (_entries[category].ContainsKey(key))
        {
            throw new InvalidOperationException($"{category} key is already registered: {key.FullName}.");
        }

        if (_entriesByInstance.TryGetValue(instance, out var existing))
        {
            throw new InvalidOperationException(
                $"Component instance is already registered as {existing.Category} with key " +
                $"{existing.PrimaryKey.FullName}.");
        }

        var entry = new DomainComponentEntry(category, key, instance);
        _entries[category].Add(key, entry);
        _entriesByInstance.Add(instance, entry);
        return entry;
    }

    public void MarkActive(DomainComponentEntry entry)
    {
        if (!entry.IsLifecycleManaged || entry.IsActive)
        {
            return;
        }

        entry.IsActive = true;
        _activationOrder[entry.Category].Add(entry);
    }

    public void Unpublish(DomainComponentEntry entry)
    {
        if (_entries[entry.Category].TryGetValue(entry.PrimaryKey, out var current) &&
            ReferenceEquals(current, entry))
        {
            _entries[entry.Category].Remove(entry.PrimaryKey);
        }

        if (_entriesByInstance.TryGetValue(entry.Instance, out current) && ReferenceEquals(current, entry))
        {
            _entriesByInstance.Remove(entry.Instance);
        }

        if (entry.IsLifecycleManaged)
        {
            _activationOrder[entry.Category].Remove(entry);
            entry.IsActive = false;
        }
    }

    public IReadOnlyList<DomainComponentEntry> GetReverseActivationOrder(ComponentCategory category)
    {
        var result = _activationOrder[category].ToArray();
        Array.Reverse(result);
        return result;
    }

    public void ClearUtilities()
    {
        foreach (var entry in _entries[ComponentCategory.Utility].Values.ToArray())
        {
            Unpublish(entry);
        }
    }

    public void Clear()
    {
        foreach (var entries in _entries.Values)
        {
            entries.Clear();
        }

        _entriesByInstance.Clear();
        foreach (var entries in _activationOrder.Values)
        {
            entries.Clear();
        }
    }

    public override string ToString()
    {
        var builder = new StringBuilder();
        AppendCategory(builder, ComponentCategory.System);
        AppendCategory(builder, ComponentCategory.Model);
        AppendCategory(builder, ComponentCategory.Utility);
        return builder.ToString();
    }

    private void AppendCategory(StringBuilder builder, ComponentCategory category)
    {
        var entries = _entries[category].Values.ToArray();
        if (entries.Length == 0)
        {
            return;
        }

        builder.AppendLine($"----{category}----");
        foreach (var entry in entries)
        {
            builder.AppendLine(entry.Instance.GetType().Name);
        }
    }
}

internal static class LifecycleOwnershipTracker
{
    private static readonly ConditionalWeakTable<object, LifecycleOwnership> Records = new();

    public static void Reserve(
        object instance,
        object ownerToken,
        ComponentCategory category,
        Type primaryKey)
    {
        if (Records.TryGetValue(instance, out var existing))
        {
            throw new InvalidOperationException(
                $"Lifecycle component {instance.GetType().FullName} cannot be registered as {category} " +
                $"with key {primaryKey.FullName}; it is already {existing.State.ToString().ToLowerInvariant()} " +
                $"as {existing.Category} with key {existing.PrimaryKey.FullName}.");
        }

        Records.Add(instance, new LifecycleOwnership(ownerToken, category, primaryKey));
    }

    public static void BeginInitialization(object instance, object ownerToken)
    {
        GetOwnedRecord(instance, ownerToken).State = LifecycleOwnershipState.Initializing;
    }

    public static void CancelReservation(object instance, object ownerToken)
    {
        var record = GetOwnedRecord(instance, ownerToken);
        if (record.State != LifecycleOwnershipState.Reserved)
        {
            throw new InvalidOperationException("Only an uninitialized lifecycle reservation can be cancelled.");
        }

        Records.Remove(instance);
    }

    public static void MarkActive(object instance, object ownerToken)
    {
        GetOwnedRecord(instance, ownerToken).State = LifecycleOwnershipState.Active;
    }

    public static void BeginRelease(object instance, object ownerToken)
    {
        GetOwnedRecord(instance, ownerToken).State = LifecycleOwnershipState.Releasing;
    }

    public static void MarkReleased(object instance, object ownerToken)
    {
        GetOwnedRecord(instance, ownerToken).State = LifecycleOwnershipState.Released;
    }

    private static LifecycleOwnership GetOwnedRecord(object instance, object ownerToken)
    {
        if (!Records.TryGetValue(instance, out var record) || !ReferenceEquals(record.OwnerToken, ownerToken))
        {
            throw new InvalidOperationException(
                $"Lifecycle component {instance.GetType().FullName} is not owned by this Domain.");
        }

        return record;
    }

    private sealed class LifecycleOwnership
    {
        public LifecycleOwnership(object ownerToken, ComponentCategory category, Type primaryKey)
        {
            OwnerToken = ownerToken;
            Category = category;
            PrimaryKey = primaryKey;
        }

        public object OwnerToken { get; }

        public ComponentCategory Category { get; }

        public Type PrimaryKey { get; }

        public LifecycleOwnershipState State { get; set; } = LifecycleOwnershipState.Reserved;
    }

    private enum LifecycleOwnershipState
    {
        Reserved,
        Initializing,
        Active,
        Releasing,
        Released
    }
}
