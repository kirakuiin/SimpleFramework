using System;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleFramework.Net;

namespace Test.Net;

[TestFixture]
public class NetTransportTests
{
    [Test]
    public async Task MemoryTransport_ClientConnectsToServer_AndServerReceivesPacket()
    {
        var network = new MemoryNetNetwork();
        await using var server = network.CreateTransport("server");
        await using var client = network.CreateTransport("client");

        var connected = new TaskCompletionSource<TransportConnectionId>();
        var received = new TaskCompletionSource<byte[]>();

        server.PeerConnected += e => connected.TrySetResult(e.ConnectionId);
        server.PacketReceived += e => received.TrySetResult(e.Data.ToArray());

        var start = await server.StartServerAsync(new NetListenOptions { Port = 7777 });
        var connect = await client.ConnectAsync(new NetConnectOptions { Host = "server", Port = 7777 });

        Assert.That(start.Status, Is.EqualTo(NetTransportStatus.Ok));
        Assert.That(connect.Status, Is.EqualTo(NetTransportStatus.Ok));

        await client.SendAsync(connect.ConnectionId, new byte[] { 1, 2, 3 }, NetChannel.Reliable);

        Assert.That(await connected.Task.WaitAsync(TimeSpan.FromSeconds(1)), Is.Not.EqualTo(TransportConnectionId.None));
        Assert.That(await received.Task.WaitAsync(TimeSpan.FromSeconds(1)), Is.EqualTo(new byte[] { 1, 2, 3 }));
    }

    [Test]
    public async Task MemoryTransport_Disconnect_NotifiesRemotePeer()
    {
        var network = new MemoryNetNetwork();
        await using var server = network.CreateTransport("server");
        await using var client = network.CreateTransport("client");
        var disconnected = new TaskCompletionSource<TransportPeerDisconnected>();

        server.PeerDisconnected += e => disconnected.TrySetResult(e);

        await server.StartServerAsync(new NetListenOptions { Port = 7777 });
        var connect = await client.ConnectAsync(new NetConnectOptions { Host = "server", Port = 7777 });

        await client.DisconnectAsync(connect.ConnectionId, DisconnectReason.LocalClosed);

        var disconnect = await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(disconnect.ConnectionId, Is.Not.EqualTo(TransportConnectionId.None));
        Assert.That(disconnect.Reason, Is.EqualTo(DisconnectReason.RemoteClosed));
    }

    [Test]
    public async Task MemoryTransport_UnreliableChannel_ReturnsChannelUnsupportedByDefault()
    {
        var network = new MemoryNetNetwork();
        await using var server = network.CreateTransport("server");
        await using var client = network.CreateTransport("client");

        await server.StartServerAsync(new NetListenOptions { Port = 7777 });
        var connect = await client.ConnectAsync(new NetConnectOptions { Host = "server", Port = 7777 });

        var result = await client.SendAsync(connect.ConnectionId, new byte[] { 1 }, NetChannel.Unreliable);

        Assert.That(result.Status, Is.EqualTo(NetSendStatus.ChannelUnsupported));
    }

    [Test]
    public async Task MemoryTransport_ApisAfterDispose_ReturnObjectDisposed()
    {
        var network = new MemoryNetNetwork();
        var transport = network.CreateTransport("server");

        await transport.DisposeAsync();

        var start = await transport.StartServerAsync(new NetListenOptions { Port = 7777 });
        var connect = await transport.ConnectAsync(new NetConnectOptions { Host = "server", Port = 7777 });
        var send = await transport.SendAsync(new TransportConnectionId(1), new byte[] { 1 }, NetChannel.Reliable);

        Assert.That(start.Status, Is.EqualTo(NetTransportStatus.ObjectDisposed));
        Assert.That(connect.Status, Is.EqualTo(NetTransportStatus.ObjectDisposed));
        Assert.That(send.Status, Is.EqualTo(NetSendStatus.ObjectDisposed));
    }
}
