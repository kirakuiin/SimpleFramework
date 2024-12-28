namespace SimpleFramework;

using Collections;

/// <summary>
/// 代表一个组件对象
/// </summary>
public interface IComponent
{
}

/// <summary>
/// 容纳组件的实体
/// </summary>
public class Entity
{
    // ReSharper disable once CollectionNeverUpdated.Local
    private readonly DefaultDict<Type, List<IComponent>> _components = new(
        () => new List<IComponent>());
    
    private readonly HashSet<int> _componentIds = new();
    
    /// <summary>
    /// 向实体中添加一个组件。
    /// </summary>
    /// <param name="component">组件对象</param>
    public void AddComponent(IComponent component)
    {
        var code = component.GetHashCode();
        if (_componentIds.Add(code))
        {
            _components[component.GetType()].Add(component);
        }
    }

    /// <summary>
    /// 从实体中移除一个组件。
    /// </summary>
    /// <param name="component">组件对象</param>
    public void RemoveComponent(IComponent component)
    {
        var code = component.GetHashCode();
        if (!_componentIds.Contains(code)) return;
        _componentIds.Remove(code);
        _components[component.GetType()].Remove(component);
    }

    /// <summary>
    /// 移除某种类型的全部组件。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public void RemoveAllComponents<T>() where T : IComponent
    {
        if (HasComponent<T>())
        {
            foreach (var component in _components[typeof(T)])
            {
                _componentIds.Remove(component.GetHashCode());
            }
            _components[typeof(T)].Clear();
        }
    }

    /// <summary>
    /// 判断实体中是否包含某个类型的组件。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns>如果包含返回<c>true</c>, 否则返回<c>false</c></returns>
    public bool HasComponent<T>() where T : IComponent
    {
        return _components.ContainsKey(typeof(T)) && _components[typeof(T)].Count > 0;
    }

    /// <summary>
    /// 从实体中获得一个类型的组件。可能返回<c>null</c>
    /// <para>如果存在多个类型相同的组件，则返回的对象和添加的顺序相关。</para>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns>组件对象</returns>
    public T GetComponent<T>() where T : class, IComponent
    {
        if (_components.TryGetValue(typeof(T), out var components))
        {
            return (T)components[0];
        }
        return null;
    }

    /// <summary>
    /// 返回实体中属于某个类型的全部组件。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public IEnumerable<T> GetComponents<T>() where T : class, IComponent
    {
        if (_components.TryGetValue(typeof(T), out var components))
        {
            foreach (var component in components)
            {
                yield return (T)component;
            }
        }
    }

    /// <summary>
    /// 清理实体的全部组件
    /// </summary>
    public void Clear()
    {
        _components.Clear();
        _componentIds.Clear();
    }
}