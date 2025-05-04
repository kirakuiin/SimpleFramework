using System.Collections;
using SimpleFramework.Utility.Extensions;

namespace SimpleFramework.ECS;

/// <summary>
/// 表示基于实体组件组成过滤实体的查询。查询用于高效地查找和处理符合特定条件的实体。
/// </summary>
/// <remarks>
/// 查询可以基于必需组件（Has）和排除组件（Not）过滤实体。
/// 查询也可以可以基于标签（HasTag）和排除标签（NotTag）过滤实体。
/// 当实体改变其组件组成时，查询结果会自动更新。
/// </remarks>
public class Query : IEnumerable<Entity>
{
    private readonly World _world;
    private readonly TypeSignature _include = new();
    private readonly TypeSignature _exclude = new();
    private readonly HashSet<string> _includeTags = new();
    private readonly HashSet<string> _excludeTags = new();
    private readonly List<Archetype> _matchingArchetypes = new();

    /// <summary>
    /// 初始化MyQuery类的新实例。
    /// </summary>
    /// <param name="world">要查询实体的世界。</param>
    public Query(World world)
    {
        _world = world;
        _world.OnArchetypeUpdate += OnArchetypeUpdate;
    }

    private void OnArchetypeUpdate(World world, Archetype archetype)
    {
        if (!_world.Equals(world)) return;
        UpdateArchetype(archetype);
    }

    ~Query()
    {
        _world.OnArchetypeUpdate -= OnArchetypeUpdate;
    }

    /// <summary>
    /// 获取此查询操作的世界。
    /// </summary>
    public World World => _world;

    /// <summary>
    /// 向查询添加必需的组件类型。
    /// </summary>
    /// <typeparam name="T">要要求的组件类型。</typeparam>
    /// <returns>此查询实例用于方法链式调用。</returns>
    public Query Has<T>()
    {
        _include.Add<T>();
        UpdateMatchingArchetypes();
        return this;
    }

    /// <summary>
    /// 向查询添加必需的组件类型。
    /// </summary>
    /// <param name="type">要要求的组件类型。</param>
    /// <returns>此查询实例用于方法链式调用。</returns>
    public Query Has(Type type)
    {
        _include.Add(type);
        UpdateMatchingArchetypes();
        return this;
    }

    /// <summary>
    /// 向查询添加排除的组件类型。
    /// </summary>
    /// <typeparam name="T">要排除的组件类型。</typeparam>
    /// <returns>此查询实例用于方法链式调用。</returns>
    public Query Not<T>()
    {
        _exclude.Add<T>();
        UpdateMatchingArchetypes();
        return this;
    }

    /// <summary>
    /// 向查询添加排除的组件类型。
    /// </summary>
    /// <param name="type">要排除的组件类型。</param>
    /// <returns>此查询实例用于方法链式调用。</returns>
    public Query Not(Type type)
    {
        _exclude.Add(type);
        UpdateMatchingArchetypes();
        return this;
    }

    /// <summary>
    /// 清除查询中的所有过滤器。
    /// </summary>
    /// <returns>此查询实例用于方法链式调用。</returns>
    public Query Clear()
    {
        _include.Clear();
        _exclude.Clear();
        _includeTags.Clear();
        _excludeTags.Clear();
        _matchingArchetypes.Clear();
        return this;
    }

    /// <summary>
    /// 对每个匹配查询的实体执行操作。
    /// </summary>
    /// <param name="action">要对每个匹配实体执行的操作。</param>
    public void Foreach(Action<Entity> action)
    {
        foreach (var entity in this)
        {
            action(entity);
        }
    }
    
    /// <summary>
    /// 向查询添加必需的标签。
    /// </summary>
    /// <param name="tag"></param>
    /// <returns>此查询实例用于方法链式调用。</returns>
    public Query HasTag(string tag)
    {
        _includeTags.Add(tag);
        return this;
    }
    
    /// <summary>
    /// 向查询添加排除的标签。
    /// </summary>
    /// <param name="tag"></param>
    /// <returns>此查询实例用于方法链式调用。</returns>
    public Query NotTag(string tag)
    {
        _excludeTags.Add(tag);
        return this;
    }

    /// <summary>
    /// 获取包含匹配查询实体的所有原型的列表。
    /// </summary>
    /// <returns>匹配原型的列表。</returns>
    public IReadOnlyList<Archetype> GetArchetypes()
    {
        return _matchingArchetypes;
    }

    /// <summary>
    /// 根据当前查询过滤器更新匹配原型的列表。
    /// </summary>
    private void UpdateMatchingArchetypes()
    {
        _matchingArchetypes.Clear();
        _world.Apply(UpdateArchetype);
    }

    private void UpdateArchetype(Archetype archetype)
    {
        if (archetype.HasAll(_include) && !archetype.HasAny(_exclude))
        {
            _matchingArchetypes.Add(archetype);
        }
    }

    public override string ToString()
    {
        return $"Query [Include: {_include}, Exclude: {_exclude} ]" +
               $"IncludeTags: {string.Join(", ", _includeTags)}, " +
               $"ExcludeTags: {string.Join(", ", _excludeTags)}]";
    }
    
    public IEnumerator<Entity> GetEnumerator()
    {
        return (from archetype in _matchingArchetypes from entity in archetype where entity.HasTags(_includeTags.ToArray()) && 
            !entity.Tags.Any(tag => _excludeTags.Contains(tag)) select entity).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
