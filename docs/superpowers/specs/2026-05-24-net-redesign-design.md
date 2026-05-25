# Net 模块重设计

## 背景

当前 `Net` 模块已经能在 Godot 平台跑通连接、协议分发和客户端信息同步，但连接管理、协议处理、认证、重连、peer 列表和同步逻辑耦合较紧。新设计不以兼容旧 API 为目标，而是从小型多人游戏的真实使用场景出发，重新设计一套易用、易理解、易维护、可扩展的网络会话库。

初版不绑定 Godot，也不依赖 SimpleFramework 的 `Domain`、`Model`、`System` 生命周期。后续可以增加 SimpleFramework 集成层，但核心网络库必须能独立测试和运行。

## 目标

- 简化小型游戏的连接管理、消息收发和多人流程协调。
- 采用权威服务器模型：客户端连接服务器，服务器裁决房间、认证、消息广播和多人流程结果。
- 底层传输可替换，不绑定 Godot。
- 提供可本地测试的 `TcpNetTransport` 和确定性测试用的 `MemoryNetTransport`。
- 支持局域网房间发现和应用层延迟检测。
- 为后续实时同步、预测回滚、Godot transport、SimpleFramework 集成预留边界，但初版不实现这些高级能力。

## 非目标

初版不实现以下能力：

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

## 总体架构

对外主入口是 `GameNet`。常用 API 直接挂在 `GameNet` 上，高级能力通过子组件访问：

```csharp
var net = new GameNet(new TcpNetTransport(), options);

await net.HostAsync(7777);
await net.JoinAsync("127.0.0.1", 7777);

net.On<PlayerReady>((ctx, msg) => { });
net.SendToServer(new PlayerReady(true));
net.Broadcast(new RoomStateChanged(...));

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

`Transport` 不知道业务消息。`Session` 不知道具体业务协议。`NetFlow` 基于 `Messenger` 的 request/response 实现，不自建底层协议。

## Transport

`INetTransport` 是最薄的底层接口，只认识连接、断开、peer、channel 和原始数据。

示意接口：

```csharp
public interface INetTransport : IAsyncDisposable
{
    Task StartServerAsync(int port, CancellationToken token = default);
    Task ConnectAsync(string host, int port, CancellationToken token = default);
    Task DisconnectAsync(DisconnectReason reason = DisconnectReason.LocalClosed);
    ValueTask SendAsync(PeerId peerId, ReadOnlyMemory<byte> data, NetChannel channel, CancellationToken token = default);

    event Action<TransportPeerConnected> PeerConnected;
    event Action<TransportPeerDisconnected> PeerDisconnected;
    event Action<TransportPacketReceived> PacketReceived;
    event Action<TransportError> Error;
}
```

初版内置：

- `TcpNetTransport`：基于 `System.Net.Sockets` 的 TCP loopback/局域网实现。TCP transport 使用 `[FrameLength][NetPacket bytes]` 解决粘包和半包问题。
- `MemoryNetTransport`：纯内存模拟，用于快速单元测试，可扩展模拟延迟、断线、丢包和乱序。

## Session

`NetSession` 负责连接管理。用户正常通过 `GameNet` 访问：

```csharp
await net.HostAsync(new HostOptions
{
    Port = 7777,
    MaxPeers = 8,
    Authenticator = async ctx => AuthResult.Accept()
});

await net.JoinAsync(new JoinOptions
{
    Host = "127.0.0.1",
    Port = 7777,
    AuthPayload = playerToken,
    Timeout = TimeSpan.FromSeconds(5),
    Reconnect = ReconnectPolicy.FixedRetry(3)
});
```

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

net.SendToServer(new PlayerReady(true));
net.Send(peerId, new PlayerReady(true));
net.Broadcast(new PlayerReady(true));
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

普通 `RequestAsync` 是短请求语义。断线或超时后请求失败；初版不做普通请求的断线恢复。

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

Attribute 绑定不是严格的编译期绑定；第一版通过启动时反射扫描实现。后续如果需要更强的构建期校验，可以增加 source generator 生成注册代码。运行时 `net.On<T>` 和 attribute handler 共用同一套 handler registry。

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
  同一消息类型允许多个 handler，按注册顺序执行。

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

第一版不支持复杂签名变体，例如直接注入 `GameNet`、`IServiceProvider` 或任意服务参数。网络 handler 默认都要求带 `NetContext`，保证 handler 能明确访问 sender、channel 和当前 session 角色。

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

开发者维护字符串 key，网络上传输数字 `MessageId`。第一版使用启动时稳定 hash 生成 `uint`，并做冲突检查。未来如果需要跨版本强兼容，可增加 `net-messages.json` manifest 固定 key 到 id 的映射。

默认 codec 使用 `System.Text.Json`，因为它是标准库、易调试、依赖少。保留替换接口：

```csharp
public interface INetCodec
{
    byte[] Serialize<T>(T message);
    object Deserialize(ReadOnlySpan<byte> data, Type type);
}
```

后续可以替换为 MessagePack、MemoryPack、protobuf 或自定义二进制 codec。

第一版流量保护：

- `MaxPacketSize`
- `MaxPendingRequests`
- 默认 request timeout
- 默认 flow timeout
- `MaxPeers`

初版不做压缩、加密、分片和复杂 ack。

## NetFlow

`NetFlow` 处理服务端发起的多人流程。它不是通用工作流引擎，初版只提供：

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
    net.Broadcast(new StartGame());
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

初版不做客户端本地 pending 恢复、Flow 持久化、服务器重启恢复、嵌套 flow 或客户端发起多人 flow。

## Discovery

`NetDiscovery` 用于局域网房间发现，基于 UDP broadcast 或 multicast，不依赖 Godot。

服务端广播：

```csharp
await net.Discovery.StartAdvertiseAsync(new LanAdvertiseInfo
{
    GameId = "my-game",
    RoomName = "Alice's Room",
    Port = 7777,
    MaxPlayers = 4,
    CurrentPlayers = 1,
    ProtocolVersion = 1
});
```

客户端扫描：

```csharp
var rooms = await net.Discovery.ScanAsync(TimeSpan.FromSeconds(2));

