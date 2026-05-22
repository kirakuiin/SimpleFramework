# SimpleFramework

SimpleFramework 是一个面向 C#/.NET 的轻量级游戏与应用框架集合。项目以模块化项目组织，提供领域容器、事件总线、命令/查询分离、ECS、常用设计模式、网络协议工具、数学工具、配置工具和 Godot 扩展。

项目当前主要面向 `.NET 8`，解决方案中包含 NUnit 测试项目用于覆盖核心行为。

## 功能概览

### 核心框架

根项目 `SimpleFramework` 提供一套轻量的应用组织方式：

- `Domain`：顶层作用域与组件容器，负责注册和查找 `System`、`Model`、`Utility`，支持父子 Domain 与生命周期管理。
- `System`：跨模型或跨实体的业务逻辑单元，初始化后可访问模型、工具和事件。
- `Model`：单一职责的数据模型，适合保存可被系统和命令读取或修改的状态。
- `Utility`：底层能力组件，例如网络传输、配置工具或序列化工具。
- `Command` / `Query`：将写操作和读操作分离，命令可带返回值，查询用于只读访问。
- `EventBus`：支持域内事件和全局事件注册、取消注册、分发。
- `BindableProperty<T>`：可监听变化的属性封装，支持注册时立即通知和无通知赋值。

Core 使用示例:

```csharp
// 简单项目可以继续使用单例 Domain
var domain = GameDomain.Instance;

// 测试、多会话或工具场景可以显式创建独立 Domain
var sessionDomain = GameDomain.Create();
sessionDomain.UnInitialize();
```

组件注册与必需查找:

```csharp
domain.RegisterUtilityAs<ITimeUtility>(new TimeUtility());
var timeUtility = domain.RequireUtility<ITimeUtility>();
```

Domain 事件默认只在当前 Domain 内触发，不会沿父子 Domain 自动传播。跨 Domain 事件应显式使用 `EventBus.Global`。

属性绑定可以为单个实例设置比较器，`WithComparer` 只影响当前实例:

```csharp
var hp = new BindableProperty<int>(100)
    .WithComparer((prev, current) => Math.Abs(prev - current) < 5);
```

### ECS

`SimpleFramework.ECS` 是一个强调可读性的轻量 ECS 实现：

- `World` 管理实体、原型和查询。
- `Entity` 持有组件和标签，支持添加、移除、查询组件。
- `Archetype` 与 `TypeSignature` 根据组件集合组织实体。
- `Query` 支持按组件包含/排除和标签包含/排除过滤实体。
- `EcsSystem` 提供可复用的 ECS 系统基类。

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

`SimpleFramework.Net` 提供网络抽象和协议处理工具：

- `ITransport`、`ITransfer`、`INetStatus` 定义连接、数据传输和延迟查询接口。
- `ConnectionModel` 维护连接状态。
- `ClientInfoSystem<TInfo>` 同步客户端信息。
- `ProtocolHandler` 通过 `[Protocol]` 标记注册协议，负责协议打包、解包、处理器分发和调试信息。
- UDP 广播工具支持发送、接收、定时广播和持续监听泛型结构体消息。

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

`SimpleFramework.GDExt` 是 Godot 集成项目：

- `MultiplayerTransport` 基于 Godot `ENetMultiplayerPeer` 实现 `ITransport`、`ITransfer`、`INetStatus`。
- 提供 Godot RPC 数据发送、连接管理、踢出、延迟测量等能力。
- 包含 Godot 节点对象池和取消注册扩展。

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
public class GameDomain : AbstractDomain<GameDomain>
{
    protected override void Init()
    {
        RegisterModel(new PlayerModel());
        RegisterUtility(new IniConfigTool());
    }
}

public class PlayerModel : AbstractModel
{
    public BindableProperty<int> Hp { get; } = new(100);
    protected override void OnInitialize() {}
}

public class ReadHpQuery : AbstractQuery<int>
{
    protected override int OnExecute() => this.GetModel<PlayerModel>().Hp.Value;
}

var hp = GameDomain.Instance.SendQuery(new ReadHpQuery());
```

### ECS 查询

```csharp
var world = new World("Battle");
var entity = world.CreateEntity<Position, Velocity>();
entity.AddTag("player");

var query = world.CreateQuery()
    .Has<Position>()
    .Not<Dead>()
    .HasTag("player");

foreach (var item in query)
{
    var position = item.Get<Position>();
}
```

## 许可证

本项目使用 MIT License，详见 [LICENSE](LICENSE)。
