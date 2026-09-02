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
    int Value { get; }
}

/// <summary>可配置数值的测试 Utility。</summary>
internal sealed class ClockUtility : IClockUtility
{
    public int Value { get; set; }
}

/// <summary>记录初始化、释放和 Context 的直接生命周期 Model。</summary>
internal sealed class ProbeModel : IPlayerModel, IModelLifecycle
{
    private readonly Action<IModelContext>? _initialize;
    private readonly Action? _release;

    public ProbeModel(Action<IModelContext>? initialize = null, Action? release = null)
    {
        _initialize = initialize;
        _release = release;
    }

    public int InitializeCount { get; private set; }
    public int ReleaseCount { get; private set; }
    public IModelContext? SavedContext { get; private set; }

    public void Initialize(IModelContext context)
    {
        InitializeCount++;
        SavedContext = context;
        _initialize?.Invoke(context);
    }

    public void Release()
    {
        ReleaseCount++;
        _release?.Invoke();
    }
}

/// <summary>提供第二个可赋值候选的测试 Model。</summary>
internal sealed class AlternateModel : IAltPlayerModel, IPlayerModel, IModelLifecycle
{
    public int ReleaseCount { get; private set; }
    public void Initialize(IModelContext context) { }
    public void Release() => ReleaseCount++;
}

/// <summary>记录初始化、释放和 Context 的直接生命周期 System。</summary>
internal sealed class ProbeSystem : IPlayerSystem, ISystemLifecycle
{
    private readonly Action<ISystemContext>? _initialize;
    private readonly Action? _release;

    public ProbeSystem(Action<ISystemContext>? initialize = null, Action? release = null)
    {
        _initialize = initialize;
        _release = release;
    }

    public int InitializeCount { get; private set; }
    public int ReleaseCount { get; private set; }
    public ISystemContext? SavedContext { get; private set; }

    public void Initialize(ISystemContext context)
    {
        InitializeCount++;
        SavedContext = context;
        _initialize?.Invoke(context);
    }

    public void Release()
    {
        ReleaseCount++;
        _release?.Invoke();
    }
}

/// <summary>提供第二个可赋值候选的测试 System。</summary>
internal sealed class AlternateSystem : IAltPlayerSystem, IPlayerSystem, ISystemLifecycle
{
    public void Initialize(ISystemContext context) { }
    public void Release() { }
}

/// <summary>验证 <see cref="AbstractModel"/> 生命周期基类的测试 Model。</summary>
internal sealed class DerivedModel : AbstractModel, IPlayerModel
{
    public int InitializeCount { get; private set; }
    public int ReleaseCount { get; private set; }
    protected override void OnInitialize()
    {
        _ = this.TryGetUtility<IClockUtility>(out _);
        InitializeCount++;
    }
    protected override void OnRelease() => ReleaseCount++;
}

/// <summary>验证 <see cref="AbstractSystem"/> 生命周期基类的测试 System。</summary>
internal sealed class DerivedSystem : AbstractSystem, IPlayerSystem
{
    public int InitializeCount { get; private set; }
    public int ReleaseCount { get; private set; }
    protected override void OnInitialize()
    {
        _ = this.GetModel<IPlayerModel>();
        InitializeCount++;
    }
    protected override void OnRelease() => ReleaseCount++;
}

/// <summary>用于验证跨组件分类注册会被拒绝的测试对象。</summary>
internal sealed class MultiCategoryComponent : IModelLifecycle, IUtility
{
    public void Initialize(IModelContext context) { }
    public void Release() { }
}

/// <summary>可注入 Configure、激活和释放行为的参数化测试 Domain。</summary>
internal sealed class ProbeDomain : AbstractDomain
{
    private readonly Action<ProbeDomain>? _configure;
    private readonly Action<ProbeDomain>? _activated;
    private readonly Action<ProbeDomain>? _deactivating;

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

    public string Name { get; }

    public static ProbeDomain Create(
        string name = "probe",
        Action<ProbeDomain>? configure = null,
        Action<ProbeDomain>? activated = null,
        Action<ProbeDomain>? deactivating = null) =>
        CreateDomain(() => new ProbeDomain(name, configure, activated, deactivating));

    protected override void Configure() => _configure?.Invoke(this);
    protected override void OnActivated() => _activated?.Invoke(this);
    protected override void OnDeactivating() => _deactivating?.Invoke(this);
    public override string ToString() => Name;
}

/// <summary>用于验证严格单例创建、发布和销毁规则的测试 Domain。</summary>
internal sealed class StrictProbeDomain : AbstractSingletonDomain<StrictProbeDomain>
{
    private static Action<StrictProbeDomain>? _configure;
    private static Action<StrictProbeDomain>? _activated;
    private StrictProbeDomain() { }

    public static StrictProbeDomain Instance => GetOrCreateInstance(() => new StrictProbeDomain());

    public static void SetHooks(Action<StrictProbeDomain>? configure = null, Action<StrictProbeDomain>? activated = null)
    {
        _configure = configure;
        _activated = activated;
    }

    protected override void Configure() => _configure?.Invoke(this);
    protected override void OnActivated() => _activated?.Invoke(this);
}

/// <summary>用于验证本地事件分发的测试消息。</summary>
internal readonly record struct Ping(int Value);

/// <summary>表示接收命令栈 Context 的测试回调。</summary>
internal delegate void CommandAction(CommandContext context);

/// <summary>表示接收命令栈 Context 并返回结果的测试回调。</summary>
internal delegate TResult CommandFunc<TResult>(CommandContext context);

/// <summary>表示接收查询栈 Context 并返回结果的测试回调。</summary>
internal delegate TResult QueryFunc<TResult>(QueryContext context);

/// <summary>将测试回调适配为无返回值命令。</summary>
internal sealed class DelegateCommand(CommandAction execute) : ICommand
{
    public void Execute(CommandContext context) => execute(context);
}

/// <summary>将测试回调适配为带返回值命令。</summary>
internal sealed class DelegateCommand<TResult>(CommandFunc<TResult> execute) : ICommand<TResult>
{
    public TResult Execute(CommandContext context) => execute(context);
}

/// <summary>将测试回调适配为查询。</summary>
internal sealed class DelegateQuery<TResult>(QueryFunc<TResult> execute) : IQuery<TResult>
{
    public TResult Execute(QueryContext context) => execute(context);
}

/// <summary>用于分配特征测试的空命令。</summary>
internal sealed class EmptyCommand : ICommand
{
    public void Execute(CommandContext context) { }
}
