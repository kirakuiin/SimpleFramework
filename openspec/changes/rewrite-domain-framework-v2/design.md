## Context

SimpleFramework 当前 Domain 核心同时承担组件容器、父子关系、初始化事务、替换回滚、事件资源、任意 `IDomain` 防御和单例创建，多个状态与递归树检查互相耦合。QFramework 的三类注册、先收集再初始化和简短调用方式更易理解，但其精确类型容器、可变 Architecture 注入、覆盖注册和 `new()` 单例不足以满足可靠生命周期要求。

本次变更没有外部兼容负担，采用原子化重写。设计优先级依次为：简单公开 API、确定生命周期和失败结果、可测试性、游戏主线程热路径，再考虑未来扩展。所有 Domain 操作遵守单线程协作约定，不建立线程安全或安全沙箱。

## Goals / Non-Goals

**Goals:**

- 提供接近 QFramework 的三类注册、获取、CQ 和事件使用体验。
- 保留可运行期调整的 Domain 树，以及挂载后父域组件回退。
- 以最小状态模型保证创建、动态注册、执行、树修改和释放互斥。
- 让业务接口看不到生命周期方法，同时支持直接实现和可选抽象基类。
- 为所有失败路径定义可观察结果，并使测试能够从零覆盖完整契约。
- 让高频精确查找、事件发送和同步 CQ 调度保持低成本。

**Non-Goals:**

- 不兼容旧 API、旧测试、任意第三方 `IDomain` 实现或旧组件替换行为。
- 不支持组件移除、替换、批量注册事务或热交换。
- 不支持异步 Command/Query、线程安全、线程捕获检查或自动调度回主线程。
- 不提供事件树传播、全局 EventBus、自动刷新已缓存父域依赖或父域参与子域初始化。
- 不把 Context、窄 `IDomain` 或访问修饰符当作恶意代码安全边界。
- 不在本次变更中顺手重构 ECS、Net、Collections、Maths 等无关模块。

## Decisions

### 1. 消费面与管理面分离

`IDomain` 只包含只读 `Parent`、三类 Get/TryGet、Command、Query 和本地事件。`AbstractDomain : IDomain, IDisposable` 是唯一受支持扩展点，并公开三类 Register、AddChild、RemoveChild、Dispose 和 DisposeSelfOnly。

注册不放进 `IDomain`，使通常只持有 `IDomain` 的消费者和组件 Context 看不到管理能力；注册仍公开在 `AbstractDomain`，使明确持有管理对象的外部协调器能够在 Active 时安装组件。该边界用于 API 引导，不阻止调用者强制转换具体对象。

纯语法糖使用扩展方法，例如 `SendCommand<T>()` 和 `SendEvent<T>()`。不会用新的能力 Trait 森林转发 Domain。

### 2. 分类接口与生命周期接口分离

`IModel`、`ISystem`、`IUtility` 只表达分类。`IModelLifecycle : IModel` 与 `ISystemLifecycle : ISystem` 才包含 Initialize 和 Release。业务接口继续写成 `IPlayerModel : IModel`，因此正常 Get 结果不暴露生命周期。

具体组件可直接实现业务接口与 Lifecycle，也可继承 `AbstractModel` 或 `AbstractSystem`。直接实现属于合作式高级用法；手动调用生命周期或泄露 Context 不受框架保护。

运行时类型若同时属于多个分类，注册在任何分类时都拒绝。Utility 可以跨 Domain 共享，但不得同时作为生命周期组件使用；Domain 永不因为 Utility 实现 `IDisposable` 或其他生命周期接口而释放它。

### 3. 注册键、解析与发布

无泛型 Register 接受生命周期实例或 Utility，并始终使用运行时具体类型作为精确键。显式泛型 Register 使用泛型契约键，并在任何状态修改前验证实例实现该契约。这样变量静态类型不会悄然改变默认键，也无需恢复 `Register*As`。

每个类别独立解析：当前域精确键、当前域唯一可赋值实例、父域递归。当前域唯一可赋值实例优先于父域精确键；本地歧义在访问父域前抛出。TryGet 只把“完整查找链缺失”转换为 false，歧义和阶段错误仍抛出。

生命周期组件初始化成功后才发布。释放前先取消发布。同一精确键、同一 Domain 的重复实例、跨类别实例和已被消费的生命周期实例均拒绝。Initialize 一旦开始，该实例永久消费；仅在 Configure 失败且 Initialize 尚未开始时解除预留。

Starting 中 Register 只收集；Active 中 Register 对全新键执行单次原子初始化，失败只清理当前候选。多次调用不构成事务；多组件动态功能应建立独立 Domain，Create 成功后再 AddChild。

