# Domain v2 设计与使用

Domain v2 提供简洁的分类组件访问、可靠的生命周期管理和可动态调整的 Domain 树。它采用同步、单线程协作模型，不提供组件热替换、双重父子关系或任意 `IDomain` 实现。

## 最小结构

每个 Domain 内部只有三部分：分类注册表、Model/System 生命周期和整棵树共享的执行状态。

```text
Root Domain
├─ Game Domain
│  ├─ Battle Domain
│  └─ UI Domain
└─ Network Domain
```

`IDomain` 是消费接口，只暴露三类 Get/TryGet、父引用、同步 Command/Query 和本地事件。注册、树管理和释放只在 `AbstractDomain` 上提供，业务组件拿到的 Context 不是 `IDomain`。

## 完整入门示例

下面的例子包含业务接口、Utility、Model、System 和 Domain，可以直接看出各类型的职责：

```csharp
using SimpleFramework;

public interface ISaveUtility : IUtility
{
    int LoadHp();
}

public sealed class SaveUtility : ISaveUtility
{
    public int LoadHp() => 100;
}

public interface IPlayerModel : IModel
{
    int Hp { get; }
}

public sealed class PlayerModel : AbstractModel, IPlayerModel
{
    public int Hp { get; private set; }

    protected override void OnInitialize()
    {
        Hp = Context.GetUtility<ISaveUtility>().LoadHp();
    }
}

public interface IPlayerSystem : ISystem { }

public sealed class PlayerSystem : AbstractSystem, IPlayerSystem
{
    protected override void OnInitialize()
    {
        var player = Context.GetModel<IPlayerModel>();
        Context.RegisterEvent<PlayerDamaged>(message =>
            Console.WriteLine($"Damage: {message.Amount}, HP before: {player.Hp}"));
    }
}

public readonly record struct PlayerDamaged(int Amount);

public sealed class GameDomain : AbstractDomain
{
    private GameDomain() { }

    public static GameDomain Create() =>
        CreateDomain(() => new GameDomain());

    protected override void Configure()
    {
        RegisterSystem<IPlayerSystem>(new PlayerSystem());
        RegisterModel<IPlayerModel>(new PlayerModel());
        RegisterUtility<ISaveUtility>(new SaveUtility());
    }
}

public static class Example
{
    public static void Run()
    {
        using var game = GameDomain.Create();
        var player = game.GetModel<IPlayerModel>();
        game.SendEvent(new PlayerDamaged(10));
    }
}
```

`Configure` 中的书写顺序不会改变启动分类顺序。上例虽然先注册 System，框架仍会先初始化全部 Model，再初始化全部 System。

## 创建与启动

具体 Domain 使用非公开构造函数和显式工厂，不使用反射或 `new()` 约束：

```csharp
public sealed class GameDomain : AbstractDomain
{
    private readonly int _saveSlot;

    private GameDomain(int saveSlot) => _saveSlot = saveSlot;

    public static GameDomain Create(int saveSlot = 0) =>
        CreateDomain(() => new GameDomain(saveSlot));

    protected override void Configure()
    {
        RegisterSystem(new PlayerSystem()); // 注册顺序不影响分类启动顺序
        RegisterModel<IPlayerModel>(new PlayerModel());
        RegisterUtility<ISaveUtility>(new SaveUtility());
    }

    protected override void OnActivated()
    {
        // 此时已是正常 Active：可以发送消息、动态注册或修改树。
    }
}
```

启动固定为：

```text
构造 Domain
  -> Configure 收集注册
  -> Model 按注册顺序初始化
  -> System 按注册顺序初始化
  -> Domain 进入 Active
  -> OnActivated
```

Utility 在 Model 初始化前即可查找。Model/System 只有在自己的 `Initialize` 成功后才发布。创建失败会清理框架资源并使该 Domain 进入终态；已经开始初始化的生命周期组件不能复用，尚未开始初始化的组件会释放预留关系，仍可用于之后的新 Domain。

需要严格单例时继承 `AbstractSingletonDomain<T>`：

```csharp
public sealed class AppDomain : AbstractSingletonDomain<AppDomain>
{
    private AppDomain() { }
    public static AppDomain Instance => GetOrCreateInstance(() => new AppDomain());
    protected override void Configure() { }
}

var current = AppDomain.Instance;
AppDomain.DestroyInstance(); // 不存在时不会隐式创建
```

单例只在 `OnActivated` 成功后发布。创建期间重入 `Instance` 或 `DestroyInstance` 会明确抛出；创建失败可用新实例重试，直接 `Dispose` 也会清空单例引用。

## 注册与查找

业务接口继承分类标记，生命周期能力放在实现类型上：

```csharp
public interface IPlayerModel : IModel
{
    int Hp { get; }
}

public sealed class PlayerModel : AbstractModel, IPlayerModel
{
    public int Hp { get; private set; } = 100;
    protected override void OnInitialize() { }
}
```

注册保留分类和生命周期语义，读取也保留三类权限边界：

