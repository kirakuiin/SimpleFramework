# SimpleFramework.Patterns

`SimpleFramework.Patterns` 提供一组轻量、可独立使用的应用与游戏模式：

- `Singleton<T>`：延迟创建、初始化成功后发布，并支持显式销毁。
- `ServiceLocator`：按注册类型优先、可赋值类型回退的轻量服务定位器。
- `ObjectPool<T>`：支持预热、回调、重复归还检测和确定性释放的对象池。
- `MessageChannel<T>`：支持嵌套发布期间安全增删订阅；`BufferedMessageChannel<T>` 会向新订阅者重放最后一条消息。
- `BlackBoard`：线程安全的层级键值存储；本地值（包括 `null`）会遮蔽父级值，变更通知在锁外执行。
- `StateMachine`：事件驱动的层次状态机，支持 AnyState、事件消费、嵌套状态机和显式生命周期恢复。

消息订阅句柄只在显式释放时取消其所拥有的注册：

```csharp
using var subscription = channel.Subscribe(message => Console.WriteLine(message));
channel.Publish("ready");
```

黑板的父级只参与查找；对子级的写入不会修改父级：

```csharp
var parent = new BlackBoard();
var child = new BlackBoard(parent: parent);

parent.Set("difficulty", "normal");
child.Set("difficulty", "hard");
```