这里的原子性只覆盖候选的注册、生命周期和自身拥有的订阅。已有组件的注册关系和生命周期不受候选失败影响；候选通过其他对象业务方法产生的状态修改或订阅，仍由对应对象或调用方负责恢复，不会按初始化调用栈自动纳入候选清理。

### 4. 显式工厂与严格单例

普通 Domain 直接继承非泛型 `AbstractDomain`，使用私有构造和具体类的一行静态 Create。受保护 `CreateDomain(Func<TDomain>)` 先调用工厂取得新候选，再统一执行启动和失败清理；候选以 Starting 状态和已开启的树转换标记开始启动。它支持带参构造，不使用 `new()` 约束或反射，也不允许外部直接获得半成品。严格单例的 Creating 重入保护由 GetOrCreateInstance 在调用工厂前建立。

`AbstractSingletonDomain<T>` 不提供多例 Create。具体类通过 `GetOrCreateInstance(factory)` 实现 Instance，并继承 GetInstance/DestroyInstance。每个单例类型维护 `Empty -> Creating -> Published` 静态状态：Creating 中再次 Instance 或 DestroyInstance 抛出；GetInstance 返回 null；只有 OnActivated 成功后发布；失败回到 Empty；Published 实例直接 Dispose 后清除引用并允许未来创建新实例。

### 5. 最小 Domain、Context 与树状态

Domain 只有 `Starting / Active / Disposing / Disposed` 四个内部状态。Starting 内部按 Configure、Model、System 顺序编排，不把每一步扩展成公共或长期状态。Model/System Context 只有 `Initializing / Ready / Invalid`，用于区分 Active 动态注册期间的新组件初始化能力。

每棵连接树共享 `DomainTreeState { int ExecutionDepth; bool IsTransitioning; }`。ExecutionDepth 支持同步嵌套 CQ/Event；IsTransitioning 统一保护创建、动态注册、挂载、脱离和释放。具体操作是否合法仍由 Domain 状态、Context 状态和私有编排入口判断，TreeState 不承担完整授权状态机。

初始化完成后，框架先将 Domain 设为 Active 并清除创建转换锁，再调用 OnActivated。OnActivated 因而拥有普通 Active 能力，包括动态注册和树修改。钩子抛出时重新进入转换并完整 Dispose；钩子主动使 Domain 不再 Active 时，Create 检测并抛出，绝不返回或发布失效对象。事件、I/O、外部业务状态等副作用不回滚。

进入 Disposing 后，OnDeactivating 和组件 Release 不再获得任何 Context 能力，只能清理此前持有的自身资源。该限制删除释放期依赖查找和权限矩阵。

### 6. 独立创建和动态 Domain 树

子 Domain 必须先独立 Create 到 Active，再 AddChild。AddChild 不创建、不重新初始化、不调用 OnAttached；父域回退只从 AddChild 成功后生效。子域初始化必须只依赖本地组件，这是明确限制，不建立临时父关系或父参与的创建事务。

AddChild 要求父子 Active、两树空闲、子为根、无重复和环路；成功后建立强双向关系，并将整棵子树切换到父 TreeState。RemoveChild 只接受直接子节点，完成断链和子树新 TreeState 替换，不释放或回调。移动必须显式 RemoveChild 后 AddChild；它只影响未来查找，不刷新组件已经缓存的父依赖。

不公开 Children。AddChild 返回 void，调用者保存独立 Create 得到的子域引用。Dispose 默认释放当前附着子树；DisposeSelfOnly 先脱离直接子树并在整个父释放调用结束前保持临时转换锁，随后只释放当前 Domain。临时锁约束组件注册、树结构、释放和 Domain 消息执行，不会使保留子域的 Context 失效或冻结其全部业务能力。异常路径在 finally 中维护父子双向一致。

### 7. Context 与同步 CQ

IModelContext 在 Initializing 只允许 Utility 查找，Ready 后增加本地 SendEvent。ISystemContext 在 Initializing 只允许 Model/Utility 查找和自动归属的本地事件注册，Ready 后增加 System 查找和 SendEvent。所属 Domain 进入 Disposing 后，其两类 Context 均 Invalid；其他尚未释放 Domain 的 Context 不随之失效。当前 Context 查找与 System 订阅只校验自身阶段，事件发送另外经过 Domain 的转换守卫，因此已有 System 在树转换期间仍可能新增自身订阅。

同步 Command/Query 不使用堆分配接口 Context，而使用构造函数 internal 的 `readonly ref struct CommandContext` 和 `QueryContext`。ICommand、ICommand<TResult>、IQuery<TResult> 的 Execute 直接接收相应具体 Context。ref struct 从类型系统阻止装箱、普通字段保存、异步捕获和跨 await 使用，因此不需要执行后失效对象或池化。

