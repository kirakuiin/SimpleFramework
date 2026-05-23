using System.Collections;

namespace SimpleFramework.ECS;

/// <summary>
/// ECS 世界，负责实体生命周期、组件访问和结构迁移。
/// </summary>
public partial class World : IEnumerable<Archetype>, IEquatable<World>
{
    private static int _nextWorldId;

    private readonly Dictionary<TypeSignature, Archetype> _archetypes = new();
    private readonly List<EntitySlot> _slots = new();

    /// <summary>
    /// 创建 ECS 世界。
    /// </summary>
    /// <param name="name">世界名称。</param>
    public World(string name = "World")
    {
        WorldId = Interlocked.Increment(ref _nextWorldId);
        Name = name;
        GetOrCreateArchetype(new TypeSignature());
    }

    /// <summary>
    /// 世界唯一编号。
    /// </summary>
    public int WorldId { get; }

    /// <summary>
    /// 世界名称。
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// 当前存活实体数量。
    /// </summary>
    public int EntityCount { get; private set; }

    internal int ArchetypeVersion { get; private set; }

    internal int StructuralVersion { get; private set; }

    /// <summary>
    /// 创建空实体。
    /// </summary>
    /// <returns>新实体句柄。</returns>
    public Entity CreateEntity()
    {
        return CreateEntityWithComponents(Array.Empty<IComponent>());
    }

    /// <summary>
    /// 使用一个组件创建实体。
    /// </summary>
    /// <typeparam name="T1">组件类型。</typeparam>
    /// <param name="c1">组件值。</param>
    /// <returns>新实体句柄。</returns>
    public Entity CreateEntity<T1>(T1 c1) where T1 : IComponent
    {
        return CreateEntityWithComponents(new IComponent[] { c1 });
    }

    /// <summary>
    /// 使用两个组件创建实体。
    /// </summary>
    /// <typeparam name="T1">第一个组件类型。</typeparam>
    /// <typeparam name="T2">第二个组件类型。</typeparam>
    /// <param name="c1">第一个组件值。</param>
    /// <param name="c2">第二个组件值。</param>
    /// <returns>新实体句柄。</returns>
    public Entity CreateEntity<T1, T2>(T1 c1, T2 c2)
        where T1 : IComponent
        where T2 : IComponent
    {
        return CreateEntityWithComponents(new IComponent[] { c1, c2 });
    }

    /// <summary>
    /// 使用三个组件创建实体。
    /// </summary>
    /// <typeparam name="T1">第一个组件类型。</typeparam>
    /// <typeparam name="T2">第二个组件类型。</typeparam>
    /// <typeparam name="T3">第三个组件类型。</typeparam>
    /// <param name="c1">第一个组件值。</param>
    /// <param name="c2">第二个组件值。</param>
    /// <param name="c3">第三个组件值。</param>
    /// <returns>新实体句柄。</returns>
    public Entity CreateEntity<T1, T2, T3>(T1 c1, T2 c2, T3 c3)
        where T1 : IComponent
        where T2 : IComponent
        where T3 : IComponent
    {
        return CreateEntityWithComponents(new IComponent[] { c1, c2, c3 });
    }

    /// <summary>
    /// 使用四个组件创建实体。
    /// </summary>
    /// <typeparam name="T1">第一个组件类型。</typeparam>
    /// <typeparam name="T2">第二个组件类型。</typeparam>
    /// <typeparam name="T3">第三个组件类型。</typeparam>
    /// <typeparam name="T4">第四个组件类型。</typeparam>
    /// <param name="c1">第一个组件值。</param>
    /// <param name="c2">第二个组件值。</param>
    /// <param name="c3">第三个组件值。</param>
    /// <param name="c4">第四个组件值。</param>
    /// <returns>新实体句柄。</returns>
    public Entity CreateEntity<T1, T2, T3, T4>(T1 c1, T2 c2, T3 c3, T4 c4)
        where T1 : IComponent
        where T2 : IComponent
        where T3 : IComponent
        where T4 : IComponent
    {
        return CreateEntityWithComponents(new IComponent[] { c1, c2, c3, c4 });
    }

