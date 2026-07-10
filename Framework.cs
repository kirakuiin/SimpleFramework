using SimpleFramework.FrameworkImpl;

namespace SimpleFramework;

/// <summary>
/// 域类型。
/// <para>最顶层的对象，用于存储容器。</para>
/// </summary>
public interface IDomain
{
    /// <summary>
    /// 设置当前域的父作用域。
    /// <para>当 System、Model、Utility 在当前作用域中无法找到时，会尝试去父作用域查找。</para>
    /// </summary>
    /// <param name="domain">父<see cref="IDomain"/></param>
    void SetParent(IDomain? domain);
    
    /// <summary>
    /// 获得父Domain
    /// </summary>
    IDomain? Parent { get; }
    
    /// <summary>
    /// 添加一个新的子域
    /// <para>子Domain必然会被父Domain管理，共享生命周期</para>
    /// </summary>
    /// <param name="domain"></param>
    void AddChild(IDomain domain);
    
    /// <summary>
    /// 移除一个子Domain 
    /// </summary>
    /// <param name="domain"></param>
    void RemoveChild(IDomain domain);
    
    /// <summary>
    /// 注册系统。
    /// </summary>
    /// <param name="system"><see cref="ISystem"/></param>
    /// <typeparam name="T"></typeparam>
    /// <exception cref="ArgumentNullException"><paramref name="system"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="Exception">组件初始化或被替换组件的释放失败；原始异常会直接传播，失败的新注册不会对查找可见。</exception>
    void RegisterSystem<T>(T system) where T : ISystem;

    /// <summary>
    /// 以指定类型注册系统。
    /// <para>System 注册会纳入生命周期管理。</para>
    /// </summary>
    /// <param name="system"><see cref="ISystem"/></param>
    /// <typeparam name="T"></typeparam>
    /// <exception cref="ArgumentNullException"><paramref name="system"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="Exception">组件初始化或被替换组件的释放失败；原始异常会直接传播，失败的新注册不会对查找可见。</exception>
    void RegisterSystemAs<T>(T system) where T : ISystem;
    
    /// <summary>
    /// 注册模型。
    /// </summary>
    /// <param name="model"><see cref="IModel"/></param>
    /// <typeparam name="T"></typeparam>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="Exception">组件初始化或被替换组件的释放失败；原始异常会直接传播，失败的新注册不会对查找可见。</exception>
    void RegisterModel<T>(T model) where T : IModel;

    /// <summary>
    /// 以指定类型注册模型。
    /// <para>Model 注册会纳入生命周期管理。</para>
    /// </summary>
    /// <param name="model"><see cref="IModel"/></param>
    /// <typeparam name="T"></typeparam>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="Exception">组件初始化或被替换组件的释放失败；原始异常会直接传播，失败的新注册不会对查找可见。</exception>
    void RegisterModelAs<T>(T model) where T : IModel;
    
    /// <summary>
    /// 注册功能组件。
    /// </summary>
    /// <param name="utility"><see cref="IUtility"/></param>
    /// <typeparam name="T"></typeparam>
    /// <exception cref="ArgumentNullException"><paramref name="utility"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="Exception">替换生命周期组件时释放失败；原始异常会直接传播，失败的新注册不会对查找可见。</exception>
    void RegisterUtility<T>(T utility) where T : IUtility;

    /// <summary>
    /// 以指定类型注册功能组件。
    /// <para>Utility 注册不会纳入生命周期管理。</para>
    /// </summary>
    /// <param name="utility"><see cref="IUtility"/></param>
    /// <typeparam name="T"></typeparam>
    /// <exception cref="ArgumentNullException"><paramref name="utility"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="Exception">替换生命周期组件时释放失败；原始异常会直接传播，失败的新注册不会对查找可见。</exception>
    void RegisterUtilityAs<T>(T utility) where T : IUtility;
    
    /// <summary>
    /// 在域中获取系统。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns><see cref="ISystem"/></returns>
    T? GetSystem<T>() where T : class, ISystem;

    /// <summary>
    /// 尝试在域中获取系统。
    /// </summary>
    /// <param name="system">找到的系统；未找到时为 null。</param>
    /// <typeparam name="T"></typeparam>
    /// <returns>找到系统时返回 true。</returns>
    bool TryGetSystem<T>(out T? system) where T : class, ISystem;

    /// <summary>
    /// 在域中获取系统，未找到时抛出异常。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns><see cref="ISystem"/></returns>
    /// <exception cref="InvalidOperationException">当前域及其父域中不存在指定系统。</exception>
    T RequireSystem<T>() where T : class, ISystem;
    
    /// <summary>
    /// 在域中获取模型。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns><see cref="IModel"/></returns>
    T? GetModel<T>() where T : class, IModel;

    /// <summary>
    /// 尝试在域中获取模型。
    /// </summary>
    /// <param name="model">找到的模型；未找到时为 null。</param>
    /// <typeparam name="T"></typeparam>
    /// <returns>找到模型时返回 true。</returns>
    bool TryGetModel<T>(out T? model) where T : class, IModel;

    /// <summary>
    /// 在域中获取模型，未找到时抛出异常。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns><see cref="IModel"/></returns>
    /// <exception cref="InvalidOperationException">当前域及其父域中不存在指定模型。</exception>
    T RequireModel<T>() where T : class, IModel;
    
    /// <summary>
    /// 在域中获取功能组件。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns><see cref="IUtility"/></returns>
    T? GetUtility<T>() where T : class, IUtility;

    /// <summary>
    /// 尝试在域中获取功能组件。
    /// </summary>
    /// <param name="utility">找到的功能组件；未找到时为 null。</param>
    /// <typeparam name="T"></typeparam>
    /// <returns>找到功能组件时返回 true。</returns>
    bool TryGetUtility<T>(out T? utility) where T : class, IUtility;

    /// <summary>
    /// 在域中获取功能组件，未找到时抛出异常。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns><see cref="IUtility"/></returns>
    /// <exception cref="InvalidOperationException">当前域及其父域中不存在指定功能组件。</exception>
    T RequireUtility<T>() where T : class, IUtility;
    
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
    /// 释放域中资源；释放期间的重入调用不会重复执行生命周期回调。
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

/// <summary>
/// 表示一个可能修改状态并返回结果的命令。
/// </summary>
/// <typeparam name="TResult">命令结果类型。</typeparam>
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
public interface IUnRegister : IDisposable
{
    /// <summary>
    /// 取消注册。
    /// </summary>
    void UnRegister();
    
    void IDisposable.Dispose() => UnRegister();
}
