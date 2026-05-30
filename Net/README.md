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

## 消息

消息类型应使用 `[NetMessage("stable.key")]` 标注稳定协议键。重命名 C# 类型时不要改变这个键，否则会破坏协议兼容性。

## 诊断

`GameNet.Diagnostics.GetSnapshot()` 返回只读计数快照，包括连接数、包计数、字节数、丢包数和错误数。
