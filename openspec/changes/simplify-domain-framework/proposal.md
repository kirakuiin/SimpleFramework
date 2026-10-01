## Why

Domain v2 的正确性已经明显强于 QFramework，但大量复杂度用于防范罕见误用：树级执行守卫、全局生命周期所有权表、Context 三态、注册候选/发布两阶段、`DisposeSelfOnly`。这些防护反而阻碍了常见用法，例如在 Command 或事件处理中挂载、释放 Domain，在 System 初始化时获取其他 System，以及脱离真实 Domain 测试 Command。框架的主要目标是“顺手”，因此需要在保留核心能力的前提下删除防御性复杂度，用法向 QFramework 看齐但更规范。健壮性底线是：用户没犯错时不出问题；用户犯错时给出明确异常。

## What Changes

- **BREAKING** 用 QFramework 风格的规则接口（`IBelongToDomain`、`ICanGetModel`、`ICanGetSystem`、`ICanGetUtility`、`ICanSendCommand`、`ICanSendQuery`、`ICanSendEvent`、`ICanRegisterEvent`）及其扩展方法取代 `IModelContext` / `ISystemContext`。能力矩阵保持与 QFramework 一致，并在编译期约束。
- **BREAKING** `IModelLifecycle` / `ISystemLifecycle` 改为 `Initialize(IDomain)` / `Release()`，并继承各自角色的规则接口。`AbstractModel` / `AbstractSystem` 以 `protected` 方法直接提供 `GetModel` / `GetSystem` / `GetUtility` 等能力。
- 新增纯接口 `IController`，供视图层（如 Godot 节点）通过 `this.GetModel<T>()`、`this.SendCommand(...)` 访问 Domain。
- **BREAKING** `TryGetModel` / `TryGetSystem` / `TryGetUtility` 只保留在 `IDomain` 上；Command/Query 上下文、基类和规则扩展只提供会抛异常的 `Get` 系列。
- `CommandContext` / `QueryContext` 保留 `readonly ref struct`，改为持有 `IDomain` 并公开构造函数，方便测试。
- **BREAKING** 删除执行期守卫：Command、Query、事件处理中可以 `AddChild`、`RemoveChild`、`Dispose`。
- **BREAKING** 删除 `DisposeSelfOnly` 和 Active 阶段动态注册；注册只能在 `Configure` 中进行。
- **BREAKING** 删除注册候选/发布两阶段：注册即可被查找；初始化期间可以取到已注册但尚未初始化的组件引用。
- 删除 System 事件订阅归属追踪和全局 `LifecycleOwnershipTracker`。
- 事件订阅增加失效标记：订阅被取消或 Domain 开始释放后，即使处于本轮快照中也不再被调用。
- 简化单例状态和释放流程；保留初始化失败回滚、释放异常汇总、查找歧义检测、重复注册检测、成环检测。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `domain-component-registry`：注册仅限 `Configure`，注册即可见；删除生命周期所有权、延迟发布、动态注册；`TryGet` 仅保留在 `IDomain`；生命周期接口携带规则接口。
- `domain-lifecycle`：简化状态与阶段规则；释放期间允许查找；删除初始化期间的操作限制。
- `domain-runtime-execution`：规则接口取代组件 Context；新增 `IController`；上下文公开构造；删除执行期树守卫和 System 订阅归属；事件快照跳过已失效订阅。
- `domain-tree`：树结构变更与释放在执行期间也允许；删除 `DisposeSelfOnly` 和 TreeState 转换锁。

## Impact

- 核心代码：`Framework.cs`、`FrameworkExtension.cs`、`AbstractDomain.cs`、`AbstractModel.cs`、`AbstractSystem.cs`、`FrameworkImpl/DomainComponentRegistry.cs`、`FrameworkImpl/Event.cs`；删除 `FrameworkImpl/ComponentContext.cs`、`FrameworkImpl/DomainTreeState.cs`。
- 测试：`Test/Framework/` 下 Domain 相关测试重写；`BindablePropertyTests.cs` 不变。
- 文档：`docs/domain-lifecycle.md`、`README.md` 核心章节、`AGENTS.md` 核心行为说明。
- 其他子项目：Toolkit（`IniConfigTool : IUtility`）与 GDExt（`IUnRegister`）只依赖保留的接口，预计无需改动。
- 无新增外部依赖。不保证源码兼容。
