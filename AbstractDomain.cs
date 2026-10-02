using System.Diagnostics.CodeAnalysis;
using SimpleFramework.FrameworkImpl;

namespace SimpleFramework;

/// <summary>
/// Domain 的扩展基类：在 <see cref="Configure"/> 中注册组件，通过 <see cref="IDomain"/> 消费组件，可组成父子树。
/// <para>Domain 采用单线程协作模型；调用方应在同一线程串行访问一棵树。</para>
/// </summary>
public abstract class AbstractDomain : IDomain, IDisposable
{
    /// <summary>诊断编号计数器，让日志能区分同类型的多个 Domain。</summary>
    private static long _nextDiagnosticId;

    /// <summary>本 Domain 的组件注册表。</summary>
    private readonly DomainComponentRegistry _registry = new();

    /// <summary>本 Domain 的本地事件。</summary>
    private readonly EventBus _events = new();

    /// <summary>直接子 Domain，按挂载顺序。</summary>
    private readonly List<AbstractDomain> _children = new();

    /// <summary>注册的 Model，按注册顺序。</summary>
    private readonly List<DomainComponentEntry> _models = new();

    /// <summary>注册的 System，按注册顺序。</summary>
    private readonly List<DomainComponentEntry> _systems = new();

    /// <summary>
    /// 初始化成功的组件，按初始化顺序。
    /// <para>因为先初始化全部 Model 再初始化全部 System，逆序遍历恰好满足“先 System 后 Model、各自逆序”的释放顺序。</para>
    /// </summary>
    private readonly List<DomainComponentEntry> _initialized = new();

    /// <summary>本实例的诊断编号。</summary>
    private readonly long _diagnosticId = Interlocked.Increment(ref _nextDiagnosticId);

    /// <summary><see cref="DiagnosticName"/> 的缓存；字段初始化器不能访问 GetType()，因此延迟生成。</summary>
    private string? _diagnosticName;

    /// <summary>父 Domain；未挂载时为 <see langword="null"/>。</summary>
    private AbstractDomain? _parent;

    /// <summary>生命周期状态。</summary>
    private DomainState _state = DomainState.Starting;

    /// <summary>是否正在执行 <see cref="Configure"/>；只有此时允许注册。</summary>
    private bool _isConfiguring;

    /// <summary>
    /// 是否已经调用过启动。单靠 <see cref="_state"/> 不够：启动过程中状态仍是 Starting，
    /// 在 Configure 里再次启动同一实例会无限递归，最终栈溢出使进程崩溃。
    /// </summary>
    private bool _isStartInvoked;

    /// <summary>
    /// 使用显式工厂创建并启动 Domain。具体类型应通过私有构造函数和静态 Create 方法调用本方法。
    /// </summary>
    /// <typeparam name="TDomain">具体 Domain 类型。</typeparam>
    /// <param name="factory">可捕获构造参数的同步工厂。</param>
    /// <param name="parent">
    /// 可选的父 Domain。指定后，子域在初始化期间就能回退查找父域的组件（例如父域注册的 <see cref="IEventHub"/>），
    /// 启动成功后自动挂为父域的子域，相当于创建后再调用 <see cref="AddChild"/>。
    /// </param>
    /// <returns>完成 Configure、组件初始化和 OnActivated 的 Active Domain。</returns>
    /// <exception cref="InvalidOperationException">父 Domain 不是 Active。</exception>
    /// <exception cref="ObjectDisposedException">父 Domain 已释放。</exception>
    protected static TDomain CreateDomain<TDomain>(Func<TDomain> factory, AbstractDomain? parent = null)
        where TDomain : AbstractDomain
    {
        ArgumentNullException.ThrowIfNull(factory);
        parent?.ThrowIfNotActive();

        var candidate = factory() ?? throw new InvalidOperationException("Domain 工厂返回了 null。");
        candidate.Start(parent);
        return candidate;
    }

