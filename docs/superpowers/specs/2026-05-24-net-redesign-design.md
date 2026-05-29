# Net 模块重设计

## 背景

当前 `Net` 模块已经能在 Godot 平台跑通连接、协议分发和客户端信息同步，但连接管理、协议处理、认证、重连、peer 列表和同步逻辑耦合较紧。新设计不以兼容旧 API 为目标，而是从小型多人游戏的真实使用场景出发，重新设计一套易用、易理解、易维护、可扩展的网络会话库。

V1 不绑定 Godot，也不依赖 SimpleFramework 的 `Domain`、`Model`、`System` 生命周期。后续可以增加 SimpleFramework 集成层，但核心网络库必须能独立测试和运行。

## 目标

- 简化小型游戏的连接管理、消息收发和多人流程协调。
- 采用权威服务器模型：客户端连接服务器，服务器裁决房间、认证、消息广播和多人流程结果。
- 底层传输可替换，不绑定 Godot。
- 提供可本地测试的 `TcpNetTransport` 和确定性测试用的 `MemoryNetTransport`。
- 支持局域网房间发现和应用层延迟检测。
- 为后续实时同步、预测回滚、Godot transport、SimpleFramework 集成预留边界，但 V1 不实现这些高级能力。

## 非目标

V1 不实现以下能力：

- Godot transport。
- SimpleFramework `Domain` 集成层。
- UDP/LiteNetLib transport。
- 公网大厅、NAT 穿透、匹配服务。
- NetworkObject / NetworkVariable。
- 状态快照同步、客户端预测、回滚、服务器校正。
- 压缩、加密、分片。
- 自动协议 manifest 生成。
- 客户端发起的多人 Flow。
- Flow 断线重连后的自动重发。

## 分阶段交付

本设计保留完整网络库能力，但实现必须分阶段推进，避免第一轮同时落地过多后台任务、协议语义和测试矩阵。

V1 是可运行的会话和消息核心：

- `GameNet` 主入口。
- `INetTransport` 抽象、`TcpNetTransport`、`MemoryNetTransport`。
- `NetSession`：Host、Dedicated Server、Join、认证、Kick、断线原因、可选重连、`PeerDirectory`。
- `Messenger`：消息注册、编解码、`On<T>`、`SendToServerAsync`、`SendAsync`、`BroadcastAsync`、受服务端校验的 `RelayAsync`。
- 基础流量保护：`MaxPacketSize`、发送队列上限、明确发送失败结果。
- 基础诊断：只读快照、结构化错误事件、DebugName 日志上下文。

V1.1 增加 request/response：

- `OnRequest<TRequest,TResponse>`。
- `RequestAsync<TRequest,TResponse>`。
- correlation、timeout、cancellation、late/duplicate response 处理。

V1.2 增加局域网发现和延迟统计：

- `NetDiscovery` 短扫和持续浏览。
- `NetStats` 应用层 ping/pong、RTT、jitter、ProbeLoss。

V1.3 增加多人流程：

- `NetFlow` proposal、vote、barrier。
- 聚合策略、pending 查询、断线后手动重发。

后续扩展保留 UDP/LiteNetLib、Godot transport、SimpleFramework adapter、Realtime Extension。分阶段只影响实现顺序，不删除这些能力的设计边界。

## 总体架构

对外主入口是 `GameNet`。常用 API 直接挂在 `GameNet` 上，高级能力通过子组件访问：

```csharp
var net = new GameNet(new TcpNetTransport(), options);

await net.HostAsync(7777);
await net.JoinAsync("127.0.0.1", 7777);

net.On<PlayerReady>((ctx, msg) => { });
await net.SendToServerAsync(new PlayerReady(true));
await net.BroadcastAsync(new RoomStateChanged(...));

var result = await net.Flow.ProposeAsync(...);
```

内部组件：

- `Transport`：底层连接和 `byte[]` 收发。
- `Session`：连接生命周期、peer 列表、认证、断线、重连、踢出。
- `Messenger`：消息注册、编解码、单发、广播、请求响应。
- `NetFlow`：多人流程，包括全员确认、投票、加载屏障、超时聚合和服务器裁决。
- `Discovery`：局域网房间广播和扫描。
- `Stats`：应用层延迟和基础连接质量统计。

依赖方向：

```text
GameNet
  -> Session
  -> Messenger
  -> NetFlow
  -> Discovery
  -> Stats
  -> Transport
```

`Transport` 不知道业务消息，也不知道会话层稳定 `PeerId`。`Session` 负责把底层连接映射到稳定 `PeerId`。`NetFlow` 基于 `Messenger` 的 request/response 实现，不自建底层协议。

## Options

`GameNetOptions` 保存整个 Net 实例的项目级配置。`ApplicationId` 和 `ProtocolVersion` 不属于 Discovery，而是所有网络能力共享的兼容性身份。

```csharp
var net = new GameNet(new TcpNetTransport(), new GameNetOptions
{
    DebugName = "Local Server",
    Application = new NetApplicationInfo
    {
        ApplicationId = Guid.Parse("2f2db4b5-4f54-47c1-b0e8-2f9e2fd7f741"),
        ProtocolVersion = 1
    },
    Discovery = new DiscoveryOptions
    {
        Port = 3344,
        AdvertiseInterval = TimeSpan.FromSeconds(1),
        RoomTimeout = TimeSpan.FromSeconds(5),
        MaxPayloadSize = 1024
    },
    EventDispatcher = null
});
```

项目级配置：

```csharp
public sealed class NetApplicationInfo
{
    public required Guid ApplicationId { get; init; }
    public int ProtocolVersion { get; init; } = 1;
}
```

Discovery 专属配置：

```csharp
public sealed class DiscoveryOptions
{
    public int Port { get; init; } = 3344;
    public TimeSpan AdvertiseInterval { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan RoomTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public int MaxPayloadSize { get; init; } = 1024;
    public TimeSpan DefaultScanTimeout { get; init; } = TimeSpan.FromSeconds(2);
}
```

这些配置的使用规则：

- `ApplicationId` 是项目级唯一 ID，创建项目时生成一次并保存。不同游戏即使都使用同一套框架，也不会互相显示房间或误连。
- `ApplicationId` 没有默认值，必须显式配置，且不能是 `Guid.Empty`。框架可以提供生成工具，但不能在运行时自动给所有项目同一个默认值。
- `ProtocolVersion` 默认值为 `1`。
- `ProtocolVersion` 用于 Discovery 过滤和 Session handshake。版本不兼容时，客户端应看到明确的 incompatible 结果，而不是认证失败。
- `DebugName` 是可选的本地实例调试名，只用于日志、调试面板和测试区分，不参与协议兼容、Discovery 过滤或房间展示。房间展示名应放在 Discovery metadata 中。
- Discovery 广播包内部自动携带 `ApplicationId` 和 `ProtocolVersion`，但业务调用 `StartAdvertiseAsync` 时不需要重复传入。
- Session handshake 也必须校验 `ApplicationId` 和 `ProtocolVersion`，避免绕过 Discovery 直接输入 IP 时连到错误游戏或错误版本。
- `DiscoveryOptions` 只负责 UDP 端口、广播间隔、房间过期时间和 payload 限制。
- `DiscoveryOptions` 有保守默认值，常规项目只需要配置 `ApplicationId`。
- `EventDispatcher` 默认为 `null`，表示事件在网络后台任务上下文中直接触发。需要 UI 或游戏主线程派发时，由业务提供 dispatcher。

Options 启动校验：

```text
Application.ApplicationId != Guid.Empty
Application.ProtocolVersion >= 1
Discovery.Port is 1..65535
Discovery.AdvertiseInterval > TimeSpan.Zero
Discovery.RoomTimeout > Discovery.AdvertiseInterval
Discovery.MaxPayloadSize > 0
Discovery.DefaultScanTimeout > TimeSpan.Zero
```

常规最小配置：