foreach (var room in rooms)
{
    Console.WriteLine($"{room.RoomName} {room.EndPoint} {room.LatencyMs}ms");
}
```

发现后仍然走正常连接：

```csharp
await net.JoinAsync(room.EndPoint.Address.ToString(), room.Port);
```

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

第一版实现 `ProbeLoss`：按最近窗口内应用层 ping/pong 的超时比例计算。`TcpNetTransport` 不暴露真实 packet loss，因此 `TransportLoss` 为 `null`。未来 UDP transport 可以提供真实发送、确认、重传和丢弃统计。

## SimpleFramework 集成

初版不实现集成层。后续可以新增可选 adapter：

- `GameNet` 作为 `IUtility` 注册。
- `NetSystem` 负责初始化和释放。
- `NetModel` 缓存连接状态、peer 列表、延迟等只读数据。
- Domain events 桥接 `PeerJoinedEvent`、`PeerLeftEvent`、`SessionStateChangedEvent`、`MessageErrorEvent` 等。

核心原则是：Net Core 不依赖 SimpleFramework 生命周期；SimpleFramework 只包一层易用集成。

## 未来扩展

预测回滚和实时同步可以作为独立扩展层，不放入初版 Net Core：

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

- Host 启动成功。
- `HostAsync` 创建本地玩家 peer。
- `StartServerAsync` 不创建本地玩家 peer。
- Client Join 成功。
- 多 Client 加入。
- `PeerJoined` / `PeerLeft` 事件正确。
- `MaxPeers` 限制。
- 认证成功和失败。
- Kick 后客户端收到断开原因。
- Client 主动离开。
- Host 关闭后客户端断开。

Messenger 测试：

- 自动扫描 `[NetMessage]`。
- 自动扫描 `[NetHandler]` / `[NetRequestHandler]` / `[NetFlowHandler]`。
- 重复 key 报错。
- 普通消息允许多个 handler 且按注册顺序执行。
- request/flow 重复 handler 注册报错。
- request 无 handler 返回 `NoHandler` 错误。
- flow proposal 无 handler 返回 `NoHandler` 错误。
- 普通 `SendToServer`。
- 服务端 `Send(peer)`。
- 服务端 `Broadcast`。
- 未注册消息处理。
- `RequestAsync` 成功返回。
- `RequestAsync` 超时。
- handler 抛异常时返回错误或触发错误事件。

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

- UDP 广播房间可被扫描到。
- 扫描结果包含房间信息和端点。
- 不同 `GameId` 不互相污染。
- 应用层 ping/pong 返回 RTT。
- 应用层 ping/pong 超时会计入 `ProbeLoss`。
- TCP transport 下 `TransportLoss` 为 `null`。
- 超时 peer 返回失败结果。

TCP loopback 测试：

- server/client 真实连接。
- 连续发送多包不会粘包错读。
- 较大消息可收发。
- 客户端异常断开被感知。

## 第一版完成标准

第一版完成后，应该能在 NUnit 中跑通以下本地示例：

```csharp
var server = new GameNet(new TcpNetTransport(), options);
var clientA = new GameNet(new TcpNetTransport(), options);
var clientB = new GameNet(new TcpNetTransport(), options);

await server.HostAsync(7777);
await clientA.JoinAsync("127.0.0.1", 7777);
await clientB.JoinAsync("127.0.0.1", 7777);

clientA.SendToServer(new PlayerReady(true));

var result = await server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
    server.Peers.Clients,
    new LoadSceneProposal("Battle01"),
    FlowPolicy.AllAccepted(),
    TimeSpan.FromSeconds(5)
);
```

并且能通过 `Discovery` 扫描局域网房间，通过 `Stats` 查询 peer 的应用层 RTT。
