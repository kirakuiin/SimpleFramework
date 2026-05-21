# Core B+ 优化设计

## 背景

SimpleFramework Core 当前架构方向保持不变：`IDomain` 作为容器和事件入口，`System` / `Model` / `Utility` 分层，`Command` / `Query` 通过 Domain 注入执行。B+ 优化不重写 Core，而是让默认用法更安全、更直观，并修正容易产生隐式 bug 的边界语义。

设计优先级：

1. 易用性优先：默认写法应该符合直觉。
2. 正确性优先：生命周期和事件订阅不能留下隐藏状态。
3. 易理解：保持轻量，不升级为完整 DI 容器。
4. 尽量兼容：保留主要 API，但允许小范围破坏性调整。

实现风格约束：

- 维持当前 Core 的轻量、直接、可读风格。
- 函数语义必须清晰，避免一个 API 同时承担多种职责。
- 功能边界要明确，一个东西只做一件事。
- 优先用小而直接的方法表达行为，不引入重型抽象。
- 新增 API 要能从命名上看出行为差异，例如 `Get` / `TryGet` / `Require`。

## Domain 生命周期

保留现有单例入口：

```csharp
var domain = GameDomain.Instance;
```

新增显式创建独立实例的入口：

```csharp
var domain = GameDomain.Create();
```

语义：

- `Instance` 适合小项目、全局游戏 Domain、示例代码。第一次访问时自动 `Init()`。
- `Create()` 每次都返回独立 Domain，并自动 `Init()`，适合测试、多会话、编辑器工具和临时模拟环境。
- `GetInstance()` 保留，表示如果单例已经存在则返回，否则返回 `null`，不触发创建。
- `UnInitialize()` 释放当前实例拥有的 children、system、model、utility 和本地事件。
- 只有当前释放对象正好是静态 `Instance` 持有对象时，才清空静态实例。
- `Create()` 创建出的非单例实例释放时，不影响静态 `Instance`。

父子 Domain 语义保持：

- `SetParent()` 只表示查找继承，不表示生命周期拥有。
- `AddChild()` 表示父 Domain 拥有子 Domain 生命周期。
- children 改为强引用列表，表达“父拥有子”的语义。
- parent 可以继续使用弱引用，表达“子不拥有父”。

## Container 与注册查找

Container 保持轻量类型 key 存储器，不升级为完整 DI 容器。

不引入：

- 自动构造依赖
- 程序集扫描注册
- 多实现同 key
- transient / scoped / singleton 生命周期
- 构造函数递归注入
- 命名注册或条件注册

增强点：

- `Register<TService>(TService instance)` 按 `TService` 作为 key 注册。
- `TryGet<TService>(out TService instance)` 明确表达可能不存在。
- `Remove<TService>()` 支持移除指定 key。
- `Clear()` 保留。
- 重复注册时返回旧实例，供 Domain 处理生命周期。

Domain 层提供更清楚的注册 API：

```csharp
domain.RegisterUtility(new FooUtility());
domain.RegisterUtilityAs<IFooUtility>(new FooUtility());
```

语义：

- `RegisterUtility(new FooUtility())` 通常按具体类型 `FooUtility` 注册。
- `RegisterUtility<IFooUtility>(new FooUtility())` 保持可用，按接口 key 注册。
- `RegisterUtilityAs<IFooUtility>(new FooUtility())` 是更直观的接口注册写法。

System 和 Model 同理提供：

```csharp
RegisterSystemAs<TService>(...)
RegisterModelAs<TService>(...)
```

## 获取 API

保留旧 API：

```csharp
domain.GetModel<T>();
domain.GetSystem<T>();
domain.GetUtility<T>();
```

旧 API 找不到时仍返回 `null`，用于兼容。

新增明确语义 API：

```csharp
domain.TryGetModel<T>(out var model);
domain.RequireModel<T>();

domain.TryGetSystem<T>(out var system);
domain.RequireSystem<T>();

domain.TryGetUtility<T>(out var utility);
domain.RequireUtility<T>();
```

推荐用法：

- 可选依赖使用 `TryGetX`。
- 必需依赖使用 `RequireX`。
- 老代码可以继续使用 `GetX`。

`RequireX` 找不到时抛出清晰异常，例如：

```text
Model not found: IPlayerModel in GameDomain.
```

扩展方法也同步增加返回实例的 `RequireModel<T>()`、`RequireSystem<T>()`、`RequireUtility<T>()`。现有返回 `void` 的 `Require<T>()` 可以保留兼容，但推荐新 API。

## System / Model 生命周期

重复注册同 key 的 `System` 或 `Model` 时，旧实例必须释放。

规则：

- 新实例替换容器中的旧实例。
- 如果旧实例和新实例不是同一个对象，则旧实例调用 `UnInitialize()`。
- 新实例调用 `SetDomain(this)` 和 `Initialize()`。
- 重复注册同一个实例时直接返回，避免重复 `Initialize()`。
- `Utility` 当前没有生命周期接口，重复注册只覆盖，不自动初始化或释放。

