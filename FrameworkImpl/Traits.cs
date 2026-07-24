namespace SimpleFramework.FrameworkImpl;

/// <summary>
/// 表示可注入所属域的对象。
/// </summary>
public interface IDomainConfigurable
{
    /// <summary>
    /// 设置对象执行时所属的域。
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