    /// <summary>注册本 Domain 的 Utility、Model 和 System。注册只能在此方法中进行。</summary>
    protected abstract void Configure();

    /// <summary>全部组件初始化完成、Domain 进入 Active 后的通知。</summary>
    protected virtual void OnActivated() { }

    /// <summary>
    /// Domain 开始释放、组件尚未 Release 时的通知；此时子域已释放、事件订阅已失效，仍可读取组件。
    /// <para>只有进入过 Active 的 Domain 才会收到，与 <see cref="OnActivated"/> 成对。</para>
    /// </summary>
    protected virtual void OnDeactivating() { }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">Domain 已释放。</exception>
    public IDomain? Parent
    {
        get
        {
            ThrowIfDisposed();
            return _parent;
        }
    }

    /// <summary>按运行时具体类型注册一个 Model。</summary>
    /// <exception cref="ArgumentNullException">组件为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException">组件不能赋值给注册键类型。</exception>
    /// <exception cref="InvalidOperationException">不在 Configure 中调用、注册键或实例已注册，或组件同时属于多个分类。</exception>
    protected void RegisterModel(IModelLifecycle model)
    {
        ArgumentNullException.ThrowIfNull(model);
        Register(ComponentCategory.Model, model.GetType(), model);
    }

    /// <summary>按显式业务契约注册一个 Model；泛型契约就是唯一注册键。</summary>
    /// <exception cref="ArgumentNullException">组件为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException">组件不能赋值给注册键类型。</exception>
    /// <exception cref="InvalidOperationException">不在 Configure 中调用、注册键或实例已注册，或组件同时属于多个分类。</exception>
    protected void RegisterModel<T>(IModelLifecycle model) where T : class, IModel
    {
        ArgumentNullException.ThrowIfNull(model);
        Register(ComponentCategory.Model, typeof(T), model);
    }

    /// <summary>按运行时具体类型注册一个 System。</summary>
    /// <exception cref="ArgumentNullException">组件为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException">组件不能赋值给注册键类型。</exception>
    /// <exception cref="InvalidOperationException">不在 Configure 中调用、注册键或实例已注册，或组件同时属于多个分类。</exception>
    protected void RegisterSystem(ISystemLifecycle system)
    {
        ArgumentNullException.ThrowIfNull(system);
        Register(ComponentCategory.System, system.GetType(), system);
    }

    /// <summary>按显式业务契约注册一个 System；泛型契约就是唯一注册键。</summary>
    /// <exception cref="ArgumentNullException">组件为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException">组件不能赋值给注册键类型。</exception>
    /// <exception cref="InvalidOperationException">不在 Configure 中调用、注册键或实例已注册，或组件同时属于多个分类。</exception>
    protected void RegisterSystem<T>(ISystemLifecycle system) where T : class, ISystem
    {
        ArgumentNullException.ThrowIfNull(system);
        Register(ComponentCategory.System, typeof(T), system);
    }

    /// <summary>按运行时具体类型注册一个由调用方管理的 Utility。</summary>
    /// <exception cref="ArgumentNullException">组件为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException">组件不能赋值给注册键类型。</exception>
    /// <exception cref="InvalidOperationException">不在 Configure 中调用、注册键或实例已注册，或组件同时属于多个分类。</exception>
    protected void RegisterUtility(IUtility utility)
    {
        ArgumentNullException.ThrowIfNull(utility);
        Register(ComponentCategory.Utility, utility.GetType(), utility);
    }

    /// <summary>按显式业务契约注册一个由调用方管理的 Utility；泛型契约就是唯一注册键。</summary>
    /// <exception cref="ArgumentNullException">组件为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException">组件不能赋值给注册键类型。</exception>
    /// <exception cref="InvalidOperationException">不在 Configure 中调用、注册键或实例已注册，或组件同时属于多个分类。</exception>
    protected void RegisterUtility<T>(IUtility utility) where T : class, IUtility
    {
        ArgumentNullException.ThrowIfNull(utility);
        Register(ComponentCategory.Utility, typeof(T), utility);
    }

