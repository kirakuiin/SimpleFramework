namespace SimpleFramework.FrameworkImpl;

/// <summary>组件 Context 的最小生命周期状态。</summary>
internal enum ComponentContextState
{
    /// <summary>组件正在初始化，仅开放可恢复能力。</summary>
    Initializing,

    /// <summary>组件初始化完成，可使用声明的运行期能力。</summary>
    Ready,

    /// <summary>Context 已永久失效，所有能力均不可使用。</summary>
    Invalid
}

/// <summary>集中管理组件 Context 的 Domain 归属和阶段校验。</summary>
internal abstract class ComponentContextBase
{
    protected ComponentContextBase(AbstractDomain domain, DomainComponentEntry entry)
    {
        Domain = domain;
        Entry = entry;
    }

    protected AbstractDomain Domain { get; }
    protected DomainComponentEntry Entry { get; }
    protected ComponentContextState State { get; private set; } = ComponentContextState.Initializing;

    public void MarkReady() => State = ComponentContextState.Ready;
    public void Invalidate() => State = ComponentContextState.Invalid;

    protected void AllowInitializingOrReady()
    {
        if (State == ComponentContextState.Invalid) throw new InvalidOperationException("组件 Context 已失效。");
    }

    protected void AllowReady()
    {
        if (State != ComponentContextState.Ready) throw new InvalidOperationException("此 Context 能力只在组件初始化完成后可用。");
    }
}

/// <summary>向 Model 提供按阶段受限的 Utility 查找与本地事件发送能力。</summary>
internal sealed class ModelContext : ComponentContextBase, IModelContext
{
    public ModelContext(AbstractDomain domain, DomainComponentEntry entry) : base(domain, entry) { }

    public T GetUtility<T>() where T : class, IUtility
    {
        AllowInitializingOrReady();
        return Domain.ResolveForContext<T>(ComponentCategory.Utility);
    }

    public bool TryGetUtility<T>(out T? utility) where T : class, IUtility
    {
        AllowInitializingOrReady();
        return Domain.TryResolveForContext(ComponentCategory.Utility, out utility);
    }

    public void SendEvent<T>(T message)
    {
        AllowReady();
        Domain.SendEvent(message);
    }
}

/// <summary>向 System 提供按阶段受限的分类查找、本地事件订阅与发送能力。</summary>
internal sealed class SystemContext : ComponentContextBase, ISystemContext
{
    public SystemContext(AbstractDomain domain, DomainComponentEntry entry) : base(domain, entry) { }

    public T GetSystem<T>() where T : class, ISystem
    {
        AllowReady();
        return Domain.ResolveForContext<T>(ComponentCategory.System);
    }

    public bool TryGetSystem<T>(out T? system) where T : class, ISystem
    {
        AllowReady();
        return Domain.TryResolveForContext(ComponentCategory.System, out system);
    }

    public T GetModel<T>() where T : class, IModel
    {
        AllowInitializingOrReady();
        return Domain.ResolveForContext<T>(ComponentCategory.Model);
    }

    public bool TryGetModel<T>(out T? model) where T : class, IModel
    {
        AllowInitializingOrReady();
        return Domain.TryResolveForContext(ComponentCategory.Model, out model);
    }

    public T GetUtility<T>() where T : class, IUtility
    {
        AllowInitializingOrReady();
        return Domain.ResolveForContext<T>(ComponentCategory.Utility);
    }

    public bool TryGetUtility<T>(out T? utility) where T : class, IUtility
    {
        AllowInitializingOrReady();
        return Domain.TryResolveForContext(ComponentCategory.Utility, out utility);
    }

    public IUnRegister RegisterEvent<T>(Action<T> handler)
    {
        AllowInitializingOrReady();
        return Domain.RegisterOwnedEvent(Entry, handler);
    }

    public void SendEvent<T>(T message)
    {
        AllowReady();
        Domain.SendEvent(message);
    }
}