```csharp
RegisterModel(new PlayerModel());                    // 主键 PlayerModel
RegisterModel<IPlayerModel>(new PlayerModel());      // 主键 IPlayerModel
RegisterSystem(new PlayerSystem());
RegisterUtility<IJsonUtility>(new JsonUtility());

var model = domain.GetModel<IPlayerModel>();
if (domain.TryGetUtility<IOptionalUtility>(out var optional)) { }
```

未指定泛型契约时，主键始终取对象的运行时具体类型，与变量的静态类型无关：

```csharp
IModelLifecycle lifecycle = new PlayerModel();
RegisterModel(lifecycle); // 主键仍然是 PlayerModel
```

显式泛型参数表示唯一的注册主键，不会同时创建具体类型键。不过，只要当前分类中该对象是唯一可赋值候选，仍可按具体类型或它实现的其他业务接口查到它。接口变量如果只声明为 `IPlayerModel`，编译器无法证明它具有生命周期能力；注册时应保留具体实现变量，或显式使用 `IModelLifecycle` 变量：

```csharp
var player = new PlayerModel();
IPlayerModel readOnlyView = player;
RegisterModel<IPlayerModel>(player);
```

每次查找按以下顺序执行：

1. 当前 Domain 的精确主键；
2. 当前分类内唯一的可赋值实例；
3. 父 Domain；
4. 多个本地候选直接抛出带候选键和运行时类型的 `InvalidOperationException`。

`Get*` 在缺失时抛出 `KeyNotFoundException`，`TryGet*` 只有在缺失时返回 false；歧义和非法阶段仍然抛出。

System/Model 实例是一锤子生命周期：初始化一旦开始，该对象永久属于一个 Domain、一个分类和一个主键，成功或失败后都不能复用。Active 期只允许注册不存在的新键，不支持替换、移除或初始化期嵌套注册。Utility 完全由调用方管理，可跨 Domain 共享，Domain 释放时只清除引用。

### 注册与查找速查

| 需求 | API | 生命周期所有者或缺失行为 |
| --- | --- | --- |
| 注册 Model | `RegisterModel(...)` | Domain |
| 注册 System | `RegisterSystem(...)` | Domain |
| 注册 Utility | `RegisterUtility(...)` | 调用方 |
| 必须存在 | `GetModel<T>()` / `GetSystem<T>()` / `GetUtility<T>()` | 缺失时抛出 |
| 可以不存在 | `TryGetModel<T>()` / `TryGetSystem<T>()` / `TryGetUtility<T>()` | 仅缺失时返回 `false` |

## 组件 Context

Model 初始化期间只能读取 Utility；初始化完成后还可以发送本地事件。System 初始化期间可以读取 Model/Utility并注册自身事件；初始化完成后增加 System 查找和事件发送。所有 Context 在 Domain 进入 Disposing 前统一失效，`OnDeactivating` 和 `Release` 只能清理组件此前保存的自身资源。

| 能力 | Model 初始化中 | Model Ready | System 初始化中 | System Ready | Command | Query |
| --- | --- | --- | --- | --- | --- | --- |
| 获取 Utility | 是 | 是 | 是 | 是 | 是 | 否 |
| 获取 Model | 否 | 否 | 是 | 是 | 是 | 是 |
| 获取 System | 否 | 否 | 否 | 是 | 是 | 是 |
| 订阅本地事件 | 否 | 否 | 是 | 是 | 否 | 否 |
| 发送本地事件 | 否 | 是 | 否 | 是 | 是 | 否 |
| 发送 Command | 否 | 否 | 否 | 否 | 是 | 否 |
| 发送 Query | 否 | 否 | 否 | 否 | 是 | 是 |

表中的 Ready 能力通过组件保存的 `Context` 使用。`OnDeactivating` 和 `Release` 开始前，所有保存的 Context 会一起失效。

```csharp
public sealed class PlayerSystem : AbstractSystem, IPlayerSystem
{
    private IUnRegister? _manualResource;

    protected override void OnInitialize()
    {
        var player = Context.GetModel<IPlayerModel>();
        Context.RegisterEvent<PlayerDamaged>(OnDamaged); // Domain 自动取消
    }

    protected override void OnRelease()
    {
        _manualResource?.UnRegister();
    }

    private void OnDamaged(PlayerDamaged message) { }
}
```

## 动态 Domain 树

子 Domain 必须先独立创建到 Active，再显式挂载：

```csharp
var battle = BattleDomain.Create(matchId);
game.AddChild(battle);

var shared = battle.GetUtility<ISaveUtility>(); // 挂载后才回退父域

game.RemoveChild(battle); // 不释放，battle 成为独立 Active 根
network.AddChild(battle); // 移动必须显式 remove + add
```

树只有一种强双向关系：父域持有直接子域，子域查找回退父域。Add/Remove 不触发生命周期或附加回调；调用方已经缓存的父组件引用不会自动刷新。

