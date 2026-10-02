namespace SimpleFramework;

/// <summary><see cref="ICanGetModel"/> 的能力扩展。</summary>
public static class CanGetModelExtensions
{
    /// <summary>通过所属 Domain 获取 Model；找不到时抛出 <see cref="KeyNotFoundException"/>。</summary>
    public static T GetModel<T>(this ICanGetModel self) where T : class, IModel
    {
        ArgumentNullException.ThrowIfNull(self);
        return self.GetDomain().GetModel<T>();
    }
}

/// <summary><see cref="ICanGetSystem"/> 的能力扩展。</summary>
public static class CanGetSystemExtensions
{
    /// <summary>通过所属 Domain 获取 System；找不到时抛出 <see cref="KeyNotFoundException"/>。</summary>
    public static T GetSystem<T>(this ICanGetSystem self) where T : class, ISystem
    {
        ArgumentNullException.ThrowIfNull(self);
        return self.GetDomain().GetSystem<T>();
    }
}

/// <summary><see cref="ICanGetUtility"/> 的能力扩展。</summary>
public static class CanGetUtilityExtensions
{
    /// <summary>通过所属 Domain 获取 Utility；找不到时抛出 <see cref="KeyNotFoundException"/>。</summary>
    public static T GetUtility<T>(this ICanGetUtility self) where T : class, IUtility
    {
        ArgumentNullException.ThrowIfNull(self);
        return self.GetDomain().GetUtility<T>();
    }
}

/// <summary><see cref="ICanSendCommand"/> 的能力扩展。</summary>
public static class CanSendCommandExtensions
{
    /// <summary>同步执行一个无返回值命令。</summary>
    public static void SendCommand(this ICanSendCommand self, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(self);
        self.GetDomain().SendCommand(command);
    }

    /// <summary>构造并同步执行一个无参命令。</summary>
    public static void SendCommand<T>(this ICanSendCommand self) where T : ICommand, new()
    {
        ArgumentNullException.ThrowIfNull(self);
        self.GetDomain().SendCommand(new T());
    }

    /// <summary>同步执行一个带返回值命令。</summary>
    public static TResult SendCommand<TResult>(this ICanSendCommand self, ICommand<TResult> command)
    {
        ArgumentNullException.ThrowIfNull(self);
        return self.GetDomain().SendCommand(command);
    }
}

/// <summary><see cref="ICanSendQuery"/> 的能力扩展。</summary>
public static class CanSendQueryExtensions
{
    /// <summary>同步执行一个查询。</summary>
    public static TResult SendQuery<TResult>(this ICanSendQuery self, IQuery<TResult> query)
    {
        ArgumentNullException.ThrowIfNull(self);
        return self.GetDomain().SendQuery(query);
    }
}

/// <summary><see cref="ICanSendEvent"/> 的能力扩展。</summary>
public static class CanSendEventExtensions
{
    /// <summary>通过所属 Domain 可见的 <see cref="IEventHub"/> 发布跨 Domain 事件。</summary>
    /// <exception cref="KeyNotFoundException">没有注册事件中心。</exception>
    public static void PublishEvent<T>(this ICanSendEvent self, T message)
    {
        ArgumentNullException.ThrowIfNull(self);
        EventHubResolver.Resolve(self.GetDomain()).Publish(message);
    }

    /// <summary>构造并发布一个无参跨 Domain 事件。</summary>
    /// <exception cref="KeyNotFoundException">没有注册事件中心。</exception>
    public static void PublishEvent<T>(this ICanSendEvent self) where T : new()
    {
        ArgumentNullException.ThrowIfNull(self);
        EventHubResolver.Resolve(self.GetDomain()).Publish(new T());
    }

    /// <summary>在所属 Domain 内发送事件。</summary>
    public static void SendEvent<T>(this ICanSendEvent self, T message)
    {
        ArgumentNullException.ThrowIfNull(self);
        self.GetDomain().SendEvent(message);
    }

    /// <summary>构造并在所属 Domain 内发送一个无参事件。</summary>
    public static void SendEvent<T>(this ICanSendEvent self) where T : new()
    {
        ArgumentNullException.ThrowIfNull(self);
        self.GetDomain().SendEvent(new T());
    }
}

/// <summary><see cref="ICanRegisterEvent"/> 的能力扩展。</summary>
public static class CanRegisterEventExtensions
{
    /// <summary>在所属 Domain 可见的 <see cref="IEventHub"/> 上订阅跨 Domain 事件；订阅由调用方取消。</summary>
    /// <exception cref="KeyNotFoundException">没有注册事件中心。</exception>
    public static IUnRegister SubscribeEvent<T>(this ICanRegisterEvent self, Action<T> handler)
    {
        ArgumentNullException.ThrowIfNull(self);
        return EventHubResolver.Resolve(self.GetDomain()).Subscribe(handler);
    }

    /// <summary>在所属 Domain 内订阅事件；Domain 释放时订阅自动失效。</summary>
    public static IUnRegister RegisterEvent<T>(this ICanRegisterEvent self, Action<T> handler)
    {
        ArgumentNullException.ThrowIfNull(self);
        return self.GetDomain().RegisterEvent(handler);
    }
}

/// <summary>订阅句柄的生命周期扩展。</summary>
public static class UnRegisterExtensions
{
    /// <summary>
    /// 让订阅随指定 System 释放而取消，适用于父域 Model 的 <see cref="BindableProperty{T}"/> 等不会随 Domain 自动失效的订阅。
    /// <para>需要提前取消时，保存并取消返回的句柄，不要再使用传入的原句柄：原句柄不知道自己已被登记，
    /// 直接取消它虽然能停止回调，但登记项要到 System 释放时才移除，反复订阅、取消会让登记列表持续增长。
    /// 订阅与 System 同生命周期时，丢弃返回值即可。</para>
    /// </summary>
    /// <exception cref="InvalidOperationException">System 未绑定 Domain 或正在释放；此时传入的订阅会被立即取消。</exception>
    public static IUnRegister UnRegisterOnRelease(this IUnRegister subscription, AbstractSystem owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return owner.Own(subscription);
    }
}

/// <summary>直接持有 <see cref="IDomain"/> 时的便利方法。</summary>
public static class DomainExtensions
{
    /// <summary>构造并同步执行一个无参命令。</summary>
    public static void SendCommand<T>(this IDomain domain) where T : ICommand, new()
    {
        ArgumentNullException.ThrowIfNull(domain);
        domain.SendCommand(new T());
    }

    /// <summary>构造并发送一个无参事件对象。</summary>
    public static void SendEvent<T>(this IDomain domain) where T : new()
    {
        ArgumentNullException.ThrowIfNull(domain);
        domain.SendEvent(new T());
    }
}