    /// <summary>
    /// 销毁实体。
    /// </summary>
    /// <param name="entity">实体句柄。</param>
    /// <returns>如果实体被销毁则为 true；实体无效或已销毁时为 false。</returns>
    public bool DestroyEntity(Entity entity)
    {
        if (!TryGetSlot(entity, out var slot))
        {
            return false;
        }

        var moved = slot.Archetype.RemoveAtSwapBack(slot.Row);
        if (moved.HasValue)
        {
            _slots[moved.Value.Id].Row = slot.Row;
        }

        slot.Alive = false;
        slot.Archetype = null!;
        slot.Row = -1;
        EntityCount--;
        StructuralVersion++;
        return true;
    }

    /// <summary>
    /// 判断实体句柄是否仍然有效且存活。
    /// </summary>
    /// <param name="entity">实体句柄。</param>
    /// <returns>如果实体存活则为 true。</returns>
    public bool IsAlive(Entity entity)
    {
        return TryGetSlot(entity, out _);
    }

    /// <summary>
    /// 判断实体是否拥有指定组件。
    /// </summary>
    /// <typeparam name="T">组件类型。</typeparam>
    /// <param name="entity">实体句柄。</param>
    /// <returns>如果实体拥有该组件则为 true。</returns>
    public bool Has<T>(Entity entity) where T : IComponent
    {
        return TryGetSlot(entity, out var slot) && slot.Archetype.Signature.Has<T>();
    }

    /// <summary>
    /// 获取组件引用。返回的引用只在下一次结构变更前有效。
    /// </summary>
    /// <typeparam name="T">组件类型。</typeparam>
    /// <param name="entity">实体句柄。</param>
    /// <returns>组件引用。</returns>
    public ref T Get<T>(Entity entity) where T : IComponent
    {
        var slot = Validate(entity);
        if (!slot.Archetype.Signature.Has<T>())
        {
            throw new InvalidOperationException($"Entity {entity} does not contain component {typeof(T).Name}.");
        }

        return ref slot.Archetype.Get<T>(slot.Row);
    }

    /// <summary>
    /// 尝试获取实体组件。
    /// </summary>
    /// <typeparam name="T">组件类型。</typeparam>
    /// <param name="entity">实体句柄。</param>
    /// <param name="component">找到时返回组件值。</param>
    /// <returns>如果找到组件则为 true。</returns>
    public bool TryGet<T>(Entity entity, out T component) where T : IComponent
    {
        if (TryGetSlot(entity, out var slot) && slot.Archetype.Signature.Has<T>())
        {
            component = slot.Archetype.Get<T>(slot.Row);
            return true;
        }

        component = default!;
        return false;
    }

    /// <summary>
    /// 添加或更新实体组件。
    /// </summary>
    /// <typeparam name="T">组件类型。</typeparam>
    /// <param name="entity">实体句柄。</param>
    /// <param name="component">组件值。</param>
    /// <returns>原实体句柄。</returns>
    public Entity Add<T>(Entity entity, T component) where T : IComponent
    {
        var slot = Validate(entity);
        if (slot.Archetype.Signature.Has<T>())
        {
            slot.Archetype.Set(slot.Row, component);
            return entity;
        }

        var values = slot.Archetype.GetAllBoxed(slot.Row).ToDictionary(pair => pair.Key, pair => pair.Value);
        values[typeof(T)] = component;
        MoveToSignature(entity, slot, values.Keys, values);
        StructuralVersion++;
        return entity;
    }

    /// <summary>
    /// 更新实体已有组件。
    /// </summary>
    /// <typeparam name="T">组件类型。</typeparam>
    /// <param name="entity">实体句柄。</param>
    /// <param name="component">组件值。</param>
    /// <returns>原实体句柄。</returns>
    public Entity Set<T>(Entity entity, T component) where T : IComponent
    {
        var slot = Validate(entity);
        if (!slot.Archetype.Signature.Has<T>())
        {
            throw new InvalidOperationException($"Entity {entity} does not contain component {typeof(T).Name}.");
        }

        slot.Archetype.Set(slot.Row, component);
        return entity;
    }

