# Domain 设计与使用

Domain 提供分类组件访问、生命周期管理和可动态调整的 Domain 树，用法与 QFramework 接近：Model、System、Utility 三类组件，同步的 Command/Query，Domain 内的本地事件。它采用同步、单线程协作模型。

与 QFramework 相比，主要区别是：可以创建多个 Domain 并组成树；查不到组件时抛异常而不是返回 `null`；重复注册会报错而不是静默覆盖；按接口或具体类型都能查到组件；初始化失败会回滚，释放失败会汇总；Command/Query 的上下文不能被保存。

## 最小结构

```text
Root Domain
├─ Game Domain
│  ├─ Battle Domain
│  └─ UI Domain
└─ Network Domain
```

- `IDomain` 是消费接口：父引用、三类 `Get`/`TryGet`、同步 Command/Query、本地事件。
- 注册只能在 `AbstractDomain.Configure` 中进行；树管理和释放在 `AbstractDomain` 上提供。
- 每个角色能做什么由能力规则接口决定，编译期检查。

## 完整入门示例

```csharp
using SimpleFramework;

public interface ISaveUtility : IUtility
{
    int LoadHp();
    void SaveHp(int hp);
}

public sealed class SaveUtility : ISaveUtility
{
    public int LoadHp() => 100;
    public void SaveHp(int hp) { }
}

public interface IPlayerModel : IModel
{
    int Hp { get; set; }
}

public sealed class PlayerModel : AbstractModel, IPlayerModel
{
    public int Hp { get; set; }

    protected override void OnInitialize() => Hp = GetUtility<ISaveUtility>().LoadHp();
}

public interface IPlayerSystem : ISystem { }

public sealed class PlayerSystem : AbstractSystem, IPlayerSystem
{
    private IPlayerModel _player = null!;

    protected override void OnInitialize()
    {
        _player = GetModel<IPlayerModel>();
        RegisterEvent<PlayerDamaged>(message => _player.Hp -= message.Amount); // Domain 释放时自动失效
    }

    protected override void OnRelease() => GetUtility<ISaveUtility>().SaveHp(_player.Hp); // 释放时仍可读取组件
}

public readonly record struct PlayerDamaged(int Amount);

public sealed class DamageCommand(int amount) : ICommand
{
    public void Execute(CommandContext context) => context.SendEvent(new PlayerDamaged(amount));
}

public sealed class GameDomain : AbstractDomain
{
    private GameDomain() { }

    public static GameDomain Create() => CreateDomain(() => new GameDomain());

    protected override void Configure()
    {
        RegisterSystem<IPlayerSystem>(new PlayerSystem());
        RegisterModel<IPlayerModel>(new PlayerModel());
        RegisterUtility<ISaveUtility>(new SaveUtility());
    }
}

using var game = GameDomain.Create();
game.SendCommand(new DamageCommand(10));
var hp = game.GetModel<IPlayerModel>().Hp; // 90
```

`Configure` 中的书写顺序不影响分类顺序：上例先注册 System，框架仍先初始化全部 Model，再初始化全部 System。

## 各角色的能力

| 能力 | Model | System | Controller | Command | Query |
| --- | --- | --- | --- | --- | --- |
| 获取 Model | 否 | 是 | 是 | 是 | 是 |
| 获取 System | 否 | 是 | 是 | 是 | 是 |
| 获取 Utility | 是 | 是 | 是 | 是 | 否 |
| 订阅事件 | 否 | 是 | 是 | 否 | 否 |
| 发送事件 | 是 | 是 | 否 | 是 | 否 |
| 发送 Command | 否 | 否 | 是 | 是 | 否 |
| 发送 Query | 否 | 否 | 是 | 是 | 是 |

写法：

```csharp
// AbstractModel / AbstractSystem 子类：受保护方法，直接调用
var player = GetModel<IPlayerModel>();

// Controller（纯接口，例如 Godot 节点）：扩展方法，需要写 this.
this.GetModel<IPlayerModel>();

// Command / Query：栈上下文
context.GetModel<IPlayerModel>();
```