```csharp
var net = new GameNet(new TcpNetTransport(), new GameNetOptions
{
    Application = new NetApplicationInfo
    {
        ApplicationId = GameIds.MyGame
    }
});
```

只有需要调整广播端口或频率时才覆盖 Discovery：

```csharp
var net = new GameNet(new TcpNetTransport(), new GameNetOptions
{
    Application = new NetApplicationInfo
    {
        ApplicationId = GameIds.MyGame
    },
    Discovery = new DiscoveryOptions
    {
        Port = 45000,
        AdvertiseInterval = TimeSpan.FromMilliseconds(500),
        RoomTimeout = TimeSpan.FromSeconds(3)
    }
});
```

## Event Dispatching

Net Core 默认不承诺事件回到主线程。`Discovery`、`Session`、`Messenger`、`NetFlow`、`Stats` 的事件可能在网络后台任务上下文触发。

如果业务需要把事件投递到 UI 线程或游戏主线程，可以配置可选 dispatcher：

```csharp
public interface INetEventDispatcher
{
    void Post(Action action);
}
```

配置 dispatcher 后，框架事件通过 dispatcher 触发：

```text
RoomFound / RoomUpdated / RoomLost
Session.StateChanged
Session.PeerJoined / PeerLeft / PeerReconnected
Messenger.MessageError
Stats.Updated
```

规则：

- 核心库不依赖 Godot、Unity、WPF、WinForms 等具体线程模型。
- 未配置 dispatcher 时，事件直接在当前网络任务上下文触发。
- 配置 dispatcher 时，事件必须通过 `Post` 投递，避免调用者手动跨线程处理。
- 发送 API、请求 API 和 Flow API 不依赖 dispatcher；dispatcher 只影响事件回调。
- 同一个 `GameNet` 实例内，由同一后台任务产生的事件必须保持投递顺序。例如 `PeerDisconnected` 不能晚于同一 peer 的 `PeerLeft`。
- dispatcher 的 `Post` 如果抛异常，核心库必须捕获并转为结构化 `EventDispatchError`，不能让网络读写循环崩溃。
- 事件回调本身抛异常时，也必须捕获并进入错误事件或日志；普通事件回调异常不能中断 session、transport 或后续事件投递。
- 核心库不要求 dispatcher 内部提供无限队列。若业务 dispatcher 队列已满，应由 dispatcher 抛出明确异常，核心库记录错误并丢弃该事件。

## Lifecycle And Concurrency

`GameNet` 必须明确生命周期和并发调用规则，避免连接状态和后台任务进入不可预测状态。

实例生命周期：

```text
Created
  -> Hosting / Connected
  -> Stopping
  -> Offline
  -> Disposed
```

规则：

- `HostAsync`、`StartServerAsync`、`JoinAsync` 互斥。实例已经 hosting、connected、connecting 或 stopping 时再次调用这些入口，应返回结构化错误或抛出 `InvalidOperationException`，不能隐式取消旧操作。
- `LeaveAsync` / `StopAsync` 可以在 offline 状态重复调用，视为 no-op。
- `DisposeAsync` 必须幂等，且会停止广告、停止 browser、取消 pending request、取消 pending flow、关闭 session、释放 transport。
- `DisposeAsync` 之后所有发送、连接、Discovery、Stats API 都应返回 `ObjectDisposed` 错误或抛出 `ObjectDisposedException`。
- `StartAdvertiseAsync` 同一实例同一时间只能有一个广告任务；重复调用应先要求业务显式 `StopAdvertiseAsync`，或提供明确的 replace 选项。
- `StartBrowserAsync` 可以允许多个 browser 实例，但每个 browser 自己独立维护快照并负责释放。
- `UpdateAdvertiseMetadataAsync` 在未广告时应返回明确错误，不应静默成功。
- 实现上必须使用内部 async gate / state lock 串行化 lifecycle 操作，包括 `HostAsync`、`StartServerAsync`、`JoinAsync`、`LeaveAsync`、`StopAsync`、`DisposeAsync`。状态检查和状态切换必须在同一临界区完成，避免两个入口同时观察到 `Offline` 后重复启动。
- 发送、事件回调和网络读循环不能长期持有 lifecycle gate，避免业务 handler 或慢发送阻塞停止流程。需要读取 session 状态时使用快照或短临界区。

后台任务关闭顺序：

```text
Stop accepting new operations
-> cancel discovery browser/advertise loops
-> cancel pending requests and flows
-> notify session disconnect/stop events
-> close transport
-> release resources
```

关闭时的 pending 操作结果：

- `RequestAsync` 返回 `Cancelled` 或 `SessionClosed`。
- `NetFlow` 返回 `FlowEndReason.Cancelled` 或 `SessionClosed`。
- `JoinAsync` / `TryJoinAsync` 返回 `Cancelled` 或 `TransportFailed`，取决于关闭发生的位置。

## Time And Clock

Timeout、重连、Discovery 房间过期、Stats loss 都依赖时间。核心库应使用可替换时钟，避免测试依赖真实时间。

推荐优先使用 .NET `TimeProvider`；如果项目需要兼容更简单的抽象，可以提供轻量包装：

```csharp
public interface INetClock
{
    DateTimeOffset UtcNow { get; }
    Task Delay(TimeSpan delay, CancellationToken token = default);
}
```

规则：

- 默认使用系统时间。
- 测试使用 fake clock 或 controllable clock。
- 所有 timeout、room expiration、reconnect delay、Stats probe window 都必须通过同一时钟来源。
- 不允许在核心逻辑里直接散落 `DateTime.UtcNow`、`Task.Delay`、`Stopwatch`，除非封装在 clock 实现内部。

## Transport

`INetTransport` 是最薄的底层接口，只认识监听、连接、断开、底层连接 ID、channel 和原始数据。它不能暴露会话层 `PeerId`，因为 `PeerId` 是认证和握手后由 `Session` 分配并维护的稳定身份；底层 socket、TCP client 或 Godot peer 只能表示当前物理连接。

底层连接使用独立强类型标识：

```csharp
public readonly record struct TransportConnectionId(ulong Value)
{
    public static readonly TransportConnectionId None = new(0);
}
```

示意接口：

```csharp
public interface INetTransport : IAsyncDisposable
{
    Task<TransportStartResult> StartServerAsync(NetListenOptions options, CancellationToken token = default);
    Task<TransportConnectResult> ConnectAsync(NetConnectOptions options, CancellationToken token = default);
    Task DisconnectAsync(TransportConnectionId connectionId, DisconnectReason reason = DisconnectReason.LocalClosed);
    ValueTask<NetSendResult> SendAsync(
        TransportConnectionId connectionId,
        ReadOnlyMemory<byte> data,
        NetChannel channel,
        CancellationToken token = default);

    event Action<TransportPeerConnected> PeerConnected;
    event Action<TransportPeerDisconnected> PeerDisconnected;
    event Action<TransportPacketReceived> PacketReceived;
    event Action<TransportError> Error;
}
```

Transport 事件 payload 必须只包含 transport 层信息，例如 `TransportConnectionId`、远端 endpoint、断开原因和原始 packet 数据。`PeerId`、认证状态、房间参与者状态由 `Session` 事件表达。

V1 内置：

- `TcpNetTransport`：基于 `System.Net.Sockets` 的 TCP loopback/局域网实现。TCP transport 使用 `[FrameLength][NetPacket bytes]` 解决粘包和半包问题。
- `MemoryNetTransport`：纯内存模拟，用于快速单元测试，可扩展模拟延迟、断线、丢包和乱序。

Transport 绑定地址应可配置，避免多网卡、VPN、虚拟网卡环境下行为不可控：

```csharp
public sealed class NetListenOptions
{
    public IPAddress? BindAddress { get; init; }
    public required int Port { get; init; }
    public int MaxConnections { get; init; } = 32;
}

public sealed class NetConnectOptions
{
    public required string Host { get; init; }
    public required int Port { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
}
```

