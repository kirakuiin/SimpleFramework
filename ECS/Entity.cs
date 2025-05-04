using System.Collections;
using SimpleFramework.Utility.Extensions;

namespace SimpleFramework.ECS;

/// <summary>
/// 表示ECS系统中的实体。实体是组件的容器，是识别和操作游戏对象的主要方式。
/// </summary>
/// <remarks>
/// 实体根据其组件组成自动分配到原型中。当添加或移除组件时，实体会自动移动到适当的原型。
/// </remarks>
public class Entity : IEnumerable<IComponent>, IEquatable<Entity>
{
    private static int _nextId = 1;
    
    private readonly Dictionary<Type, IComponent> _components = new();
    private readonly HashSet<string> _tags = new();
    private Archetype _currentArchetype;

    /// <summary>
    /// 在指定的世界中初始化MyEntity类的新实例。
    /// </summary>
    /// <param name="world">此实体所属的世界。</param>
    /// <param name="signature">此实体的原型</param>
    internal Entity(World world, TypeSignature signature)
    {
        Id = _nextId++;
        World = world;
        _currentArchetype = world.GetArchetype(signature);
        BuildComponentByArchetype();
    }

    private void BuildComponentByArchetype()
    {
        var signature = _currentArchetype.TypeSignature;
        foreach (var type in signature)
        {
            if (Activator.CreateInstance(type) is IComponent component)
            {
                _components.Add(type, component);
            }
        }
    }

    /// <summary>
    /// 获取此实体的唯一标识符。
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// 获取此实体所属的世界。
    /// </summary>
    public World World { get; init; }

    /// <summary>
    /// 获取此实体当前所属的原型。
    /// </summary>
    public Archetype Archetype => _currentArchetype;

    /// <summary>
    /// 检查实体是否具有指定类型的组件。
    /// </summary>
    /// <typeparam name="T">要检查的组件类型。</typeparam>
    /// <returns>如果实体具有该组件则为true；否则为false。</returns>
    public bool Has<T>() where T : IComponent
    {
        return _components.ContainsKey(typeof(T));
    }

    /// <summary>
    /// 检查实体是否具有指定类型的组件。
    /// </summary>
    /// <param name="type">要检查的组件类型。</param>
    /// <returns>如果实体具有该组件则为true；否则为false。</returns>
    public bool Has(Type type)
    {
        return _components.ContainsKey(type);
    }

    /// <summary>
    /// 向实体添加组件。
    /// </summary>
    /// <typeparam name="T">要添加的组件类型。</typeparam>
    /// <param name="component">要添加的组件实例。</param>
    public Entity Add<T>(T component) where T : IComponent
    {
        if (_components.TryAdd(typeof(T), component))
        {
            UpdateArchetype();
        }

        return this;
    }

    /// <summary>
    /// 从实体中移除组件。
    /// </summary>
    /// <typeparam name="T">要移除的组件类型。</typeparam>
    public Entity Remove<T>() where T : IComponent
    {
        Remove(typeof(T));
        return this;
    }

    /// <summary>
    /// 从实体中移除组件。
    /// </summary>
    /// <param name="type">要移除的组件类型。</param>
    public Entity Remove(Type type)
    {
        if (_components.Remove(type))
        {
            UpdateArchetype();
        }
        
        return this;
    }

    /// <summary>
    /// 根据实体的当前组件组成更新其原型。
    /// </summary>
    private void UpdateArchetype()
    {
        var newSignature = new TypeSignature(_components.Keys);
        var newArchetype = World.GetArchetype(newSignature);

        if (newArchetype == _currentArchetype) return;
        World.MoveEntity(this, _currentArchetype.TypeSignature, newSignature);
        _currentArchetype = newArchetype;
    }

    /// <summary>
    /// 从实体获取指定类型的组件。
    /// </summary>
    /// <typeparam name="T">要获取的组件类型。</typeparam>
    /// <returns>组件实例。</returns>
    /// <exception cref="KeyNotFoundException">当实体不具有该组件时抛出。</exception>
    public T Get<T>() where T : IComponent
    {
        if (_components.TryGetValue(typeof(T), out var component))
        {
            return (T)component;
        }
        throw new KeyNotFoundException($"实体不具有{typeof(T).Name}类型的组件");
    }

    /// <summary>
    /// 从实体获取指定类型的组件。
    /// </summary>
    /// <param name="type">要获取的组件类型。</param>
    /// <returns>组件实例。</returns>
    /// <exception cref="KeyNotFoundException">当实体不具有该组件时抛出。</exception>
    public object Get(Type type)
    {
        if (_components.TryGetValue(type, out var component))
        {
            return component;
        }
        throw new KeyNotFoundException($"实体不具有{type.Name}类型的组件");
    }

    /// <summary>
    /// 尝试从实体获取指定类型的组件。
    /// </summary>
    /// <typeparam name="T">要获取的组件类型。</typeparam>
    /// <param name="component">当此方法返回时，如果找到组件则包含该组件；否则为默认值。</param>
    /// <returns>如果找到组件则为true；否则为false。</returns>
    public bool TryGet<T>(out T component) where T : IComponent
    {
        if (_components.TryGetValue(typeof(T), out var obj))
        {
            component = (T)obj;
            return true;
        }
        component = default!;
        return false;
    }

    /// <summary>
    /// 尝试从实体获取指定类型的组件。
    /// </summary>
    /// <param name="type">要获取的组件类型。</param>
    /// <param name="component">当此方法返回时，如果找到组件则包含该组件；否则为null。</param>
    /// <returns>如果找到组件则为true；否则为false。</returns>
    public bool TryGet(Type type, out IComponent component)
    {
        return _components.TryGetValue(type, out component!);
    }

    /// <summary>
    /// 获取此实体中所有组件的数组。
    /// </summary>
    /// <returns>组件实例数组。</returns>
    public IComponent[] GetComponents()
    {
        var values = new IComponent[_components.Count];
        _components.Values.CopyTo(values, 0);
        return values;
    }
    
    /// <summary>
    /// 为实体打上标签
    /// </summary>
    /// <param name="tags">标签列表</param>
    /// <returns></returns>
    public Entity AddTag(params string[] tags)
    {
        tags.Apply(_tags.Add);
        return this;
    }

    /// <summary>
    /// 为实体移除标签
    /// </summary>
    /// <param name="tags">标签列表</param>
    /// <returns></returns>
    public Entity RemoveTag(params string[] tags)
    {
        tags.Apply(_tags.Remove);
        return this;
    }

    /// <summary>
    /// 判断实体是否含有标签
    /// </summary>
    /// <param name="tags">标签名</param>
    /// <returns>如果找到标签则为true；否则为false。</returns>
    public bool HasTags(params string[] tags)
    {
        return tags.All(tag => _tags.Contains(tag));
    }

    /// <summary>
    /// 得到实体的标签
    /// </summary>
    public IEnumerable<string> Tags => _tags;

    public override string ToString()
    {
        return $"Entity_{Id} sig:{_currentArchetype.TypeSignature} Tags:{string.Join(",", _tags)}";
    }

    public IEnumerator<IComponent> GetEnumerator()
    {
        return _components.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public bool Equals(Entity? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id == other.Id && World.Equals(other.World);
    }
}