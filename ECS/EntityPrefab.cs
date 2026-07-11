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
    /// <typeparam name="T">组件的声明类型；存储和重复检测使用组件值的运行时类型。</typeparam>
    /// <param name="component">默认组件值。</param>
    /// <returns>当前预制体。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="component"/> 为 null。</exception>
    /// <exception cref="ArgumentException">预制体已包含相同运行时类型的组件。</exception>
    public EntityPrefab With<T>(T component) where T : IComponent
    {
        if (component is null)
        {
            throw new ArgumentNullException(nameof(component));
        }

        var type = component.GetType();
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
