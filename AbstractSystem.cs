namespace SimpleFramework;

/// <summary>
/// System 生命周期基类。子类可直接调用 <see cref="GetModel{T}"/>、<see cref="GetSystem{T}"/>、
/// <see cref="GetUtility{T}"/>、<see cref="SendEvent{T}(T)"/> 和 <see cref="RegisterEvent{T}"/>。
/// </summary>
public abstract class AbstractSystem : ISystemLifecycle
{
    /// <summary>所属 Domain；初始化前和释放后为 <see langword="null"/>。</summary>
    private IDomain? _domain;

    /// <summary>所属 Domain；未绑定时抛出异常，让错误发生在调用处而不是之后的空引用。</summary>
    private IDomain Domain => _domain ?? throw new InvalidOperationException($"{GetType().FullName} 未绑定 Domain：尚未初始化，或已经释放（包括所属 Domain 已释放）。");

    /// <inheritdoc />
    IDomain IBelongToDomain.GetDomain() => Domain;

    /// <inheritdoc />
    void ISystemLifecycle.Initialize(IDomain domain)
    {
        ArgumentNullException.ThrowIfNull(domain);
        if (_domain is not null) throw new InvalidOperationException($"{GetType().FullName} 已绑定 Domain，不能重复初始化。");

        _domain = domain;
        try
        {
            OnInitialize();
        }
        catch
        {
            // 初始化失败时框架不会调用 Release，这里解除绑定，避免实例停留在半绑定状态。
            _domain = null;
            throw;
        }
    }

    /// <inheritdoc />
    void ISystemLifecycle.Release()
    {
        try { OnRelease(); }
        finally { _domain = null; }
    }

    /// <summary>
    /// 初始化 System。可以获取并缓存其他组件的引用，但不要依赖其他 System 已经初始化完成，
    /// 因为 System 按注册顺序初始化。
    /// </summary>
    protected abstract void OnInitialize();

    /// <summary>
    /// 释放 System 自身资源。仍可读取组件，但本 Domain 的事件订阅已失效。
    /// <para>System 按初始化逆序释放，后注册的 System 此时已经释放，不要调用它们的方法；
    /// 需要多个组件配合的退出逻辑（例如统一保存）应放在 Domain 的 OnDeactivating 中，那时所有组件都还可用。</para>
    /// </summary>
    protected virtual void OnRelease() { }

    /// <summary>获取 Model；找不到时抛出 <see cref="KeyNotFoundException"/>。</summary>
    protected T GetModel<T>() where T : class, IModel => Domain.GetModel<T>();

    /// <summary>获取 System；找不到时抛出 <see cref="KeyNotFoundException"/>。</summary>
    protected T GetSystem<T>() where T : class, ISystem => Domain.GetSystem<T>();

    /// <summary>获取 Utility；找不到时抛出 <see cref="KeyNotFoundException"/>。</summary>
    protected T GetUtility<T>() where T : class, IUtility => Domain.GetUtility<T>();

    /// <summary>在所属 Domain 内发送事件。</summary>
    protected void SendEvent<T>(T message) => Domain.SendEvent(message);

    /// <summary>构造并在所属 Domain 内发送一个无参事件。</summary>
    protected void SendEvent<T>() where T : new() => Domain.SendEvent(new T());

    /// <summary>
    /// 在所属 Domain 内订阅事件；Domain 释放时自动失效，通常无需保存返回的句柄。
    /// <para>只有本地事件会自动失效。订阅父域 Model 的 <c>BindableProperty</c>、Utility 上的事件等非本地来源时，
    /// 必须保存句柄并在 <see cref="OnRelease"/> 中取消，否则回调会在本 Domain 释放后继续被调用。</para>
    /// </summary>
    protected IUnRegister RegisterEvent<T>(Action<T> handler) => Domain.RegisterEvent(handler);
}
