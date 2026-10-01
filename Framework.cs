using System.Diagnostics.CodeAnalysis;

namespace SimpleFramework;

/// <summary>
/// Domain 的消费接口。
/// <para>注册、树管理和释放由 <see cref="AbstractDomain"/> 提供，业务代码只通过此接口消费组件和执行同步消息。</para>
/// </summary>
public interface IDomain
{
    /// <summary>获取当前 Domain 的父 Domain；根 Domain 返回 <see langword="null"/>。</summary>
    IDomain? Parent { get; }

    /// <summary>获取 Model。按当前域精确键、当前域唯一可赋值对象、父域的顺序解析。</summary>
    /// <typeparam name="T">继承 <see cref="IModel"/> 的业务类型。</typeparam>
    /// <exception cref="KeyNotFoundException">完整父链中不存在匹配对象。</exception>
    /// <exception cref="InvalidOperationException">当前域存在多个可赋值候选对象。</exception>
    /// <exception cref="ObjectDisposedException">Domain 已释放。</exception>
    T GetModel<T>() where T : class, IModel;

    /// <summary>尝试获取 Model；只有不存在匹配对象时返回 <see langword="false"/>，歧义仍会抛出异常。</summary>
    bool TryGetModel<T>([NotNullWhen(true)] out T? model) where T : class, IModel;

    /// <summary>获取 System。解析顺序与异常同 <see cref="GetModel{T}"/>。</summary>
    T GetSystem<T>() where T : class, ISystem;

    /// <summary>尝试获取 System；只有不存在匹配对象时返回 <see langword="false"/>，歧义仍会抛出异常。</summary>
    bool TryGetSystem<T>([NotNullWhen(true)] out T? system) where T : class, ISystem;

    /// <summary>获取 Utility。解析顺序与异常同 <see cref="GetModel{T}"/>。</summary>
    T GetUtility<T>() where T : class, IUtility;

    /// <summary>尝试获取 Utility；只有不存在匹配对象时返回 <see langword="false"/>，歧义仍会抛出异常。</summary>
    bool TryGetUtility<T>([NotNullWhen(true)] out T? utility) where T : class, IUtility;

    /// <summary>同步执行一个无返回值命令。</summary>
    void SendCommand(ICommand command);

    /// <summary>同步执行一个带返回值命令。</summary>
    TResult SendCommand<TResult>(ICommand<TResult> command);

    /// <summary>同步执行一个查询。</summary>
    TResult SendQuery<TResult>(IQuery<TResult> query);

    /// <summary>在当前 Domain 内同步发送事件；事件不会向父子域传播。Domain 正在释放时不会调用任何处理器。</summary>
    /// <exception cref="ObjectDisposedException">Domain 已释放。</exception>
    void SendEvent<T>(T message);

    /// <summary>在当前 Domain 内订阅事件；Domain 释放时订阅自动失效。</summary>
    /// <exception cref="InvalidOperationException">Domain 正在释放。</exception>
    /// <exception cref="ObjectDisposedException">Domain 已释放。</exception>
    IUnRegister RegisterEvent<T>(Action<T> handler);
}

/// <summary>Model 的分类标记。业务接口继承它以参与 Model 查找。</summary>
public interface IModel { }

/// <summary>System 的分类标记。业务接口继承它以参与 System 查找。</summary>
public interface ISystem { }

/// <summary>由调用方管理生命周期的底层能力标记，可在多个 Domain 间共享。</summary>
public interface IUtility { }

/// <summary>能够提供所属 Domain 的对象，是所有能力规则接口的基础。</summary>
public interface IBelongToDomain
{
    /// <summary>获取所属 Domain。</summary>
    /// <exception cref="InvalidOperationException">对象当前未绑定 Domain。</exception>
    IDomain GetDomain();
}

/// <summary>允许获取 Model 的能力规则。</summary>
public interface ICanGetModel : IBelongToDomain { }

/// <summary>允许获取 System 的能力规则。</summary>
public interface ICanGetSystem : IBelongToDomain { }

/// <summary>允许获取 Utility 的能力规则。</summary>
public interface ICanGetUtility : IBelongToDomain { }

/// <summary>允许发送 Command 的能力规则。</summary>
public interface ICanSendCommand : IBelongToDomain { }

/// <summary>允许发送 Query 的能力规则。</summary>
public interface ICanSendQuery : IBelongToDomain { }

/// <summary>允许发送本地事件的能力规则。</summary>
public interface ICanSendEvent : IBelongToDomain { }

/// <summary>允许订阅本地事件的能力规则。</summary>
public interface ICanRegisterEvent : IBelongToDomain { }

/// <summary>
/// 由 Domain 管理一次性生命周期的 Model，只能读取 Utility 和发送事件。
/// <para>业务接口应继承 <see cref="IModel"/> 而不是本接口，避免消费者看到生命周期方法和能力扩展。</para>
/// </summary>
public interface IModelLifecycle : IModel, ICanGetUtility, ICanSendEvent
{
    /// <summary>绑定所属 Domain 并初始化。抛出异常时框架不会调用 <see cref="Release"/>。</summary>
    void Initialize(IDomain domain);

