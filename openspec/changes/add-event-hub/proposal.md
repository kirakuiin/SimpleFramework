## Why

Domain 事件只在本 Domain 内分发，跨 Domain 通信只能由每个项目自己写共享 Utility，或者订阅父域 Model 的 `BindableProperty`。后者的订阅不会随子域释放而失效，必须在 `OnRelease` 里手动取消，是最容易踩的坑。QFramework 用静态全局总线解决跨域事件，但那是隐式全局状态，订阅泄漏只能靠用户自觉。需要一个所有权明确、写法和本地事件同样顺手、订阅能随组件释放自动取消的跨 Domain 事件方案。

## What Changes

- 新增 `IEventHub : IUtility, IDisposable` 及默认实现 `EventHub`：同步、按注册顺序分发、写时复制，语义与 Domain 本地事件一致；由调用方持有，可在多个 Domain 间共享；`Dispose` 后失效全部订阅。
- hub 的作用范围由注册位置决定：注册在根 Domain 时整棵树共享，子域可注册自己的 hub 遮蔽父级，同一实例也可注册到互不相连的多个 Domain。
- 新增语法糖，能力沿用现有矩阵：
  - `PublishEvent` 挂在 `ICanSendEvent` 上（Model、System、Command 可用）；
  - `SubscribeEvent` 挂在 `ICanRegisterEvent` 上（System、Controller 可用）；
  - `AbstractModel` / `AbstractSystem` 提供同名 `protected` 方法，`CommandContext` 提供 `PublishEvent`。
- `AbstractSystem.SubscribeEvent` 的订阅随 System 释放自动取消；扩展方法版本返回 `IUnRegister`，由调用方取消。
- 新增 `UnRegisterOnRelease(this IUnRegister, AbstractSystem)`，让任意订阅（如父域 `BindableProperty`）随 System 释放自动取消。
- 未注册 hub 时，抛出提示需要注册 `IEventHub` 的 `KeyNotFoundException`。
- `CreateDomain` 增加可选参数 `parent`：子域在初始化期间就能回退查找父域组件（hub、父域 Model），启动成功后自动挂载。否则子域 System 无法在 `OnInitialize` 中订阅根上的 hub。不传时行为不变。

## Capabilities

### New Capabilities

- `domain-event-hub`：跨 Domain 共享事件中心、发布/订阅语法糖，以及订阅随 System 释放自动取消。

### Modified Capabilities

- `domain-tree`：`CreateDomain` 可选指定父域，使子域初始化期间即可回退查找父域组件。

Domain 本地事件的需求不变；hub 不是静态全局总线，与“不提供 EventBus.Global”的现有需求一致。

## Impact

- 核心代码：新增 `EventHub.cs`；修改 `FrameworkExtension.cs`、`AbstractModel.cs`、`AbstractSystem.cs`、`Framework.cs`（`CommandContext`）、`AbstractDomain.cs`（`CreateDomain` 的 `parent` 参数）。复用 `FrameworkImpl/Event.cs` 中的事件实现。
- 测试：新增 `Test/Framework/EventHubTests.cs`。
- 文档：`docs/domain-lifecycle.md` 新增跨 Domain 事件一节；更新 `README.md` 核心章节与 `AGENTS.md` 核心行为说明。
- 无破坏性变更，无新增外部依赖。
