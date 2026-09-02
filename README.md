# SimpleFramework

SimpleFramework 是一个面向 C#/.NET 的轻量级游戏与应用框架集合。项目以模块化项目组织，提供领域容器、事件总线、命令/查询分离、ECS、常用设计模式、网络协议工具、数学工具、配置工具和 Godot 扩展。

项目当前主要面向 `.NET 8`，解决方案中包含 NUnit 测试项目用于覆盖核心行为。

## 功能概览

### 核心框架

根项目 `SimpleFramework` 提供一套外部简洁、内部可靠的 Domain 组织方式：

- `Domain`：分类保存 `System`、`Model`、`Utility`，支持可动态挂载/移除的强引用树。
- `System` / `Model`：业务接口只继承分类标记，实现类通过独立生命周期接口或抽象基类接入 Domain。
- `Utility`：由调用方管理、可跨 Domain 共享的底层能力。
- `Command` / `Query`：接收不可逃逸的同步栈 Context，避免保存或异步滥用执行上下文。
- 本地事件：按注册顺序同步分发，写时复制订阅数组，System 订阅自动随生命周期取消。
- `BindableProperty<T>`：可监听变化的属性封装。

```csharp
public sealed class GameDomain : AbstractDomain
{
    private GameDomain() { }
    public static GameDomain Create() => CreateDomain(() => new GameDomain());

    protected override void Configure()
    {
        RegisterModel<IPlayerModel>(new PlayerModel());
        RegisterSystem(new PlayerSystem());
        RegisterUtility<IJsonUtility>(new JsonUtility());
    }
}

using var domain = GameDomain.Create();
var byInterface = domain.GetModel<IPlayerModel>();
var byConcrete = domain.GetModel<PlayerModel>(); // 同一实例
```

启动先收集全部注册，再按 Model、System 的分类顺序初始化。Active 后可注册不存在的新键；不支持替换或移除已启动组件。查找按“本地精确键、本地唯一可赋值对象、父域”解析，歧义明确抛出。

子 Domain 先独立创建，再动态挂载：

```csharp
var battle = BattleDomain.Create(matchId);
domain.AddChild(battle);
domain.RemoveChild(battle); // 不释放，battle 仍为 Active 根
```

`Dispose()` 释放仍附着的完整子树；`DisposeSelfOnly()` 保留并分离直接子树。Domain 采用单线程协作模型，Command、Query、事件及整棵树的结构/生命周期守卫都是同步的。

核心文档：

- [Domain v2 完整使用与生命周期](docs/domain-lifecycle.md)
- [BindableProperty 使用说明](docs/bindable-property.md)

属性绑定可以为单个实例设置比较器，`WithComparer` 只影响当前实例。通知同步完成期间禁止写入不同的新值，以避免监听器观察到逆序的旧通知；相同值仍按比较器作为无操作处理:

```csharp
var hp = new BindableProperty<int>(100)
    .WithComparer((prev, current) => Math.Abs(prev - current) < 5);

using var token = hp.Register((previous, current) =>
    Console.WriteLine($"HP: {previous} -> {current}"));

hp.Value = 80;
```

### ECS

`SimpleFramework.ECS` 是一个强调可读性的轻量 ECS 实现：

- `World` 管理实体、原型和查询。
- `Entity` 是由 `WorldId`、全局递增 `Id` 和 `Version` 组成的值句柄；组件读写与结构变更由所属 `World` 完成。
- `Archetype` 与 `TypeSignature` 根据组件集合组织实体。
- `Query` 支持按组件包含/排除过滤实体，并在枚举期间拒绝直接结构变更。
- `EcsSystem` 提供可复用的 ECS 系统基类。
- `CommandBuffer` 用于在查询枚举后按记录顺序回放结构变更。

### Collections

`SimpleFramework.Collections` 提供常用集合补充：

- `DefaultDict<TKey, TValue>`：访问不存在的键时按工厂创建默认值。
- `Counter<T>`：面向计数场景的字典封装。

### Patterns

`SimpleFramework.Patterns` 收录游戏和应用中常见的结构模式：

- `Singleton<T>`：带生命周期状态的单例基类。
- `ServiceLocator`：服务注册与获取。
- `ObjectPool<T>`：对象池抽象和默认实现。
- `MessageChannel<T>` / `BufferedMessageChannel<T>`：发布订阅消息通道。
- `BlackBoard`：线程安全、类型安全、支持父级查找和变更通知的黑板数据存储。
- `StateMachine`：事件驱动状态机，支持任意状态转换、事件消费和嵌套子状态机。

### Net

