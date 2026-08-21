using SimpleFramework.FrameworkImpl;

namespace SimpleFramework;

/// <summary>
/// 域类型。
/// <para>最顶层的对象，用于组织分类组件、父子查找、事件和一次性生命周期。</para>
/// </summary>
/// <remarks>
/// 标准 <see cref="AbstractDomain"/> 会将受保护的组件初始化、释放和同步执行阶段应用到其所有权子树；
/// 祖先操作可能因标准后代仍处于对应阶段而被拒绝。自定义实现自行定义等价契约。
/// </remarks>
public interface IDomain
{
    /// <summary>
    /// 设置当前域的父作用域。
    /// <para>当 System、Model、Utility 在当前作用域中无法找到时，会尝试去父作用域查找。</para>
    /// <para>切换会先完成旧父域关系的移除；若该步骤失败，不会提交新的父引用。</para>
    /// </summary>
    /// <param name="domain">父<see cref="IDomain"/></param>
    /// <exception cref="InvalidOperationException">Domain 尚未初始化、正在组件初始化/清理/同步执行，标准所有权后代处于对应阶段，或者已经释放。</exception>
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
    /// <exception cref="InvalidOperationException">Domain 尚未初始化、正在组件初始化/清理/同步执行，标准所有权后代处于对应阶段，或者已经释放。</exception>
    void AddChild(IDomain domain);
    
    /// <summary>
    /// 移除一个子Domain 
    /// </summary>
    /// <param name="domain"></param>
    /// <exception cref="InvalidOperationException">Domain 尚未初始化、正在组件初始化/清理/同步执行，标准所有权后代处于对应阶段，或者已经释放。</exception>
    void RemoveChild(IDomain domain);
    
    /// <summary>
    /// 注册系统。
    /// <para>以泛型参数作为唯一主键，并由当前 Domain 独占管理实例生命周期。</para>
    /// </summary>
    /// <param name="system"><see cref="ISystem"/></param>
    /// <typeparam name="T"></typeparam>
    /// <exception cref="ArgumentNullException"><paramref name="system"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="InvalidOperationException">当前生命周期阶段不允许注册，实例已被管理，初始化期间发生重复键/循环注册，或同步执行期间尝试替换已有生命周期键。</exception>
    /// <exception cref="Exception">组件初始化、回滚或被替换组件的释放失败；失败后该键保持为空。</exception>
    void RegisterSystem<T>(T system) where T : ISystem;

    /// <summary>
    /// 以指定类型注册系统。
    /// <para><typeparamref name="T"/> 是唯一精确主键，不是附加别名；System 注册会纳入生命周期管理。</para>
    /// </summary>
    /// <param name="system"><see cref="ISystem"/></param>
    /// <typeparam name="T"></typeparam>
    /// <exception cref="ArgumentNullException"><paramref name="system"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="InvalidOperationException">当前生命周期阶段不允许注册，实例已被管理，初始化期间发生重复键/循环注册，或同步执行期间尝试替换已有生命周期键。</exception>
    /// <exception cref="Exception">组件初始化、回滚或被替换组件的释放失败；失败后该键保持为空。</exception>
    void RegisterSystemAs<T>(T system) where T : ISystem;
    
    /// <summary>
    /// 注册模型。
    /// <para>以泛型参数作为唯一主键，并由当前 Domain 独占管理实例生命周期。</para>
    /// </summary>
    /// <param name="model"><see cref="IModel"/></param>
    /// <typeparam name="T"></typeparam>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="InvalidOperationException">当前生命周期阶段不允许注册，实例已被管理，初始化期间发生重复键/循环注册，或同步执行期间尝试替换已有生命周期键。</exception>
    /// <exception cref="Exception">组件初始化、回滚或被替换组件的释放失败；失败后该键保持为空。</exception>
    void RegisterModel<T>(T model) where T : IModel;

    /// <summary>
    /// 以指定类型注册模型。
    /// <para><typeparamref name="T"/> 是唯一精确主键，不是附加别名；Model 注册会纳入生命周期管理。</para>
    /// </summary>
    /// <param name="model"><see cref="IModel"/></param>
    /// <typeparam name="T"></typeparam>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="InvalidOperationException">当前生命周期阶段不允许注册，实例已被管理，初始化期间发生重复键/循环注册，或同步执行期间尝试替换已有生命周期键。</exception>
    /// <exception cref="Exception">组件初始化、回滚或被替换组件的释放失败；失败后该键保持为空。</exception>
    void RegisterModelAs<T>(T model) where T : IModel;
    
    /// <summary>
    /// 注册功能组件。
    /// <para>Utility 生命周期始终由调用方管理，可在多个 Domain 共享。</para>
    /// </summary>
    /// <param name="utility"><see cref="IUtility"/></param>
    /// <typeparam name="T"></typeparam>
    /// <exception cref="ArgumentNullException"><paramref name="utility"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="InvalidOperationException">当前生命周期阶段不允许注册，或实例已经占用当前 Domain 的其他键/分类。</exception>
    void RegisterUtility<T>(T utility) where T : IUtility;

