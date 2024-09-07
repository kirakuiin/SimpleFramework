using SimpleFramework.FrameworkImpl;

namespace SimpleFramework;

public static class ModelReadableExtensions
{
    /// <summary>
    /// 在域中获取模型。
    /// </summary>
    /// <param name="self"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns><see cref="IModel"/></returns>
    public static T GetModel<T>(this IModelAccessible self) where T : class, IModel =>
        self.Domain.GetModel<T>();
}

public static class SystemReadableExtensions
{
    /// <summary>
    /// 从域中获取系统。
    /// </summary>
    /// <param name="self"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns><see cref="ISystem"/></returns>
    public static T GetSystem<T>(this ISystemAccessible self) where T : class, ISystem =>
        self.Domain.GetSystem<T>();
}

public static class UtilityReadableExtensions
{
    /// <summary>
    /// 从域中获取功能组件。
    /// </summary>
    /// <param name="self"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns><see cref="IUtility"/></returns>
    public static T GetUtility<T>(this IUtilityAccessible self) where T : class, IUtility =>
        self.Domain.GetUtility<T>();
}

public static class RegisterAbleExtensions
{
    /// <summary>
    /// 注册事件回调。
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
