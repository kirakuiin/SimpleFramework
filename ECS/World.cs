using System.Collections;
using SimpleFramework.Collections;

namespace SimpleFramework.ECS;

/// <summary>
/// 表示ECS系统中所有实体和原型的主容器。World管理实体的创建和组织及其组件。
/// </summary>
/// <remarks>
/// World负责：
/// - 创建和管理实体
/// - 根据实体的组件组成将其组织到原型中
/// - 提供查询功能以查找和处理实体
/// - 管理实体和原型的生命周期
/// </remarks>
public partial class World : IEnumerable<Archetype>, IEquatable<World>
{
    private readonly Dictionary<TypeSignature, Archetype> _signatureToArchetype = new();
    // ReSharper disable once CollectionNeverUpdated.Local
    private readonly DefaultDict<TypeSignature, List<Entity>> _signatureToEntities = new(() => new List<Entity>());
    private readonly Dictionary<int, Entity> _idToEntity = new();
    
    /// <summary>
    /// 当Archetype发生变化时触发的事件。
    /// </summary>
    public event Action<World, Archetype>? OnArchetypeUpdate;
    
    /// <summary>
    /// 当实体被添加到世界时触发的事件。
    /// </summary>
    public event Action<World, Entity>? OnEntityAdded;
    
    /// <summary>
    /// 当实体被移除时触发的事件。
    /// </summary>
    public event Action<World, Entity>? OnEntityRemoved;

    /// <summary>
    /// 初始化MyWorld类的新实例。
    /// </summary>
    /// <param name="name">世界的名称。默认为"World"。</param>
    public World(string name = "World")
    {
        Name = name;
    }

    /// <summary>
    /// 获取此世界的名称。
    /// </summary>
    public string Name { init; get; }

    /// <summary>
    /// 获取此世界中的实体总数。
    /// </summary>
    public int EntityCount => _idToEntity.Count;

    /// <summary>
    /// 创建一个没有组件的新实体。
    /// </summary>
    /// <returns>新创建的实体。</returns>
    public Entity CreateEntity()
    {
        var signature = new TypeSignature();
        return GetArchetype(signature).CreateEntity();
    }

    /// <summary>
    /// 创建一个具有指定组件类型的新实体。
    /// </summary>
    /// <param name="componentTypes">实体应具有的组件类型。</param>
    /// <returns>新创建的实体。</returns>
    public Entity CreateEntity(params Type[] componentTypes)
    {
        var signature = new TypeSignature(componentTypes);
        return GetArchetype(signature).CreateEntity();
    }

    /// <summary>
    /// 为此世界创建新查询。
    /// </summary>
    /// <returns>新的查询实例。</returns>
    public Query CreateQuery()
    {
        return new Query(this);
    }

    /// <summary>
    /// 根据实体的ID获取实体。
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public Entity? GetEntity(int id)
    {
        return _idToEntity.GetValueOrDefault(id);
    }
    
    /// <summary>
    /// 移除实体
    /// </summary>
    /// <param name="entity"></param>
    /// <returns>如果成功移除则返回真</returns>
    public bool RemoveEntity(Entity entity)
    {
        if (!entity.World.Equals(this)) return false;
        var archetype = entity.Archetype;
        _signatureToEntities[archetype.TypeSignature].Remove(entity);
        OnEntityRemoved?.Invoke(this, entity);
        return _idToEntity.Remove(entity.Id);
    }
    
    /// <summary>
    /// 移除实体
    /// </summary>
    /// <param name="id"></param>
    /// <returns>如果成功移除则返回真</returns>
    public bool RemoveEntity(int id)
    {
        return _idToEntity.TryGetValue(id, out var entity) && RemoveEntity(entity);
    }

    /// <summary>
    /// 获取此世界中所有实体的列表。
    /// </summary>
    /// <returns>所有实体的列表。</returns>
    public IEnumerable<Entity> GetEntities()
    {
        return _idToEntity.Values;
    }

    /// <summary>
    /// 获取或创建具有指定类型签名的原型。
    /// </summary>
    /// <param name="signature">要查找或创建原型的类型签名。</param>
    /// <returns>匹配的或新创建的原型。</returns>
    public Archetype GetArchetype(TypeSignature signature)
    {
        if (_signatureToArchetype.TryGetValue(signature, out var archetype))
        {
            return archetype;
        }

        archetype = new Archetype(this, signature);
        _signatureToArchetype[signature] = archetype;
        OnArchetypeUpdate?.Invoke(this, archetype);
        return archetype;
    }

    /// <summary>
    /// 移动entity到新的原型
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="before">旧的签名</param>
    /// <param name="after">新的签名</param>
    internal void MoveEntity(Entity entity, TypeSignature before, TypeSignature after)
    {
        if (!entity.World.Equals(this)) return;
        
        _signatureToEntities[before].Remove(entity);
        _signatureToEntities[after].Add(entity);
    }

    /// <summary>
    /// 销毁此世界及其所有实体和原型。
    /// </summary>
    public void Destroy()
    {
        _signatureToArchetype.Clear();
        _signatureToEntities.Clear();
        _idToEntity.Clear();
    }

    /// <summary>
    /// 添加一个实体的具体逻辑
    /// </summary>
    /// <param name="entity"></param>
    internal void AddEntity(Entity entity)
    {
        if (!entity.World.Equals(this)) return;
        _idToEntity[entity.Id] = entity;
        _signatureToEntities[entity.Archetype.TypeSignature].Add(entity);
        OnEntityAdded?.Invoke(this, entity);
    }
    
    /// <summary>
    /// 获取某个签名下的所有的Entity
    /// </summary>
    /// <param name="signature"></param>
    /// <returns></returns>
    internal IEnumerable<Entity> GetEntities(TypeSignature signature)
    {
        return _signatureToEntities.TryGetValue(signature, out var entities) ? entities : Enumerable.Empty<Entity>();
    }

    /// <summary>
    /// 返回表示当前世界的字符串。
    /// </summary>
    /// <returns>包含世界名称和实体数量的字符串。</returns>
    public override string ToString()
    {
        return $"World '{Name}' ({EntityCount} entities)";
    }

    public IEnumerator<Archetype> GetEnumerator()
    {
        return (from pair in _signatureToEntities where pair.Value.Count > 0 select GetArchetype(pair.Key)).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public bool Equals(World? other)
    {
        return other is not null && ReferenceEquals(this, other);
    }
}