CommandContext 提供三类 Get/TryGet 以及 Send Event/Command/Query；QueryContext 只提供 System/Model Get/TryGet 和 SendQuery。Query 表达读取意图，但不保证取得对象的深层不可变。Domain 不缓存或释放 CQ 对象，同一实例只承诺顺序复用。

### 8. 本地事件采用 copy-on-write

Domain 事件不沿树传播，也没有 EventBus.Global。应用如需共享总线，应显式创建并自行持有 Utility/服务。

每个事件类型保存当前不可变监听器数组。注册和注销创建新数组；发送只捕获数组引用并顺序遍历，不产生快照分配。因而发送期间的注册或注销不改变当前轮，但影响下一轮。首个监听器异常 fail-fast，finally 恢复 ExecutionDepth。

System 通过 Context 注册的 token 自动归属该 System；系统释放时在 Release 前取消。外部通过 IDomain 注册的 token 由调用者持有。所有 token 幂等；System 或 Domain 已清理后再次注销无操作且不抛出。

订阅不会因为由另一个候选的 Initialize 间接触发，或回调捕获了该候选，就转移到候选名下。候选动态注册失败时，仅自动取消候选自身拥有的订阅；若业务需要撤销通过已有 System 创建的订阅，应取得 token 并在候选 Release 中取消。

### 9. 确定且穷尽的释放

子域采用逆 AddChild 顺序的后序释放。每个 Domain 在所有子域完成后执行：进入 Disposing并停止 Context 能力、OnDeactivating、System 逆成功激活顺序、Model 逆成功激活顺序、清空本地事件、清除 Utility 引用、断开关系、进入 Disposed。

每个 System 先取消发布、取消自动事件，再 Release；每个 Model 先取消发布再 Release。任何步骤失败都继续后续清理。一个失败保留原异常；多个失败按发生顺序扁平化到 AggregateException。创建失败且清理也失败时，原创建异常排第一。所有终止状态和树断链在 finally 中完成。

### 10. 热路径保持简单并以基准驱动缓存

精确键使用 Dictionary O(1)。只有缺少精确键时才扫描当前类别寻找唯一可赋值实例，再按树深递归父域。v2 不预先缓存 assignable 结果，因为父域动态注册和子树移动会引入跨树失效复杂度；稳定高频依赖应显式注册业务契约键。实施后用基准测试判断是否需要版本化缓存。

事件发送使用 copy-on-write 数组避免分配；CQ 使用 ref struct Context 避免分配。树忙碌检查 O(1)，AddChild/RemoveChild 只在结构变化时对子树执行 O(n) TreeState 替换。

### 11. 测试与代码迁移采用原子替换

不建立 Obsolete 或兼容转发。先删除旧 Framework 测试、Fixture、辅助 Domain 和旧契约断言，再按新能力建立创建、注册、生命周期、Context、树、CQ/Event、单例、异常清理和热路径测试。仓库内调用方在同一变更中适配；无关模块仅做必要编译修复。

## Risks / Trade-offs

- [独立子域初始化不能使用父依赖] → 作为明确契约写入 XML 与测试；需要父依赖的工作延迟到 AddChild 后的显式业务调用。
- [Active 新增组件可让宽类型查找从唯一变为歧义] → 公开记录该行为；稳定依赖显式注册业务契约键。
- [OnActivated 已是普通 Active，失败无法回滚副作用] → 只承诺资源清理；建议把就绪事件放在最后，并对非 Active 返回做强校验。
- [公开 AbstractDomain Register 可被强制转换后调用] → 定位为合作式能力引导，不增加安全接口层。
- [Assignable 查找扫描在极大 Registry 中可能变慢] → 保持初版简单并添加基准；只有证据充分时增加版本化缓存。
- [copy-on-write 增加注册和注销分配] → 事件发送远高于订阅变化，优先优化发送路径并用测试验证快照语义。
- [ref struct CQ Context 限制异步和自定义 Context] → 这与 v2 同步、框架拥有 Context 的目标一致；未来异步采用独立接口和堆 Context。
- [严格单例静态状态增加测试隔离要求] → DestroyInstance 幂等且测试 TearDown 必须清理；创建重入和失败重试单独覆盖。

## Migration Plan

1. 创建新核心接口、最小状态与 Registry/EventBus 内部结构。
2. 原子替换 Domain、生命周期组件、CQ 和树实现，不保留旧转发。
3. 删除旧 Framework 测试与辅助类型，按新规格从零建立测试。
4. 适配 Toolkit、GDExt 和仓库内其他核心调用方。
5. 运行完整 restore、build、test 和针对 Registry/Event/CQ 的基准或分配检查。
6. 若实施无法通过新契约测试，回滚整个变更而不是恢复局部兼容层。

## Open Questions

无。异步 CQ、解析缓存、公开树观察和第三方插件能力均明确延后，只有未来出现实际需求或性能证据时再建立独立变更。
