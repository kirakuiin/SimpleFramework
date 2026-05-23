using System.Collections;

namespace SimpleFramework.ECS;

/// <summary>
/// 存储相同组件签名的实体行。
/// </summary>
public sealed class Archetype : IEnumerable<Entity>
{
    private readonly Dictionary<Type, IComponentColumn> _columns = new();
    private readonly List<Entity> _entities = new();

    internal Archetype(TypeSignature signature)
    {
        Signature = signature ?? throw new ArgumentNullException(nameof(signature));

        foreach (var type in signature)
        {
            _columns[type] = CreateColumn(type);
        }
    }

    /// <summary>
    /// 原型的组件签名。
    /// </summary>
    public TypeSignature Signature { get; }

    /// <summary>
    /// 原型中的实体数量。
    /// </summary>
    public int EntityCount => _entities.Count;

    internal int Add(Entity entity, IReadOnlyDictionary<Type, IComponent> components)
    {
        ArgumentNullException.ThrowIfNull(components);

        var row = _entities.Count;
        _entities.Add(entity);

        foreach (var type in Signature)
        {
            if (!components.TryGetValue(type, out var component))
            {
                throw new InvalidOperationException($"Missing component {type.Name}.");
            }

            _columns[type].AddBoxed(component);
        }

        return row;
    }

    internal int Add(Entity entity, IEnumerable<IComponent> components)
    {
        return Add(entity, components.ToDictionary(component => component.GetType(), component => component));
    }

    internal Entity GetEntity(int row)
    {
        return _entities[row];
    }

    internal ref T Get<T>(int row) where T : IComponent
    {
        return ref GetColumn<T>().GetRef(row);
    }

    internal void Set<T>(int row, T value) where T : IComponent
    {
        GetColumn<T>().GetRef(row) = value;
    }

    internal IComponent GetBoxed(int row, Type type)
    {
        return _columns[type].GetBoxed(row);
    }

    internal IReadOnlyDictionary<Type, IComponent> GetAllBoxed(int row)
    {
        var values = new Dictionary<Type, IComponent>();
        foreach (var type in Signature)
        {
            values[type] = GetBoxed(row, type);
        }

        return values;
    }

    internal Entity? RemoveAtSwapBack(int row)
    {
        var last = _entities.Count - 1;
        Entity? moved = null;

        if (row != last)
        {
            moved = _entities[last];
            _entities[row] = _entities[last];
        }

        _entities.RemoveAt(last);

        foreach (var column in _columns.Values)
        {
            column.RemoveAtSwapBack(row);
        }

        return moved;
    }

    /// <summary>
    /// 判断原型是否包含指定组件类型。
    /// </summary>
    /// <typeparam name="T">组件类型。</typeparam>
    /// <returns>如果包含该组件类型则为 true。</returns>
    public bool Has<T>() where T : IComponent
    {
        return Signature.Has<T>();
    }

    /// <summary>
    /// 判断原型是否包含指定组件类型。
    /// </summary>
    /// <param name="type">组件类型。</param>
    /// <returns>如果包含该组件类型则为 true。</returns>
    public bool Has(Type type)
    {
        return Signature.Has(type);
    }

    /// <summary>
    /// 判断原型是否包含签名中的全部组件类型。
    /// </summary>
    /// <param name="signature">要检查的签名。</param>
    /// <returns>如果包含全部组件类型则为 true。</returns>
    public bool HasAll(TypeSignature signature)
    {
        return Signature.HasAll(signature);
    }

    /// <summary>
    /// 判断原型是否包含签名中的任意组件类型。
    /// </summary>
    /// <param name="signature">要检查的签名。</param>
    /// <returns>如果包含任意组件类型则为 true。</returns>
    public bool HasAny(TypeSignature signature)
    {
        return Signature.HasAny(signature);
    }

    public IEnumerator<Entity> GetEnumerator()
    {
        return _entities.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public override string ToString()
    {
        return $"Archetype [{Signature}]";
    }

    private ComponentColumn<T> GetColumn<T>() where T : IComponent
    {
        if (!_columns.TryGetValue(typeof(T), out var column))
        {
            throw new InvalidOperationException($"Archetype does not contain component {typeof(T).Name}.");
        }

        return (ComponentColumn<T>)column;
    }

    private static IComponentColumn CreateColumn(Type type)
    {
        var columnType = typeof(ComponentColumn<>).MakeGenericType(type);
        return (IComponentColumn)Activator.CreateInstance(columnType)!;
    }
}