    /// <summary>释放 Model 自身资源。此时 Domain 正在释放，仍可读取组件。</summary>
    void Release();
}

/// <summary>由 Domain 管理一次性生命周期的 System，可读取各类组件并收发本地事件。</summary>
public interface ISystemLifecycle : ISystem, ICanGetModel, ICanGetSystem, ICanGetUtility, ICanSendEvent, ICanRegisterEvent
{
    /// <summary>绑定所属 Domain 并初始化。抛出异常时框架不会调用 <see cref="Release"/>。</summary>
    void Initialize(IDomain domain);

    /// <summary>释放 System 自身资源。此时 Domain 正在释放，仍可读取组件，但事件订阅已经失效。</summary>
    void Release();
}

/// <summary>
/// 表现层入口，例如 UI 或引擎节点。实现者只需提供 <see cref="IBelongToDomain.GetDomain"/>，
/// 即可通过扩展方法获取组件、发送命令与查询、订阅事件；订阅需由实现者自行取消。
/// </summary>
public interface IController : ICanGetModel, ICanGetSystem, ICanGetUtility, ICanSendCommand, ICanSendQuery, ICanRegisterEvent { }

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

/// <summary>一个表达只读意图的同步查询；框架不保证返回对象不可变。</summary>
public interface IQuery<out TResult>
{
    /// <summary>使用只暴露查询能力的不可逃逸栈上下文同步执行。</summary>
    TResult Execute(QueryContext context);
}

/// <summary>
/// 命令执行期间的同步栈上下文。
/// <para>使用 <c>ref struct</c> 是为了让编译器禁止保存到字段、装箱或跨越 <c>await</c>，避免命令在 Domain 释放后继续持有它。</para>
/// </summary>
public readonly ref struct CommandContext
{
    /// <summary>命令所在的 Domain。</summary>
    private readonly IDomain _domain;

    /// <summary>为指定 Domain 创建上下文；测试可直接构造并调用命令的 Execute。</summary>
    public CommandContext(IDomain domain)
    {
        ArgumentNullException.ThrowIfNull(domain);
        _domain = domain;
    }

    /// <summary>获取 System。</summary>
    public T GetSystem<T>() where T : class, ISystem => _domain.GetSystem<T>();

    /// <summary>获取 Model。</summary>
    public T GetModel<T>() where T : class, IModel => _domain.GetModel<T>();

    /// <summary>获取 Utility。</summary>
    public T GetUtility<T>() where T : class, IUtility => _domain.GetUtility<T>();

    /// <summary>发送本地事件。</summary>
    public void SendEvent<T>(T message) => _domain.SendEvent(message);

    /// <summary>构造并发送一个无参事件。</summary>
    public void SendEvent<T>() where T : new() => _domain.SendEvent(new T());

    /// <summary>同步发送命令。</summary>
    public void SendCommand(ICommand command) => _domain.SendCommand(command);

    /// <summary>构造并同步发送一个无参命令。</summary>
    public void SendCommand<T>() where T : ICommand, new() => _domain.SendCommand(new T());

    /// <summary>同步发送带返回值命令。</summary>
    public TResult SendCommand<TResult>(ICommand<TResult> command) => _domain.SendCommand(command);

    /// <summary>同步发送查询。</summary>
    public TResult SendQuery<TResult>(IQuery<TResult> query) => _domain.SendQuery(query);
}

/// <summary>查询执行期间的同步栈上下文，只暴露 Model、System 读取与嵌套查询能力。</summary>
public readonly ref struct QueryContext
{
    /// <summary>查询所在的 Domain。</summary>
    private readonly IDomain _domain;

    /// <summary>为指定 Domain 创建上下文；测试可直接构造并调用查询的 Execute。</summary>
    public QueryContext(IDomain domain)
    {
        ArgumentNullException.ThrowIfNull(domain);
        _domain = domain;
    }

    /// <summary>获取 System。</summary>
    public T GetSystem<T>() where T : class, ISystem => _domain.GetSystem<T>();

    /// <summary>获取 Model。</summary>
    public T GetModel<T>() where T : class, IModel => _domain.GetModel<T>();

    /// <summary>同步发送嵌套查询。</summary>
    public TResult SendQuery<TResult>(IQuery<TResult> query) => _domain.SendQuery(query);
}

/// <summary>支持幂等取消注册的句柄。</summary>
public interface IUnRegister : IDisposable
{
    /// <summary>取消注册；重复调用无效果。</summary>
    void UnRegister();

    /// <summary>等同于 <see cref="UnRegister"/>，便于配合 <c>using</c> 使用。</summary>
    void IDisposable.Dispose() => UnRegister();
}
