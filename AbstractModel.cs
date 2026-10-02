namespace SimpleFramework;

/// <summary>
/// Model 生命周期基类。子类可直接调用 <see cref="GetUtility{T}"/>、<see cref="SendEvent{T}(T)"/>
/// 和跨 Domain 的 <see cref="PublishEvent{T}(T)"/>。
/// </summary>
public abstract class AbstractModel : IModelLifecycle
{
    /// <summary>所属 Domain；初始化前和释放后为 <see langword="null"/>。</summary>
    private IDomain? _domain;

    /// <summary>所属 Domain；未绑定时抛出异常，让错误发生在调用处而不是之后的空引用。</summary>
    private IDomain Domain => _domain ?? throw new InvalidOperationException($"{GetType().FullName} 未绑定 Domain：尚未初始化，或已经释放（包括所属 Domain 已释放）。");

    /// <inheritdoc />
    IDomain IBelongToDomain.GetDomain() => Domain;

    /// <inheritdoc />
    void IModelLifecycle.Initialize(IDomain domain)
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
    void IModelLifecycle.Release()
    {
        try { OnRelease(); }
        finally { _domain = null; }
    }

    /// <summary>初始化 Model。可以读取 Utility。</summary>
    protected abstract void OnInitialize();

    /// <summary>
    /// 释放 Model 自身资源。仍可读取 Utility。
    /// <para>Model 在全部 System 之后、按初始化逆序释放；不要在这里依赖其他 Model 尚未释放。</para>
    /// </summary>
    protected virtual void OnRelease() { }

    /// <summary>获取 Utility；找不到时抛出 <see cref="KeyNotFoundException"/>。</summary>
    protected T GetUtility<T>() where T : class, IUtility => Domain.GetUtility<T>();

    /// <summary>在所属 Domain 内发送事件。</summary>
    protected void SendEvent<T>(T message) => Domain.SendEvent(message);

    /// <summary>构造并在所属 Domain 内发送一个无参事件。</summary>
    protected void SendEvent<T>() where T : new() => Domain.SendEvent(new T());

    /// <summary>通过所属 Domain 可见的 <see cref="IEventHub"/> 发布跨 Domain 事件。</summary>
    /// <exception cref="KeyNotFoundException">没有注册事件中心。</exception>
    protected void PublishEvent<T>(T message) => EventHubResolver.Resolve(Domain).Publish(message);

    /// <summary>构造并发布一个无参跨 Domain 事件。</summary>
    /// <exception cref="KeyNotFoundException">没有注册事件中心。</exception>
    protected void PublishEvent<T>() where T : new() => EventHubResolver.Resolve(Domain).Publish(new T());
}