    /// <summary>
    /// 将一个已经独立创建完成的 Active 根 Domain 挂为直接子域。挂载后子域查找才会回退到父域。
    /// <para>可以在命令或事件处理中调用。</para>
    /// </summary>
    /// <exception cref="InvalidOperationException">任一方不是 Active、挂载自身、子域已有父域，或会形成环路。</exception>
    /// <exception cref="ObjectDisposedException">任一方已释放。</exception>
    public void AddChild(AbstractDomain child)
    {
        ArgumentNullException.ThrowIfNull(child);
        ThrowIfNotActive();
        child.ThrowIfNotActive();

        if (ReferenceEquals(this, child)) throw new InvalidOperationException("Domain 不能挂载自身。");
        if (child._parent is not null) throw new InvalidOperationException("待挂载 Domain 已经有父 Domain；移动必须先 RemoveChild。");
        for (var node = _parent; node is not null; node = node._parent)
        {
            if (ReferenceEquals(node, child)) throw new InvalidOperationException("挂载会形成 Domain 环路。");
        }

        child._parent = this;
        _children.Add(child);
        Log.Info($"Domain {DiagnosticName} 已挂载子 Domain {child.DiagnosticName}。");
    }

    /// <summary>移除一个直接子域但不释放它；移除后的子树仍为 Active 独立树。</summary>
    /// <exception cref="InvalidOperationException">任一方不是 Active（例如子域正在释放），或目标不是直接子域。</exception>
    /// <exception cref="ObjectDisposedException">当前 Domain 已释放。</exception>
    public void RemoveChild(AbstractDomain child)
    {
        ArgumentNullException.ThrowIfNull(child);
        ThrowIfNotActive();

        var index = IndexOfChild(child);
        if (index < 0) throw new InvalidOperationException("只能移除当前 Domain 的直接子域。");

        // 正在释放的子域由释放流程自己从父级摘除；提前摘除会让它剩余的释放回调失去父域回退查找。
        child.ThrowIfNotActive();

        _children.RemoveAt(index);
        child._parent = null;
        Log.Info($"Domain {DiagnosticName} 已移除子 Domain {child.DiagnosticName}。");
    }

    /// <inheritdoc />
    public T GetModel<T>() where T : class, IModel => Resolve<T>(ComponentCategory.Model);

    /// <inheritdoc />
    public bool TryGetModel<T>([NotNullWhen(true)] out T? model) where T : class, IModel =>
        TryResolve(ComponentCategory.Model, out model);

    /// <inheritdoc />
    public T GetSystem<T>() where T : class, ISystem => Resolve<T>(ComponentCategory.System);

    /// <inheritdoc />
    public bool TryGetSystem<T>([NotNullWhen(true)] out T? system) where T : class, ISystem =>
        TryResolve(ComponentCategory.System, out system);

    /// <inheritdoc />
    public T GetUtility<T>() where T : class, IUtility => Resolve<T>(ComponentCategory.Utility);

    /// <inheritdoc />
    public bool TryGetUtility<T>([NotNullWhen(true)] out T? utility) where T : class, IUtility =>
        TryResolve(ComponentCategory.Utility, out utility);

    /// <inheritdoc />
    public IUnRegister RegisterEvent<T>(Action<T> handler)
    {
        ThrowIfDisposed();
        return _events.Register(handler);
    }

    /// <inheritdoc />
    public void SendEvent<T>(T message)
    {
        ThrowIfDisposed();
        _events.Send(message);
    }

    /// <inheritdoc />
    public void SendCommand(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ThrowIfDisposed();
        command.Execute(new CommandContext(this));
    }

    /// <inheritdoc />
    public TResult SendCommand<TResult>(ICommand<TResult> command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ThrowIfDisposed();
        return command.Execute(new CommandContext(this));
    }

