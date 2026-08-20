# Domain 生命周期与组件注册契约

## 状态与线程模型

每个 Domain 实例只经历一次内部状态流转：`Created -> Initializing -> Active -> Uninitializing -> Disposed`。状态不属于公共 API；调用方只需要遵守各操作的阶段约束。

Domain 是单线程对象，不提供并发同步。创建、组件访问、父子关系、事件、命令、查询和 `UnInitialize()` 应在同一线程执行，通常是游戏或应用主线程。

`Disposed` 是实例的终态，不能重新注册组件或执行事件、命令、查询。`Get*`、`TryGet*`、`Require*`、`Parent`、`ToString()` 和重复 `UnInitialize()` 仍可调用；此时本地注册表和父引用已经清空。单例 Domain 释放后，下一次 `Instance` 会创建新实例，不会复活旧实例。

同类型 Domain 初始化期间访问 `Instance` 会立即抛出 `InvalidOperationException`，不会暴露半初始化对象，也不会递归创建。

## 父子域

`AddChild` 同时表示生命周期所有权和查找继承：父域持有子域强引用，释放时先释放子域。标准 `AbstractDomain` 所有权树在终止前会递归预检；任一子孙正在执行事件/命令/查询、初始化或释放组件，或者其 Domain 清理回调尚未结束时，整个父域释放会在修改状态前被拒绝。任意自定义 `IDomain` 不暴露这些内部阶段，因此仍按现有方式尽力清理并聚合失败。

`SetParent` 只表示查找继承，不建立生命周期所有权。当前域找不到 `System`、`Model` 或 `Utility` 时才继续查询父域。关系修改只能发生在 Domain 初始化本体或活动期，组件初始化和清理回调中禁止修改关系。

父域切换是异常原子的：框架会先从旧的标准父域移除子域所有权，再提交新的父引用；若旧关系不能变更，子域仍指向旧父域，旧父域也仍保有原所有权。对自定义 `IDomain`，框架会先调用其 `RemoveChild`，成功后才提交本地父引用。

## 分类、主键与查找

Domain 内部把注册项分成 `System`、`Model`、`Utility` 三个互不泄漏的分类。每个本地实例只有一个分类、一个主键：

```csharp
public interface IPlayer : IModel
{
}

var player = new Player();

// 主键是 Player；仍可通过唯一可赋值接口解析。
domain.RegisterModel(player);
var byInterface = domain.RequireModel<IPlayer>();

// 需要明确的服务键时，直接选 IPlayer 作为唯一主键。
domain.RegisterModelAs<IPlayer>(new NetworkPlayer());
```

`Register*As<T>` 中的 `T` 是唯一精确主键，不是附加别名。不要把同一实例再注册到具体类型、其他接口或其他分类。

每次查找按以下顺序执行：

1. 返回当前分类中的精确主键。
2. 没有精确键时，扫描运行时类型可赋值给请求类型的本地实例。
3. 没有本地候选项时才查询父 Domain。
4. 只有一个本地候选项时返回它；多个候选项时抛出 `AmbiguousComponentException`。

精确键始终可以消除可赋值歧义。本地歧义不会回退父域。异常提供 `RequestedType` 和只读 `CandidateKeys`，用于诊断冲突注册。

公共 `FrameworkImpl.Container` 仍然只按精确键工作，不采用上述 Domain 解析规则。

## System 与 Model 所有权

System 和 Model 的实例生命周期由一个 Domain 独占。框架注册时会调用公开的 `IDomainBindable.BindDomain`；该调用对生命周期组件是一次性的，之后即使已经清理也不能重新绑定或复用。无论初始化和清理成功还是失败，需要重新注册时都必须创建新实例。`Command` 与 `Query` 仍使用可重复的执行上下文注入，因此同一实例可在不同 Domain 中依次执行。

活动期替换同一主键时，Domain 先取消发布并完整释放旧组件，再发布和初始化新组件：

- 旧组件清理失败：异常直接传播，键保持为空，新实例没有开始初始化，仍可用于之后的注册。
- 新组件初始化失败：回滚新注册，键保持为空，旧实例不会恢复。

查找过程中不会同时看到新旧两个实例。

## Utility 所有权

Utility 的生命周期始终由调用方管理。Domain 只保存和移除注册项，不调用其 `Initialize()` 或 `UnInitialize()`，即使运行时类型也实现了 `ISystem` 或 `IModel`。

同一个 Utility 实例可以共享给多个 Domain，但在同一个 Domain 中仍只能占用一个分类和主键。

## 初始化事务

组件 `BindDomain()` 与 `Initialize()` 处于同一个受保护的组件初始化阶段。两者执行期间允许：

- `Get*`、`TryGet*`、`Require*` 查找；
- `SendQuery`；
- 本地 `RegisterEvent`；
- 向不存在且不在初始化链中的主键注册新 System、Model 或 Utility。

