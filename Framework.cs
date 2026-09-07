using System.Diagnostics.CodeAnalysis;

namespace SimpleFramework;

/// <summary>
/// Domain 的只读消费接口。
/// <para>注册、树管理和释放由 <see cref="AbstractDomain"/> 提供，业务代码只通过此接口消费组件和执行同步消息。</para>
/// </summary>
public interface IDomain
{
    /// <summary>获取当前 Domain 的父 Domain；根 Domain 返回 <see langword="null"/>。</summary>
    IDomain? Parent { get; }

    /// <summary>获取 Model。按当前域精确键、当前域唯一可赋值对象、父域的顺序解析。</summary>
    /// <typeparam name="T">继承 <see cref="IModel"/> 的业务类型。</typeparam>
    /// <exception cref="KeyNotFoundException">完整父链中不存在匹配对象。</exception>
    /// <exception cref="InvalidOperationException">当前域存在多个可赋值候选对象，或生命周期阶段不允许访问。</exception>
    T GetModel<T>() where T : class, IModel;

    /// <summary>尝试获取 Model；只有不存在匹配对象时返回 <see langword="false"/>。</summary>
    bool TryGetModel<T>([NotNullWhen(true)] out T? model) where T : class, IModel;

    /// <summary>获取 System。按当前域精确键、当前域唯一可赋值对象、父域的顺序解析。</summary>
    T GetSystem<T>() where T : class, ISystem;

    /// <summary>尝试获取 System；只有不存在匹配对象时返回 <see langword="false"/>。</summary>
    bool TryGetSystem<T>([NotNullWhen(true)] out T? system) where T : class, ISystem;

    /// <summary>获取 Utility。按当前域精确键、当前域唯一可赋值对象、父域的顺序解析。</summary>
    T GetUtility<T>() where T : class, IUtility;

    /// <summary>尝试获取 Utility；只有不存在匹配对象时返回 <see langword="false"/>。</summary>
    bool TryGetUtility<T>([NotNullWhen(true)] out T? utility) where T : class, IUtility;

    /// <summary>同步执行一个无返回值命令。</summary>
    void SendCommand(ICommand command);

    /// <summary>同步执行一个带返回值命令。</summary>
    TResult SendCommand<TResult>(ICommand<TResult> command);

    /// <summary>同步执行一个查询。</summary>
    TResult SendQuery<TResult>(IQuery<TResult> query);

    /// <summary>在当前 Domain 内同步发送事件；事件不会向父子域传播。</summary>
    void SendEvent<T>(T message);

    /// <summary>在当前 Domain 内订阅事件。</summary>
    IUnRegister RegisterEvent<T>(Action<T> handler);
}

/// <summary>Model 的业务分类标记。</summary>
public interface IModel { }

/// <summary>System 的业务分类标记。</summary>
public interface ISystem { }

/// <summary>由调用方管理生命周期的底层能力标记。</summary>
public interface IUtility { }

/// <summary>由 Domain 独占并管理一次性生命周期的 Model。</summary>
public interface IModelLifecycle : IModel
{
    /// <summary>初始化 Model。初始化期间只能读取 Utility。</summary>
    void Initialize(IModelContext context);

    /// <summary>释放 Model 自身资源；此时 Context 已失效。</summary>
    void Release();
}

/// <summary>由 Domain 独占并管理一次性生命周期的 System。</summary>
public interface ISystemLifecycle : ISystem
{
    /// <summary>初始化 System。初始化期间可读取 Model、Utility 并订阅自身事件。</summary>
    void Initialize(ISystemContext context);

    /// <summary>释放 System 自身资源；此时 Context 已失效且自身事件订阅已取消。</summary>
    void Release();
}

/// <summary>Model 获得的最小运行上下文。</summary>
public interface IModelContext
{
    /// <summary>获取 Utility。</summary>
    T GetUtility<T>() where T : class, IUtility;

    /// <summary>尝试获取 Utility。</summary>
    bool TryGetUtility<T>([NotNullWhen(true)] out T? utility) where T : class, IUtility;

    /// <summary>在 Model 初始化完成后发送当前 Domain 的本地事件。</summary>
    void SendEvent<T>(T message);
}

/// <summary>System 获得的最小运行上下文。</summary>
public interface ISystemContext
{
    /// <summary>在 System 初始化完成后获取 System。</summary>
    T GetSystem<T>() where T : class, ISystem;

    /// <summary>在 System 初始化完成后尝试获取 System。</summary>
    bool TryGetSystem<T>([NotNullWhen(true)] out T? system) where T : class, ISystem;