推荐顺序：

1. 检查是否为同一实例，是则返回。
2. 注册新实例并取回旧实例。
3. 释放旧实例。
4. 设置新实例 Domain。
5. 初始化新实例。

这样可以避免旧实例在释放期间仍被容器查到。

## BindableProperty

移除 `BindableProperty<T>.Comparer` 静态属性。

`WithComparer()` 改为实例级 comparer：

```csharp
var hp = new BindableProperty<int>(100)
    .WithComparer((a, b) => Math.Abs(a - b) < 5);
```

语义：

- 自定义 comparer 只影响当前 `BindableProperty<T>` 实例。
- 不再影响同类型的其他绑定属性。
- 默认 comparer 使用 `EqualityComparer<T>.Default.Equals`。

这是一项小范围破坏性调整，但它修正了当前 API 与用户直觉相反的问题。

## EventBus

事件模型保持隔离：

- `RegisterEvent<T>()` 注册到当前 Domain。
- `SendEvent<T>()` 只触发当前 Domain。
- 事件不沿 parent 或 children 自动传播。
- 跨 Domain 事件继续显式使用 `EventBus.Global`。

不新增 `SendEventUp()`。当前没有强需求，默认冒泡会增加传播顺序、重复处理、停止传播等复杂度。

内部质量增强：

- `EventBus.Clear()` 清空事件容器。
- `EventContainer.Clear()` 清空全部事件。
- `EventContainer.RemoveEvent<T>()` 移除指定事件类型。
- `Event<T>` 使用 listener 列表替代隐藏委托链。
- 取消最后一个 listener 后，`EventBus` 自动移除对应事件类型。
- `AbstractDomain.UnInitialize()` 清理本地 `_eventBus`。
- `EventBus.Global` 不因任意 Domain 释放而清空，只能显式 `Clear()` 或自动移除空事件类型。

事件触发时对 listener 做快照，避免触发过程中取消订阅破坏遍历。

## Nullable 策略

本阶段不启用 Core nullable。

原因：

- 根项目当前明确关闭 nullable。
- 一次开启会带来大量标注和警告处理。
- `TryGetX` 和 `RequireX` 已经能表达主要空值语义。
- nullable 可以作为后续独立阶段处理。

## 测试策略

Domain 生命周期测试：

- `Create()` 返回独立实例。
- 非单例实例 `UnInitialize()` 不清空静态 `Instance`。
- 静态 `Instance.UnInitialize()` 清空静态实例。
- `AddChild()` 强拥有 child，父释放时释放子。
- `SetParent()` 只继承查找，不共享生命周期。

Container / 注册测试：

- 默认按具体类型注册。
- 显式泛型注册可按接口查找。
- `RegisterXAs<TService>()` 可按接口查找。
- 查找 fallback 到 parent。
- 本地注册覆盖 parent 查找。

System / Model 生命周期测试：

- 重复注册同 key 会释放旧实例。
- 重复注册同一个实例不会重复初始化。
- 新实例正常初始化。
- Utility 重复注册不触发生命周期。

BindableProperty 测试：

- 默认 comparer 下相同值不触发。
- 不同值触发。
- `WithComparer()` 只影响当前实例。
- 两个实例使用不同 comparer 时互不影响。
- `null` 到 `null` 不触发。
- `null` 到非 `null` 触发。
- 非 `null` 到 `null` 触发。

EventBus 测试：

- 取消最后一个订阅者后 `Contains<T>() == false`。
- 重复取消不出错。
- 触发过程中取消订阅不破坏迭代。
- `Clear()` 清空事件。
- Domain `UnInitialize()` 清空本地事件。
- 事件不沿父子 Domain 自动传播。

## 文档策略

README 面向使用者：

- 展示推荐的 `Instance` 和 `Create()` 用法。
- 展示 `RegisterUtilityAs<IFoo>()` 和 `RequireUtility<IFoo>()`。
- 说明事件默认只在当前 Domain 内触发。
- 展示 `BindableProperty.WithComparer()` 是实例级配置。

AGENTS.md 面向 coding agents：

- 更新 Core sharp edges。
- 删除 “`BindableProperty<T>.Comparer` 是静态泛型级别”。
- 增加重复注册生命周期规则。
- 增加事件不沿父子传播的规则。

## 非目标

本阶段不做：

- 完整 DI 容器。
- 自动依赖构造。
- 全项目 nullable 开启。
- 事件父子冒泡。
- Utility 生命周期接口。
- 多实现注册。
- 大规模命名空间或文件结构重排。

## 成功标准

- Core 默认用法比当前更符合直觉。
- 重复注册不会泄漏旧 `System/Model` 生命周期。
- 组件查找和事件传播的边界可以用几句话解释清楚。
- `BindableProperty` 比较器不再有跨实例副作用。
- 现有主要 API 保持可用，破坏性变更集中在静态 comparer 移除。
- 新增测试覆盖设计中的边界语义。
