# Domain 生命周期契约

## 线程模型

`Domain` 是单线程对象，不提供并发同步。创建、组件访问、父子关系、事件、命令、查询和 `UnInitialize()` 应在同一线程执行；该线程通常是游戏或应用主线程。后台任务完成后，调用方应先调度回 Domain 所属线程，再调用 Domain API。

## 父子域

`AddChild` 表示生命周期所有权。父域释放时会释放子域，并保持对子域的强引用。

`SetParent` 只表示查找继承。当前域找不到 `System`、`Model`、`Utility` 时，会继续从父域查找。

## 组件生命周期

`System` 和 `Model` 注册后由 `Domain` 初始化和释放。重复注册同一实例不会重复初始化。

组件的 `Initialize()` 一旦开始，即使随后抛出异常，`Domain` 也会先撤销容器注册，再调用该失败组件自身的 `UnInitialize()`。因此组件清理逻辑必须能够处理部分初始化状态，只释放实际取得的资源。

若组件初始化失败且组件清理也失败，调用方会收到同时保留两类错误的 `AggregateException`。更早成功注册的组件由外层 Domain 初始化回滚继续释放。

`Utility` 注册不纳入生命周期管理，即使运行时类型同时实现了 `System` 或 `Model` 接口。

Domain 的 `Init()` 失败时会自动执行完整 `UnInitialize()`。派生 Domain 的 `UnInit()` 因而也必须能够处理 `Init()` 尚未完整成功的状态。初始化异常不会被清理异常替换；两者同时发生时通过 `AggregateException` 保留。

## 配置化 Domain

需要初始参数的 Domain 应继承 `AbstractConfiguredDomain<TConfiguration>`，使用非公开构造函数和受控静态工厂。工厂必须在返回实例前调用受保护的 `Initialize()`：

```csharp
public sealed record MatchOptions(int Seed);

public sealed class MatchDomain : AbstractConfiguredDomain<MatchOptions>
{
    private MatchDomain(MatchOptions options)
        : base(options)
    {
    }

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

`Configuration` 只在 `Init()` 及其同步触发的组件初始化期间可用。初始化成功后，或者初始化失败并完成清理后，框架都会释放内部配置引用；运行期需要的值应由 Model 或 System 明确持有。

配置化 Domain 不提供框架单例或无参创建入口。派生类型负责保持构造函数非公开，避免未初始化实例逃逸。工厂只会返回完整实例；失败时直接抛出初始化异常，清理也失败时抛出同时包含两者的 `AggregateException`。

## 查找语义

`Get*` 未找到时返回 `null`。`TryGet*` 用布尔值表达是否找到。`Require*` 未找到时抛出 `InvalidOperationException`。

## 事件边界

`Domain` 事件只在当前 `Domain` 内生效，不沿父域查找。需要跨 `Domain` 通信时显式使用 `EventBus.Global`。

## 命令和查询

`Command` 代表可能修改状态的操作。`Query` 代表只读查询。`Domain` 在执行前会把自身注入到 `Command` 或 `Query`。