    /// <summary>获取 Model。</summary>
    T GetModel<T>() where T : class, IModel;

    /// <summary>尝试获取 Model。</summary>
    bool TryGetModel<T>([NotNullWhen(true)] out T? model) where T : class, IModel;

    /// <summary>获取 Utility。</summary>
    T GetUtility<T>() where T : class, IUtility;

    /// <summary>尝试获取 Utility。</summary>
    bool TryGetUtility<T>([NotNullWhen(true)] out T? utility) where T : class, IUtility;

    /// <summary>订阅当前 Domain 的本地事件；订阅由此 System 自动管理。</summary>
    IUnRegister RegisterEvent<T>(Action<T> handler);

    /// <summary>在 System 初始化完成后发送当前 Domain 的本地事件。</summary>
    void SendEvent<T>(T message);
}

/// <summary>一个同步命令。</summary>
public interface ICommand
{
    /// <summary>使用不可逃逸的栈上下文同步执行。</summary>
    void Execute(CommandContext context);
}

/// <summary>一个带返回值的同步命令。</summary>
public interface ICommand<out TResult>
{
    /// <summary>使用不可逃逸的栈上下文同步执行。</summary>
    TResult Execute(CommandContext context);
}

/// <summary>一个表达只读意图的同步查询。</summary>
public interface IQuery<out TResult>
{
    /// <summary>使用只暴露查询能力的不可逃逸栈上下文同步执行。</summary>
    TResult Execute(QueryContext context);
}

/// <summary>命令执行期间的同步栈上下文，不能装箱、保存到对象字段或跨越 await。</summary>
public readonly ref struct CommandContext
{
    private readonly AbstractDomain _domain;

    internal CommandContext(AbstractDomain domain) => _domain = domain;

    /// <summary>获取 System。</summary>
    public T GetSystem<T>() where T : class, ISystem => _domain.GetSystem<T>();

    /// <summary>尝试获取 System。</summary>
    public bool TryGetSystem<T>([NotNullWhen(true)] out T? system) where T : class, ISystem => _domain.TryGetSystem(out system);

    /// <summary>获取 Model。</summary>
    public T GetModel<T>() where T : class, IModel => _domain.GetModel<T>();

    /// <summary>尝试获取 Model。</summary>
    public bool TryGetModel<T>([NotNullWhen(true)] out T? model) where T : class, IModel => _domain.TryGetModel(out model);

    /// <summary>获取 Utility。</summary>
    public T GetUtility<T>() where T : class, IUtility => _domain.GetUtility<T>();

    /// <summary>尝试获取 Utility。</summary>
    public bool TryGetUtility<T>([NotNullWhen(true)] out T? utility) where T : class, IUtility => _domain.TryGetUtility(out utility);

    /// <summary>发送本地事件。</summary>
    public void SendEvent<T>(T message) => _domain.SendEvent(message);

    /// <summary>同步发送命令。</summary>
    public void SendCommand(ICommand command) => _domain.SendCommand(command);

    /// <summary>同步发送带返回值命令。</summary>
    public TResult SendCommand<TResult>(ICommand<TResult> command) => _domain.SendCommand(command);

    /// <summary>同步发送查询。</summary>
    public TResult SendQuery<TResult>(IQuery<TResult> query) => _domain.SendQuery(query);
}

/// <summary>查询执行期间的同步栈上下文，只暴露读取与嵌套查询能力。</summary>
public readonly ref struct QueryContext
{
    private readonly AbstractDomain _domain;

    internal QueryContext(AbstractDomain domain) => _domain = domain;

    /// <summary>获取 System。</summary>
    public T GetSystem<T>() where T : class, ISystem => _domain.GetSystem<T>();

    /// <summary>尝试获取 System。</summary>
    public bool TryGetSystem<T>([NotNullWhen(true)] out T? system) where T : class, ISystem => _domain.TryGetSystem(out system);

    /// <summary>获取 Model。</summary>
    public T GetModel<T>() where T : class, IModel => _domain.GetModel<T>();

    /// <summary>尝试获取 Model。</summary>
    public bool TryGetModel<T>([NotNullWhen(true)] out T? model) where T : class, IModel => _domain.TryGetModel(out model);

    /// <summary>同步发送嵌套查询。</summary>
    public TResult SendQuery<TResult>(IQuery<TResult> query) => _domain.SendQuery(query);
}

/// <summary>支持幂等取消注册的句柄。</summary>
public interface IUnRegister : IDisposable
{
    /// <summary>取消注册；重复调用无效果。</summary>
    void UnRegister();

    void IDisposable.Dispose() => UnRegister();
}
