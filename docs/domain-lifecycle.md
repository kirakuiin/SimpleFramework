# Domain 生命周期契约

## 线程模型

`Domain` 是单线程对象，不提供并发同步。创建、组件访问、父子关系、事件、命令、查询和 `UnInitialize()` 应在同一线程执行；该线程通常是游戏或应用主线程。后台任务完成后，调用方应先调度回 Domain 所属线程，再调用 Domain API。

## 父子域

`AddChild` 表示生命周期所有权。父域释放时会释放子域，并保持对子域的强引用。

`SetParent` 只表示查找继承。当前域找不到 `System`、`Model`、`Utility` 时，会继续从父域查找。

## 组件生命周期

`System` 和 `Model` 注册后由 `Domain` 初始化和释放。重复注册同一实例不会重复初始化。

`Utility` 注册不纳入生命周期管理，即使运行时类型同时实现了 `System` 或 `Model` 接口。

## 查找语义

`Get*` 未找到时返回 `null`。`TryGet*` 用布尔值表达是否找到。`Require*` 未找到时抛出 `InvalidOperationException`。

## 事件边界

`Domain` 事件只在当前 `Domain` 内生效，不沿父域查找。需要跨 `Domain` 通信时显式使用 `EventBus.Global`。

## 命令和查询

`Command` 代表可能修改状态的操作。`Query` 代表只读查询。`Domain` 在执行前会把自身注入到 `Command` 或 `Query`。
