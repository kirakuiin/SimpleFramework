using SimpleFramework.FrameworkImpl;

namespace SimpleFramework;

/// <summary>
/// 域类型。
/// <para>最顶层的对象，用于存储容器。</para>
/// </summary>
public interface IDomain
{
    /// <summary>
    /// 注册系统。
    /// </summary>
    /// <param name="system"><see cref="ISystem"/></param>
    /// <typeparam name="T"></typeparam>
    void RegisterSystem<T>(T system) where T : ISystem;
    
    /// <summary>
    /// 注册模型。
    /// </summary>
    /// <param name="model"><see cref="IModel"/></param>
    /// <typeparam name="T"></typeparam>
    void RegisterModel<T>(T model) where T : IModel;
    
    /// <summary>
    /// 注册功能组件。
    /// </summary>
    /// <param name="utility"><see cref="IUtility"/></param>
    /// <typeparam name="T"></typeparam>
    void RegisterUtility<T>(T utility) where T : IUtility;
    
    /// <summary>
    /// 在域中获取系统。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns><see cref="ISystem"/></returns>
    T GetSystem<T>() where T : class, ISystem;
    
    /// <summary>
    /// 在域中获取模型。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns><see cref="IModel"/></returns>
    T GetModel<T>() where T : class, IModel;
    
    /// <summary>
    /// 在域中获取功能组件。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns><see cref="IUtility"/></returns>
    T GetUtility<T>() where T : class, IUtility;
    
    /// <summary>
    /// 在域中注册一个事件回调。
    /// </summary>
    /// <param name="onEvent"></param>
    /// <typeparam name="TEvent"></typeparam>
    /// <returns></returns>
    IUnRegister RegisterEvent<TEvent>(Action<TEvent> onEvent);
    
    /// <summary>
    /// 在域中取消注册一个事件回调。
    /// </summary>
    /// <param name="onEvent"></param>
    /// <typeparam name="TEvent"></typeparam>
    void UnRegisterEvent<TEvent>(Action<TEvent> onEvent);

    /// <summary>
    /// 在域中发送一个事件。
    /// </summary>
    /// <typeparam name="TEvent"></typeparam>
    void SendEvent<TEvent>() where TEvent : new();

    /// <summary>
    /// 在域中发送一个事件。
    /// </summary>
    /// <param name="event"></param>
    /// <typeparam name="TEvent"></typeparam>
    void SendEvent<TEvent>(TEvent @event);

    /// <summary>
    /// 在域中执行一个命令。
    /// </summary>
    /// <param name="command"></param>
    /// <typeparam name="TCommand"></typeparam>
    void SendCommand<TCommand>(TCommand command) where TCommand : ICommand;
    
    /// <summary>
    /// 在域中执行一个命令。带有返回结果。
    /// </summary>
    /// <param name="command"></param>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    TResult SendCommand<TResult>(ICommand<TResult> command);

    /// <summary>
    /// 在域中执行一个查询。带有返回结果。
    /// </summary>
    /// <param name="query"></param>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    TResult SendQuery<TResult>(IQuery<TResult> query);

    /// <summary>
    /// 释放域中资源。
    /// </summary>
    void UnInitialize();
}

/// <summary>
/// 代表一个控制器，它可以直接访问并操作可视化界面。
/// </summary>
public interface IController : ISystemAccessible, IModelAccessible, IUtilityAccessible, IEventRegistrable, IQueryTransmittable, ICommandTransmittable
{
}

/// <summary>
/// 代表一个横跨多个实体的数据模型。
/// </summary>
public interface ISystem : IDomainConfigurable, IModelAccessible,
    IUtilityAccessible, IEventRegistrable, IEventTransmittable, IConstructable
{
}

/// <summary>
/// 代表一个单一的数据模型。
/// </summary>
public interface IModel : IDomainConfigurable, IUtilityAccessible,
    IEventTransmittable, IConstructable
{
}

/// <summary>
/// 代表实现了一个底层功能的类型。
/// </summary>
public interface IUtility
{
}


/// <summary>
/// 一个命令代表一个面向对象的回调，内部可能会修改数据。
/// </summary>
public interface ICommand : IDomainConfigurable,
    ISystemAccessible, IModelAccessible, IUtilityAccessible,
    IEventTransmittable, IQueryTransmittable, ICommandTransmittable
{
    /// <summary>
    /// 执行命令。
    /// </summary>
    void Execute();
}

public interface ICommand<out TResult> : IDomainConfigurable,
    ISystemAccessible, IModelAccessible, IUtilityAccessible,
    IEventTransmittable, IQueryTransmittable, ICommandTransmittable
{
    /// <summary>
    /// 执行命令。
    /// </summary>
    /// <returns></returns>
    TResult Execute();
}

/// <summary>
/// 一个查询也代表一个面向对象的回调，但它保证不会修改任何数据。
/// </summary>
public interface IQuery<out TResult> : IDomainConfigurable,
    ISystemAccessible, IModelAccessible, IQueryTransmittable
{
    /// <summary>
    /// 执行查询。
    /// </summary>
    /// <returns></returns>
    TResult Execute();
}

/// <summary>
/// 代表一个支持取消注册的实体。
/// </summary>
public interface IUnRegister
{
    /// <summary>
    /// 取消注册。
    /// </summary>
    void UnRegister();
}
