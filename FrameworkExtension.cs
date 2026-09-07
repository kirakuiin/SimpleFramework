using System.Diagnostics.CodeAnalysis;
using SimpleFramework.FrameworkImpl;

namespace SimpleFramework;

/// <summary>Model 生命周期实现的组件查找便利方法。</summary>
/// <remarks>
/// 方法转发到当前 <see cref="IModelContext"/>：初始化期间和初始化完成后可用；
/// 组件尚未附加 Context、Context 已失效或释放完成后调用会抛出 <see cref="InvalidOperationException"/>。
/// TryGet 只将“组件不存在”转换为 <see langword="false"/>，不会屏蔽生命周期或解析歧义错误。
/// </remarks>
public static class ModelLookupExtensions
{
    /// <summary>通过当前 Model Context 获取 Utility。</summary>
    public static T GetUtility<T>(this IModelLifecycle self) where T : class, IUtility =>
        GetContext(self).GetUtility<T>();

    /// <summary>通过当前 Model Context 尝试获取 Utility。</summary>
    public static bool TryGetUtility<T>(this IModelLifecycle self, [NotNullWhen(true)] out T? utility) where T : class, IUtility =>
        GetContext(self).TryGetUtility(out utility);

    private static IModelContext GetContext(IModelLifecycle model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return (IModelContext)LifecycleOwnershipTracker.GetContext(model);
    }
}

/// <summary>System 生命周期实现的组件查找便利方法。</summary>
/// <remarks>
/// 方法转发到当前 <see cref="ISystemContext"/>：Model 和 Utility 查找在初始化期间即可使用，
/// System 查找仅在初始化成功后可用；组件尚未附加 Context、Context 已失效或释放完成后调用会抛出
/// <see cref="InvalidOperationException"/>。TryGet 只将“组件不存在”转换为 <see langword="false"/>，
/// 不会屏蔽生命周期或解析歧义错误。
/// </remarks>
public static class SystemLookupExtensions
{
    /// <summary>通过当前 System Context 获取 Model。</summary>
    public static T GetModel<T>(this ISystemLifecycle self) where T : class, IModel =>
        GetContext(self).GetModel<T>();

    /// <summary>通过当前 System Context 尝试获取 Model。</summary>
    public static bool TryGetModel<T>(this ISystemLifecycle self, [NotNullWhen(true)] out T? model) where T : class, IModel =>
        GetContext(self).TryGetModel(out model);

    /// <summary>通过当前 System Context 获取 System。</summary>
    public static T GetSystem<T>(this ISystemLifecycle self) where T : class, ISystem =>
        GetContext(self).GetSystem<T>();

    /// <summary>通过当前 System Context 尝试获取 System。</summary>
    public static bool TryGetSystem<T>(this ISystemLifecycle self, [NotNullWhen(true)] out T? system) where T : class, ISystem =>
        GetContext(self).TryGetSystem(out system);

    /// <summary>通过当前 System Context 获取 Utility。</summary>
    public static T GetUtility<T>(this ISystemLifecycle self) where T : class, IUtility =>
        GetContext(self).GetUtility<T>();

    /// <summary>通过当前 System Context 尝试获取 Utility。</summary>
    public static bool TryGetUtility<T>(this ISystemLifecycle self, [NotNullWhen(true)] out T? utility) where T : class, IUtility =>
        GetContext(self).TryGetUtility(out utility);

    private static ISystemContext GetContext(ISystemLifecycle system)
    {
        ArgumentNullException.ThrowIfNull(system);
        return (ISystemContext)LifecycleOwnershipTracker.GetContext(system);
    }
}

/// <summary>Domain 的轻量组合便利方法。</summary>
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