默认 `BindAddress = null` 表示监听所有地址。Discovery 可以独立配置广播使用的网络接口；V1 至少要保留手动指定接口或 bind address 的扩展点。

`NetChannel` 是逻辑通道，不等于底层 transport 一定支持对应可靠性：

```text
System
  握手、认证、peer 列表、ping/pong、request error 等系统消息。

Reliable
  默认业务消息通道。

Unreliable
  未来 UDP/LiteNetLib transport 使用。TCP transport 下不支持真正不可靠发送。
```

TCP transport 的通道规则：

- `System` 和 `Reliable` 都可靠有序。
- `Unreliable` 默认返回 `ChannelUnsupported`，或在显式配置下退化为 reliable；默认不应悄悄退化，避免误导实时同步调用方。
- 后续 UDP transport 可以实现真正的 unreliable / unordered / sequenced 语义。

Transport 必须有发送背压策略，避免单个 peer 或 relay/broadcast 把内存打满。V1 至少需要：

```text
MaxPacketSize
MaxSendQueueBytesPerPeer
MaxSendQueuePacketsPerPeer
SendQueueFullPolicy
```

推荐默认策略：

- 系统消息优先级最高。
- 普通 reliable 消息在队列满时返回 `SendQueueFull`。
- 不自动无限等待，不无限增长内存。
- 对持续超限或恶意发送的 peer，服务端可以断开并返回 `RateLimited` / `SendQueueOverflow`。

这些限制应出现在 `SendAsync`、`BroadcastAsync`、`RelayAsync`、`RequestAsync` 和 `NetFlow` 的错误路径中。

发送和连接失败必须使用结构化结果，而不是只靠异常或日志：

```csharp
public readonly record struct NetSendResult(NetSendStatus Status, string? Message = null)
{
    public bool Succeeded => Status == NetSendStatus.Ok;
}

public readonly record struct TransportStartResult(NetTransportStatus Status, string? Message = null);
public readonly record struct TransportConnectResult(
    NetTransportStatus Status,
    TransportConnectionId ConnectionId,
    string? Message = null);

public enum NetSendStatus
{
    Ok,
    ObjectDisposed,
    SessionClosed,
    PeerUnavailable,
    ConnectionUnavailable,
    ChannelUnsupported,
    PacketTooLarge,
    SendQueueFull,
    RateLimited,
    TransportFailed
}

public enum NetTransportStatus
{
    Ok,
    ObjectDisposed,
    InvalidEndpoint,
    AlreadyRunning,
    ConnectionRefused,
    Timeout,
    Cancelled,
    TransportFailed
}
```

异常只用于编程错误，例如参数为空、非法端口、重复释放后的无效对象访问；网络、队列、通道和 peer 状态失败应走结果对象。

## Session

`NetSession` 负责连接管理。用户正常通过 `GameNet` 访问：

```csharp
await net.HostAsync(new HostOptions
{
    Port = 7777,
    MaxPeers = 8,
    ReconnectGrace = TimeSpan.FromSeconds(30),
    Authenticator = async ctx => AuthResult.Accept()
});

await net.JoinAsync(new JoinOptions
{
    Host = "127.0.0.1",
    Port = 7777,
    Timeout = TimeSpan.FromSeconds(5),
    Reconnect = ReconnectPolicy.FixedRetry(3)
}.WithAuthPayload(playerToken));
```

重连配置：

```csharp
public sealed class JoinOptions
{
    public string Host { get; init; }
    public int Port { get; init; }
    public byte[]? AuthPayload { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);

    // 默认不自动重连，业务必须显式开启。
    public ReconnectPolicy Reconnect { get; init; } = ReconnectPolicy.None;
}

public sealed class HostOptions
{
    public IPAddress? BindAddress { get; init; }
    public int Port { get; init; }
    public int MaxPeers { get; init; } = 8;
    public TimeSpan ReconnectGrace { get; init; } = TimeSpan.FromSeconds(30);
    public Func<AuthContext, Task<AuthResult>>? Authenticator { get; init; }
}

public sealed class ReconnectPolicy
{
    public static ReconnectPolicy None { get; }
    public static ReconnectPolicy FixedRetry(int attempts, TimeSpan interval);
    public static ReconnectPolicy ExponentialBackoff(
        int attempts,
        TimeSpan initialDelay,
        TimeSpan maxDelay);
}
```

重连规则：

- 客户端自动重连默认关闭，需要通过 `JoinOptions.Reconnect` 显式开启。
- Join 成功后，服务端返回 `PeerId` 和 `ReconnectToken`。
- 客户端异常断开且断开原因可重连时，Session 进入 `Reconnecting`。
- 客户端使用 `ReconnectToken` 自动尝试恢复连接。
- 服务端在 `HostOptions.ReconnectGrace` 内保留断线 peer 的 `PeerId`。
- 重连成功后恢复原 `PeerId`，客户端触发 `Reconnected`，服务端触发 `PeerReconnected(peerId)`。
- 重连失败后触发 `Disconnected(ReconnectFailed)`。

不自动重连的原因：

```text
UserClosed
Kicked
AuthenticationRejected
WrongPassword
IncompatibleApplication
IncompatibleProtocol
ServerClosed
Cancelled
```

重连不负责完整玩法状态恢复：

- 普通 `RequestAsync` 不恢复。
- 普通消息不重放。
- `NetFlow` pending proposal 不自动重发。
- 玩法状态同步由业务在 `Reconnected` / `PeerReconnected` 后处理。

Session 事件是低频控制面事件，只用于连接状态、peer 生命周期、重连和服务器生命周期，不用于高频 gameplay 数据。高频业务消息走 `Messenger`，未来实时同步走 Realtime 扩展。

事件分为三类。

本地连接状态事件：

```csharp
net.Session.StateChanged += e => { };
net.Session.JoinSucceeded += e => { };
net.Session.JoinFailed += e => { };
net.Session.Disconnected += e => { };
net.Session.Reconnecting += e => { };
net.Session.Reconnected += e => { };
net.Session.ReconnectFailed += e => { };
```

Peer 生命周期事件：

```csharp
net.Session.PeerJoined += e => { };
net.Session.PeerDisconnected += e => { };
net.Session.PeerReconnected += e => { };
net.Session.PeerLeft += e => { };
```

`PeerDisconnected` 表示 peer 暂时断线并进入 `ReconnectGrace`，仍可能回来。`PeerLeft` 表示 peer 已确认离开、被踢、超出 grace 或服务器决定移除，已经不再是有效 participant。

Host / server 生命周期事件：

```csharp
net.Session.HostStarted += e => { };
net.Session.HostFailed += e => { };
net.Session.ServerStarted += e => { };
net.Session.ServerStopped += e => { };
```

事件 payload 必须结构化且保持轻量：

```csharp
public readonly record struct SessionStateChangedEvent(
    SessionState OldState,
    SessionState NewState,
    DisconnectReason? Reason);

public readonly record struct PeerSessionEvent(
    PeerId PeerId,
    PeerStatus Status,
    DisconnectReason? Reason);

public readonly record struct ReconnectAttemptEvent(
    int Attempt,
    int MaxAttempts,
    TimeSpan NextDelay);
```

实现上可以内部统一走 `SessionEvent` 分发，再暴露强类型便利事件。配置 `INetEventDispatcher` 后，这些事件必须通过 dispatcher 投递。

Session 职责：

- Host / Client 状态切换。
- 维护稳定 `PeerId` 和当前连接。
- 握手与认证。
- 最大人数限制。
- 踢出和断开原因。
- 主动离开、服务端关闭、异常断开。
- 可选重连和 `PeerReconnected` 事件。
- 服务端权威 peer 列表广播。

状态集合保持简短：

```text
Offline
StartingHost
Hosting
Connecting
Connected
Reconnecting
Stopping
```

`PeerId` 是会话内稳定玩家 ID；底层 socket 或 transport connection ID 只是当前物理连接，不能作为业务身份。