    /// <summary>
    /// 以指定类型注册功能组件。
    /// <para><typeparamref name="T"/> 是唯一精确主键，不是附加别名；Utility 注册不会纳入生命周期管理。</para>
    /// </summary>
    /// <param name="utility"><see cref="IUtility"/></param>
    /// <typeparam name="T"></typeparam>
    /// <exception cref="ArgumentNullException"><paramref name="utility"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="InvalidOperationException">当前生命周期阶段不允许注册，或实例已经占用当前 Domain 的其他键/分类。</exception>
    void RegisterUtilityAs<T>(T utility) where T : IUtility;
    
    /// <summary>
    /// 在域中获取系统。
    /// <para>先匹配当前 System 分类的精确主键，再匹配唯一可赋值实例，最后回退父 Domain。</para>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns><see cref="ISystem"/></returns>
    /// <exception cref="AmbiguousComponentException">当前 Domain 存在多个可赋值候选项且没有精确主键。</exception>
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
    /// <para>先匹配当前 Model 分类的精确主键，再匹配唯一可赋值实例，最后回退父 Domain。</para>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns><see cref="IModel"/></returns>
    /// <exception cref="AmbiguousComponentException">当前 Domain 存在多个可赋值候选项且没有精确主键。</exception>
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
    /// <para>先匹配当前 Utility 分类的精确主键，再匹配唯一可赋值实例，最后回退父 Domain。</para>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns><see cref="IUtility"/></returns>
    /// <exception cref="AmbiguousComponentException">当前 Domain 存在多个可赋值候选项且没有精确主键。</exception>
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
    /// <para>订阅归 Domain/调用方管理；不会因无关生命周期组件替换而自动取消。</para>
    /// </summary>
    /// <param name="onEvent"></param>
    /// <typeparam name="TEvent"></typeparam>
    /// <returns>可提前取消订阅的句柄；句柄的 <see cref="IUnRegister.UnRegister"/> 与 <see cref="IDisposable.Dispose"/> 遵循 Domain 的事件注销阶段限制。</returns>
    /// <exception cref="InvalidOperationException">Domain 尚未初始化、正在清理或已经释放。</exception>
    IUnRegister RegisterEvent<TEvent>(Action<TEvent> onEvent);
    
    /// <summary>
    /// 在域中取消注册一个事件回调。
    /// </summary>
    /// <param name="onEvent"></param>
    /// <typeparam name="TEvent"></typeparam>
    /// <exception cref="InvalidOperationException">Domain 尚未初始化、正在组件初始化或已经释放。</exception>
    void UnRegisterEvent<TEvent>(Action<TEvent> onEvent);

    /// <summary>
    /// 在域中发送一个事件。
    /// </summary>
    /// <typeparam name="TEvent"></typeparam>
    /// <exception cref="InvalidOperationException">Domain 不处于活动状态，或正在组件初始化/清理。</exception>
    void SendEvent<TEvent>() where TEvent : new();

    /// <summary>
    /// 在域中发送一个事件。
    /// </summary>
    /// <param name="event"></param>
    /// <typeparam name="TEvent"></typeparam>
    /// <exception cref="InvalidOperationException">Domain 不处于活动状态，或正在组件初始化/清理。</exception>
    void SendEvent<TEvent>(TEvent @event);

    /// <summary>
    /// 在域中执行一个命令。
    /// </summary>
    /// <param name="command"></param>
    /// <typeparam name="TCommand"></typeparam>
    /// <exception cref="InvalidOperationException">Domain 不处于活动状态，或正在组件初始化/清理。</exception>
    void SendCommand<TCommand>(TCommand command) where TCommand : ICommand;
    
    /// <summary>
    /// 在域中执行一个命令。带有返回结果。
    /// </summary>
    /// <param name="command"></param>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Domain 不处于活动状态，或正在组件初始化/清理。</exception>
    TResult SendCommand<TResult>(ICommand<TResult> command);

    /// <summary>
    /// 在域中执行一个查询。带有返回结果。
    /// </summary>
    /// <param name="query"></param>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Domain 尚未初始化、正在清理或已经释放。</exception>
    TResult SendQuery<TResult>(IQuery<TResult> query);

    /// <summary>
    /// 永久释放当前实例；释放期间的重入调用和释放后的重复调用不会重复执行生命周期回调。
    /// <para>释放后只允许组件查找、<see cref="Parent"/>、<see cref="object.ToString"/> 和再次调用本方法。</para>
    /// </summary>
    /// <exception cref="InvalidOperationException">当前 Domain 或标准所有权子树仍在组件初始化/释放、清理回调，或本地事件、Command、Query 的同步执行尚未返回。</exception>
    /// <exception cref="AggregateException">一个或多个子域、生命周期组件或域释放回调失败；清理其余资源后聚合抛出。</exception>
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
public interface ISystem : IDomainBindable, IModelAccessible,
    IUtilityAccessible, IEventRegistrable, IEventTransmittable, IConstructable
{
}

/// <summary>
/// 代表一个单一的数据模型。
/// </summary>
public interface IModel : IDomainBindable, IUtilityAccessible,
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
/// 一个查询也代表一个面向对象的回调，约定上不修改数据。
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
