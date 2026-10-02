## Context

Domain v2 核心约 1960 行，其中 `AbstractDomain.cs` 约 800 行。复杂度主要来自：

- 整棵树共享的 `DomainTreeState`（`IsTransitioning`、`ExecutionDepth`），用于在执行和转换期间拒绝注册、改树和释放；
- 全局 `LifecycleOwnershipTracker`（`ConditionalWeakTable`），跨 Domain 追踪组件一次性所有权，同时承担扩展方法查找 Context 的职责；
- 组件 Context 的 `Initializing → Ready → Invalid` 三态和按阶段开放的能力；
- 注册表的候选/发布两阶段、Active 动态注册、`DisposeSelfOnly`；
- System 事件订阅的归属追踪。

与 QFramework 对比后，用户确认框架目标是“顺手”，健壮性可以让步，但底线是：用户没犯错时不出问题，用户犯错时给出明确异常，且整体强于 QFramework。

## Goals / Non-Goals

**Goals:**

- 保留：多实例 Domain、Domain 树、可选单例；精确 → 唯一可赋值 → 父级的查找规则与歧义检测；重复注册抛异常；`Get` 缺失抛异常；QFramework 一致的能力矩阵；Domain 本地事件；`ref struct` Command/Query 上下文。
- 用法统一：Model/System 子类直接写 `GetModel<T>()`，视图层通过 `IController` 写 `this.GetModel<T>()`，Command/Query 写 `context.GetModel<T>()`。
- Command、Query、事件处理中可以自由挂载、移除、释放 Domain。
- 核心代码降到约 900～1000 行。

**Non-Goals:**

- 不提供异步 Command/Query、延迟操作队列、按需初始化。
- 不提供引擎相关的自动取消订阅扩展（GDExt 现有扩展不动）。
- 不修改 `BindableProperty<T>` 和其他子项目。
- 不提供测试替换组件的钩子。

## Decisions

### D1. 规则接口 + 扩展方法取代组件 Context

`IBelongToDomain.GetDomain()` 是唯一的取 Domain 入口，`ICanXxx` 规则接口各自携带扩展方法，转发到 `GetDomain()`。生命周期接口直接继承本角色的规则接口：

- `IModelLifecycle : IModel, ICanGetUtility, ICanSendEvent`
- `ISystemLifecycle : ISystem, ICanGetModel, ICanGetSystem, ICanGetUtility, ICanSendEvent, ICanRegisterEvent`
- `IController : ICanGetModel, ICanGetSystem, ICanGetUtility, ICanSendCommand, ICanSendQuery, ICanRegisterEvent`

理由：能力矩阵变成编译期约束（Model 里写 `GetSystem` 直接编译失败），比运行时 Context 检查更规范；`IController` 天然就是规则接口的组合，Godot 节点无需继承基类即可接入。

备选：保留 Context 对象（现状）或统一 `Get<T>()`。前者需要 Context 三态和全局表，后者只能在运行时检查能力矩阵，均被否决。

`AbstractModel` / `AbstractSystem` 额外提供同名 `protected` 实例方法，子类不必写 `this.`。业务契约（如 `IPlayerModel : IModel`）只继承分类标记，不继承规则接口，避免消费者通过模型引用拿到它的能力。

`GetDomain()` 在接口上是公开的，可以绕过能力矩阵。基类显式实现该接口方法，但规则接口上的扩展方法仍会挂到具体类型上：调用方按具体类型获取组件（如 `GetModel<PlayerModel>()`）时，无需强转就能对它调用 `SendEvent` 等能力。文档建议按业务接口获取组件；不做运行时防护。

### D2. Command/Query 保留 `readonly ref struct` 上下文

命令是一次性对象，上下文作为参数传入，不需要像 QFramework 那样在命令对象上 `SetArchitecture`（可变状态、可被跨 Domain 复用）。上下文改为持有 `IDomain` 并公开构造函数，测试可以直接执行命令。上下文只提供 `Get` 系列，不提供 `TryGet`。

### D3. 生命周期与阶段规则

状态保留 `Starting → Active → Disposing → Disposed`。`Disposing` 必须保留：释放回调中可能再次调用 `Dispose`（需要无操作），也需要据此拒绝 `RegisterEvent` 和改树。