`SimpleFramework.Net` 以 `GameNet` 为入口组合多人网络能力：

- `INetTransport` 提供传输抽象，内置确定性的 `MemoryNetTransport` 与 TCP 实现。
- `NetSession`、`PeerDirectory` 和 `GameNet` 管理主机、专用服务器、加入、离开、重连与对等体目录。
- `NetMessageRegistry` 与 `NetMessenger` 提供稳定协议键、类型化消息、请求/响应、中继和程序集处理器注册。
- `NetDiscovery` 提供可替换后端的 LAN 广告、一次性扫描和连续浏览器。
- `NetStats`、`NetFlow` 与 `NetDiagnostics` 提供延迟统计、多方提案流程和可观察诊断。

### Maths

`SimpleFramework.Maths` 提供基础数学结构与网格工具：

- `Point`、`MathUtils` 等基础工具。
- `Matrix` 二维矩阵结构。
- `HexagonGrid` 六边形网格实现，基于 Red Blob Games 的六边形坐标体系，支持尖角朝上和平边朝上布局、方向、邻居和坐标换算。

### Utility

`SimpleFramework.Utility` 提供跨模块基础能力：

- `Disposable` / `DisposableGroup`：资源释放和组合管理。
- `SerializeUtil`：基于 `System.Text.Json` 的对象和字节序列化。
- `Logging`：日志级别、处理器、格式器、控制台和文件输出。
- `FileUtil`、`TimeUtil`、`TaskUtil`、`MiscUtil`：文件、时间、异步等待和类型哈希等工具。
- 列表、枚举器、随机数和字符串扩展方法。

### Toolkit

`SimpleFramework.Toolkit` 当前提供 `IniConfigTool`：

- 加载、读取、写入和保存 INI 配置。
- 支持基础类型转换、默认值、自动保存、刷新和重新加载。
- 实现 `IUtility`，可注册到核心框架的 Domain 中使用。

### GDExt

`SimpleFramework.GDExt` 是 Godot 集成项目，当前包含 `Node2D` 场景对象池、节点退出时自动取消注册的扩展和逻辑信道常量。

## 项目结构

```text
SimpleFramework/
├─ SimpleFramework.csproj      # 核心框架
├─ Collections/                # 集合工具
├─ ECS/                        # 轻量 ECS
├─ Maths/                      # 数学和六边形网格
├─ Net/                        # 网络抽象、连接模型和协议工具
├─ Patterns/                   # 设计模式组件
├─ Toolkit/                    # 配置等工具
├─ Utility/                    # 通用基础工具
├─ GDExt/                      # Godot 扩展
└─ Test/                       # NUnit 测试
```

## 环境要求

- .NET SDK：`global.json` 指定 `9.0.0`，允许滚动到最新 minor。
- 项目目标框架：`net8.0`。
- 测试框架：NUnit。
- Godot 扩展项目依赖 `GodotSharp`，普通核心开发不一定需要直接使用它。

## 构建与测试

在仓库根目录运行：

```powershell
dotnet restore .\SimpleFramework.sln
dotnet build .\SimpleFramework.sln
dotnet test .\SimpleFramework.sln
```

如果只验证测试项目：

```powershell
dotnet test .\Test\Test.csproj
```

## 使用示例

### Domain、Model、Command、Query

```csharp
public sealed class GameDomain : AbstractDomain
{
    private GameDomain() { }
    public static GameDomain Create() => CreateDomain(() => new GameDomain());

    protected override void Configure()
    {
        RegisterModel(new PlayerModel());
        RegisterUtility(new IniConfigTool());
    }
}

public sealed class PlayerModel : AbstractModel
{
    public BindableProperty<int> Hp { get; } = new(100);
    protected override void OnInitialize() {}
}

public sealed class ReadHpQuery : AbstractQuery<int>
{
    protected override int OnExecute(QueryContext context) =>
        context.GetModel<PlayerModel>().Hp.Value;
}

using var game = GameDomain.Create();
var hp = game.SendQuery(new ReadHpQuery());
```

### ECS 查询

```csharp
var world = new World("Battle");
var entity = world.CreateEntity<Position, Velocity>(
    new Position { X = 0, Y = 0 },
    new Velocity { X = 1, Y = 0 });

var query = world.Query<Position, Velocity>()
    .Not<Dead>();

foreach (var item in query)
{
    ref var position = ref world.Get<Position>(item);
    ref var velocity = ref world.Get<Velocity>(item);
    position.X += velocity.X;
}
```

## 许可证

本项目使用 MIT License，详见 [LICENSE](LICENSE)。