    /// <summary>
    /// 移除实体组件。
    /// </summary>
    /// <typeparam name="T">组件类型。</typeparam>
    /// <param name="entity">实体句柄。</param>
    /// <returns>如果组件被移除则为 true。</returns>
    public bool Remove<T>(Entity entity) where T : IComponent
    {
        var slot = Validate(entity);
        var type = typeof(T);
        if (!slot.Archetype.Signature.Has(type))
        {
            return false;
        }

        var values = slot.Archetype.GetAllBoxed(slot.Row)
            .Where(pair => pair.Key != type)
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        MoveToSignature(entity, slot, values.Keys, values);
        StructuralVersion++;
        return true;
    }

    /// <summary>
    /// 获取实体当前所属原型。
    /// </summary>
    /// <param name="entity">实体句柄。</param>
    /// <returns>实体当前原型。</returns>
    public Archetype GetArchetype(Entity entity)
    {
        return Validate(entity).Archetype;
    }

    /// <summary>
    /// 根据实体编号获取当前存活实体句柄。
    /// </summary>
    /// <param name="id">实体编号。</param>
    /// <returns>实体存活时返回实体句柄，否则返回 null。</returns>
    public Entity? GetEntity(int id)
    {
        if (id < 0 || id >= _slots.Count)
        {
            return null;
        }

        var slot = _slots[id];
        return slot.Alive ? new Entity(WorldId, id, slot.Version) : null;
    }

    /// <summary>
    /// 获取当前全部存活实体。
    /// </summary>
    /// <returns>存活实体序列。</returns>
    public IEnumerable<Entity> GetEntities()
    {
        for (var id = 0; id < _slots.Count; id++)
        {
            var slot = _slots[id];
            if (slot.Alive)
            {
                yield return new Entity(WorldId, id, slot.Version);
            }
        }
    }

    /// <summary>
    /// 创建空查询。
    /// </summary>
    /// <returns>查询对象。</returns>
    public Query Query()
    {
        return new Query(this);
    }

    /// <summary>
    /// 创建包含一个组件类型的查询。
    /// </summary>
    /// <typeparam name="T1">组件类型。</typeparam>
    /// <returns>查询对象。</returns>
    public Query Query<T1>() where T1 : IComponent
    {
        return Query().Has<T1>();
    }

    /// <summary>
    /// 创建包含两个组件类型的查询。
    /// </summary>
    /// <typeparam name="T1">第一个组件类型。</typeparam>
    /// <typeparam name="T2">第二个组件类型。</typeparam>
    /// <returns>查询对象。</returns>
    public Query Query<T1, T2>()
        where T1 : IComponent
        where T2 : IComponent
    {
        return Query().Has<T1>().Has<T2>();
    }

    /// <summary>
    /// 创建包含三个组件类型的查询。
    /// </summary>
    /// <typeparam name="T1">第一个组件类型。</typeparam>
    /// <typeparam name="T2">第二个组件类型。</typeparam>
    /// <typeparam name="T3">第三个组件类型。</typeparam>
    /// <returns>查询对象。</returns>
    public Query Query<T1, T2, T3>()
        where T1 : IComponent
        where T2 : IComponent
        where T3 : IComponent
    {
        return Query().Has<T1>().Has<T2>().Has<T3>();
    }

    /// <summary>
    /// 创建包含四个组件类型的查询。
    /// </summary>
    /// <typeparam name="T1">第一个组件类型。</typeparam>
    /// <typeparam name="T2">第二个组件类型。</typeparam>
    /// <typeparam name="T3">第三个组件类型。</typeparam>
    /// <typeparam name="T4">第四个组件类型。</typeparam>
    /// <returns>查询对象。</returns>
    public Query Query<T1, T2, T3, T4>()
        where T1 : IComponent
        where T2 : IComponent
        where T3 : IComponent
        where T4 : IComponent
    {
        return Query().Has<T1>().Has<T2>().Has<T3>().Has<T4>();
    }

