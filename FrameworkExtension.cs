using SimpleFramework.FrameworkImpl;

namespace SimpleFramework;

/// <summary>
/// 提供从域中读取模型的扩展方法。
/// </summary>
public static class ModelReadableExtensions
{
    /// <summary>
    /// 在域中获取模型。
    /// </summary>
    /// <param name="self"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns><see cref="IModel"/></returns>
    public static T? GetModel<T>(this IModelAccessible self) where T : class, IModel =>
        self.Domain.GetModel<T>();

    /// <summary>
    /// 在域中获取模型，未找到时抛出异常。
    /// </summary>
    /// <param name="self"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns><see cref="IModel"/></returns>
    /// <exception cref="InvalidOperationException">当前域及其父域中不存在指定模型。</exception>
    public static T RequireModel<T>(this IModelAccessible self) where T : class, IModel =>
        self.Domain.RequireModel<T>();
    
    /// <summary>
    /// 如果依赖的组件不存在则抛出
    /// </summary>
    /// <param name="self"></param>
    /// <typeparam name="T"></typeparam>
    /// <exception cref="InvalidOperationException">当前域及其父域中不存在指定模型。</exception>
    public static void Require<T>(this IModelAccessible self) where T : class, IModel
    {
        self.RequireModel<T>();
    }
}

/// <summary>
/// 提供从域中读取系统的扩展方法。
/// </summary>
public static class SystemReadableExtensions
{
    /// <summary>
    /// 从域中获取系统。
    /// </summary>
    /// <param name="self"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns><see cref="ISystem"/></returns>
    public static T? GetSystem<T>(this ISystemAccessible self) where T : class, ISystem =>
        self.Domain.GetSystem<T>();

    /// <summary>
    /// 从域中获取系统，未找到时抛出异常。
    /// </summary>
    /// <param name="self"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns><see cref="ISystem"/></returns>
    /// <exception cref="InvalidOperationException">当前域及其父域中不存在指定系统。</exception>
    public static T RequireSystem<T>(this ISystemAccessible self) where T : class, ISystem =>
        self.Domain.RequireSystem<T>();
}

/// <summary>
/// 提供从域中读取功能组件的扩展方法。
/// </summary>
public static class UtilityReadableExtensions
{
    /// <summary>
    /// 从域中获取功能组件。
    /// </summary>
    /// <param name="self"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns><see cref="IUtility"/></returns>
    public static T? GetUtility<T>(this IUtilityAccessible self) where T : class, IUtility =>
        self.Domain.GetUtility<T>();

    /// <summary>
    /// 从域中获取功能组件，未找到时抛出异常。
    /// </summary>
    /// <param name="self"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns><see cref="IUtility"/></returns>
    /// <exception cref="InvalidOperationException">当前域及其父域中不存在指定功能组件。</exception>
    public static T RequireUtility<T>(this IUtilityAccessible self) where T : class, IUtility =>
        self.Domain.RequireUtility<T>();

    /// <summary>
    /// 如果依赖的组件不存在则抛出
    /// </summary>
    /// <param name="self"></param>
    /// <typeparam name="T"></typeparam>
    /// <exception cref="InvalidOperationException">当前域及其父域中不存在指定功能组件。</exception>
    public static void Require<T>(this IUtilityAccessible self) where T : class, IUtility
    {
        self.RequireUtility<T>();
    }
}

/// <summary>
/// 提供域事件订阅的扩展方法。
/// </summary>
public static class RegisterAbleExtensions
{
    /// <summary>
    /// 注册随当前 System 生命周期自动取消的本地事件回调。
    /// </summary>
    /// <param name="self">订阅所属的 System。</param>
    /// <param name="action">事件回调。</param>
    /// <typeparam name="T">事件类型。</typeparam>
    /// <returns>可提前取消订阅的句柄；取消操作遵循所属 Domain 的事件注销阶段限制。</returns>
    public static IUnRegister RegisterEvent<T>(this ISystem self, Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(self);
        return self.Domain is IComponentEventRegistrar registrar
            ? registrar.RegisterComponentEvent(self, action)
            : self.Domain.RegisterEvent(action);
    }

