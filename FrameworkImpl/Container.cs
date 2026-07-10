using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace SimpleFramework.FrameworkImpl;

/// <summary>
/// 存储组件的容器。
/// </summary>
public class Container
{
    private readonly Dictionary<Type, object> _instances = new();

    /// <summary>
    /// 注册组件。
    /// </summary>
    /// <param name="instance">组件实例</param>
    /// <typeparam name="T"></typeparam>
    /// <returns>同一键已注册的旧实例；不存在时返回默认值。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="instance"/> 为 <see langword="null"/>。</exception>
    [return: MaybeNull]
    public T Register<T>(T instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var key = typeof(T);
        var previous = _instances.TryGetValue(key, out var oldInstance) && oldInstance is T result ? result : default;
        _instances[key] = instance;
        return previous;
    }

    /// <summary>
    /// 获得组件。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns>组件实例</returns>
    public T? Get<T>() where T : class
    {
        return TryGet<T>(out var instance) ? instance : null;
    }

    /// <summary>
    /// 按注册键类型获得组件。
    /// </summary>
    /// <param name="key">注册键类型</param>
    /// <returns>组件实例</returns>
    public object? Get(Type key)
    {
        return _instances.GetValueOrDefault(key);
    }

    /// <summary>
    /// 尝试获得组件。
    /// </summary>
    /// <param name="instance">组件实例</param>
    /// <typeparam name="T"></typeparam>
    /// <returns>是否存在组件</returns>
    public bool TryGet<T>(out T? instance) where T : class
    {
        if (_instances.TryGetValue(typeof(T), out var value) && value is T result)
        {
            instance = result;
            return true;
        }

        instance = null;
        return false;
    }

    /// <summary>
    /// 移除组件。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns>是否移除成功</returns>
    public bool Remove<T>() => _instances.Remove(typeof(T));

    /// <summary>
    /// 获得指定类型的全部实例。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public IEnumerable<T> GetComponents<T>()
    {
        return _instances.Values.Where(component => component is T).Cast<T>();
    }
    
    /// <summary>
    /// 清除存储的实例。
    /// </summary>
    public void Clear() => _instances.Clear();

    public override string ToString()
    {
        StringBuilder sb = new();
        sb.Append(ToByType<ISystem>());
        sb.Append(ToByType<IModel>());
        sb.Append(ToByType<IUtility>());

        return sb.ToString();
    }

    private string ToByType<T>()
    {
        var components = GetComponents<T>().ToList();
        if (components.Count == 0) return "";

        StringBuilder sb = new();
        sb.AppendLine($"----{typeof(T).Name}----");
        components.ForEach(comp => sb.AppendLine($"{comp?.GetType().Name}"));

        return sb.ToString();
    }
}