公开 API 使用轻量强类型 peer 标识：

```csharp
public readonly record struct PeerId(ulong Value)
{
    public static readonly PeerId None = new(0);
    public static readonly PeerId Server = new(1);
}
```

`PeerId` 本质是数字 ID，但不能用裸 `long` / `ulong` 暴露给业务层，避免和 `RoomId`、`ConnectionId`、用户账号 ID 混用。`0` 保留为 `None` / invalid。`PeerId.Server = 1` 作为服务端稳定身份；普通客户端 peer id 从服务端分配。

`Session` 内部维护 `PeerId` 与 `TransportConnectionId` 的映射：

```text
TransportConnectionId
  临时物理连接，连接建立时由 transport 产生。

PeerId
  稳定会话身份，握手、兼容性检查和认证通过后由服务端分配。

ReconnectToken
  断线重连凭据，用于把新的 TransportConnectionId 重新绑定到旧 PeerId。
```

握手前的连接不能出现在 `PeerDirectory`，也不能接收业务消息。重连成功后，旧 `PeerId` 保持不变，内部映射替换到新的 `TransportConnectionId`。

Session 应维护一份 `PeerDirectory`，作为客户端和服务端都能读取的 session 通讯录。服务端/Host 维护权威目录并广播必要变化；客户端维护服务端同步来的只读目录，不能自行决定 peer 加入或离开。

`PeerDirectory` 只共享一份内部 peer 存储，对外提供主集合和常用过滤方法，不维护多份重复列表：

```csharp
public sealed class PeerDirectory
{
    public PeerId LocalPeerId { get; }
    public PeerId ServerPeerId { get; }
    public NetRole Role { get; }

    public IReadOnlyCollection<PeerInfo> Peers { get; }

    public PeerInfo? Get(PeerId peerId);
    public bool IsLocal(PeerId peerId);
    public bool IsServer(PeerId peerId);
    public IEnumerable<PeerInfo> Participants();
    public IEnumerable<PeerInfo> RemoteParticipants();
}
```

字段含义：

- `LocalPeerId`：当前本机在 session 内的 peer id。客户端是自己的 peer id；Host 是房主本地玩家 peer id；Dedicated Server 可使用 `PeerId.Server` 或 `PeerId.None`，具体取决于是否把纯服务器暴露为本地 peer。
- `ServerPeerId`：权威服务器 peer id，通常为 `PeerId.Server`。
- `Role`：当前本机角色，例如 `Host`、`DedicatedServer`、`Client`、`Offline`。
- `Peers`：当前 session 已知 peer 的只读集合，是唯一主视图。
- `Participants()`：游戏参与者视图，包含 Host 本地玩家、已加入客户端，以及仍在 grace 期内的断线玩家；不包含 dedicated server 自身、未认证连接和已彻底离开的 peer。
- `RemoteParticipants()`：从本机视角看，除本地参与者以外的参与者，常用于广播目标、Stats 查询和 UI 显示远端玩家状态。

`PeerInfo` 只放网络/session 层通用信息：

```csharp
public sealed class PeerInfo
{
    public PeerId PeerId { get; init; }
    public PeerStatus Status { get; init; }
    public bool IsParticipant { get; init; }
    public DateTimeOffset JoinedAt { get; init; }
    public DateTimeOffset LastSeenAt { get; init; }
}
```

这些字段是必要的最小集合：

- `PeerId`：发送、Flow target、Stats 查询和目录索引都需要的唯一标识。
- `Status`：表示 `Connected`、`DisconnectedGrace`、`Left`、`Kicked` 等 session 状态，供 UI、Flow、重连和 Stats 判断使用。
- `IsParticipant`：区分游戏参与者和服务器、观察者、未认证连接等非参与者，不能简单由 `!IsServer` 推导。
- `JoinedAt`：用于加入顺序、调试和轻量 UI 展示。
- `LastSeenAt`：用于连接质量、断线 grace、Stats 和调试。

`PeerInfo` 不包含昵称、角色、队伍、准备状态、房主标记等业务信息。这些应通过业务自定义 `ClientInfo`、`RoomState` 或普通消息同步，避免 Session 层承担玩法状态。

认证 payload 由业务定义，Session 只负责传输原始字节并调用服务端 `Authenticator`。这可以覆盖密码房、版本校验、玩家名重复、黑名单、mod 列表不一致等场景。Join 失败必须返回明确原因，而不是只给通用连接失败。

底层 `JoinOptions.AuthPayload` 使用 `byte[]?`，避免跨进程序列化时依赖运行时 `object` 类型信息。易用层可以提供 typed helper：

```csharp
public static JoinOptions WithAuthPayload<T>(
    this JoinOptions options,
    T payload,
    INetCodec? codec = null);
```

`AuthContext.GetPayload<T>()` 使用同一 codec 反序列化。反序列化失败必须返回 `AuthenticationRejected` 或业务指定拒绝原因，并携带明确错误信息。

服务端认证返回 `AuthResult`，客户端连接返回结构化 `JoinResult`。核心 API 优先提供 `TryJoinAsync`，`JoinAsync` 可以作为失败时抛异常的便捷包装：

```csharp
JoinResult result = await net.TryJoinAsync(options);

await net.JoinAsync(options);
```

Join 失败必须给出明确原因。错误码至少覆盖：

```text
Timeout
TransportFailed
IncompatibleApplication
IncompatibleProtocol
ServerFull
AuthenticationRejected
WrongPassword
Kicked
ServerClosed
Cancelled
UnknownError
```

`WrongPassword` 可以作为常用内置认证拒绝原因，但业务也可以使用自定义 reject code 或 message。

密码房示例：

```csharp
await net.HostAsync(new HostOptions
{
    Port = 7777,
    MaxPeers = 4,
    Authenticator = async ctx =>
    {
        var payload = ctx.GetPayload<JoinAuthPayload>();

        if (payload.Password != roomPassword)
            return AuthResult.Reject(AuthRejectReason.WrongPassword);

        return AuthResult.Accept();
    }
});

await net.JoinAsync(new JoinOptions
{
    Host = room.EndPoint.Address.ToString(),
    Port = room.Info.GamePort
}.WithAuthPayload(new JoinAuthPayload
{
    PlayerName = "Alice",
    Password = inputPassword
}));
```

Session 必须区分两种服务端入口：

```text
Host
  本机启动权威服务器，同时本地也作为一个 peer 参与游戏。
  适合局域网开房、小型联机、房主即玩家的场景。

Dedicated Server
  只启动权威服务器，不创建本地玩家 peer。
  适合专用服务器、自动化房间服务或测试服务端。
```

建议 API：

```csharp
await net.HostAsync(7777);
await net.StartServerAsync(7777);
await net.JoinAsync("127.0.0.1", 7777);
```

`HostAsync` 和 `StartServerAsync` 可以复用同一套 transport、session、messenger 和 flow 管线；区别只在于是否创建本地玩家身份，以及 `Peers` 是否包含本地 host player。

`HostOptions.BindAddress` 和 `Port` 会转换为 transport 层 `NetListenOptions`。`HostOptions.MaxPeers` 是认证通过后的参与者上限；`NetListenOptions.MaxConnections` 是握手前底层连接上限，两者不能混用。

## Messenger

消息层提供三种主要模型：

```text
On<T> + Send<T>
OnRequest<TRequest,TResponse> + RequestAsync<TRequest,TResponse>
底层支持 NetFlow 使用的 proposal request/response
```

消息定义使用可读 key：

```csharp
[NetMessage("room.player_ready")]
public readonly struct PlayerReady
{
    public readonly bool IsReady;
}
```

启动时按程序集自动扫描注册：

```csharp
net.Messages.RegisterAssembly(typeof(GameMessageMarker).Assembly);
```

新增协议只需要添加 `[NetMessage("...")]` 类型，不需要逐个 `Register<T>()`。手动注册仅用于测试或动态加载场景。