`TryGetModel` / `TryGetSystem` / `TryGetUtility` 只在 `IDomain` 上提供，用于确实可能不存在的组件。

**建议按业务接口获取组件**，例如 `GetModel<IPlayerModel>()`。如果按具体类型获取，比如 `GetModel<PlayerModel>()`，拿到的对象继承了 `AbstractModel`，调用方就能对它使用 `SendEvent` 等扩展方法，绕过上表的能力限制。

### Controller

`IController` 是纯接口，实现者只需提供所属 Domain，适合 UI 或引擎节点：

```csharp
public partial class HpLabel : Label, IController
{
    private IUnRegister? _token;

    public IDomain GetDomain() => GameApp.Instance;

    public override void _Ready() =>
        _token = this.RegisterEvent<PlayerDamaged>(_ => Text = this.GetModel<IPlayerModel>().Hp.ToString());

    public override void _ExitTree() => _token?.UnRegister(); // Controller 的订阅由自己取消
}
```

### 直接实现生命周期接口

不继承基类时，实现 `IModelLifecycle` 或 `ISystemLifecycle`：保存 `Initialize(IDomain)` 传入的 Domain，并从 `GetDomain()` 返回它，即可使用 `this.GetModel<T>()` 等扩展方法。基类会拒绝同一实例在仍被使用时重复初始化，直接实现者需要自己保证不重复注册到多个 Domain。

## 创建与启动

具体 Domain 使用非公开构造函数和显式工厂：

```csharp
public sealed class BattleDomain : AbstractDomain
{
    private readonly int _matchId;

    private BattleDomain(int matchId) => _matchId = matchId;

    public static BattleDomain Create(int matchId) => CreateDomain(() => new BattleDomain(matchId));

    protected override void Configure() { /* RegisterXxx */ }

    protected override void OnActivated() { /* 已是 Active，可以发消息、改树 */ }
}
```

启动流程：

```text
构造 Domain
  -> Configure 注册（注册即可被查找）
  -> Model 按注册顺序初始化
  -> System 按注册顺序初始化
  -> 进入 Active
  -> OnActivated
```

- 注册只能在 `Configure` 中进行，其他时候调用抛 `InvalidOperationException`。
- 初始化期间可以取到任何已注册组件的引用，但对方可能尚未初始化。**`OnInitialize` 里可以缓存引用，不要依赖其他 System 已经初始化完成。**
- 任一步失败：已初始化成功的组件按逆序 `Release`，Domain 进入 Disposed，原异常抛给调用方。初始化失败的组件本身不会被 `Release`，需要在 `OnInitialize` 内自行清理已占用的资源。
- 如果失败发生在进入 Active 之前（`Configure` 或组件初始化失败），不会调用 `OnDeactivating`；它只和 `OnActivated` 成对出现。`OnActivated` 自己抛异常时已处于 Active，仍会调用 `OnDeactivating`。
- 每个 Domain 只能启动一次，工厂必须返回新实例；返回已在使用的实例会直接抛异常，不会影响那个实例。

需要严格单例时继承 `AbstractSingletonDomain<T>`：

```csharp
public sealed class GameApp : AbstractSingletonDomain<GameApp>
{
    private GameApp() { }
    public static GameApp Instance => GetOrCreateInstance(() => new GameApp());
    protected override void Configure() { }
}

GameApp.DestroyInstance(); // 不存在时不会隐式创建
```

单例在 `OnActivated` 成功后才发布。创建期间重入 `Instance` 或 `DestroyInstance` 会抛异常；创建失败可以重试；直接 `Dispose` 单例也会清空引用。

## 注册与查找

业务接口只继承分类标记，生命周期放在实现类型上，这样消费者拿到 `IPlayerModel` 时看不到 `Initialize`、`GetUtility` 等内部能力：

