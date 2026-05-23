namespace SimpleFramework.ECS;

/// <summary>
/// 单实体预制体，保存一组默认组件值。
/// </summary>
public sealed class EntityPrefab
{
    private readonly Dictionary<Type, IComponent> _components = new();

    private EntityPrefab()
    {
    }

    /// <summary>
    /// 创建空预制体。
    /// </summary>
    /// <returns>新的预制体。</returns>
    public static EntityPrefab Create()
    {
        return new EntityPrefab();
    }

    /// <summary>
    /// 添加默认组件值。
    /// </summary>
    /// <typeparam name="T">组件类型。</typeparam>
    /// <param name="component">默认组件值。</param>
    /// <returns>当前预制体。</returns>
    public EntityPrefab With<T>(T component) where T : IComponent
    {
        var type = typeof(T);
        if (!_components.TryAdd(type, component))
        {
            throw new ArgumentException($"Duplicate component type {type.Name}.", nameof(component));
        }

        return this;
    }

    internal IReadOnlyCollection<IComponent> GetComponents()
    {
        return _components.Values;
    }
}