消息注册有明确冻结点：

- 所有 `RegisterAssembly`、手动消息注册、`net.On<T>`、attribute handler 注册都必须在 `HostAsync`、`StartServerAsync` 或 `JoinAsync` 之前完成。
- `GameNet` 进入 hosting、connected、connecting 或 reconnecting 后，消息表和 handler registry 冻结。继续注册应返回明确错误或抛出 `InvalidOperationException`，不能静默改变运行中 schema。
- `MessageSchemaFingerprint` 在冻结时计算，并参与 Session handshake。
- `LeaveAsync` / `StopAsync` 回到 offline 后不自动解冻消息表。若需要动态加载不同协议集合，建议创建新的 `GameNet` 实例，避免同一实例在连接生命周期中切换 schema。
- 测试可以使用显式 `FreezeMessages()` 触发冻结和冲突检查，便于不启动网络也能验证消息注册。

注册时检查：

- 重复 message key。
- 重复 message type。
- 系统保留 key 冲突，例如 `sys.*`。
- 生成后的数字 message id 冲突。
- 不支持序列化的消息类型。

收发示例：

```csharp
net.On<PlayerReady>((ctx, msg) =>
{
    Console.WriteLine($"{ctx.SenderId} ready: {msg.IsReady}");
});

await net.SendToServerAsync(new PlayerReady(true));
await net.SendAsync(peerId, new PlayerReady(true));
await net.BroadcastAsync(new PlayerReady(true));
await net.RelayAsync(peerId, new PrivateChatMessage("hi"));
```

发送语义：

```text
SendToServerAsync
  客户端发送给权威服务器。

SendAsync(peerId, message)
  服务端/Host 发送给指定 peer。客户端不能直接用它绕过服务器给其他客户端发消息。

BroadcastAsync
  服务端/Host 广播给多个 peer。

RelayAsync(peerId, message)
  客户端请求服务器转发给指定 peer。它是易用包装，不是 P2P。
```

`Relay` 保持权威服务器模型：客户端发送 relay request 到服务器，服务器校验发送者、目标和消息类型后，再把消息转发给目标 peer。目标 peer 收到消息时，`NetContext.SenderId` 应表示原始发送者，而不是服务器。服务器可以拒绝 relay，例如目标不存在、目标不是 participant、消息类型不允许 relay、发送者权限不足或超过速率限制。

所有发送 API 都返回 `ValueTask<NetSendResult>`。调用方可以忽略成功结果，但不能只能从日志里知道失败：

```csharp
NetSendResult result = await net.SendToServerAsync(new PlayerReady(true));
if (!result.Succeeded)
{
    ShowNetworkWarning(result.Status);
}
```

请求响应示例：

```csharp
net.OnRequest<JoinRoomRequest, JoinRoomResponse>(async (ctx, req) =>
{
    return new JoinRoomResponse(accepted: true, reason: "");
});

var response = await net.RequestAsync<JoinRoomRequest, JoinRoomResponse>(
    targetPeerId,
    new JoinRoomRequest("room-1"),
    TimeSpan.FromSeconds(3)
);
```

V1.1 的普通 `RequestAsync` 是短请求语义。断线或超时后请求失败；不做普通请求的断线恢复。

Request API 必须支持取消和 pending 清理：

```csharp
Task<RequestResult<TResponse>> RequestAsync<TRequest, TResponse>(
    PeerId target,
    TRequest request,
    TimeSpan timeout,
    CancellationToken token = default);
```

规则：

- 每个 request 分配唯一 `CorrelationId`，在同一 `GameNet` 实例生命周期内不能与未完成 request 冲突。
- `CorrelationId` 可以使用递增 `ulong`，但必须处理溢出和 pending 冲突；也可以使用随机值。
- timeout、cancellation、session closed、transport closed 都必须从 pending 表移除 request。
- late response 到达时，如果找不到 pending request，忽略并打 debug 日志。
- duplicate response 只处理第一份，后续响应忽略。
- request target 断线时，默认立即返回 `PeerUnavailable` 或 `SessionClosed`；普通 request 不等待重连。
- request handler 抛异常时返回 `HandlerException`，发送方不应只看到 timeout。

`NetFlow` 复用同一 correlation 机制，但 pending 表归 Flow 管理；Flow timeout/cancel/session closed 也必须清理所有目标 peer 的 pending 状态。

### Handler 注册

除了运行时显式绑定，Messenger 也支持 attribute 声明式绑定：

```csharp
public sealed class RoomHandlers
{
    [NetHandler]
    public void OnPlayerReady(NetContext ctx, PlayerReady msg)
    {
    }

    [NetRequestHandler]
    public JoinRoomResponse OnJoinRoom(NetContext ctx, JoinRoomRequest req)
    {
        return new JoinRoomResponse(true, "");
    }

    [NetFlowHandler]
    public async Task<LoadSceneAck> OnLoadScene(NetContext ctx, LoadSceneProposal proposal)
    {
        var ok = await LoadSceneAsync(proposal.SceneName);
        return new LoadSceneAck(ok, "");
    }
}
```

启动时注册 handler 容器或程序集：

```csharp
net.Handlers.Register(new RoomHandlers());
net.Handlers.RegisterAssembly(typeof(RoomHandlers).Assembly);
```

Attribute 绑定不是严格的编译期绑定；V1 通过启动时反射扫描实现。后续如果需要更强的构建期校验，可以增加 source generator 生成注册代码。运行时 `net.On<T>` 和 attribute handler 共用同一套 handler registry。

Attribute 扫描注册必须使用稳定顺序，例如按类型全名再按方法名排序。`[NetHandler]` 和 `net.On<T>` 共用同一 registry；谁先注册谁先执行。

Handler 不直接绑定发送端，而是绑定消息类型。发送者身份由 `NetContext` 提供：

```text
PlayerReady packet
  -> message id 解码为 PlayerReady
  -> 查找 PlayerReady 的所有 handler
  -> 调用 handler，并通过 ctx.SenderId 暴露发送者
```

签名规则必须保持严格：

```text
[NetHandler]
  void Handle(NetContext ctx, TMessage msg)
  Task Handle(NetContext ctx, TMessage msg)
  ValueTask Handle(NetContext ctx, TMessage msg)
  同一消息类型允许多个 handler，按注册顺序串行执行。

[NetRequestHandler]
  TResponse Handle(NetContext ctx, TRequest req)
  Task<TResponse> Handle(NetContext ctx, TRequest req)
  ValueTask<TResponse> Handle(NetContext ctx, TRequest req)
  同一 request 类型只能有一个 handler。

[NetFlowHandler]
  TAck Handle(NetContext ctx, TProposal proposal)
  Task<TAck> Handle(NetContext ctx, TProposal proposal)
  ValueTask<TAck> Handle(NetContext ctx, TProposal proposal)
  同一 proposal 类型只能有一个 handler。
```

V1 不支持复杂签名变体，例如直接注入 `GameNet`、`IServiceProvider` 或任意服务参数。网络 handler 默认都要求带 `NetContext`，保证 handler 能明确访问 sender、channel 和当前 session 角色。

Handler 异常规则：

```text
普通 Message handler 抛异常：
  记录日志并触发错误事件。
  不阻止同一消息的后续 handler 执行。

Request handler 抛异常：
  自动返回 RequestError.HandlerException。

Flow handler 抛异常：
  自动返回 FlowError.HandlerException，服务端按失败响应交给 policy 聚合。
```

无 handler 行为：

```text
普通 Message 无 handler：
  忽略或输出 debug 日志，不视为错误。

Request 无 handler：
  自动返回 RequestError.NoHandler，避免请求方只能等 timeout。

Flow Proposal 无 handler：
  自动返回 FlowError.NoHandler，服务端将其计入失败响应，由 policy 决定最终结果。
```

## Packet 和 Codec

所有普通消息、request/response、NetFlow 和系统消息共用一个轻量 envelope。

