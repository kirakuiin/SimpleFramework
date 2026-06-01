# SimpleFramework.Net

`SimpleFramework.Net` 提供轻量级多人游戏网络基础设施。新的入口是 `GameNet`，它组合了传输、会话、对等体目录、类型化消息和诊断能力。

## 最小示例

```csharp
using SimpleFramework.Net;

[NetMessage("player.ready")]
public sealed record PlayerReady(bool Ready);

var appId = Guid.Parse("2f2db4b5-4f54-47c1-b0e8-2f9e2fd7f741");
var options = new GameNetOptions
{
    Application = new NetApplicationInfo
    {
        ApplicationId = appId,
        ProtocolVersion = 1
    }
};

await using var server = new GameNet(new TcpNetTransport(), options);
await using var client = new GameNet(new TcpNetTransport(), options);

server.Messages.RegisterMessage<PlayerReady>();
client.Messages.RegisterMessage<PlayerReady>();

server.On<PlayerReady>((ctx, message) =>
{
    Console.WriteLine($"Peer {ctx.SenderId.Value} ready: {message.Ready}");
});

await server.HostAsync(new HostOptions { Port = 7777 });
await client.JoinAsync(new JoinOptions { Host = "127.0.0.1", Port = 7777 });
await client.SendToServerAsync(new PlayerReady(true));
```

## 传输

- `MemoryNetTransport` 用于确定性单元测试，不依赖真实 Socket。
- `TcpNetTransport` 用于 TCP loopback/LAN 场景，当前使用 `[FrameLength][Packet]` 帧格式。
- 默认不支持 `NetChannel.Unreliable`；调用方会收到 `ChannelUnsupported`。

## 会话

- `HostAsync` 启动主机模式，服务器自身也是本地参与者。
- `StartServerAsync` 启动专用服务器模式，不创建本地玩家参与者。
- `JoinAsync` 会校验 `ApplicationId` 和 `ProtocolVersion`，并可通过 `HostOptions.Authenticator` 校验认证载荷。
- 客户端自动重连默认关闭；需要时在 `JoinOptions.Reconnect` 中显式配置，例如 `ReconnectPolicy.FixedRetry(3, TimeSpan.FromSeconds(1))`。服务端仍需通过 `HostOptions.ReconnectPolicy` 开启重连宽限窗口。

## 消息

消息类型应使用 `[NetMessage("stable.key")]` 标注稳定协议键。重命名 C# 类型时不要改变这个键，否则会破坏协议兼容性。
所有会参与握手指纹的消息类型必须在 `HostAsync` / `StartServerAsync` / `JoinAsync` 前注册；会话启动后协议 manifest 会被冻结。启动后仍可以为已注册的消息追加 handler，但不能首次注册新的消息类型。

```csharp
[NetMessage("chat.line")]
public sealed record ChatLine(string Text);

server.On<ChatLine>((ctx, message) =>
{
    Console.WriteLine($"from {ctx.SenderId.Value}: {message.Text}");
});

await client.SendToServerAsync(new ChatLine("hello"));
await server.BroadcastAsync(new ChatLine("welcome"));
```

如果希望通过 attribute 扫描绑定处理器，可以使用 `RegisterAssemblyHandlers`。静态方法可直接绑定；实例方法需要提供目标实例或工厂。

```csharp
server.Messages.RegisterAssemblyHandlers(typeof(ChatHandlers).Assembly);
```

请求/响应适合一次性协商，不会在普通断线后自动恢复：

```csharp
[NetMessage("join.room.request")]
public sealed record JoinRoomRequest(string RoomId);

[NetMessage("join.room.response")]
public sealed record JoinRoomResponse(bool Accepted, string Reason);

server.OnRequest<JoinRoomRequest, JoinRoomResponse>((ctx, request) =>
    new JoinRoomResponse(request.RoomId == "room-1", ""));

var response = await client.RequestAsync<JoinRoomRequest, JoinRoomResponse>(
    PeerId.Server,
    new JoinRoomRequest("room-1"),
    TimeSpan.FromSeconds(2));
```

## 发现

LAN 发现只承载公开展示和筛选信息。不要把真实密码、token、私钥或私有房间 key 放入 metadata；可以放 `HasPassword` 这类公开标识。

