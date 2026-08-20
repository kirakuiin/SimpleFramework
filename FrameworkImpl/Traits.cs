namespace SimpleFramework.FrameworkImpl;

/// <summary>
/// 表示可注入所属域的对象。
/// </summary>
public interface IDomainBindable
{
    /// <summary>
    /// 绑定生命周期组件唯一所属的域。
    /// </summary>
    /// <param name="domain">所属域。</param>
    void BindDomain(IDomain domain);
}

/// <summary>
/// 表示可在每次执行前注入所属域的对象。
/// </summary>
public interface IDomainConfigurable
{
    /// <summary>
    /// 设置对象本次执行时所属的域。
    /// </summary>
    /// <param name="domain">所属域。</param>
    void SetDomain(IDomain domain);
}

/// <summary>
/// 表示可访问所属域的对象。
/// </summary>
public interface IDomainAccessible
{
    /// <summary>
    /// 获取所属域。
    /// </summary>
    IDomain Domain { get; }
}

/// <summary>
/// 表示可读取域模型的对象。
/// </summary>
public interface IModelAccessible : IDomainAccessible
{
}

/// <summary>
/// 表示可读取域系统的对象。
/// </summary>
public interface ISystemAccessible : IDomainAccessible
{
}

/// <summary>
/// 表示可读取域功能组件的对象。
/// </summary>
public interface IUtilityAccessible : IDomainAccessible
{
}

/// <summary>
/// 表示可订阅所属域事件的对象。
/// </summary>
public interface IEventRegistrable : IDomainAccessible
{
}

/// <summary>
/// 表示支持将 System 本地事件订阅归属到生命周期组件的域。
/// </summary>
public interface IComponentEventRegistrar
{
    /// <summary>
    /// 为已注册的 System 注册由其生命周期管理的本地事件回调。
    /// </summary>
    /// <param name="owner">订阅所属的 System。</param>
    /// <param name="onEvent">事件回调。</param>
    /// <typeparam name="TEvent">事件类型。</typeparam>
    /// <returns>可提前取消订阅的句柄。</returns>
    IUnRegister RegisterComponentEvent<TEvent>(ISystem owner, Action<TEvent> onEvent);
}

/// <summary>
/// 表示可向所属域发送事件的对象。
/// </summary>
public interface IEventTransmittable : IDomainAccessible
{
}

/// <summary>
/// 表示可向所属域发送命令的对象。
/// </summary>
public interface ICommandTransmittable : IDomainAccessible
{
}

/// <summary>
/// 表示可向所属域发送查询的对象。
/// </summary>
public interface IQueryTransmittable : IDomainAccessible
{
}

/// <summary>
/// 表示由域管理初始化和释放过程的组件。
/// </summary>
public interface IConstructable
{
    /// <summary>
    /// 初始化组件。
    /// </summary>
    void Initialize();

    /// <summary>
    /// 释放组件持有的资源；组件初始化开始后即使失败也会调用，因此实现必须能处理部分初始化状态。
    /// </summary>
    void UnInitialize();
}
