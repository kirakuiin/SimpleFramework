using SimpleFramework;

namespace Test.Framework;

/// <summary>用于验证 Model 接口解析的测试契约。</summary>
internal interface IPlayerModel : IModel { }

/// <summary>用于验证多 Model 候选和显式键的测试契约。</summary>
internal interface IAltPlayerModel : IModel { }

/// <summary>用于验证 System 接口解析的测试契约。</summary>
internal interface IPlayerSystem : ISystem { }

/// <summary>用于验证多 System 候选和显式键的测试契约。</summary>
internal interface IAltPlayerSystem : ISystem { }

/// <summary>用于验证 Utility 查找和共享的测试契约。</summary>
internal interface IClockUtility : IUtility
{
    /// <summary>测试数值。</summary>
    int Value { get; }
}

/// <summary>可配置数值的测试 Utility。</summary>
internal sealed class ClockUtility : IClockUtility
{
    /// <inheritdoc />
    public int Value { get; set; }
}

/// <summary>直接实现生命周期接口、记录初始化与释放的测试 Model。</summary>
internal sealed class ProbeModel(Action<IDomain>? initialize = null, Action? release = null) : IPlayerModel, IModelLifecycle
{
    /// <summary>初始化时绑定的 Domain。</summary>
    private IDomain? _domain;

    /// <summary>Initialize 调用次数。</summary>
    public int InitializeCount { get; private set; }

    /// <summary>Release 调用次数。</summary>
    public int ReleaseCount { get; private set; }

    /// <inheritdoc />
    public IDomain GetDomain() => _domain ?? throw new InvalidOperationException("未绑定 Domain。");

    /// <inheritdoc />
    public void Initialize(IDomain domain)
    {
        InitializeCount++;
        _domain = domain;
        initialize?.Invoke(domain);
    }

    /// <inheritdoc />
    public void Release()
    {
        ReleaseCount++;
        release?.Invoke();
    }
}

/// <summary>提供第二个可赋值候选的测试 Model。</summary>
internal sealed class AlternateModel : IAltPlayerModel, IPlayerModel, IModelLifecycle
{
    /// <summary>Release 调用次数。</summary>
    public int ReleaseCount { get; private set; }

    /// <inheritdoc />
    public IDomain GetDomain() => throw new NotSupportedException();

    /// <inheritdoc />
    public void Initialize(IDomain domain) { }

    /// <inheritdoc />
    public void Release() => ReleaseCount++;
}

/// <summary>直接实现生命周期接口、记录初始化与释放的测试 System。</summary>
internal sealed class ProbeSystem(Action<IDomain>? initialize = null, Action? release = null) : IPlayerSystem, ISystemLifecycle
{
    /// <summary>初始化时绑定的 Domain。</summary>
    private IDomain? _domain;

    /// <summary>Initialize 调用次数。</summary>
    public int InitializeCount { get; private set; }

    /// <summary>Release 调用次数。</summary>
    public int ReleaseCount { get; private set; }

    /// <inheritdoc />
    public IDomain GetDomain() => _domain ?? throw new InvalidOperationException("未绑定 Domain。");

    /// <inheritdoc />
    public void Initialize(IDomain domain)
    {
        InitializeCount++;
        _domain = domain;
        initialize?.Invoke(domain);
    }

    /// <inheritdoc />
    public void Release()
    {
        ReleaseCount++;
        release?.Invoke();
    }
}

/// <summary>提供第二个可赋值候选的测试 System。</summary>
internal sealed class AlternateSystem : IAltPlayerSystem, IPlayerSystem, ISystemLifecycle
{
    /// <inheritdoc />
    public IDomain GetDomain() => throw new NotSupportedException();

    /// <inheritdoc />
    public void Initialize(IDomain domain) { }

    /// <inheritdoc />
    public void Release() { }
}

/// <summary>继承 <see cref="AbstractModel"/> 的测试 Model，把受保护能力转发给测试回调。</summary>
internal sealed class DerivedModel(Action<DerivedModel>? initialize = null, Action<DerivedModel>? release = null)
    : AbstractModel, IPlayerModel
{
    /// <summary>OnInitialize 调用次数。</summary>
    public int InitializeCount { get; private set; }

    /// <summary>OnRelease 调用次数。</summary>
    public int ReleaseCount { get; private set; }

    /// <summary>转发 <see cref="AbstractModel.GetUtility{T}"/>。</summary>
    public T ReadUtility<T>() where T : class, IUtility => GetUtility<T>();

    /// <summary>转发 <see cref="AbstractModel.SendEvent{T}(T)"/>。</summary>
    public void Publish<T>(T message) => SendEvent(message);

    /// <inheritdoc />
    protected override void OnInitialize()
    {
        InitializeCount++;
        initialize?.Invoke(this);
    }

    /// <inheritdoc />
    protected override void OnRelease()
    {
        ReleaseCount++;
        release?.Invoke(this);
    }
}