```csharp
RegisterModel(new PlayerModel());                    // 注册键 PlayerModel（运行时类型）
RegisterModel<IPlayerModel>(new PlayerModel());      // 注册键 IPlayerModel
RegisterUtility<IJsonUtility>(new JsonUtility());
```

查找顺序：

1. 当前 Domain 的精确注册键；
2. 当前 Domain 中唯一可赋值的实例；有多个时抛出列出全部候选的 `InvalidOperationException`；
3. 本地没有候选时查找父 Domain。

规则：

- 同一分类重复注册同一个键、同一实例注册两次、组件同时属于多个分类，都会抛 `InvalidOperationException`。
- 不支持替换和移除组件。需要动态增减的一组功能，用子 Domain 表达。
- Utility 由调用方管理，可以在多个 Domain 间共享；Domain 释放时只清除引用，不会调用它的 `Dispose`。

## 动态 Domain 树

子 Domain 先独立创建，再挂载：

```csharp
var battle = BattleDomain.Create(matchId);
game.AddChild(battle);
var save = battle.GetUtility<ISaveUtility>(); // 挂载后才回退到父域

game.RemoveChild(battle);  // 不释放，battle 成为独立 Active 根
network.AddChild(battle);  // 移动必须先 Remove 再 Add
```

挂载、移除、释放**可以在 Command、Query 或事件处理中进行**。命令上下文不暴露所属 Domain（与 QFramework 一致，命令没有管理能力），切换场景由持有子域的对象完成，例如单例 Domain：

```csharp
public sealed class EnterBattleCommand(int matchId) : ICommand
{
    public void Execute(CommandContext context)
    {
        var game = GameApp.Instance;
        game.AddChild(BattleDomain.Create(matchId));
    }
}
```

释放后的 Domain 再被访问会抛 `ObjectDisposedException`。例如命令释放了自己所在的 Domain 后继续使用 `context`，就会立刻得到这个异常。

挂载会拒绝：挂载自身、已有父级的 Domain、形成环、非 Active 的 Domain。树操作只按引用身份判断，不受派生类重写 `Equals` 影响。已缓存的父组件引用不会因移动而刷新。

## Command、Query 与事件

```csharp
public sealed class ReadHpQuery : IQuery<int>
{
    public int Execute(QueryContext context) => context.GetModel<IPlayerModel>().Hp;
}

var hp = domain.SendQuery(new ReadHpQuery());
domain.SendCommand<RestartBattleCommand>(); // 无参便利写法
```

- 上下文是 `readonly ref struct`，不能保存到字段、装箱或跨越 `await`。构造函数是公开的，测试可以直接 `command.Execute(new CommandContext(domain))`。
- Query 表达只读意图，但不保证返回对象不可变。
- 不提供异步 Command/Query。

事件：

- 只在当前 Domain 内同步分发，不沿树传播，没有全局事件总线。跨 Domain 通信使用共享 Utility，或父级 Model 上的 `BindableProperty`。
- **只有本地事件会在 Domain 释放时自动失效。** 订阅父域 Model 的 `BindableProperty`、Utility 上的事件等非本地来源时，必须保存句柄并在 `OnRelease` 中取消；否则子域释放后回调仍挂在父域上、继续被调用，回调里一旦访问组件就会抛出 `InvalidOperationException`，而且异常是从父域的触发处抛出的：

```csharp
public sealed class BattleHudSystem : AbstractSystem
{
    private IUnRegister? _scoreToken;

    // IMatchModel 注册在父域，Score 是 BindableProperty<int>。
    protected override void OnInitialize() =>
        _scoreToken = GetModel<IMatchModel>().Score.Register((_, score) => Refresh(score));

    protected override void OnRelease() => _scoreToken?.UnRegister();

    private void Refresh(int score) { }
}
```
- 按注册顺序执行；第一个异常会停止后续处理器并原样抛出。
- 分发中新注册的处理器从下一次发送开始生效；分发中被取消的处理器，本轮不再调用。
- 分发中 Domain 被释放时，本轮剩余处理器不再调用。
- 取消注册句柄是幂等的，Domain 释放后调用也不会报错。