    /// <summary>
    /// 根据预制体创建一个实体。
    /// </summary>
    /// <param name="prefab">实体预制体。</param>
    /// <returns>新实体句柄。</returns>
    public Entity Instantiate(EntityPrefab prefab)
    {
        ArgumentNullException.ThrowIfNull(prefab);
        return CreateEntityWithComponents(prefab.GetComponents());
    }

    /// <summary>
    /// 销毁世界中的全部实体和原型。
    /// </summary>
    public void Destroy()
    {
        _archetypes.Clear();
        foreach (var slot in _slots)
        {
            if (slot.Alive)
            {
                slot.Version++;
            }

            slot.Alive = false;
            slot.Archetype = null!;
            slot.Row = -1;
        }

        EntityCount = 0;
        ArchetypeVersion++;
        StructuralVersion++;
        GetOrCreateArchetype(new TypeSignature());
    }

    internal IReadOnlyCollection<Archetype> GetAllArchetypes()
    {
        return _archetypes.Values;
    }

    internal Entity CreateEntityWithBufferedComponents(IReadOnlyCollection<IComponent> components)
    {
        return CreateEntityWithComponents(components);
    }

    public override string ToString()
    {
        return $"World '{Name}' ({EntityCount} entities)";
    }

    public IEnumerator<Archetype> GetEnumerator()
    {
        return _archetypes.Values.Where(archetype => archetype.EntityCount > 0).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public bool Equals(World? other)
    {
        return ReferenceEquals(this, other);
    }

    private Entity CreateEntityWithComponents(IReadOnlyCollection<IComponent> components)
    {
        var values = new Dictionary<Type, IComponent>();
        foreach (var component in components)
        {
            var type = component.GetType();
            if (!values.TryAdd(type, component))
            {
                throw new ArgumentException($"Duplicate component type {type.Name}.", nameof(components));
            }
        }

        return CreateEntityWithComponents(values);
    }

    private Entity CreateEntityWithComponents(IReadOnlyDictionary<Type, IComponent> values)
    {
        var signature = new TypeSignature(values.Keys);
        var archetype = GetOrCreateArchetype(signature);
        var id = _slots.Count;
        var slot = new EntitySlot { Version = 1, Alive = true, Archetype = archetype };
        var entity = new Entity(WorldId, id, slot.Version);
        slot.Row = archetype.Add(entity, values);
        _slots.Add(slot);
        EntityCount++;
        StructuralVersion++;
        return entity;
    }

    private Archetype GetOrCreateArchetype(TypeSignature signature)
    {
        if (_archetypes.TryGetValue(signature, out var archetype))
        {
            return archetype;
        }

        archetype = new Archetype(signature);
        _archetypes[signature] = archetype;
        ArchetypeVersion++;
        return archetype;
    }

    private void MoveToSignature(
        Entity entity,
        EntitySlot slot,
        IEnumerable<Type> types,
        IReadOnlyDictionary<Type, IComponent> values)
    {
        var oldArchetype = slot.Archetype;
        var oldRow = slot.Row;
        var target = GetOrCreateArchetype(new TypeSignature(types));
        var newRow = target.Add(entity, values);
        var moved = oldArchetype.RemoveAtSwapBack(oldRow);

        if (moved.HasValue)
        {
            _slots[moved.Value.Id].Row = oldRow;
        }

        slot.Archetype = target;
        slot.Row = newRow;
    }

    private EntitySlot Validate(Entity entity)
    {
        if (!TryGetSlot(entity, out var slot))
        {
            throw new InvalidOperationException($"Invalid entity handle {entity} for world {WorldId}.");
        }

        return slot;
    }

    private bool TryGetSlot(Entity entity, out EntitySlot slot)
    {
        slot = null!;

        if (entity.WorldId != WorldId || entity.Id < 0 || entity.Id >= _slots.Count)
        {
            return false;
        }

        var candidate = _slots[entity.Id];
        if (!candidate.Alive || candidate.Version != entity.Version)
        {
            return false;
        }

        slot = candidate;
        return true;
    }

    private sealed class EntitySlot
    {
        public int Version;
        public bool Alive;
        public Archetype Archetype = null!;
        public int Row;
    }
}
