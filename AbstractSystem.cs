using SimpleFramework.FrameworkImpl;

namespace SimpleFramework;

/// <summary>
/// System 生命周期基类。子类可直接调用 <see cref="GetModel{T}"/>、<see cref="GetSystem{T}"/>、
/// <see cref="GetUtility{T}"/>、<see cref="SendEvent{T}(T)"/>、<see cref="RegisterEvent{T}"/>，
/// 以及跨 Domain 的 <see cref="PublishEvent{T}(T)"/> 和 <see cref="SubscribeEvent{T}"/>。
/// </summary>
public abstract class AbstractSystem : ISystemLifecycle
{
    /// <summary>所属 Domain；初始化前和释放后为 <see langword="null"/>。</summary>
    private IDomain? _domain;

    /// <summary>随本 System 释放而取消的订阅句柄，按登记顺序保存。</summary>
    private readonly List<IUnRegister> _ownedSubscriptions = new();

    /// <summary>是否正在执行 Release；此时登记的订阅不会再有机会被取消，必须拒绝。</summary>
    private bool _isReleasing;

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
        catch (Exception initializationFailure)
        {
            // 初始化失败时框架不会调用 Release：这里取消初始化期间登记的订阅并解除绑定，
            // 否则事件中心等外部来源会继续回调一个半初始化的实例。原始异常放在首位，不能被取消失败掩盖。
            var failures = new List<Exception>();
            Failures.Add(failures, initializationFailure);

            // 清理期间与 Release 一样拒绝新的登记，否则取消回调里再订阅会在解除绑定后泄漏。
            _isReleasing = true;
            CancelOwnedSubscriptions(failures);
            _domain = null;
            _isReleasing = false;

            Failures.ThrowIfAny(failures);
        }
    }

    /// <inheritdoc />
    void ISystemLifecycle.Release()
    {
        _isReleasing = true;
        var failures = new List<Exception>();

        // 先取消订阅再调用 OnRelease，使 System 在自己的释放回调中不再被外部事件回调；
        // 取消与 OnRelease 的失败都收集起来，任何一步失败都不能跳过其余清理。
        CancelOwnedSubscriptions(failures);
        try { OnRelease(); }
        catch (Exception exception) { Failures.Add(failures, exception); }

        _domain = null;
        _isReleasing = false;
        Failures.ThrowIfAny(failures);
    }

    /// <summary>
    /// 初始化 System。可以获取并缓存其他组件的引用，但不要依赖其他 System 已经初始化完成，
    /// 因为 System 按注册顺序初始化。
    /// </summary>
    protected abstract void OnInitialize();

    /// <summary>
    /// 释放 System 自身资源。仍可读取组件，但本 Domain 的事件订阅已失效；
    /// 通过 <see cref="SubscribeEvent{T}"/> 或 <c>UnRegisterOnRelease</c> 登记的订阅也已取消，此时不能再登记新的订阅。
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
    /// 对句柄调用 <c>UnRegisterOnRelease(this)</c>，否则回调会在本 System 释放后继续被调用。</para>
    /// </summary>
    protected IUnRegister RegisterEvent<T>(Action<T> handler) => Domain.RegisterEvent(handler);

    /// <summary>通过所属 Domain 可见的 <see cref="IEventHub"/> 发布跨 Domain 事件。</summary>
    /// <exception cref="KeyNotFoundException">没有注册事件中心。</exception>
    protected void PublishEvent<T>(T message) => EventHubResolver.Resolve(Domain).Publish(message);

    /// <summary>构造并发布一个无参跨 Domain 事件。</summary>
    /// <exception cref="KeyNotFoundException">没有注册事件中心。</exception>
    protected void PublishEvent<T>() where T : new() => EventHubResolver.Resolve(Domain).Publish(new T());

    /// <summary>
    /// 在所属 Domain 可见的 <see cref="IEventHub"/> 上订阅跨 Domain 事件。订阅随本 System 释放自动取消，
    /// 通常无需保存句柄；需要提前取消时调用返回句柄的 <see cref="IUnRegister.UnRegister"/>。
    /// </summary>
    /// <exception cref="KeyNotFoundException">没有注册事件中心。</exception>
    /// <exception cref="InvalidOperationException">System 未绑定 Domain 或正在释放。</exception>
    protected IUnRegister SubscribeEvent<T>(Action<T> handler)
    {
        // 先校验再订阅：校验失败时不能在事件中心上留下一个无人负责取消的订阅。
        if (!CanOwn) throw CreateCannotOwnException();
        return Own(EventHubResolver.Resolve(Domain).Subscribe(handler));
    }

    /// <summary>
    /// 登记一个随本 System 释放而取消的订阅句柄，返回可提前取消的包装句柄。
    /// 无法登记时会先取消传入的订阅再抛出异常。
    /// </summary>
    /// <exception cref="InvalidOperationException">System 未绑定 Domain 或正在释放。</exception>
    internal IUnRegister Own(IUnRegister subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        if (!CanOwn)
        {
            // 这个订阅再也没有机会随释放取消，留着只会在 System 释放后继续回调，因此拒绝前先取消它。
            var rejection = CreateCannotOwnException();
            try { subscription.UnRegister(); }
            catch (Exception cancelFailure) { throw new AggregateException(rejection, cancelFailure); }
            throw rejection;
        }

        // 提前取消时同时移出列表，避免反复订阅、取消的 System 让列表无限增长。
        IUnRegister? owned = null;
        owned = new CustomUnRegister(() =>
        {
            _ownedSubscriptions.Remove(owned!);
            subscription.UnRegister();
        });
        _ownedSubscriptions.Add(owned);
        return owned;
    }

    /// <summary>当前能否登记随释放取消的订阅：必须已绑定 Domain 且不在释放或初始化失败清理中。</summary>
    private bool CanOwn => _domain is not null && !_isReleasing;

    /// <summary>创建“不能登记订阅”的异常。</summary>
    private InvalidOperationException CreateCannotOwnException() =>
        new($"{GetType().FullName} 未绑定 Domain 或正在释放，不能登记随释放取消的订阅。");

    /// <summary>
    /// 逆序取消全部登记的订阅，把失败收集到 <paramref name="failures"/>。
    /// 先整体取出再遍历，因为取消回调会修改列表；单个句柄失败不能让其余订阅泄漏。
    /// </summary>
    private void CancelOwnedSubscriptions(List<Exception> failures)
    {
        var subscriptions = _ownedSubscriptions.ToArray();
        _ownedSubscriptions.Clear();

        for (var index = subscriptions.Length - 1; index >= 0; index--)
        {
            try { subscriptions[index].UnRegister(); }
            catch (Exception exception) { Failures.Add(failures, exception); }
        }
    }
}