- 注册仅限 `Configure`，用一个 `_isConfiguring` 布尔判断。注册即进入查找表。
- 初始化：先 Model 后 System，按注册顺序；记录已初始化列表，用于失败回滚和逆序释放。
- 初始化期间可取到尚未初始化的组件引用（与 QFramework 一致）。文档约定：`OnInitialize` 里可以缓存引用，但不要依赖其他 System 已初始化。
- 释放：先进入 Disposing 并失效全部事件订阅 → 子树（遍历子节点快照，因为子节点释放时会从父列表中摘除自己）→ `OnDeactivating`（仅进入过 Active 时，与 `OnActivated` 成对）→ System 逆序 Release → Model 逆序 Release → 清空注册表 → 摘除父子关系 → Disposed。查找在 Release 期间仍然可用，便于“退出时保存”。
  - 必须在释放子树之前进入 Disposing：否则子域的释放回调间接再次释放父域时，父域仍是 Active，会被完整释放两次；同一窗口内还能修改父域的子域列表。
  - 事件失效与进入 Disposing 同时发生：否则在子树释放阶段父域已是 Disposing，却仍能订阅、事件仍会分发，与“释放期间”的承诺不一致。代价是子域释放回调向正在释放的父域发送的事件被丢弃。
- 启动只能进行一次：`Start` 在 try 之外用独立布尔标记检查，工厂误返回已在使用的实例，或在 Configure 中再次启动同一实例时，都直接抛异常，不触发失败清理。
- 异常：每一步 try/catch 收集，一个异常原样重抛，多个按发生顺序抛 `AggregateException`。

### D4. 删除执行期守卫

不再记录 `ExecutionDepth` 和 `IsTransitioning`。允许在执行中改树和释放的前提是：
- 已释放 Domain 的所有访问抛 `ObjectDisposedException`，错误立即暴露；
- 事件订阅带失效标记（D5），避免正确用法在分发途中命中已释放组件。

`DomainTreeState.cs` 整体删除。

### D5. 事件订阅失效标记

`Event<T>` 的每个订阅对象持有可置空的回调。取消注册时置空回调并从数组移除；Domain 开始释放时，事件总线把所有订阅的回调置空。`Trigger` 遍历快照时跳过回调为空的订阅。置空回调同时释放了对回调目标的引用。一个机制同时覆盖“分发途中取消订阅”和“分发途中 Domain 被释放”。

理由：放开随时释放后，“在事件处理里释放战斗 Domain”是正确用法；没有该标记，同一 Domain 里排在后面的 System 会在已释放状态下被回调。

### D6. 删除全局所有权表与订阅归属

- 同一 Domain 内重复注册由注册表按实例去重检测。
- 跨 Domain 复用由 `AbstractModel` / `AbstractSystem` 的“已绑定则拒绝初始化”检测；直接实现者自行负责。
- System 与 Domain 同生共死（无动态注册/移除），Domain 释放时统一失效事件订阅，不再需要按 System 追踪令牌。

### D7. 单例简化

`SingletonState` 三态改为 `_instance` + `_isCreating` 布尔。行为不变：创建中重入抛异常，失败不发布，释放后可重建。

### D8. 日志保留

保留激活、组件注册与释放完成、挂载/移除子域、释放完成、启动失败、释放失败日志，供现有日志诊断使用。

## Risks / Trade-offs

- [初始化期间拿到未初始化组件] → 文档写明约定；与 QFramework 行为一致；需要时调整注册顺序。
- [`GetDomain()` 可被强转后绕过能力矩阵] → 基类显式实现接口隐藏该方法；不做运行时防护。
- [直接实现生命周期接口的类型可被注册到多个 Domain] → 属于用户错误；推荐使用基类。
- [命令释放自身 Domain 后继续使用上下文] → 抛 `ObjectDisposedException`，错误信息明确。
- [非本地订阅泄漏：子域 System 订阅父域 `BindableProperty` 等非本地来源时不会自动失效] → 文档和 `AbstractSystem.RegisterEvent` 注释写明必须在 `OnRelease` 中取消。
- [直接释放子域时，其释放回调又释放父域：父域会先于该子域完成释放，子域之后在 `OnRelease` 中回退查找父域组件会得到 `KeyNotFoundException`] → 记为已知限制。异常明确，且规格只约束“父域正在释放该子域”的情形，不为此增加复杂度。
- [System 在 `OnRelease` 中调用后注册的 System：对方已释放，基类会抛 `InvalidOperationException`] → 与初始化阶段对称，文档与 `OnRelease` 注释写明不要这样做，协调性的退出逻辑放在 `OnDeactivating`。不在 Release 后保留 Domain 绑定，因为对方可能已清理自身状态，让调用“成功”只会掩盖问题。
- [源码不兼容] → 仓库内其他子项目仅依赖 `IUtility`、`IUnRegister`，预计无需改动；测试与文档同步重写。

## Migration Plan

1. 先改接口与上下文，再精简注册表，再重写 `AbstractDomain`，最后改基类。
2. 测试按新规格重写；删除仅验证已移除守卫的用例。
3. 构建和全部测试通过后更新文档。回滚方式为还原本 change 的提交。

## Open Questions

无。