两者执行期间禁止：

- `SendCommand`；
- `SendEvent`；
- 取消已有的本地事件订阅；
- `AddChild`、`RemoveChild`、`SetParent`；
- 替换任何已有主键；
- 重复注册同一实例；
- 直接或间接形成初始化循环；
- 调用 `Domain.UnInitialize()`。

上述“取消已有的本地事件订阅”同时适用于 `Domain.UnRegisterEvent(...)`，以及 Domain 返回的 `IUnRegister.UnRegister()`/`Dispose()` 句柄。被初始化守卫拒绝的句柄不会被消费，可在 Domain 恢复活动期后再次取消；框架自身的事务回滚和组件清理不受该公开守卫阻断。

一次顶层组件注册会形成框架本地事务。外层初始化失败时，事务会撤销嵌套新增的注册项和本地事件订阅；已经开始初始化的 System/Model 会获得一次清理回调，Utility 只移除注册项。初始化和回滚清理同时失败时，以 `AggregateException` 保留全部错误。

事务中的任意嵌套 System/Model 注册一旦失败，整个顶层事务即被标记为失败。即使外层组件捕获该异常，之后也不能继续登记组件或本地事件，顶层注册最终仍会回滚并以原始失败结束。组件若要容错，应在其自身初始化逻辑内处理错误，不应让嵌套注册调用抛出后再继续注册。

事务不覆盖任意 Utility 调用、`EventBus.Global`、I/O 或其他外部副作用。组件作者必须自行补偿这些行为。

## 终止清理

`UnInitialize()` 是终止操作。清理顺序固定为：

1. `AddChild` 持有的子 Domain；
2. System，按成功激活顺序逆序；
3. Model，按成功激活顺序逆序；
4. Utility 注册项、本地事件和父引用；
5. Domain 自身的清理钩子与 `UnInit()`。

每个生命周期组件会先从注册表取消发布，再执行 `UnInitialize()`。因此回调查不到自身，但仍能只读访问尚未轮到清理的依赖项。

整个过程始终保持 `Uninitializing`，包括派生 Domain 的 `UnInit()`，所以清理回调无法重新填充组件或事件。单个清理失败不会中断后续阶段；框架收集全部错误、最终进入 `Disposed`，然后抛出一个 `AggregateException`。清理中的重入和释放后的重复 `UnInitialize()` 都是无操作。

本地事件、Command 或 Query 的同步执行尚未返回时，`UnInitialize()` 会拒绝终止释放。这一守卫覆盖完整的事件派发和可重写 `ExecuteCommand`/`ExecuteQuery` 调用，允许三者相互嵌套，并在用户代码抛出异常时恢复。需要切换场景或关闭 Domain 的回调应在最外层 `SendEvent`、`SendCommand` 或 `SendQuery` 返回后执行或自行调度；异步任务不属于该同步守卫范围。

同一同步执行阶段也不能替换已有的 System 或 Model 生命周期键，因为旧组件可能仍在当前事件快照或调用栈中。执行期间仍可注册不存在的新键，也可替换不受 Domain 生命周期管理的 Utility。

## 配置化 Domain

需要初始参数的 Domain 应继承 `AbstractConfiguredDomain<TConfiguration>`，使用非公开构造函数和受控静态工厂：

```csharp
public sealed class MatchDomain : AbstractConfiguredDomain<MatchOptions>
{
    private MatchDomain(MatchOptions options) : base(options) { }

    public static MatchDomain Create(MatchOptions options)
    {
        var domain = new MatchDomain(options);
        domain.Initialize();
        return domain;
    }

    protected override void Init()
    {
        RegisterModel(new MatchModel(Configuration.Seed));
    }
}
```

`Configuration` 只在 `Init()` 及其同步触发的组件初始化期间可用。初始化成功或失败清理完成后，框架都会释放内部配置引用。

## 事件、命令和查询

Domain 事件只在当前 Domain 生效，不沿父域传播。跨 Domain 通信必须显式使用 `EventBus.Global`。

System 通过 `this.RegisterEvent(...)` 注册的本地事件订阅默认归该 System 所有：替换、初始化失败回滚和终止清理都会自动取消，返回的 `IUnRegister` 仍可用于提前取消。若订阅创建时正处于任意组件初始化事务，它也会临时归该事务所有；事务失败会取消本次新增订阅，即使 System 早已活动且不会随事务释放，事务成功则继续由 System 管理。直接调用 `domain.RegisterEvent(...)`（或经 `IEventRegistrable` 扩展调用）是 Domain 级订阅，由调用方/Domain 生命周期管理，不会随无关组件替换而取消。

Query 仍可访问完整 `IDomain`；“只读”是使用约定，不是由能力接口强制隔离的保证。

Command 代表可能修改状态的操作，只能在活动期执行。Query 代表只读查询，在 Domain/组件初始化和活动期可执行；清理和释放后禁止执行。