```csharp
[DiscoveryMetadata("room.metadata.v1")]
public sealed record RoomMetadata(string RoomName, int CurrentPlayers, int MaxPlayers, bool HasPassword);

await using var server = new GameNet(new TcpNetTransport(), options);
await using var client = new GameNet(new TcpNetTransport(), options);

await server.HostAsync(new HostOptions { Port = 7777 });

var schemaId = DiscoveryMetadataRegistry.GetSchemaId("room.metadata.v1");
await server.Discovery.StartAdvertiseAsync(
    new LanAdvertiseInfo
    {
        RoomId = "room-1",
        GamePort = 7777,
        MetadataSchemaId = schemaId
    },
    new RoomMetadata("Test Room", 1, 4, false));

var rooms = await client.Discovery.ScanAsync<RoomMetadata>(TimeSpan.FromMilliseconds(200));
```

`GameNet` 默认使用 UDP broadcast 后端用于 LAN 发现。单元测试需要确定性行为时，可以注入共享的 `MemoryDiscoveryNetwork`：

```csharp
var discoveryNetwork = new MemoryDiscoveryNetwork();
var memoryNet = new MemoryNetNetwork();
await using var server = new GameNet(memoryNet.CreateTransport("server"), options, discoveryNetwork);
await using var client = new GameNet(memoryNet.CreateTransport("client"), options, discoveryNetwork);
```

连续浏览器会产生 found、updated、lost 事件；如果 `GameNetOptions.EventDispatcher` 已配置，这些事件会通过 dispatcher 投递。

## 统计

`NetStats` 使用应用层 ping/pong 估算 RTT、平均 RTT、抖动、探测丢失率和最近响应时间，不依赖 ICMP。

```csharp
var latency = await client.Stats.GetLatencyAsync(PeerId.Server, TimeSpan.FromSeconds(2));
if (latency.Status == NetStatsStatus.Ok)
{
    Console.WriteLine(latency.PeerStats.Rtt);
}
```

TCP 和内存传输当前无法提供传输层丢包率，因此 `TransportLoss` 保持为 `null`。

## Flow

Flow 是服务器拥有的多人协商流程。客户端不能发起 Flow；客户端只注册提案处理器并返回确认。

```csharp
[NetMessage("load.scene.proposal")]
public sealed record LoadSceneProposal(string SceneName);

[NetMessage("load.scene.ack")]
public sealed record LoadSceneAck(bool Accepted, string Reason);

server.Messages.RegisterMessage<LoadSceneProposal>();
server.Messages.RegisterMessage<LoadSceneAck>();
client.Messages.RegisterMessage<LoadSceneProposal>();
client.Messages.RegisterMessage<LoadSceneAck>();

client.Flow.OnProposal<LoadSceneProposal, LoadSceneAck>((ctx, proposal) =>
    new LoadSceneAck(Accepted: true, Reason: ""));

var result = await server.Flow.ProposeAsync<LoadSceneProposal, LoadSceneAck>(
    server.Peers.RemoteParticipantIds(),
    new LoadSceneProposal("Battle01"),
    FlowPolicy.AllAccepted(),
    TimeSpan.FromSeconds(5));
```

服务器可以查询 pending peer，并手动重发仍在等待的提案：

```csharp
foreach (var flowId in server.Flow.PendingFlowIds)
{
    foreach (var peerId in server.Flow.GetPendingPeers(flowId))
        await server.Flow.ResendPendingToAsync(flowId, peerId);
}
```

V1.3 明确不提供客户端侧 pending 恢复、Flow 持久化、服务器重启恢复、嵌套 Flow、客户端发起 Flow，也不会在客户端重连后自动 replay。需要恢复或重发时，由服务器显式查询 pending 并调用 `ResendPendingToAsync`。

## 诊断

`GameNet.Diagnostics.GetSnapshot()` 返回只读计数快照，包括连接数、包计数、字节数、丢包数和错误数。

## 安全边界

当前 TCP 传输和发现 metadata 不提供加密或隐私保护。需要保密的数据应放在应用自己的认证载荷或上层安全通道中，不应放入 LAN metadata。