    /// <summary>
    /// 注册由 Domain 管理的事件回调。
    /// </summary>
    /// <param name="self"></param>
    /// <param name="action"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns><see cref="IUnRegister"/></returns>
    public static IUnRegister RegisterEvent<T>(this IEventRegistrable self, Action<T> action) =>
        self.Domain.RegisterEvent(action);
    
    /// <summary>
    /// 取消注册事件回调。
    /// </summary>
    /// <param name="self"></param>
    /// <param name="action">之前注册时使用的回调</param>
    /// <typeparam name="T"></typeparam>
    public static void UnRegisterEvent<T>(this IEventRegistrable self, Action<T> action) =>
        self.Domain.UnRegisterEvent(action);
}

/// <summary>
/// 提供域事件发送的扩展方法。
/// </summary>
public static class SendEventAbleExtensions
{
    /// <summary>
    /// 发送一个事件。将会触发注册的回调。
    /// </summary>
    /// <param name="self"></param>
    /// <typeparam name="T"></typeparam>
    public static void SendEvent<T>(this IEventTransmittable self) where T : new() =>
        self.Domain.SendEvent<T>();
    
    /// <summary>
    /// 发送一个事件。将会触发注册的回调。
    /// </summary>
    /// <param name="self"></param>
    /// <param name="event"></param>
    /// <typeparam name="T"></typeparam>
    public static void SendEvent<T>(this IEventTransmittable self, T @event) =>
        self.Domain.SendEvent(@event);
}

/// <summary>
/// 提供全局事件总线订阅的扩展方法。
/// </summary>
public static class GlobalEventsExtensions
{
    /// <summary>
    /// 将回调注册到全局事件总线中。
    /// </summary>
    /// <param name="self"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static IUnRegister RegisterEvent<T>(this IOnGlobalEvent<T> self)
        where T : struct => EventBus.Global.Register<T>(self.OnEvent);
    
    /// <summary>
    /// 从全局事件总线中取消注册。
    /// </summary>
    /// <param name="self"></param>
    /// <typeparam name="T"></typeparam>
    public static void UnRegisterEvent<T>(this IOnGlobalEvent<T> self)
        where T : struct => EventBus.Global.UnRegister<T>(self.OnEvent);
}

/// <summary>
/// 提供命令发送的扩展方法。
/// </summary>
public static class CommandExtensions
{
    /// <summary>
    /// 在域中执行一个命令。
    /// </summary>
    /// <param name="self"></param>
    /// <typeparam name="T"></typeparam>
    public static void SendCommand<T>(this ICommandTransmittable self) where T : ICommand, new() => self.Domain.SendCommand(new T());
    
    /// <summary>
    /// 在域中执行一个构造好的命令。
    /// </summary>
    /// <param name="self"></param>
    /// <param name="command"></param>
    /// <typeparam name="T"></typeparam>
    public static void SendCommand<T>(this ICommandTransmittable self, T command) where T : ICommand
        => self.Domain.SendCommand(command);
    
    /// <summary>
    /// 在域中执行一个构造好的命令。
    /// </summary>
    /// <param name="self"></param>
    /// <param name="command"></param>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    public static TResult SendCommand<TResult>(this ICommandTransmittable self, ICommand<TResult> command) => self.Domain.SendCommand(command);
}

/// <summary>
/// 提供查询发送的扩展方法。
/// </summary>
public static class QueryExtensions
{
    /// <summary>
    /// 在域中执行一个查询。
    /// </summary>
    /// <param name="self"></param>
    /// <param name="query"></param>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    public static TResult SendQuery<TResult>(this IQueryTransmittable self, IQuery<TResult> query) => self.Domain.SendQuery(query);
}
