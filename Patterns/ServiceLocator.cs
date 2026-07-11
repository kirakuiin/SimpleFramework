namespace SimpleFramework.Patterns;

/// <summary>
/// 需要提供游戏服务的类型需要实现此接口。
/// </summary>
public interface IGameService
{
}

/// <summary>
/// 为 <see cref="IGameService"/> 类型对象提供定位器服务。
/// <remarks>本质上是一个中间层，通过这一层使得接口的调用和服务的提供者解耦</remarks>
/// </summary>
public class ServiceLocator : Singleton<ServiceLocator>
{
    private readonly Dictionary<Type, IGameService> _services = new();
    
    /// <summary>
    /// 获得注册过的某个服务。
    /// </summary>
    /// <typeparam name="T">实现了<c>IGameService</c>的类型</typeparam>
    /// <returns>与请求类型完全匹配或可赋值给请求类型的已注册服务。</returns>
    /// <exception cref="KeyNotFoundException">找不到兼容的服务。</exception>
    public T Get<T>() where T : IGameService
    {
        if (_services.TryGetValue(typeof(T), out var service)) return (T)service;
        return GetAtAll<T>();
    }
    
    private T GetAtAll<T>()
    {
        foreach (var service in _services.Values)
        {
            if (service is T gameService)
            {
                return gameService;
            }
        }
        throw new KeyNotFoundException($"No service found for {typeof(T).Name}");
    }

    /// <summary>
    /// 注册一个服务到定位器。
    /// </summary>
    /// <param name="service">服务对象</param>
    /// <typeparam name="T">实现了<c>IGameService</c>的类型</typeparam>
    /// <exception cref="ArgumentNullException"><paramref name="service"/> 为 <see langword="null"/>。</exception>
    public void Register<T>(T service) where T : IGameService
    {
        ArgumentNullException.ThrowIfNull(service);
        _services[typeof(T)] = service;
    }

    /// <summary>
    /// 从定位器中取消某个对象的注册。
    /// </summary>
    /// <typeparam name="T">实现了<c>IGameService</c>的类型</typeparam>
    public void UnRegister<T>() where T : IGameService
    {
        _services.Remove(typeof(T));
    }

    /// <summary>
    /// 清理全部的注册信息
    /// </summary>
    public override void Clear()
    {
        _services.Clear();
    }
}