## 释放

`Dispose()` 的顺序：

```text
进入 Disposing，全部事件订阅失效
  -> 子 Domain 按逆挂载顺序后序释放
  -> OnDeactivating（仅当进入过 Active）
  -> System 逆序 Release
  -> Model 逆序 Release
  -> 清空注册表、从父级摘除
  -> Disposed
```

- 释放期间仍可读取组件，适合在 `OnRelease` 中保存数据；Model 在 System 之后释放，所以 System 释放时仍能拿到 Model。
- 组件按初始化逆序释放，与初始化对称：先注册的 System 后释放。**`OnRelease` 里不要调用其他 System 的方法**，后注册的 System 此时已经释放，调用会抛 `InvalidOperationException`。需要多个组件配合的退出逻辑（例如统一保存），放到 Domain 的 `OnDeactivating` 中，那时所有组件都还可用：

```csharp
public sealed class GameDomain : AbstractDomain
{
    protected override void OnDeactivating() => GetSystem<IStoreSystem>().Flush(); // 所有组件尚未释放

    // ...
}
```
- 释放期间（从进入 Disposing 起，包括释放子域的阶段）发送事件不会调用任何处理器；订阅事件、挂载或移除子域会抛 `InvalidOperationException`。子域在释放回调里向正在释放的父域发送的事件会被丢弃。
- 释放过程中再次调用 `Dispose` 无效果，包括子域释放回调间接释放父域的情况。启动期间（例如在 `Configure` 中）调用 `Dispose` 会导致启动失败。
- 父域在释放子域时已处于 Disposing，所以子域的释放回调里不能再挂载或移除父域的子域。
- 每一步都会执行到底：一个失败保留原异常和堆栈，多个失败按发生顺序展开为 `AggregateException`；无论如何 Domain 都会进入 Disposed 并从树中摘除。回调自己抛出的非空 `AggregateException` 也会被展开，调用方拿到的是其中的内层异常。
- 直接释放一个已挂载的子 Domain，会同时把它从父级移除。

## 常见失败与处理方式

| 情况 | 结果 | 建议 |
| --- | --- | --- |
| `Get*` 找不到组件 | `KeyNotFoundException` | 检查注册；可选依赖用 `IDomain.TryGet*` |
| 本地存在多个可赋值候选 | `InvalidOperationException`，列出候选 | 用显式接口键注册 |
| 重复注册 | `InvalidOperationException` | 每个键只注册一次 |
| 在 `Configure` 之外注册 | `InvalidOperationException` | 移到 `Configure`，或用子 Domain |
| 使用已释放的 Domain | `ObjectDisposedException` | 停止持有失效引用 |
| 组件初始化失败 | 原异常抛出，Domain 被释放 | 在 `OnInitialize` 内清理自身资源 |
| 多个释放步骤失败 | `AggregateException` | 按 `InnerExceptions` 顺序检查 |

框架使用名为 `Framework` 的 Logger 记录关键生命周期：Domain 激活与释放、Model/System 注册与释放、子 Domain 挂载与移除记录为 Info，启动和释放失败记录为 Error 并附带原始异常。每个 Domain 有进程内递增的诊断编号，日志以“完整类型名#编号”关联同一实例。查找、Command、Query、事件不会自动记录。日志只是诊断旁路，不影响异常和清理顺序。

## 线程边界

框架不加锁、不记录线程所有者，也不自动切回主线程。应用应在一个线程串行使用同一棵 Domain 树；后台任务完成后，先调度回该线程，再调用 Domain API。

## 延伸阅读

- [BindableProperty 使用说明](bindable-property.md)
