using System.Collections;

namespace SimpleFramework.ECS;

/// <summary>
/// 按组件包含和排除条件筛选实体。
/// </summary>
public sealed class Query : IEnumerable<Entity>
{
    private readonly HashSet<Type> _exclude = new();
    private readonly HashSet<Type> _include = new();
    private readonly List<Archetype> _matchingArchetypes = new();
    private readonly IReadOnlyList<Archetype> _matchingArchetypesView;
    private readonly World _world;
    private int _lastArchetypeVersion = -1;

    /// <summary>
    /// 创建世界查询。
    /// </summary>
    /// <param name="world">要查询的世界。</param>
    public Query(World world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _matchingArchetypesView = _matchingArchetypes.AsReadOnly();
    }

    /// <summary>
    /// 查询所属世界。
    /// </summary>
    public World World => _world;

    /// <summary>
    /// 添加必须包含的组件类型。
    /// </summary>
    /// <typeparam name="T">组件类型。</typeparam>
    public Query Has<T>() where T : IComponent
    {
        return Has(typeof(T));
    }

    /// <summary>
    /// 添加必须包含的组件类型。
    /// </summary>
    /// <param name="type">组件类型。</param>
    public Query Has(Type type)
    {
        _include.Add(ValidateComponentType(type));
        Invalidate();
        return this;
    }

    /// <summary>
    /// 添加必须排除的组件类型。
    /// </summary>
    /// <typeparam name="T">组件类型。</typeparam>
    public Query Not<T>() where T : IComponent
    {
        return Not(typeof(T));
    }

    /// <summary>
    /// 添加必须排除的组件类型。
    /// </summary>
    /// <param name="type">组件类型。</param>
    public Query Not(Type type)
    {
        _exclude.Add(ValidateComponentType(type));
        Invalidate();
        return this;
    }

    /// <summary>
    /// 清除全部查询条件。
    /// </summary>
    public Query Clear()
    {
        _include.Clear();
        _exclude.Clear();
        Invalidate();
        return this;
    }

    /// <summary>
    /// 获取当前匹配的原型只读视图；后续刷新会更新同一视图的内容。
    /// </summary>
    /// <returns>不能由调用方修改的原型列表视图。</returns>
    public IReadOnlyList<Archetype> GetArchetypes()
    {
        Refresh();
        return _matchingArchetypesView;
    }

    /// <summary>
    /// 对每个匹配实体执行操作。
    /// </summary>
    /// <param name="action">实体处理函数。</param>
    public void Foreach(Action<Entity> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        foreach (var entity in this)
        {
            action(entity);
        }
    }

    public IEnumerator<Entity> GetEnumerator()
    {
        Refresh();
        var structuralVersion = _world.StructuralVersion;

        foreach (var archetype in _matchingArchetypes)
        {
            for (var row = 0; row < archetype.EntityCount; row++)
            {
                ThrowIfStructureChanged(structuralVersion);
                yield return archetype.GetEntity(row);
                ThrowIfStructureChanged(structuralVersion);
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public override string ToString()
    {
        return
            $"Query [Include: {string.Join(", ", _include.Select(type => type.Name))}, Exclude: {string.Join(", ", _exclude.Select(type => type.Name))}]";
    }

    private void Refresh()
    {
        if (_lastArchetypeVersion == _world.ArchetypeVersion)
        {
            return;
        }

        _matchingArchetypes.Clear();
        foreach (var archetype in _world.GetAllArchetypes())
        {
            if (Matches(archetype))
            {
                _matchingArchetypes.Add(archetype);
            }
        }

        _lastArchetypeVersion = _world.ArchetypeVersion;
    }

    private bool Matches(Archetype archetype)
    {
        return _include.All(archetype.Signature.Has) && !_exclude.Any(archetype.Signature.Has);
    }

    private void Invalidate()
    {
        _lastArchetypeVersion = -1;
    }

    private void ThrowIfStructureChanged(int structuralVersion)
    {
        if (_world.StructuralVersion != structuralVersion)
        {
            throw new InvalidOperationException("World structure changed during query enumeration.");
        }
    }

    private static Type ValidateComponentType(Type type)
    {
        if (type is null || !typeof(IComponent).IsAssignableFrom(type))
        {
            throw new ArgumentException("Type must implement IComponent.", nameof(type));
        }

        return type;
    }
}