/// <summary>继承 <see cref="AbstractSystem"/> 的测试 System，把受保护能力转发给测试回调。</summary>
internal sealed class DerivedSystem(Action<DerivedSystem>? initialize = null, Action<DerivedSystem>? release = null)
    : AbstractSystem, IPlayerSystem
{
    /// <summary>OnInitialize 调用次数。</summary>
    public int InitializeCount { get; private set; }

    /// <summary>OnRelease 调用次数。</summary>
    public int ReleaseCount { get; private set; }

    /// <summary>转发 <see cref="AbstractSystem.GetModel{T}"/>。</summary>
    public T ReadModel<T>() where T : class, IModel => GetModel<T>();

    /// <summary>转发 <see cref="AbstractSystem.GetSystem{T}"/>。</summary>
    public T ReadSystem<T>() where T : class, ISystem => GetSystem<T>();

    /// <summary>转发 <see cref="AbstractSystem.GetUtility{T}"/>。</summary>
    public T ReadUtility<T>() where T : class, IUtility => GetUtility<T>();

    /// <summary>转发 <see cref="AbstractSystem.SendEvent{T}(T)"/>。</summary>
    public void Publish<T>(T message) => SendEvent(message);

    /// <summary>转发 <see cref="AbstractSystem.RegisterEvent{T}"/>。</summary>
    public IUnRegister Subscribe<T>(Action<T> handler) => RegisterEvent(handler);

    /// <inheritdoc />
    protected override void OnInitialize()
    {
        InitializeCount++;
        initialize?.Invoke(this);
    }

    /// <inheritdoc />
    protected override void OnRelease()
    {
        ReleaseCount++;
        release?.Invoke(this);
    }
}

/// <summary>以显式键注册、用于验证 System 间查找的第二个 System。</summary>
internal sealed class AltDerivedSystem : AbstractSystem, IAltPlayerSystem
{
    /// <summary>OnInitialize 调用次数。</summary>
    public int InitializeCount { get; private set; }

    /// <inheritdoc />
    protected override void OnInitialize() => InitializeCount++;
}

/// <summary>在 OnRelease 中执行给定回调的测试 System，用于验证释放顺序。</summary>
internal sealed class AltReleaseCallerSystem(Action onRelease) : AbstractSystem, IAltPlayerSystem
{
    /// <inheritdoc />
    protected override void OnInitialize() { }

    /// <inheritdoc />
    protected override void OnRelease() => onRelease();
}

/// <summary>同时属于 Model 和 Utility 分类、应被拒绝注册的测试对象。</summary>
internal sealed class MultiCategoryComponent : IModelLifecycle, IUtility
{
    /// <inheritdoc />
    public IDomain GetDomain() => throw new NotSupportedException();

    /// <inheritdoc />
    public void Initialize(IDomain domain) { }

    /// <inheritdoc />
    public void Release() { }
}

/// <summary>实现 <see cref="IController"/> 的测试表现层对象。</summary>
internal sealed class ProbeController(IDomain domain) : IController
{
    /// <inheritdoc />
    public IDomain GetDomain() => domain;
}

/// <summary>可注入 Configure、激活和停用行为的参数化测试 Domain。</summary>
internal sealed class ProbeDomain : AbstractDomain
{
    /// <summary>Configure 回调。</summary>
    private readonly Action<ProbeDomain>? _configure;

    /// <summary>OnActivated 回调。</summary>
    private readonly Action<ProbeDomain>? _activated;

    /// <summary>OnDeactivating 回调。</summary>
    private readonly Action<ProbeDomain>? _deactivating;

    /// <summary>仅供工厂使用的构造函数。</summary>
    private ProbeDomain(
        string name,
        Action<ProbeDomain>? configure,
        Action<ProbeDomain>? activated,
        Action<ProbeDomain>? deactivating)
    {
        Name = name;
        _configure = configure;
        _activated = activated;
        _deactivating = deactivating;
    }

    /// <summary>测试名称，用于验证构造参数传递。</summary>
    public string Name { get; }

    /// <summary>创建并启动测试 Domain。</summary>
    public static ProbeDomain Create(
        string name = "probe",
        Action<ProbeDomain>? configure = null,
        Action<ProbeDomain>? activated = null,
        Action<ProbeDomain>? deactivating = null) =>
        CreateDomain(() => new ProbeDomain(name, configure, activated, deactivating));

    /// <summary>模拟工厂误返回已在使用的实例，用于验证只能启动一次。</summary>
    public static ProbeDomain CreateFrom(ProbeDomain existing) => CreateDomain(() => existing);

