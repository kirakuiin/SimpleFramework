using System.Collections;

namespace SimpleFramework.ECS;

/// <summary>
/// 表示共享相同组件类型的实体集合。原型用于高效地分组和处理具有相似结构的实体。
/// </summary>
/// <remarks>
/// 原型由World创建和管理。实体根据其组件组成自动分配到适当的原型。
/// </remarks>
public class Archetype : IEnumerable<Entity>, IEquatable<Archetype>
{
    private readonly World _world;
    private readonly TypeSignature _typeSignature;

    /// <summary>
    /// 初始化MyArchetype类的新实例。
    /// </summary>
    /// <param name="world">此原型所属的世界。</param>
    /// <param name="typeSignature">定义此原型结构的类型签名。</param>
    public Archetype(World world, TypeSignature typeSignature)
    {
        _world = world;
        _typeSignature = typeSignature;
    }

    /// <summary>
    /// 获取此原型所属的世界。
    /// </summary>
    public World World => _world;

    /// <summary>
    /// 获取定义此原型结构的类型签名。
    /// </summary>
    public TypeSignature TypeSignature => _typeSignature;

    /// <summary>
    /// 获取此原型中的实体数量。
    /// </summary>
    public int EntityCount => _world.GetEntities(_typeSignature).Count();

    /// <summary>
    /// 在此原型中创建新实体。
    /// </summary>
    /// <returns>新创建的实体。</returns>
    public Entity CreateEntity()
    {
        var entity = new Entity(_world, this._typeSignature);
        _world.AddEntity(entity);
        return entity;
    }

    /// <summary>
    /// 检查此原型是否具有指定类型的组件。
    /// </summary>
    /// <typeparam name="T">要检查的组件类型。</typeparam>
    /// <returns>如果此原型具有该组件类型则为true；否则为false。</returns>
    public bool Has<T>()
    {
        return _typeSignature.Has<T>();
    }

    /// <summary>
    /// 检查此原型是否具有指定类型的组件。
    /// </summary>
    /// <param name="type">要检查的组件类型。</param>
    /// <returns>如果此原型具有该组件类型则为true；否则为false。</returns>
    public bool Has(Type type)
    {
        return _typeSignature.Has(type);
    }
    
    /// <summary>
    /// 检查此原型是否匹配指定的类型签名。
    /// </summary>
    /// <param name="signature">要检查的类型签名。</param>
    /// <returns>如果此原型匹配该签名则为true；否则为false。</returns>
    public bool HasAll(TypeSignature signature)
    {
        return _typeSignature.HasAll(signature);
    }
    
    /// <summary>
    /// 检查此原型是否含有指定的类型签名。
    /// </summary>
    /// <param name="signature">要检查的类型签名。</param>
    /// <returns>如果此原型匹配该签名则为true；否则为false。</returns>
    public bool HasAny(TypeSignature signature)
    {
        return _typeSignature.HasAny(signature);
    }

    public override string ToString()
    {
        return $"Archetype [{_typeSignature}]";
    }

    public IEnumerator<Entity> GetEnumerator()
    {
        return _world.GetEntities(_typeSignature).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public bool Equals(Archetype? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return _world.Equals(other._world) && _typeSignature.Equals(other._typeSignature);
    }
}