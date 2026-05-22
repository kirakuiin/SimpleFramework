# SimpleFramework

一个轻量级的 C# 框架，提供了模块化、事件驱动的架构设计。

## 核心功能

### 领域驱动设计模块

基本模块:
- **Domain**: 顶层容器，管理整个应用的生命周期
- **System**: 跨实体的业务逻辑实现
- **Model**: 单一职责的数据模型
- **Utility**: 提供底层功能支持

事件系统:
- 支持域内事件传递和处理
- 提供全局事件总线
- 支持事件注册与注销
- 支持带通知的属性绑定

命令与查询分离:
- **Command**: 用于修改数据的操作
- **Query**: 用于只读数据的查询
- 支持带返回值的命令执行

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

### ECS模块

实现了一个轻量级的ECS框架

### Collections模块
- **DefaultDict**: 带默认值的字典实现
- **Counter**: 带有计数器功能的字典

## Patterns模块
- **EntityComponent**: 实体组件模式
- **Singleton**: 单例模式

## Utility模块
- **DisposableGroup**: 资源管理组
- **SerializeTool**: JSON序列化工具
- **TimeUtil**: 时间转换工具
- **扩展方法**
  - **List**: 列表操作扩展
  - **Random**: 随机数相关扩展
  - **String**: 字符串处理扩展

## Net模块

- **NetworkUtil**: 网络工具类
- **UdpBroadcast**: UDP广播工具
  - 支持基础的UDP广播发送和接收
  - 支持定时广播功能
  - 支持持续监听广播
  - 支持泛型消息格式

## Maths模块

- **六边形网格**: 基于 [Red Blob Games](http://www.redblobgames.com/grids/hexagons/) 的六边形网格实现，支持尖角朝上和平边朝上两种布局，可自定义大小和形状。