    /// <summary>转发 <see cref="AbstractDomain.RegisterModel(IModelLifecycle)"/>。</summary>
    public void AddModel(IModelLifecycle model) => RegisterModel(model);

    /// <summary>转发 <see cref="AbstractDomain.RegisterModel{T}(IModelLifecycle)"/>。</summary>
    public void AddModel<T>(IModelLifecycle model) where T : class, IModel => RegisterModel<T>(model);

    /// <summary>转发 <see cref="AbstractDomain.RegisterSystem(ISystemLifecycle)"/>。</summary>
    public void AddSystem(ISystemLifecycle system) => RegisterSystem(system);

    /// <summary>转发 <see cref="AbstractDomain.RegisterSystem{T}(ISystemLifecycle)"/>。</summary>
    public void AddSystem<T>(ISystemLifecycle system) where T : class, ISystem => RegisterSystem<T>(system);

    /// <summary>转发 <see cref="AbstractDomain.RegisterUtility(IUtility)"/>。</summary>
    public void AddUtility(IUtility utility) => RegisterUtility(utility);

    /// <summary>转发 <see cref="AbstractDomain.RegisterUtility{T}(IUtility)"/>。</summary>
    public void AddUtility<T>(IUtility utility) where T : class, IUtility => RegisterUtility<T>(utility);

    /// <inheritdoc />
    protected override void Configure() => _configure?.Invoke(this);

    /// <inheritdoc />
    protected override void OnActivated() => _activated?.Invoke(this);

    /// <inheritdoc />
    protected override void OnDeactivating() => _deactivating?.Invoke(this);

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>按名称判等的测试 Domain，用于验证树操作只使用引用身份。</summary>
internal sealed class EqualByNameDomain : AbstractDomain
{
    /// <summary>仅供工厂使用的构造函数。</summary>
    private EqualByNameDomain() { }

    /// <summary>创建并启动测试 Domain。</summary>
    public static EqualByNameDomain Create() => CreateDomain(() => new EqualByNameDomain());

    /// <inheritdoc />
    protected override void Configure() { }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is EqualByNameDomain;

    /// <inheritdoc />
    public override int GetHashCode() => 0;
}

/// <summary>用于验证严格单例创建、发布和销毁规则的测试 Domain。</summary>
internal sealed class StrictProbeDomain : AbstractSingletonDomain<StrictProbeDomain>
{
    /// <summary>Configure 回调。</summary>
    private static Action<StrictProbeDomain>? _configure;

    /// <summary>OnActivated 回调。</summary>
    private static Action<StrictProbeDomain>? _activated;

    /// <summary>仅供单例工厂使用的构造函数。</summary>
    private StrictProbeDomain() { }

    /// <summary>单例实例。</summary>
    public static StrictProbeDomain Instance => GetOrCreateInstance(() => new StrictProbeDomain());

    /// <summary>设置下一次创建使用的回调。</summary>
    public static void SetHooks(Action<StrictProbeDomain>? configure = null, Action<StrictProbeDomain>? activated = null)
    {
        _configure = configure;
        _activated = activated;
    }

    /// <inheritdoc />
    protected override void Configure() => _configure?.Invoke(this);

    /// <inheritdoc />
    protected override void OnActivated() => _activated?.Invoke(this);
}

/// <summary>用于验证本地事件分发的测试消息。</summary>
/// <param name="Value">消息数值。</param>
internal readonly record struct Ping(int Value);

/// <summary>表示接收命令栈上下文的测试回调。</summary>
internal delegate void CommandAction(CommandContext context);

/// <summary>表示接收命令栈上下文并返回结果的测试回调。</summary>
internal delegate TResult CommandFunc<TResult>(CommandContext context);

/// <summary>表示接收查询栈上下文并返回结果的测试回调。</summary>
internal delegate TResult QueryFunc<TResult>(QueryContext context);

/// <summary>将测试回调适配为无返回值命令。</summary>
internal sealed class DelegateCommand(CommandAction execute) : ICommand
{
    /// <inheritdoc />
    public void Execute(CommandContext context) => execute(context);
}

/// <summary>将测试回调适配为带返回值命令。</summary>
internal sealed class DelegateCommand<TResult>(CommandFunc<TResult> execute) : ICommand<TResult>
{
    /// <inheritdoc />
    public TResult Execute(CommandContext context) => execute(context);
}

/// <summary>将测试回调适配为查询。</summary>
internal sealed class DelegateQuery<TResult>(QueryFunc<TResult> execute) : IQuery<TResult>
{
    /// <inheritdoc />
    public TResult Execute(QueryContext context) => execute(context);
}

/// <summary>用于分配测试的空命令。</summary>
internal sealed class EmptyCommand : ICommand
{
    /// <inheritdoc />
    public void Execute(CommandContext context) { }
}