新实现不兼容旧 `ProtocolHandler`。旧的 `[Protocol]`、`guardValue`、基于 C# 类型哈希的协议编号、`PackData` / `HandleData` 都不进入新 API。新消息层使用 `[NetMessage("stable.key")]`、稳定 message id、统一 envelope 和可替换 `INetCodec`。如果后续需要迁移旧业务协议，应在业务层重写消息定义，而不是在新核心里保留兼容分支。

逻辑结构：

```text
NetPacket
  Version
  Flags
  MessageId
  CorrelationId optional
  PayloadLength
  Payload
```

建议字段：

```text
Magic/Version  1-2 bytes
Flags          1 byte
MessageId      4 bytes
CorrelationId  8 bytes，仅 request/response/flow 使用
PayloadLength  变长 int 或 int32
Payload        byte[]
```

开发者维护字符串 key，网络上传输数字 `MessageId`。`MessageId` 由 `[NetMessage("domain.name")]` 的稳定字符串 key 生成 `uint`。不使用 C# 类型全名作为唯一来源，避免重命名、移动命名空间或拆程序集破坏协议兼容。

Message id 规则：

```text
[NetMessage("room.player_ready")]
  -> stable hash uint
  -> packet MessageId
```

启动时必须检测：

- 重复 message key。
- 同一 message type 注册到多个 key。
- 不同 key 生成相同 id。
- request/response/flow 使用了未注册消息类型。

冲突直接启动失败。未来如果需要强版本兼容，可增加 `net-messages.json` manifest 固定 key 到 id 的映射。

除了 `ApplicationId` 和 `ProtocolVersion`，Messenger 可以计算消息表指纹，用于发现双方消息注册表不一致：

```text
MessageSchemaFingerprint =
  hash(sorted(message key, message id, message kind))
```

Handshake 可配置兼容策略：

```text
Strict
  双方 fingerprint 不一致则拒绝连接。

Warn
  记录警告但允许连接。适合开发期或只用公共子集的工具。

Ignore
  不检查消息表。只建议测试或特殊兼容场景使用。
```

V1 推荐默认 `Warn` 或 `Strict` 由 `GameNetOptions` 明确配置；不能在不记录任何信息的情况下忽略不一致。

默认 codec 使用 `System.Text.Json`，因为它是标准库、易调试、依赖少。保留替换接口：

```csharp
public interface INetCodec
{
    byte[] Serialize<T>(T message);
    object Deserialize(ReadOnlySpan<byte> data, Type type);
}
```

后续可以替换为 MessagePack、MemoryPack、protobuf 或自定义二进制 codec。

默认 JSON codec 的消息类型约束：

- 推荐使用 public record / class / struct，字段或属性必须能被 `System.Text.Json` 正常序列化。
- 推荐使用 public init 属性或 public set 属性；readonly fields、私有构造函数、复杂多态对象不作为默认推荐。
- 消息 payload 应保持小而明确，不要直接发送大型对象图。
- 版本演进时优先追加可选字段，避免删除或改变已有字段语义。
- unknown fields 由 JSON 默认行为忽略；missing fields 使用类型默认值。业务必须自己处理默认值是否有效。
- 如果需要严格 schema、required 字段或高性能二进制编码，应替换 `INetCodec`。

V1 流量保护：

- `MaxPacketSize`
- `MaxPendingRequests`
- 默认 request timeout
- 默认 flow timeout
- `MaxPeers`

V1 不做压缩、加密、分片和复杂 ack。

## NetFlow

V1.3 的 `NetFlow` 处理服务端发起的多人流程。它不是通用工作流引擎，只提供：

- `Propose`：服务器发起提案，等待目标客户端响应，按策略聚合结果。
- `Vote`：提案的语义化包装，用于投票或确认。
- `Barrier`：等待一组客户端到达某阶段，例如加载完成。

客户端注册响应：

```csharp
net.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>(async (ctx, proposal) =>
{
    var ok = await LoadSceneAsync(proposal.SceneName);
    return new LoadSceneAck(ok, "");
});
```

服务端发起：

```csharp
var result = await net.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
    targets: net.Peers.Clients,
    proposal: new LoadSceneProposal("Battle01"),
    policy: FlowPolicy.AllAccepted(),
    timeout: TimeSpan.FromSeconds(10)
);

if (result.Accepted)
{
    await net.BroadcastAsync(new StartGame());
}
```

内置策略：

- `FlowPolicy.AllAccepted()`
- `FlowPolicy.AnyAccepted()`
- `FlowPolicy.MajorityAccepted()`
- `FlowPolicy.Quorum(count)`
- `FlowPolicy.Custom(...)`

服务器始终拥有最终裁决权。Flow 结果只给出聚合结论，业务可以选择通过、失败或执行其他分支。

结果对象：

```csharp
public sealed class FlowResult<TResponse>
{
    public bool Accepted { get; }
    public FlowEndReason Reason { get; }
    public IReadOnlyDictionary<PeerId, FlowPeerResponse<TResponse>> Responses { get; }
    public IReadOnlySet<PeerId> TimeoutPeers { get; }
    public IReadOnlySet<PeerId> RejectedPeers { get; }
}
```

结束原因：

```text
Accepted
Rejected
Timeout
Cancelled
NoTargets
SessionClosed
```

断线规则：

- Flow 按稳定 `PeerId` 追踪目标。
- 目标 peer 断线不会立即让 Flow 失败。
- Flow 到 timeout 前一直等待响应。
- 重连后是否重发 pending proposal，由业务显式决定。
- 框架提供 pending 查询和手动重发 API。

示例：

```csharp
net.Session.PeerReconnected += peerId =>
{
    net.Flow.ResendPendingTo(peerId);
};
```

V1.3 不做客户端本地 pending 恢复、Flow 持久化、服务器重启恢复、嵌套 flow 或客户端发起多人 flow。

## Discovery

`NetDiscovery` 用于局域网房间发现，基于 UDP broadcast 或 multicast，不依赖 Godot。

Discovery 广播包必须有明确内部 envelope，业务层不需要重复传 `ApplicationId` 和 `ProtocolVersion`，但包内必须携带它们用于过滤和诊断：

```text
DiscoveryPacket
  Magic
  PacketVersion
  ApplicationId
  ProtocolVersion
  RoomId
  GamePort
  MetadataSchemaId
  PayloadLength
  MetadataPayload
```

规则：

- `Magic` 和 `PacketVersion` 用于快速排除非本库广播或未来不兼容包格式。
- `ApplicationId` 不匹配的包直接忽略。
- `ProtocolVersion` 不匹配的包可以忽略，也可以以 `IncompatibleProtocol` 标记展示，但不能当作可加入房间。
- `MetadataPayload` 大小受 `DiscoveryOptions.MaxPayloadSize` 限制。
- UDP 包超过限制时直接丢弃并记录诊断事件，不做分片。

Discovery 的业务可见基础数据结构只保留发现和连接真正必要的数据：

```csharp
public sealed class LanAdvertiseInfo
{
    public string RoomId { get; init; }
    public int GamePort { get; init; }
    public uint MetadataSchemaId { get; init; }
}
```

房间名、人数、密码标记、地图、模式等都属于业务展示或筛选信息，应通过公开 metadata 扩展：

```csharp
public sealed class RoomListMetadata
{
    public const string SchemaKey = "room.list.v1";
    public static readonly uint SchemaId = DiscoverySchema.Hash(SchemaKey);

    public string RoomName { get; init; }
    public int CurrentPlayers { get; init; }
    public int MaxPlayers { get; init; }
    public bool HasPassword { get; init; }
    public string MapName { get; init; }
    public string Mode { get; init; }
}
```

服务端广播：