执行 Command、Query 或 Event 时，整棵树共享 `ExecutionDepth`。执行中禁止注册组件、修改树或释放；结构/生命周期转换时也禁止这些操作。检查是 O(1)，只有 Add/Remove 时会为被移动子树更新共享状态。

## Command、Query 与事件

Command/Query 同步接收不可逃逸的 `readonly ref struct` Context：

```csharp
public sealed class DamageCommand : ICommand
{
    public void Execute(CommandContext context)
    {
        var player = context.GetModel<IPlayerModel>();
        context.SendEvent(new PlayerDamaged(10));
    }
}

public sealed class ReadHpQuery : IQuery<int>
{
    public int Execute(QueryContext context) =>
        context.GetModel<IPlayerModel>().Hp;
}

domain.SendCommand(new DamageCommand());
var hp = domain.SendQuery(new ReadHpQuery());
```

Command Context 可读取三类组件并嵌套发送 Event/Command/Query。Query Context 只提供 System/Model 读取和嵌套 Query；这是明确的只读意图，不承诺返回对象深度不可变。Context 不能装箱、保存到普通对象字段、捕获到异步闭包或跨越 `await`。

Domain 事件只在当前 Domain 内同步分发，不沿树传播。每类事件使用写时复制监听器数组：注册和注销会创建新数组，Send 只捕获一个数组引用，不创建快照。当前分发中的修改只影响下一次发送；监听器按注册顺序执行，首次异常立即停止后续监听器。跨 Domain 通信应显式提供共享 Utility/服务。

不带参数的便利调用可以使用扩展方法：

```csharp
domain.SendCommand<RestartBattleCommand>();
domain.SendEvent<BattleStarted>();
```

这些扩展只负责调用无参构造函数，不改变同步执行、异常传播或事件作用域。需要构造参数时，直接创建对象后调用非泛型方法。

## 释放

`Dispose()` 默认释放当前附着子树：子域按逆挂载顺序后序释放，然后当前域执行 `OnDeactivating -> System 逆激活顺序 -> Model 逆激活顺序 -> 清理事件和 Utility 引用 -> 断链 -> Disposed`。

```csharp
root.Dispose();          // 明确释放仍附着的完整子树
root.DisposeSelfOnly();  // 分离并保留直接子树，只释放 root
```

System 在 `Release` 前先取消发布并取消其自动事件订阅；Model 在 `Release` 前先取消发布。所有清理阶段都会尽力执行。一个失败保留原异常和堆栈，多个失败按发生顺序扁平聚合；无论异常如何，目标 Domain 都会进入 Disposed 并保持树结构一致。重复 `Dispose()` 无效果，其他 Disposed 对象操作抛出 `ObjectDisposedException`。

## 常见失败与处理方式

| 情况 | 结果 | 建议 |
| --- | --- | --- |
| `Get*` 找不到对象 | `KeyNotFoundException` | 可选依赖改用 `TryGet*` |
| 本地存在多个可赋值候选 | `InvalidOperationException`，消息列出候选 | 用显式接口键消除歧义 |
| 重复注册相同主键 | `InvalidOperationException` | 不替换；为不同能力使用不同契约或新建 Domain |
| 初始化期间嵌套注册 | `InvalidOperationException` | 在 `Configure` 中声明依赖，或 Active 后由外部注册 |
| 执行消息时修改树、注册或释放 | `InvalidOperationException` | 等同步调用返回后再执行结构变更 |
| 使用已释放 Domain | `ObjectDisposedException` | 调用方负责停止持有和使用失效引用 |
| 组件初始化失败 | 原异常继续抛出 | 候选会被清理，且不能再次注册 |
| 多个释放步骤失败 | `AggregateException` | 按 `InnerExceptions` 顺序检查全部失败 |

框架使用名为 `Framework` 的 Logger 自动记录关键生命周期。每个 Domain 在进程内获得递增的诊断编号，日志以“完整类型名#编号”关联同一实例；Domain 激活和释放、Model/System 注册与释放、子 Domain 挂载、移除和分离记录为 Info，生命周期失败记录为 Error 并附带原始异常。Utility、查找、Command、Query、Event 和业务消息内容不会自动记录，以免污染业务数据或增加消息热路径开销。

日志只作为诊断旁路，不改变生命周期异常、清理顺序或聚合结果。`Logger` 会隔离单个处理器的格式化或输出异常并继续调用后续处理器；`BasicConfig`、处理器创建、移除和释放等显式资源管理失败仍会向调用方传播。应用可以通过 `SimpleFramework.Utility.Logging.BasicConfig` 配置根输出，也可以取得 `Logging.GetLogger("Framework")` 单独配置框架日志。

## 线程边界

框架不加锁、不记录线程所有者，也不自动切回主线程。应用应在一个拥有线程串行使用同一棵 Domain 树；后台任务完成后，由应用调度回拥有线程，再调用 Domain 或 Context API。v2 暂不提供异步 Command/Query。

## 延伸阅读

- [BindableProperty 使用说明](bindable-property.md)
