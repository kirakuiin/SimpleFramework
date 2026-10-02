# Domain 设计与使用

Domain 是 SimpleFramework 的核心框架：用 Model、System、Utility 三类组件组织业务，通过同步的 Command/Query 读写状态，用 Domain 内的本地事件和跨 Domain 的事件中心通信。用法与 QFramework 接近，采用同步、单线程协作模型。

与 QFramework 相比，主要区别是：可以创建多个 Domain 并组成树；查不到组件时抛异常而不是返回 `null`；重复注册会报错而不是静默覆盖；按接口或具体类型都能查到组件；初始化失败会回滚，释放失败会汇总；Command/Query 的上下文不能被保存；跨 Domain 事件没有静态全局总线，System 的订阅会随释放自动取消。

设计目标、健壮性底线和有意不做的事见文末的[设计目标与原则](#设计目标与原则)。

## 核心概念

| 角色 | 放什么 | 例子 |
| --- | --- | --- |
| Domain | 一组组件的容器和生命周期边界；可以组成树，子域查不到组件时回退到父域 | 应用根、一场对局、一个场景 |
| Model | 状态数据，以及只涉及自身数据的简单操作 | 玩家血量、背包、设置 |
| System | 业务规则、跨多个 Model 的逻辑、需要长期订阅事件的流程 | 伤害结算、成就统计 |
| Utility | 与业务无关、可替换的底层能力或外部服务，由调用方创建，可跨 Domain 共享 | 存档、配置读写、网络、事件中心 |
| Command | 一次修改操作的入口；表现层的修改都通过它 | 攻击、购买、开始对局 |
| Query | 一次只读查询，可组合多个 Model、System 的数据 | 计算总战力 |
| Controller | 表现层入口（UI、引擎节点）：读数据、发 Command/Query、订阅变化并刷新显示 | 血条、按钮 |

判断原则：

- 有状态放 Model，有规则或流程放 System，与业务无关、测试时想替换的放 Utility。
- 表现层读取 Model（或发 Query）并发送 Command，不直接修改 Model。
- 状态变化通知表现层有两种方式：Model 暴露 `BindableProperty`（见[数据驱动 UI](#数据驱动-ui)），或由 System、Command 发送事件（见下方入门示例）。

一个应用通常由一棵 Domain 树组成：

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

下面的代码可以直接作为控制台程序的 `Program.cs`（C# 要求顶层语句写在类型声明之前）。流程是：Command 发送事件 → System 处理事件并修改 Model → 调用方读取 Model。

```csharp
using SimpleFramework;

using var game = GameDomain.Create();
game.SendCommand(new DamageCommand(10));
Console.WriteLine(game.GetModel<IPlayerModel>().Hp); // 90

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
```

`Configure` 中的书写顺序不影响分类顺序：上例先注册 System，框架仍先初始化全部 Model，再初始化全部 System。

## 各角色的能力

| 能力 | Model | System | Controller | Command | Query |
| --- | --- | --- | --- | --- | --- |
| 获取 Model | 否 | 是 | 是 | 是 | 是 |
| 获取 System | 否 | 是 | 是 | 是 | 是 |
| 获取 Utility | 是 | 是 | 是 | 是 | 否 |
| 订阅事件（本地 `RegisterEvent`、跨域 `SubscribeEvent`） | 否 | 是 | 是 | 否 | 否 |
| 发送事件（本地 `SendEvent`、跨域 `PublishEvent`） | 是 | 是 | 否 | 是 | 否 |
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

`IController` 是纯接口，实现者只需提供所属 Domain，适合 UI 或引擎节点。Controller 的订阅（本地和跨域）都由它自己取消：

```csharp
public partial class HpLabel : Label, IController
{
    private IUnRegister? _damaged;
    private IUnRegister? _gameOver;

    public IDomain GetDomain() => GameApp.Instance;

    public override void _Ready()
    {
        _damaged = this.RegisterEvent<PlayerDamaged>(_ => Text = this.GetModel<IPlayerModel>().Hp.ToString());
        _gameOver = this.SubscribeEvent<GameOver>(_ => Hide()); // 跨 Domain 事件
    }

    public override void _ExitTree()
    {
        _damaged?.UnRegister();
        _gameOver?.UnRegister();
    }
}
```

Godot 节点也可以配合 GDExt 的 `UnRegisterWhenNodeExit`。

`GetDomain()` 返回 Controller 所属的 Domain。单例 Domain 直接返回 `Instance`；不是单例，或者 Controller 是普通 C# 类时，在构造时传入 Domain 并保存，见下一节的 `CounterView`。

### 数据驱动 UI

最常见的写法：Model 用 `BindableProperty` 保存状态并对外只读，Controller 订阅它刷新显示，修改通过 Command 完成。

下面是可独立替换 `Program.cs` 的完整示例。创建 View 时显示初始值，点击后自动刷新；作用域结束时先释放 View 的订阅，再释放 Domain。

```csharp
using SimpleFramework;

using var domain = CounterDomain.Create();
using var view = new CounterView(domain); // 输出 Count: 0
view.OnClick();                          // 输出 Count: 1

public interface ICounterModel : IModel
{
    IReadonlyBindableProperty<int> Count { get; } // 对外只读，表现层不能直接改
    void Increase();
}

public sealed class CounterModel : AbstractModel, ICounterModel
{
    private readonly BindableProperty<int> _count = new(0);

    public IReadonlyBindableProperty<int> Count => _count;

    public void Increase() => _count.Value++;

    protected override void OnInitialize() { }
}

public sealed class IncreaseCommand : ICommand
{
    public void Execute(CommandContext context) => context.GetModel<ICounterModel>().Increase();
}

// 普通 C# 类实现的 Controller：构造时传入所属 Domain
public sealed class CounterView : IController, IDisposable
{
    private readonly IDomain _domain;
    private readonly IUnRegister _count;

    public CounterView(IDomain domain)
    {
        _domain = domain;
        _count = this.GetModel<ICounterModel>().Count.RegisterWithNotify((_, value) => Render(value)); // 立即显示一次初始值
    }

    public IDomain GetDomain() => _domain;

    public void OnClick() => this.SendCommand<IncreaseCommand>();

    public void Dispose() => _count.UnRegister(); // Controller 的订阅由自己取消

    private static void Render(int value) => Console.WriteLine($"Count: {value}");
}

public sealed class CounterDomain : AbstractDomain
{
    private CounterDomain() { }

    public static CounterDomain Create() => CreateDomain(() => new CounterDomain());

    protected override void Configure() => RegisterModel<ICounterModel>(new CounterModel());
}
```

`BindableProperty` 的比较器、静默写入和通知期间的写入限制见 [BindableProperty 使用说明](bindable-property.md)。

### 直接实现生命周期接口

不继承基类时，实现 `IModelLifecycle` 或 `ISystemLifecycle`：保存 `Initialize(IDomain)` 传入的 Domain，并从 `GetDomain()` 返回它，即可使用 `this.GetModel<T>()` 等扩展方法。基类提供的这些便利，直接实现者都没有：重复初始化检查、`SubscribeEvent` 订阅随释放自动取消、`UnRegisterOnRelease`。因此推荐继承 `AbstractModel` / `AbstractSystem`。

## 创建与启动

具体 Domain 使用非公开构造函数和显式工厂：

```csharp
public sealed class BattleDomain : AbstractDomain
{
    private readonly int _matchId;

    private BattleDomain(int matchId) => _matchId = matchId;

    public static BattleDomain Create(int matchId, AbstractDomain? parent = null) =>
        CreateDomain(() => new BattleDomain(matchId), parent);

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
  -> 进入 Active（指定了父域时，此时挂到父域下）
  -> OnActivated
```

- 注册只能在 `Configure` 中进行，其他时候调用抛 `InvalidOperationException`。
- 初始化期间可以取到任何已注册组件的引用，但对方可能尚未初始化。**`OnInitialize` 里可以缓存引用，不要依赖其他 System 已经初始化完成。**
- 任一步失败：已初始化成功的组件按逆序 `Release`，Domain 进入 Disposed，原异常抛给调用方。初始化失败的组件本身不会被 `Release`，需要在 `OnInitialize` 内自行清理已占用的资源；继承 `AbstractSystem` 时，`SubscribeEvent` 和 `UnRegisterOnRelease` 登记的订阅会自动取消。
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

子 Domain 有两种挂到父域下的方式：

```csharp
// 方式一：创建时指定父域。子域初始化期间就能使用父域的组件（例如根上的 IEventHub、父域 Model），
// 启动成功后自动挂载。子域需要父域服务时用这种方式。
var battle = BattleDomain.Create(matchId, parent: game);

// 方式二：先独立创建，再挂载。挂载之前，子域查找不会回退到父域。
var lobby = LobbyDomain.Create();
game.AddChild(lobby);

game.RemoveChild(battle);  // 不释放，battle 成为独立 Active 根
network.AddChild(battle);  // 移动必须先 Remove 再 Add
```

创建时指定父域的规则：

- 父域必须是 Active。在父域的 `Configure` 或组件 `OnInitialize` 中以它为父创建子域会被拒绝；需要随父域一起创建的子域，放在父域的 `OnActivated` 中创建。
- 子域启动失败时，父域不受任何影响，子域不会出现在父域的子域列表里。
- 子域初始化期间父域被释放，子域启动失败；此时回退查找会抛出说明“父 Domain 已释放”的 `ObjectDisposedException`。

挂载、移除、释放**可以在 Command、Query 或事件处理中进行**。本框架的命令上下文不暴露所属 Domain，切换场景由持有子域的对象完成，例如单例 Domain：

```csharp
public sealed class EnterBattleCommand(int matchId) : ICommand
{
    public void Execute(CommandContext context) =>
        BattleDomain.Create(matchId, parent: GameApp.Instance);
}
```

释放后的 Domain 再被访问会抛 `ObjectDisposedException`。例如命令释放了自己所在的 Domain 后继续使用 `context`，就会立刻得到这个异常。

挂载会拒绝：挂载自身、已有父级的 Domain、形成环、非 Active 的 Domain。树操作只按引用身份判断，不受派生类重写 `Equals` 影响。已缓存的父组件引用和订阅不会因移动而刷新。

### 释放子域

结束一个子域直接调用它的 `Dispose()`，它会自动从父域摘除；父域释放时也会连带释放全部子域。`RemoveChild` 只用于“断开但继续存活”（例如移到另一个父域下），不属于释放流程：

| 想做的事 | 调用 |
| --- | --- |
| 场景结束，彻底销毁 | `child.Dispose()`，自动从父域摘除 |
| 父域销毁时连带销毁子域 | 无需操作 |
| 断开父子关系但子域继续存活 | `parent.RemoveChild(child)` |

管理子域的对象收到“子域关闭”之类的通知时，只清理自己的引用，**不要调用 `RemoveChild`**。此时子域正在释放（可能是被直接释放，也可能是随父域一起释放），`RemoveChild` 会抛 `InvalidOperationException`；摘除由框架完成，子域剩余的释放回调也因此仍能回退查找父域组件。同理，也不要在关闭通知里移动正在释放的子域的上级（例如把它的父域从祖父域上 `RemoveChild`），否则它剩余的释放回调会失去回退查找。

示例中 `SceneDomain` 在 `OnDeactivating` 里通过事件中心发布关闭通知（本地 `SendEvent` 到不了父域的 System）：

```csharp
public sealed class SceneManager : AbstractSystem
{
    private SceneDomain? _current;

    // SceneDomain.OnDeactivating 中：GetUtility<IEventHub>().Publish(new SceneClosed(this));
    protected override void OnInitialize() =>
        SubscribeEvent<SceneClosed>(e =>
        {
            if (ReferenceEquals(_current, e.Scene)) _current = null; // 只清理引用
        });

    public void Enter(int sceneId, AbstractDomain root)
    {
        _current?.Dispose(); // 自动从父域摘除
        _current = SceneDomain.Create(sceneId, parent: root);
    }
}
```

## Command、Query 与本地事件

```csharp
public sealed class ReadHpQuery : IQuery<int>
{
    public int Execute(QueryContext context) => context.GetModel<IPlayerModel>().Hp;
}

// 带返回值的命令：执行修改并返回结果
public sealed class HealCommand(int amount) : ICommand<int>
{
    public int Execute(CommandContext context)
    {
        var player = context.GetModel<IPlayerModel>();
        player.Hp += amount;
        return player.Hp;
    }
}

var hp = domain.SendQuery(new ReadHpQuery());
var healed = domain.SendCommand(new HealCommand(5));
domain.SendCommand<RestartBattleCommand>(); // 无参命令的便利写法
```

也可以继承 `AbstractCommand`、`AbstractCommand<TResult>`、`AbstractQuery<TResult>` 并重写 `OnExecute`。它们只是把 `Execute` 转发给 `OnExecute`，与直接实现接口等价，按团队习惯选择即可。

- 上下文是 `readonly ref struct`，不能保存到字段、装箱或跨越 `await`。构造函数是公开的，测试可以直接 `command.Execute(new CommandContext(domain))`。
- Query 表达只读意图，但不保证返回对象不可变。
- 不提供异步 Command/Query。

本地事件（`SendEvent` / `RegisterEvent`）：

- 只在当前 Domain 内同步分发，不沿树传播。跨 Domain 通信见下一节。
- Domain 释放时，本地事件的订阅自动失效，System 通常不需要保存句柄。
- 按注册顺序执行；第一个异常会停止后续处理器并原样抛出。
- 分发中新注册的处理器从下一次发送开始生效；分发中被取消的处理器，本轮不再调用。
- 分发中 Domain 被释放时，本轮剩余处理器不再调用。
- 取消注册句柄是幂等的，Domain 释放后调用也不会报错。

## 跨 Domain 通信

### 怎么选

| 需求 | 推荐方式 |
| --- | --- |
| Domain 之间的通知或请求 | 事件中心：`PublishEvent` / `SubscribeEvent` |
| 子域需要父域的**当前状态**，并跟随变化 | 订阅父域 Model 上的 `IReadonlyBindableProperty`，并 `UnRegisterOnRelease(this)` |
| 子域要**修改**父域状态 | 发布事件，由父域的 System 处理 |

### 事件中心 `IEventHub`

`IEventHub` 是一个普通 Utility，框架提供默认实现 `EventHub`。它由调用方创建、持有和释放，Domain 不会替你释放它。

**注册**：在根 Domain 的 `Configure` 中注册一次，推荐从外部传入，便于测试时每次换一个新实例：

```csharp
using var hub = new EventHub();
using var root = RootDomain.Create(hub);

public sealed class RootDomain : AbstractDomain
{
    private readonly IEventHub _hub;

    private RootDomain(IEventHub hub) => _hub = hub;

    public static RootDomain Create(IEventHub hub) => CreateDomain(() => new RootDomain(hub));

    protected override void Configure() => RegisterUtility<IEventHub>(_hub);
}
```

`using` 按声明的逆序释放：先释放 `root` 及其子域，再释放 `hub`，让组件退出期间仍可使用事件中心。

也可以放在静态字段里，例如 `AppEvents.Hub`，再在 `Configure` 中注册。这样写更简单，但测试之间会共享订阅，相当于回到了全局状态。

**作用范围由注册位置决定**：

- 注册在根 Domain：整棵树共享。子域需要在初始化期间订阅时，用 `parent:` 创建子域。
- 子域注册自己的事件中心：会遮蔽父域的（查找本地优先），适合只在某场对局内部流转的事件。
- 同一实例注册到多个互不相连的 Domain：这些 Domain 共用它。

**发布与订阅**：

```csharp
public sealed class BattleSystem : AbstractSystem
{
    protected override void OnInitialize() =>
        SubscribeEvent<GameOver>(OnGameOver);       // 随本 System 释放自动取消，无需保存句柄

    public void Finish() => PublishEvent(new GameOver());

    private void OnGameOver(GameOver message) { }
}

context.PublishEvent(new GameOver());               // Command 中
var token = this.SubscribeEvent<GameOver>(handler); // Controller 中，需要自己取消
```

- 分发规则与本地事件完全相同：同步、按注册顺序、分发中取消的订阅本轮跳过、第一个异常停止后续处理器。
- 没有注册事件中心时，抛出提示“请在根 Domain 的 Configure 中注册 IEventHub”的 `KeyNotFoundException`。
- `AbstractSystem.SubscribeEvent` 的订阅在 System 释放时、`OnRelease` 之前自动取消；System 初始化失败时也会取消。需要提前取消时，调用返回句柄的 `UnRegister()`。
- 与本地事件的差异：本地事件在 Domain 一开始释放时就失效；事件中心的订阅要到各 System `Release` 时才取消，所以 `OnDeactivating` 期间，事件中心的事件仍会送达本 Domain 的 System。此时组件都还可用，是安全的。
- 释放事件中心后，订阅全部失效，之后再发布或订阅抛 `ObjectDisposedException`。任何拿到它的人都能释放它，因此约定由创建者释放。
- 事件中心不加锁：即使被互不相连的多个 Domain 共享，也必须在同一线程使用。

### 订阅父域 `BindableProperty`

只有 Domain 本地事件和 `SubscribeEvent` 的订阅会自动取消。`BindableProperty` 是普通对象，不知道订阅者属于哪个 Domain。子域订阅父域的属性后如果不取消，子域释放后回调仍挂在父域上、继续被调用；对继承 `AbstractSystem` 的订阅者，回调一旦访问组件就会抛出 `InvalidOperationException`，而且是从父域修改属性的地方抛出的。

对订阅句柄调用 `UnRegisterOnRelease(this)`，让它随 System 释放自动取消：

```csharp
public interface IMatchModel : IModel
{
    IReadonlyBindableProperty<int> Score { get; } // 对外只读：子域只能读和订阅，不能直接改父域状态
}

public sealed class BattleHudSystem : AbstractSystem
{
    // IMatchModel 注册在父域；BattleDomain 用 parent: 创建，初始化时即可找到它。
    protected override void OnInitialize() =>
        GetModel<IMatchModel>().Score.Register((_, score) => Refresh(score)).UnRegisterOnRelease(this);

    private void Refresh(int score) { }
}
```

注意：

- `UnRegisterOnRelease` 只在 `AbstractSystem` 上可用。在 System 未绑定或正在释放时调用会抛 `InvalidOperationException`，传入的订阅会被立即取消。
- 订阅与 System 同生命周期时丢弃返回值即可。需要提前取消时，**保存并取消 `UnRegisterOnRelease` 的返回值**，不要再用原句柄：原句柄不知道自己已被登记，直接取消它虽然能停止回调，但登记项要到 System 释放时才移除，反复订阅、取消会让登记列表持续增长。`SubscribeEvent` 只返回包装句柄，没有这个问题。
- 在通知回调里回写同一个属性会被拒绝（`BindableProperty` 的防重入保护）。
- 移动子树后，订阅仍指向原父域的属性，不会自动切换。

## 释放

`Dispose()` 的顺序：

```text
进入 Disposing，全部本地事件订阅失效
  -> 子 Domain 按逆挂载顺序后序释放
  -> OnDeactivating（仅当进入过 Active）
  -> System 逆序 Release（每个 System 先取消自有订阅，再调用 OnRelease）
  -> Model 逆序 Release
  -> 清空注册表、从父级摘除
  -> Disposed
```

- 释放期间仍可读取组件，适合在 `OnRelease` 中保存数据；Model 在 System 之后释放，所以 System 释放时仍能拿到 Model。
- 组件按初始化逆序释放，与初始化对称：先注册的 System 后释放。**`OnRelease` 里不要调用其他 System 的方法**，后注册的 System 此时已经释放，它的方法一旦访问组件或事件就会抛 `InvalidOperationException`。需要多个组件配合的退出逻辑（例如统一保存），放到 Domain 的 `OnDeactivating` 中，那时所有组件都还可用：

```csharp
public sealed class GameDomain : AbstractDomain
{
    protected override void OnDeactivating() => GetSystem<IStoreSystem>().Flush(); // 所有组件尚未释放

    // ...
}
```

- 释放期间（从进入 Disposing 起，包括释放子域的阶段）发送本地事件不会调用任何处理器；订阅本地事件、挂载或移除子域会抛 `InvalidOperationException`。子域在释放回调里向正在释放的父域发送的本地事件会被丢弃。
- System 在 `OnRelease` 中不能再 `SubscribeEvent` 或 `UnRegisterOnRelease`，否则抛 `InvalidOperationException`。
- 释放过程中再次调用 `Dispose` 无效果，包括子域释放回调间接释放父域的情况。启动期间（例如在 `Configure` 中）调用 `Dispose` 会导致启动失败。
- 父域在释放子域时已处于 Disposing，所以子域的释放回调里不能再挂载或移除父域的子域。
- 正在释放的子域不能被 `RemoveChild`，即使父域仍是 Active（例如直接释放子域时，子域回调或其他 System 收到关闭通知后去摘除它）：会抛 `InvalidOperationException`。子域会在释放结束时自己脱离父域。
- 每一步都会执行到底：一个失败保留原异常和堆栈，多个失败按发生顺序展开为 `AggregateException`；无论如何 Domain 都会进入 Disposed 并从树中摘除。回调自己抛出的非空 `AggregateException` 也会被展开，调用方拿到的是其中的内层异常。
- 直接释放一个已挂载的子 Domain，会同时把它从父级移除。
- 已知限制：直接释放子域时，如果子域的释放回调又释放了父域，父域会先于子域释放完；之后子域在 `OnRelease` 中回退查找父域组件，会得到说明“父 Domain 已释放”的 `ObjectDisposedException`。

## 如何测试

- **每个测试创建新的 Domain**，用 `using` 释放。不要在测试之间共享单例或静态事件中心，否则订阅和状态会串到下一个测试。
- **依赖按接口获取**，测试时注册假实现。让 Domain 通过 `Create` 参数接收 Utility，生产和测试就能复用同一个 Domain。
- **单独测试 Command/Query**：上下文接收 `IDomain` 且构造函数公开，可以对只注册了所需组件的小 Domain 执行 `command.Execute(new CommandContext(domain))`，也可以直接 `domain.SendCommand(...)`。

```csharp
public sealed class FakeSaveUtility(int hp) : ISaveUtility
{
    public int Saved { get; private set; }
    public int LoadHp() => hp;
    public void SaveHp(int value) => Saved = value;
}

public sealed class PlayerTestDomain : AbstractDomain
{
    private readonly ISaveUtility _save;

    private PlayerTestDomain(ISaveUtility save) => _save = save;

    public static PlayerTestDomain Create(ISaveUtility save) => CreateDomain(() => new PlayerTestDomain(save));

    protected override void Configure()
    {
        RegisterUtility<ISaveUtility>(_save); // 假实现
        RegisterModel<IPlayerModel>(new PlayerModel());
        RegisterSystem<IPlayerSystem>(new PlayerSystem());
    }
}

[Test]
public void DamageIsSavedOnRelease()
{
    var save = new FakeSaveUtility(hp: 50);

    using (var domain = PlayerTestDomain.Create(save))
    {
        domain.SendCommand(new DamageCommand(10));
        Assert.That(domain.GetModel<IPlayerModel>().Hp, Is.EqualTo(40));
    }

    Assert.That(save.Saved, Is.EqualTo(40)); // PlayerSystem 释放时保存
}
```

## 常见失败与处理方式

| 情况 | 结果 | 建议 |
| --- | --- | --- |
| `Get*` 找不到组件 | `KeyNotFoundException` | 检查注册；可选依赖用 `IDomain.TryGet*` |
| 本地存在多个可赋值候选 | `InvalidOperationException`，列出候选 | 用显式接口键注册 |
| 重复注册 | `InvalidOperationException` | 每个键只注册一次 |
| 在 `Configure` 之外注册 | `InvalidOperationException` | 移到 `Configure`，或用子 Domain |
| 使用已释放的 Domain | `ObjectDisposedException` | 停止持有失效引用 |
| 回退查找时父域已释放 | `ObjectDisposedException`，说明父域已释放 | 检查是否在子域启动或释放过程中释放了父域 |
| 组件初始化失败 | 原异常抛出，Domain 被释放 | 在 `OnInitialize` 内清理自身资源 |
| 以未就绪的 Domain 为父创建子域 | `InvalidOperationException` | 在父域的 `OnActivated` 中创建 |
| 对正在释放的子域调用 `RemoveChild` | `InvalidOperationException` | 关闭通知中只清理引用，摘除由框架完成 |
| `PublishEvent` / `SubscribeEvent` 找不到事件中心 | `KeyNotFoundException`，提示注册 `IEventHub` | 在根 Domain 的 `Configure` 中注册；子域用 `parent:` 创建 |
| System 释放期间订阅 | `InvalidOperationException` | 订阅放在 `OnInitialize` 或运行期 |
| 多个释放步骤失败 | `AggregateException` | 按 `InnerExceptions` 顺序检查 |

框架使用名为 `Framework` 的 Logger 记录关键生命周期：Domain 激活与释放、Model/System 注册与释放、子 Domain 挂载与移除记录为 Info，启动和释放失败记录为 Error 并附带原始异常。每个 Domain 有进程内递增的诊断编号，日志以“完整类型名#编号”关联同一实例。查找、Command、Query、事件不会自动记录。日志只是诊断旁路，不影响异常和清理顺序。

## 线程边界

框架不加锁、不记录线程所有者，也不自动切回主线程。应用应在一个线程串行使用同一棵 Domain 树和它共享的事件中心；后台任务完成后，先调度回该线程，再调用 Domain API。

## 设计目标与原则

**目标是“顺手”**：用法向 QFramework 看齐但更规范，以尽量低的复杂度提供多实例、Domain 树和可靠的生命周期。

**健壮性底线**，可以为易用让步，但不能低于以下要求，且整体强于 QFramework：

- 用户没有犯错时，框架绝不出问题。
- 用户犯错时，抛出说明原因的明确异常，不静默覆盖、不返回 `null`、不吞掉错误。

**性能**：成功查找组件，以及发送本地事件、发布到事件中心、执行 Command/Query 的框架分发路径，在热身后不产生每次调用的内存分配（由测试覆盖）。这不包含业务对象创建、回调内部、异常路径或值类型 Command/Query 转为接口时的装箱。`new DamageCommand(...)`、创建引用类型事件，以及 `SendCommand<T>()` / `SendEvent<T>()` / `PublishEvent<T>()` 便利方法创建引用类型实例，仍会分配内存。需要零分配时，应在语义允许的前提下复用命令和查询对象，并使用值类型事件或已有事件对象。

**有意不做的事**：

| 不做 | 原因 |
| --- | --- |
| 自定义异常类型 | 使用 .NET 标准异常（`KeyNotFoundException`、`InvalidOperationException`、`ObjectDisposedException`、`AggregateException`），调用方无需认识新类型 |
| 终结器 | Domain 必须显式 `Dispose`；终结器线程上的释放会违反单线程模型 |
| 按需初始化 | 与 QFramework 一致，初始化期间可能拿到尚未初始化的组件；靠约定（`OnInitialize` 只缓存引用）而不是框架追踪依赖 |
| 替换、删除组件，Active 阶段注册 | 需要动态增减的功能用子 Domain 表达 |
| 执行期树守卫、全局生命周期所有权表、Context 三态、注册两阶段、`DisposeSelfOnly` | 这些机制只防范罕见误用，却妨碍常见用法（例如在 Command 中切换场景、脱离真实 Domain 测试 Command），已经删除 |
| 静态全局事件总线 | 跨 Domain 通信使用调用方持有的 `IEventHub`，测试之间不共享状态 |
| 线程安全 | 采用同步、单线程协作模型，见[线程边界](#线程边界) |

新增防护前先确认它针对的是“用户没犯错却出问题”，或者是“用户犯错却没有明确异常”；只为防范罕见误用而增加的复杂度不符合上述目标。

## 延伸阅读

- [BindableProperty 使用说明](bindable-property.md)
