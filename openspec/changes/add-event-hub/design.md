## Context

`simplify-domain-framework` 之后，Domain 事件只在本 Domain 内分发，跨 Domain 通信没有框架级方案。用户已确认：采用“框架提供事件中心 Utility”的方案，命名为 Publish/Subscribe，并同时提供语法糖与订阅随释放自动取消。

## Goals / Non-Goals

**Goals:**

- 跨 Domain 事件的写法与本地事件同样顺手：`PublishEvent(...)` / `SubscribeEvent<T>(...)`。
- 所有权明确：hub 是调用方持有的 Utility，没有静态全局状态，测试之间不会互相污染。
- System 的订阅随 System 释放自动取消，同时解决跨域 `BindableProperty` 订阅泄漏。

**Non-Goals:**

- 不提供沿 Domain 树传播的事件。
- 不提供静态全局总线。
- 不为 Model、Controller 或直接实现生命周期接口的类型提供自动取消。

## Decisions

### D1. `IEventHub` 是普通 Utility

复用现有查找规则：注册在根 Domain 即整棵树共享，子域注册自己的 hub 可以遮蔽父级，同一实例也可注册到互不相连的 Domain。作用范围由注册位置决定，不引入新的作用域概念。

hub 实现 `IDisposable`：谁创建谁释放，与其他 Utility 的所有权规则一致；释放后失效全部订阅，再发布或订阅抛 `ObjectDisposedException`。

### D2. 复用现有事件实现

`EventHub` 内部持有一个 `EventBus`，只在外层加已释放检查。这样分发语义（顺序、写时复制、分发中失效跳过、首个异常即停止）与本地事件天然一致，不重复实现。

备选：独立实现一套字典与事件。否决原因是重复同一份稳定知识。

### D3. 语法糖沿用能力矩阵

`PublishEvent` 跟随 `ICanSendEvent`，`SubscribeEvent` 跟随 `ICanRegisterEvent`，用户不需要记第二套规则。基类提供同名 `protected` 方法；`CommandContext` 提供 `PublishEvent`；`QueryContext` 不提供。

查找 hub 用 `IDomain.TryGetUtility<IEventHub>`，找不到时抛带“需要注册 IEventHub”提示的 `KeyNotFoundException`，比通用的“找不到组件”信息更直接。

### D4. System 订阅的自动取消放在 `AbstractSystem` 内

`AbstractSystem` 维护一个句柄列表。`SubscribeEvent` 与 `UnRegisterOnRelease` 都把句柄登记进去，返回一个包装句柄：通过包装句柄提前取消时同时从列表移除，避免反复订阅、取消时列表无限增长。`UnRegisterOnRelease` 传入的原句柄不知道自己已被登记，直接取消它能停止回调，但登记项保留到 System 释放；文档要求提前取消时使用返回的句柄，不为此增加 API（`SubscribeEvent` 只返回包装句柄，不受影响）。`Release` 时先逆序取消全部句柄，再调用 `OnRelease`，最后解除 Domain 绑定，使 System 在自己的 `OnRelease` 中不再收到 hub 事件。

只放在 `AbstractSystem`：订阅能力只属于 System 和 Controller；Controller 的生命周期由引擎决定，框架无法替它判断何时取消。

### D5. 与本地事件在释放期间的差异

本地事件在 Domain 进入 Disposing 时就失效；hub 订阅要到各 System `Release` 时才取消。因此 `OnDeactivating` 期间 hub 事件仍会送达本 Domain 的 System。此时组件都还可用，是安全的，文档写明这一差异。

### D6. 创建时可选指定父域

现有规则是子域先独立启动、再 `AddChild`，挂载前查找不回退父域。这使子域 System 无法在 `OnInitialize` 中使用根上的 hub 或父域 Model，而那正是订阅最自然的位置。

`CreateDomain(factory, parent)` 的 `parent` 为可选参数，对外接口只多这一处：
- 先校验父域是 Active；
- 启动期间只建立子到父的查找链接，不加入父域子域列表，使启动失败时父域完全不受影响；
- 组件初始化完成后再次确认父域仍是 Active，然后进入 Active、加入父域子域列表，再调用 `OnActivated`；
- 释放时只有在父域列表中找到自己才移除。

父域在子域启动时一定已完整初始化：Domain 只能由工厂在启动完成后返回，且这里显式拒绝 Starting、Disposing、Disposed 的父域。

备选：组件新增“挂载后”钩子。否决原因是多一个生命周期概念，且移除再挂载会重复触发。

## Risks / Trade-offs

- [Controller 订阅 hub 后忘记取消] → 与本地事件扩展方法的规则相同，由调用方取消；Godot 节点可配合 GDExt 现有的 `UnRegisterWhenNodeExit`。
- [直接实现 `ISystemLifecycle` 的类型没有自动取消] → 文档推荐继承 `AbstractSystem`。
- [hub 被持有者提前释放，其他 Domain 仍在使用] → 发布或订阅抛 `ObjectDisposedException`，错误明确。
- [查找回退到已释放的父域] → 抛出说明“父 Domain 已释放”的 `ObjectDisposedException`（`TryGet*` 同样抛出，而不是返回 false）。这也改变了 `simplify-domain-framework` 中记录的已知限制（直接释放子域、其释放回调又释放父域）的表现：原先是 `KeyNotFoundException`，现在是该异常，原因更明确。

## Migration Plan

纯新增功能，无迁移。

## Open Questions

无。