    /// <inheritdoc />
    public TResult SendQuery<TResult>(IQuery<TResult> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        ThrowIfDisposed();
        return query.Execute(new QueryContext(this));
    }

    /// <summary>
    /// 按逆挂载后序释放仍挂载的子树，再释放当前 Domain。可以在命令或事件处理中调用；重复调用无效果。
    /// </summary>
    /// <exception cref="InvalidOperationException">Domain 仍在启动中。</exception>
    /// <exception cref="AggregateException">释放过程中出现多个异常。</exception>
    public void Dispose()
    {
        if (_state is DomainState.Disposing or DomainState.Disposed) return;
        if (_state == DomainState.Starting) throw new InvalidOperationException("Domain 启动期间不能释放。");

        var failures = new List<Exception>();
        DisposeCore(failures);
        Failures.ThrowIfAny(failures);
    }

    /// <summary>Domain 进入 Disposed 后的内部回调，供单例清理静态引用。</summary>
    private protected virtual void OnTerminalDisposed() { }

    /// <summary>执行 Configure 和组件初始化；任一步失败都会释放已初始化的组件并抛出。</summary>
    private void Start(AbstractDomain? parent)
    {
        // 放在 try 之外：工厂误返回已在使用的 Domain 时只报错，不能走失败清理把它释放掉。
        if (_isStartInvoked) throw new InvalidOperationException($"Domain {DiagnosticName} 只能启动一次，工厂必须返回新实例。");
        _isStartInvoked = true;

        // 启动期间只建立子到父的查找链接，不加入父域的子域列表：启动失败时父域完全不受影响，
        // 父域在此期间被释放也不会去释放一个仍在启动中的子域。
        _parent = parent;

        try
        {
            _isConfiguring = true;
            try { Configure(); }
            finally { _isConfiguring = false; }

            foreach (var entry in _models) InitializeComponent(entry);
            foreach (var entry in _systems) InitializeComponent(entry);

            // 子域组件初始化期间可能间接释放了父域，此时不能挂到一个已不再 Active 的父域上。
            parent?.ThrowIfNotActive();

            _state = DomainState.Active;
            if (parent is not null)
            {
                parent._children.Add(this);
                Log.Info($"Domain {parent.DiagnosticName} 已挂载子 Domain {DiagnosticName}。");
            }

            OnActivated();

            // OnActivated 里释放自身会让 Create 返回一个不可用的实例，必须视为启动失败。
            if (_state != DomainState.Active)
            {
                throw new InvalidOperationException("OnActivated 期间 Domain 已被释放（可能是父域被释放），不能返回该实例。");
            }

            Log.Info($"Domain {DiagnosticName} 已激活。");
        }
        catch (Exception startupFailure)
        {
            Log.Error($"Domain {DiagnosticName} 启动失败。", startupFailure);

            var failures = new List<Exception>();
            Failures.Add(failures, startupFailure);
            DisposeCore(failures);
            Failures.ThrowIfAny(failures);
        }
    }

    /// <summary>校验并登记一个组件；登记后立即可被查找。</summary>
    private void Register(ComponentCategory category, Type key, object component)
    {
        ThrowIfDisposed();
        if (!_isConfiguring) throw new InvalidOperationException("组件只能在 Configure 中注册。");
        if (!key.IsInstanceOfType(component))
        {
            throw new ArgumentException($"{component.GetType().FullName} 不能赋值给 {category} 契约 {key.FullName}。", nameof(component));
        }

        ValidateCategory(component);

        var entry = _registry.Add(category, key, component, DiagnosticName);
        if (category == ComponentCategory.Model) _models.Add(entry);
        else if (category == ComponentCategory.System) _systems.Add(entry);
    }

    /// <summary>初始化一个 Model 或 System；成功后才记入释放列表，失败的组件由自己负责清理。</summary>
    private void InitializeComponent(DomainComponentEntry entry)
    {
        switch (entry.Instance)
        {
            case IModelLifecycle model:
                model.Initialize(this);
                break;
            case ISystemLifecycle system:
                system.Initialize(this);
                break;
        }

        _initialized.Add(entry);
        Log.Info($"Domain {DiagnosticName} 的 {entry} 注册完成。");
    }