```csharp
await net.Discovery.StartAdvertiseAsync(
    new LanAdvertiseInfo
    {
        RoomId = "room-001",
        GamePort = 7777,
        MetadataSchemaId = RoomListMetadata.SchemaId
    },
    new RoomListMetadata
    {
        RoomName = "Alice's Room",
        CurrentPlayers = 1,
        MaxPlayers = 4,
        HasPassword = true,
        MapName = "Forest",
        Mode = "Coop"
    });
```

客户端扫描：

```csharp
var rooms = await net.Discovery.ScanAsync<RoomListMetadata>(
    TimeSpan.FromSeconds(2));

foreach (var room in rooms)
{
    Console.WriteLine($"{room.Metadata.RoomName} {room.EndPoint} {room.LatencyMs}ms");
}
```

发现后仍然走正常连接：

```csharp
await net.JoinAsync(new JoinOptions
{
    Host = room.EndPoint.Address.ToString(),
    Port = room.Info.GamePort
}.WithAuthPayload(new JoinAuthPayload
{
    PlayerName = "Alice",
    Password = inputPassword
}));
```

Discovery 只负责公开广播。metadata 可以包含 `HasPassword` 这类展示字段，但不能包含真实密码、认证 token 或其他敏感信息。客户端看到 metadata 中的 `HasPassword = true` 后由 UI 提示用户输入密码，再通过 `JoinOptions.AuthPayload` 交给 Session 认证流程。

Metadata 规则：

- Metadata 是公开数据，只用于展示和筛选。
- Metadata 大小受 `DiscoveryOptions.MaxPayloadSize` 限制。
- Metadata 类型由业务定义，默认 codec 负责序列化。
- 不同游戏通过 `NetApplicationInfo.ApplicationId` 隔离；协议版本不匹配的房间可以过滤或标记不可加入。
- `MetadataSchemaId` 由 metadata 的稳定 schema key 生成 `uint`，例如 `[DiscoveryMetadata("room.list.v1")]` 或类型上的等价常量。它用于避免用错误 metadata 类型反序列化房间数据。
- 不使用 C# metadata 类型全名作为 schema 唯一来源，避免重命名破坏兼容。
- 启动或扫描注册时必须检测 schema key/id 冲突，冲突直接失败。
- 认证、安全和加入裁决仍由 Session 的 `Authenticator` 处理。

Discovery 提供两种客户端调用方式。

短扫一次，适合刷新按钮和测试：

```csharp
var rooms = await net.Discovery.ScanAsync<RoomListMetadata>(
    TimeSpan.FromSeconds(2));
```

持续浏览，适合大厅房间列表 UI：

```csharp
await using var browser = await net.Discovery.StartBrowserAsync<RoomListMetadata>(
    new DiscoveryBrowserOptions
    {
        RefreshInterval = TimeSpan.FromSeconds(1),
        RoomTimeout = TimeSpan.FromSeconds(5)
    });

browser.RoomFound += room => AddRoom(room);
browser.RoomUpdated += room => UpdateRoom(room);
browser.RoomLost += roomId => RemoveRoom(roomId);

var rooms = browser.GetRooms();
```

服务端广告也是持续过程：

```csharp
await net.Discovery.StartAdvertiseAsync(info, metadata);
await net.Discovery.UpdateAdvertiseMetadataAsync(updatedMetadata);
await net.Discovery.StopAdvertiseAsync();
```

`StartBrowserAsync` 维护一个本地房间快照：收到新的 room id 触发 `RoomFound`，同一 room id 的 metadata 或 endpoint 更新触发 `RoomUpdated`，超过 `RoomTimeout` 未再收到广播触发 `RoomLost`。离开大厅页面时释放 browser 即停止监听。

限制：

- 只保证局域网发现。
- 不解决公网 NAT 穿透。
- 可能受防火墙影响。
- 必须保留手动输入 IP 的兜底路径。

## Stats

`NetStats` 使用应用层 ping/pong 统计延迟，不依赖系统 ICMP ping。

示例：

```csharp
var latency = await net.Stats.GetLatencyAsync(peerId);
```

可统计：

- 当前 RTT。
- 平均 RTT。
- jitter。
- 应用层探测 loss。
- transport loss，如果底层 transport 支持。
- 超时次数。
- 最近一次收到包时间。

Discovery 扫描阶段也可以记录 discovery request/reply 的往返时间，作为房间列表延迟估算。

Loss 指标必须区分来源，避免在 TCP 下误导用户：

```csharp
public sealed class NetPeerStats
{
    public TimeSpan? Rtt { get; init; }
    public TimeSpan? AverageRtt { get; init; }
    public TimeSpan? Jitter { get; init; }

    // 应用层 ping/pong 超时率，TCP/UDP 都可支持。
    public double ProbeLoss { get; init; }

    // 真实传输层丢包率。TCP 下通常为 null；UDP/LiteNetLib 后续可填充。
    public double? TransportLoss { get; init; }

    public DateTimeOffset LastSeenAt { get; init; }
}
```

V1.2 实现 `ProbeLoss`：按最近窗口内应用层 ping/pong 的超时比例计算。`TcpNetTransport` 不暴露真实 packet loss，因此 `TransportLoss` 为 `null`。未来 UDP transport 可以提供真实发送、确认、重传和丢弃统计。

## Security Boundary

当前设计面向小型游戏和局域网易用性，不提供安全传输保证。必须明确这些边界：

- V1 TCP transport 不加密，不等同于 TLS。
- `AuthPayload`、业务消息、Discovery metadata 默认明文传输。
- Discovery metadata 是公开广播数据，不能包含密码、token、个人敏感信息或私有房间密钥。
- `ReconnectToken` 必须使用足够随机的不可预测值，并只在服务端 grace window 内有效。
- 密码房只能防止普通误入，不应被当作强安全边界。
- Relay 必须有 allowlist、权限检查或速率限制，避免客户端把服务器当作无限转发器。
- 服务端必须校验所有客户端请求；客户端 UI 限制不能作为安全依据。

未来如果需要公网或强安全，应新增 TLS/加密 transport、token 签名或平台认证集成，而不是把这些隐含在当前 TCP transport 里。

## Diagnostics

基础网络框架需要轻量诊断能力，否则连接、消息和 Discovery 问题很难定位。V1 提供只读诊断快照和基础错误事件，不做重型监控系统；后续 Request、Discovery、Stats、NetFlow 接入同一诊断模型。

```csharp
public sealed class NetDiagnosticsSnapshot
{
    public int ConnectedPeerCount { get; init; }
    public int PendingRequestCount { get; init; }
    public int PendingFlowCount { get; init; }
    public long PacketsSent { get; init; }
    public long PacketsReceived { get; init; }
    public long BytesSent { get; init; }
    public long BytesReceived { get; init; }
    public long DroppedPackets { get; init; }
    public long ErrorCount { get; init; }
}
```

规则：

- `GameNet.Diagnostics.GetSnapshot()` 返回当前轻量快照。
- 关键错误进入结构化错误事件，例如 `TransportError`、`MessageError`、`RequestError`、`FlowError`、`DiscoveryError`。
- 诊断事件同样遵守 `INetEventDispatcher`。
- 不要求 V1 接入外部日志框架，但应允许业务把日志/错误事件转接到自己的 logging 系统。
- DebugName 应出现在日志上下文中，便于区分同进程多个 `GameNet` 实例。

## SimpleFramework 集成

V1 不实现集成层。后续可以新增可选 adapter：

- `GameNet` 作为 `IUtility` 注册。
- `NetSystem` 负责初始化和释放。
- `NetModel` 缓存连接状态、peer 列表、延迟等只读数据。
- Domain events 桥接 `PeerJoinedEvent`、`PeerLeftEvent`、`SessionStateChangedEvent`、`MessageErrorEvent` 等。

核心原则是：Net Core 不依赖 SimpleFramework 生命周期；SimpleFramework 只包一层易用集成。

## 未来扩展

预测回滚和实时同步可以作为独立扩展层，不放入 V1 Net Core：

```text
Realtime Extension
  TickClock
  InputBuffer
  SnapshotBuffer
  PredictionClient
  RollbackDriver
  ServerReconciliation
```

