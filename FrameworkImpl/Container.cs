using System.Diagnostics;
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
    public void Register<T>(T instance)
    {
        Debug.Assert(instance != null, nameof(instance) + " != null");
        _instances[typeof(T)] = instance;
    }

    /// <summary>
    /// 获得组件。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns>组件实例</returns>
    public T Get<T>() where T : class
    {
        var key = typeof(T);
        if (_instances.TryGetValue(key, out var instance))
        {
            return (instance as T)!;
        }
        return null!;
    }

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