    /// <summary>执行完整释放流程；每一步的异常都收集到 <paramref name="failures"/>，不会中断后续步骤。</summary>
    private void DisposeCore(List<Exception> failures)
    {
        if (_state is DomainState.Disposing or DomainState.Disposed) return;

        // 在释放子域之前就进入 Disposing：子域的释放回调可能间接再次释放本 Domain 或修改它的子域列表，
        // 此时必须表现为重入无操作和拒绝改树，而不是把本 Domain 再完整释放一遍。
        var wasActivated = _state == DomainState.Active;
        _state = DomainState.Disposing;
        try
        {
            // 与进入 Disposing 同时失效全部订阅，使“释放期间不能订阅、发送事件不调用处理器”从第一步起就成立，
            // 包括子域释放回调向本 Domain 发事件或订阅的情况。
            RunCleanupStep(_events.Clear, failures, "清理本地事件失败");

            // 子域释放时会把自己从 _children 中摘除，因此遍历快照。
            var children = _children.ToArray();
            for (var index = children.Length - 1; index >= 0; index--)
            {
                children[index].DisposeCore(failures);
            }

            // 进入 Active 之前失败时 OnActivated 从未执行，OnDeactivating 里清理激活期资源的代码会遇到未初始化的字段。
            if (wasActivated) RunCleanupStep(OnDeactivating, failures, "停用回调失败");

            for (var index = _initialized.Count - 1; index >= 0; index--)
            {
                var entry = _initialized[index];
                RunCleanupStep(() => ReleaseComponent(entry), failures, $"{entry} 释放失败");
            }
        }
        finally
        {
            _initialized.Clear();
            _models.Clear();
            _systems.Clear();
            _registry.Clear();

            // 启动失败的子域只有查找链接，不在父域的子域列表里，因此要先确认存在再移除。
            var indexInParent = _parent?.IndexOfChild(this) ?? -1;
            if (indexInParent >= 0) _parent!._children.RemoveAt(indexInParent);
            _parent = null;
            _state = DomainState.Disposed;
            RunCleanupStep(OnTerminalDisposed, failures, "终态清理回调失败");
            Log.Info($"Domain {DiagnosticName} 已释放。");
        }
    }

    /// <summary>调用组件的 Release。</summary>
    private void ReleaseComponent(DomainComponentEntry entry)
    {
        switch (entry.Instance)
        {
            case ISystemLifecycle system:
                system.Release();
                break;
            case IModelLifecycle model:
                model.Release();
                break;
        }

        Log.Info($"Domain {DiagnosticName} 的 {entry} 释放完成。");
    }

    /// <summary>执行一个释放步骤，记录并收集它的异常。</summary>
    private void RunCleanupStep(Action step, List<Exception> failures, string description)
    {
        try { step(); }
        catch (Exception exception)
        {
            Log.Error($"Domain {DiagnosticName} {description}。", exception);
            Failures.Add(failures, exception);
        }
    }

    /// <summary>沿父链查找，找不到时抛出 <see cref="KeyNotFoundException"/>。</summary>
    private T Resolve<T>(ComponentCategory category) where T : class
    {
        if (TryResolve<T>(category, out var result)) return result;
        throw new KeyNotFoundException($"Domain {DiagnosticName} 及其父链中不存在 {category} {typeof(T).FullName}。");
    }