当前设计需要为它们预留：

- 可替换 transport。
- `NetChannel`，未来支持 reliable/unreliable。
- 高频消息不依赖反射分发路径的优化空间。
- message payload 可携带 tick、sequence、state hash。
- codec 可替换为二进制格式。

## 测试计划

Session 测试：

- `HostAsync`、`StartServerAsync`、`JoinAsync` 互斥调用返回明确错误。
- `DisposeAsync` 幂等，并取消 pending request、flow、advertise 和 browser。
- Host 启动成功。
- `HostAsync` 创建本地玩家 peer。
- `StartServerAsync` 不创建本地玩家 peer。
- 公开 peer 标识使用 `PeerId`，`PeerId.None` 和 `PeerId.Server` 语义正确。
- `PeerDirectory` 在服务端维护权威目录，客户端维护同步来的只读目录。
- `PeerDirectory.Peers`、`Participants()`、`RemoteParticipants()` 来自同一份内部 peer 存储。
- `PeerInfo` 只包含 session 通用字段，不包含昵称、队伍、准备状态等业务信息。
- Client Join 成功。
- `TryJoinAsync` 返回结构化 `JoinResult`。
- 多 Client 加入。
- `PeerJoined` / `PeerLeft` 事件正确。
- `MaxPeers` 限制。
- 认证成功和失败。
- 密码房通过 `AuthPayload` 认证成功。
- 密码错误时 Join 返回明确 `WrongPassword` 拒绝原因。
- 应用不兼容和协议不兼容返回明确错误码。
- `JoinOptions.Reconnect` 默认不自动重连。
- 可重连断线进入 `Reconnecting` 并按策略重试。
- 重连成功恢复原 `PeerId`。
- 超过 `ReconnectGrace` 后原 `PeerId` 被释放。
- 不可重连原因不会触发自动重连。
- Kick 后客户端收到断开原因。
- Client 主动离开。
- Host 关闭后客户端断开。
- `StateChanged`、`Disconnected`、`Reconnecting`、`Reconnected`、`ReconnectFailed` 事件正确触发。
- `PeerDisconnected` 和 `PeerLeft` 能区分 grace window 内暂时断线与最终移除。
- Host/server 生命周期事件正确触发。

Messenger 测试：

- 自动扫描 `[NetMessage]`。
- 自动扫描 `[NetHandler]` / `[NetRequestHandler]` / `[NetFlowHandler]`。
- 重复 key 报错。
- 不同 key 生成相同 message id 时启动失败。
- 消息表 fingerprint 不一致时按配置 strict/warn/ignore 处理。
- 类型重命名不影响显式 message key 生成的 id。
- 默认 JSON codec 支持推荐消息形态，并对不支持类型给出明确错误。
- 普通消息允许多个 handler 且按注册顺序执行。
- Attribute handler 扫描顺序稳定。
- 普通消息 handler 异常不阻止后续 handler。
- request/flow 重复 handler 注册报错。
- request 无 handler 返回 `NoHandler` 错误。
- request handler 异常返回 `HandlerException` 错误。
- flow proposal 无 handler 返回 `NoHandler` 错误。
- flow handler 异常返回 `HandlerException` 错误。
- 普通 `SendToServerAsync`。
- 服务端 `SendAsync(peer)`。
- 服务端 `BroadcastAsync`。
- 客户端 `RelayAsync(peer)` 经服务器转发到目标 peer。
- `Relay` 保留原始发送者 `SenderId`。
- `Relay` 目标不存在或权限不足时返回明确错误。
- 未注册消息处理。
- `RequestAsync` 成功返回。
- `RequestAsync` 超时。
- `RequestAsync` cancellation 清理 pending 表。
- late response 和 duplicate response 被忽略且不污染 pending 表。
- handler 抛异常时返回错误或触发错误事件。
- 发送队列满时返回 `SendQueueFull`，不无限增长内存。
- TCP transport 下 `Unreliable` channel 默认返回 `ChannelUnsupported` 或按显式配置退化。

NetFlow 测试：

- `AllAccepted` 成功。
- 任意 peer 拒绝导致 `Rejected`。
- 部分 peer 超时导致 `Timeout`。
- `MajorityAccepted`。
- `Quorum`。
- `NoTargets`。
- flow 期间 peer 断线不立刻失败。
- timeout 前手动 `ResendPendingTo(peer)`。
- timeout 后 late response 被忽略。

Discovery 和 Stats 测试：

- Discovery 和 Stats timeout 使用可替换 clock，测试可用 fake clock 推进。
- UDP 广播房间可被扫描到。
- 扫描结果包含基础连接信息和端点。
- 扫描结果可以反序列化业务 metadata。
- `MetadataSchemaId` 由稳定 schema key 生成。
- metadata schema key/id 冲突启动失败。
- 密码房 metadata 可包含 `HasPassword`，但不包含真实密码。
- 不同 `ApplicationId` 不互相污染。
- 协议版本不兼容的广播不会被当作可加入房间。
- `StartBrowserAsync` 能触发 `RoomFound`、`RoomUpdated` 和 `RoomLost`。
- 应用层 ping/pong 返回 RTT。
- 应用层 ping/pong 超时会计入 `ProbeLoss`。
- TCP transport 下 `TransportLoss` 为 `null`。
- 超时 peer 返回失败结果。

Event dispatcher 测试：

- 未配置 dispatcher 时事件可在网络任务上下文触发。
- 配置 dispatcher 后 `RoomFound`、`RoomUpdated`、`RoomLost` 通过 dispatcher 投递。
- Session 和 Messenger 错误事件通过 dispatcher 投递。

Security 和 diagnostics 测试：

- Discovery metadata 不参与认证，密码仍通过 `AuthPayload` 校验。
- ReconnectToken 不可预测且超出 grace 后失效。
- `GameNet.Diagnostics.GetSnapshot()` 能返回 pending、peer、包计数和错误计数。
- 关键错误会触发结构化错误事件。

TCP loopback 测试：

- server/client 真实连接。
- 连续发送多包不会粘包错读。
- 较大消息可收发。
- bind address 配置可用于本地 loopback。
- 客户端异常断开被感知。

## 阶段完成标准

V1 完成后，应该能在 NUnit 中跑通以下本地示例：

```csharp
var server = new GameNet(new TcpNetTransport(), options);
var clientA = new GameNet(new TcpNetTransport(), options);
var clientB = new GameNet(new TcpNetTransport(), options);

await server.HostAsync(7777);
await clientA.JoinAsync("127.0.0.1", 7777);
await clientB.JoinAsync("127.0.0.1", 7777);

await clientA.SendToServerAsync(new PlayerReady(true));
await server.BroadcastAsync(new RoomStateChanged(...));
```

V1.1 完成后，应能跑通 request/response：

```csharp
server.OnRequest<JoinRoomRequest, JoinRoomResponse>((ctx, req) =>
    new JoinRoomResponse(true, ""));

var response = await clientA.RequestAsync<JoinRoomRequest, JoinRoomResponse>(
    PeerId.Server,
    new JoinRoomRequest("room-1"),
    TimeSpan.FromSeconds(3));
```

V1.2 完成后，应能通过 `Discovery` 扫描局域网房间，并通过 `Stats` 查询 peer 的应用层 RTT。

V1.3 完成后，应能跑通服务端多人 Flow：

```csharp
var server = new GameNet(new TcpNetTransport(), options);
var clientA = new GameNet(new TcpNetTransport(), options);
var clientB = new GameNet(new TcpNetTransport(), options);

await server.HostAsync(7777);
await clientA.JoinAsync("127.0.0.1", 7777);
await clientB.JoinAsync("127.0.0.1", 7777);

var result = await server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
    server.Peers.Clients,
    new LoadSceneProposal("Battle01"),
    FlowPolicy.AllAccepted(),
    TimeSpan.FromSeconds(5)
);
```