    /// <summary>沿父链查找：本地有候选（含歧义）时不再查父级，保证本地优先。</summary>
    private bool TryResolve<T>(ComponentCategory category, [NotNullWhen(true)] out T? result) where T : class
    {
        ThrowIfDisposed();

        for (var domain = this; domain is not null; domain = domain._parent)
        {
            // 只有启动中的子域，或子域释放回调里释放了父域，才会走到已释放的父域；
            // 明确报出原因，而不是在已清空的注册表里误报“找不到组件”。
            if (domain._state == DomainState.Disposed)
            {
                throw new ObjectDisposedException(domain.DiagnosticName, "父 Domain 已释放，无法回退查找组件。");
            }

            if (domain._registry.TryResolveLocal(category, typeof(T), domain.DiagnosticName, out var instance)
                && instance is T typed)
            {
                result = typed;
                return true;
            }
        }

        result = null;
        return false;
    }

    /// <summary>已释放时抛出 <see cref="ObjectDisposedException"/>。</summary>
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_state == DomainState.Disposed, this);

    /// <summary>非 Active 时抛出异常；已释放时抛出 <see cref="ObjectDisposedException"/>。</summary>
    private void ThrowIfNotActive()
    {
        ThrowIfDisposed();
        if (_state != DomainState.Active) throw new InvalidOperationException($"Domain {DiagnosticName} 处于 {_state} 阶段，只有 Active 的 Domain 才能参与挂载或移除。");
    }

    /// <summary>按引用查找直接子域；不能使用 List.IndexOf，派生类可能重写了 Equals。</summary>
    private int IndexOfChild(AbstractDomain child)
    {
        for (var index = 0; index < _children.Count; index++)
        {
            if (ReferenceEquals(_children[index], child)) return index;
        }

        return -1;
    }

    /// <summary>用于日志和异常信息的 Domain 标识：类型名加诊断编号。查找时每次都会传入，因此缓存避免重复分配。</summary>
    private string DiagnosticName => _diagnosticName ??= $"{GetType().FullName ?? GetType().Name}#{_diagnosticId}";

    /// <summary>拒绝同时属于多个分类的组件，否则它会被两套生命周期规则同时管理。</summary>
    private static void ValidateCategory(object component)
    {
        var count = (component is IModel ? 1 : 0) + (component is ISystem ? 1 : 0) + (component is IUtility ? 1 : 0);
        if (count != 1)
        {
            throw new InvalidOperationException($"组件 {component.GetType().FullName} 同时属于多个分类。");
        }
    }
}

/// <summary>
/// 严格单例 Domain 基类。具体类型应以私有构造函数和静态 Instance 属性调用 <see cref="GetOrCreateInstance"/>。
/// </summary>
public abstract class AbstractSingletonDomain<TDomain> : AbstractDomain where TDomain : AbstractSingletonDomain<TDomain>
{
    /// <summary>已发布的实例；创建完成前保持 <see langword="null"/>，避免泄露半初始化实例。</summary>
    private static TDomain? _instance;

    /// <summary>是否正在创建，用于拒绝启动过程中重入 Instance。</summary>
    private static bool _isCreating;

    /// <summary>获取或创建单例。</summary>
    /// <exception cref="InvalidOperationException">在创建过程中重入访问。</exception>
    protected static TDomain GetOrCreateInstance(Func<TDomain> factory)
    {
        if (_instance is not null) return _instance;
        if (_isCreating) throw new InvalidOperationException($"{typeof(TDomain).FullName} 单例正在创建，禁止重入 Instance。");

        _isCreating = true;
        try
        {
            _instance = CreateDomain(factory);
            return _instance;
        }
        finally
        {
            _isCreating = false;
        }
    }

    /// <summary>返回已发布单例；未创建或正在创建时返回 <see langword="null"/>。</summary>
    public static TDomain? GetInstance() => _instance;

    /// <summary>释放已发布单例；不存在时无效果。</summary>
    /// <exception cref="InvalidOperationException">单例正在创建。</exception>
    public static void DestroyInstance()
    {
        if (_isCreating) throw new InvalidOperationException($"{typeof(TDomain).FullName} 单例正在创建，不能销毁。");
        _instance?.Dispose();
    }

    /// <inheritdoc />
    private protected override void OnTerminalDisposed()
    {
        if (ReferenceEquals(_instance, this)) _instance = null;
    }
}
