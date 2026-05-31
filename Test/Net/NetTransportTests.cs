using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
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
    public async Task MemoryTransport_SendsMultiplePacketsInOrder()
    {
        var network = new MemoryNetNetwork();
        await using var server = network.CreateTransport("server");
        await using var client = network.CreateTransport("client");
        var packets = new List<byte[]>();
        var received = new TaskCompletionSource();

        server.PacketReceived += e =>
        {
            if (e.Data.Span[0] == 1)
                Thread.Sleep(200);

            lock (packets)
            {
                packets.Add(e.Data.ToArray());
                if (packets.Count == 2)
                    received.TrySetResult();
            }
        };

        await server.StartServerAsync(new NetListenOptions { Port = 7777 });
        var connect = await client.ConnectAsync(new NetConnectOptions { Host = "server", Port = 7777 });

        await client.SendAsync(connect.ConnectionId, new byte[] { 1 }, NetChannel.Reliable);
        await client.SendAsync(connect.ConnectionId, new byte[] { 2 }, NetChannel.Reliable);

        await received.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(packets[0], Is.EqualTo(new byte[] { 1 }));
        Assert.That(packets[1], Is.EqualTo(new byte[] { 2 }));
    }

    [Test]
    public async Task MemoryTransport_StartServerDuplicate_ReturnsAlreadyRunning()
    {
        var network = new MemoryNetNetwork();
        await using var first = network.CreateTransport("server");
        await using var second = network.CreateTransport("server");

        var firstStart = await first.StartServerAsync(new NetListenOptions { Port = 7777 });
        var secondStart = await second.StartServerAsync(new NetListenOptions { Port = 7777 });

        Assert.That(firstStart.Status, Is.EqualTo(NetTransportStatus.Ok));
        Assert.That(secondStart.Status, Is.EqualTo(NetTransportStatus.AlreadyRunning));
    }

    [Test]
    public async Task MemoryTransport_ConnectWithoutServer_ReturnsConnectionRefused()
    {
        var network = new MemoryNetNetwork();
        await using var client = network.CreateTransport("client");

        var connect = await client.ConnectAsync(new NetConnectOptions { Host = "missing", Port = 7777 });

        Assert.That(connect.Status, Is.EqualTo(NetTransportStatus.ConnectionRefused));
        Assert.That(connect.ConnectionId, Is.EqualTo(TransportConnectionId.None));
    }

    [Test]
    public async Task MemoryTransport_InvalidEndpointAndCancelledToken_ReturnClearStatus()
    {
        var network = new MemoryNetNetwork();
        await using var transport = network.CreateTransport("server");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var invalidStart = await transport.StartServerAsync(new NetListenOptions { Port = 0 });
        var invalidConnect = await transport.ConnectAsync(new NetConnectOptions { Host = "", Port = 7777 });
        var cancelledStart = await transport.StartServerAsync(new NetListenOptions { Port = 7777 }, cts.Token);
        var cancelledConnect = await transport.ConnectAsync(new NetConnectOptions { Host = "server", Port = 7777 }, cts.Token);
        var cancelledSend = await transport.SendAsync(new TransportConnectionId(99), new byte[] { 1 }, NetChannel.Reliable, cts.Token);

        Assert.That(invalidStart.Status, Is.EqualTo(NetTransportStatus.InvalidEndpoint));
        Assert.That(invalidConnect.Status, Is.EqualTo(NetTransportStatus.InvalidEndpoint));
        Assert.That(cancelledStart.Status, Is.EqualTo(NetTransportStatus.Cancelled));
        Assert.That(cancelledConnect.Status, Is.EqualTo(NetTransportStatus.Cancelled));
        Assert.That(cancelledSend.Status, Is.EqualTo(NetSendStatus.TransportFailed));
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
    public async Task MemoryTransport_SendToMissingConnection_ReturnsConnectionUnavailable()
    {
        var network = new MemoryNetNetwork();
        await using var transport = network.CreateTransport("client");

        var result = await transport.SendAsync(new TransportConnectionId(42), new byte[] { 1 }, NetChannel.Reliable);

        Assert.That(result.Status, Is.EqualTo(NetSendStatus.ConnectionUnavailable));
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

    [Test]
    public async Task TcpTransport_SendsMultiplePacketsWithoutFrameCorruption()
    {
        await using var server = new TcpNetTransport();
        await using var client = new TcpNetTransport();
        var packets = new List<byte[]>();
        var received = new TaskCompletionSource();

        server.PacketReceived += e =>
        {
            packets.Add(e.Data.ToArray());
            if (packets.Count == 3)
                received.TrySetResult();
        };

        var start = await server.StartServerAsync(new NetListenOptions
        {
            BindAddress = System.Net.IPAddress.Loopback,
            Port = 0
        });
        var port = server.LocalEndPoint!.Port;
        var connect = await client.ConnectAsync(new NetConnectOptions { Host = "127.0.0.1", Port = port });

        Assert.That(start.Status, Is.EqualTo(NetTransportStatus.Ok));
        Assert.That(connect.Status, Is.EqualTo(NetTransportStatus.Ok));

        await client.SendAsync(connect.ConnectionId, new byte[] { 1 }, NetChannel.Reliable);
        await client.SendAsync(connect.ConnectionId, new byte[] { 2, 2 }, NetChannel.Reliable);
        await client.SendAsync(connect.ConnectionId, new byte[] { 3, 3, 3 }, NetChannel.Reliable);

        await received.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.That(packets[0], Is.EqualTo(new byte[] { 1 }));
        Assert.That(packets[1], Is.EqualTo(new byte[] { 2, 2 }));
        Assert.That(packets[2], Is.EqualTo(new byte[] { 3, 3, 3 }));
    }

    [Test]
    public async Task TcpTransport_InvalidEndpointAndRepeatedStart_ReturnClearStatus()
    {
        await using var server = new TcpNetTransport();

        var invalid = await server.StartServerAsync(new NetListenOptions { Port = -1 });
        var start = await server.StartServerAsync(new NetListenOptions
        {
            BindAddress = IPAddress.Loopback,
            Port = 0
        });
        var repeated = await server.StartServerAsync(new NetListenOptions
        {
            BindAddress = IPAddress.Loopback,
            Port = 0
        });

        Assert.That(invalid.Status, Is.EqualTo(NetTransportStatus.InvalidEndpoint));
        Assert.That(start.Status, Is.EqualTo(NetTransportStatus.Ok));
        Assert.That(repeated.Status, Is.EqualTo(NetTransportStatus.AlreadyRunning));
    }

    [Test]
    public async Task TcpTransport_ConnectToUnusedPort_ReturnsConnectionRefused()
    {
        await using var client = new TcpNetTransport();
        var port = GetUnusedLoopbackPort();

        var result = await client.ConnectAsync(new NetConnectOptions { Host = "127.0.0.1", Port = port });

        Assert.That(result.Status, Is.EqualTo(NetTransportStatus.ConnectionRefused));
        Assert.That(result.ConnectionId, Is.EqualTo(TransportConnectionId.None));
    }

    [Test]
    public async Task TcpTransport_CancelledToken_ReturnsCancelled()
    {
        await using var server = new TcpNetTransport();
        await using var client = new TcpNetTransport();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var start = await server.StartServerAsync(new NetListenOptions { Port = 0 }, cts.Token);
        var connect = await client.ConnectAsync(new NetConnectOptions { Host = "127.0.0.1", Port = 1 }, cts.Token);

        Assert.That(start.Status, Is.EqualTo(NetTransportStatus.Cancelled));
        Assert.That(connect.Status, Is.EqualTo(NetTransportStatus.Cancelled));
    }

    [Test]
    public async Task TcpTransport_SendsLargePacket()
    {
        await using var server = new TcpNetTransport();
        await using var client = new TcpNetTransport();
        var received = new TaskCompletionSource<byte[]>();
        var payload = Enumerable.Range(0, 16 * 1024).Select(i => (byte)(i % 251)).ToArray();

        server.PacketReceived += e => received.TrySetResult(e.Data.ToArray());

        await server.StartServerAsync(new NetListenOptions
        {
            BindAddress = System.Net.IPAddress.Loopback,
            Port = 0
        });
        var connect = await client.ConnectAsync(new NetConnectOptions { Host = "127.0.0.1", Port = server.LocalEndPoint!.Port });

        await client.SendAsync(connect.ConnectionId, payload, NetChannel.Reliable);

        Assert.That(await received.Task.WaitAsync(TimeSpan.FromSeconds(2)), Is.EqualTo(payload));
    }

    [Test]
    public async Task TcpTransport_RejectsFrameHeaderLargerThanConfiguredLimit()
    {
        await using var server = new TcpNetTransport(maxFrameSize: 16);
        using var client = new TcpClient();
        var connected = new TaskCompletionSource<TransportConnectionId>();
        var disconnected = new TaskCompletionSource<TransportPeerDisconnected>();
        var error = new TaskCompletionSource<TransportError>();

        server.PeerConnected += e => connected.TrySetResult(e.ConnectionId);
        server.PeerDisconnected += e => disconnected.TrySetResult(e);
        server.Error += e => error.TrySetResult(e);

        await server.StartServerAsync(new NetListenOptions
        {
            BindAddress = IPAddress.Loopback,
            Port = 0
        });
        await client.ConnectAsync(IPAddress.Loopback, server.LocalEndPoint!.Port);
        await connected.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await client.GetStream().WriteAsync(BitConverter.GetBytes(1024));

        Assert.That((await error.Task.WaitAsync(TimeSpan.FromSeconds(2))).Message, Does.Contain("Frame length"));
        Assert.That((await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(2))).Reason, Is.EqualTo(DisconnectReason.TransportFailed));
    }

    [Test]
    public async Task TcpTransport_UnreliableChannel_ReturnsChannelUnsupportedByDefault()
    {
        await using var server = new TcpNetTransport();
        await using var client = new TcpNetTransport();

        await server.StartServerAsync(new NetListenOptions
        {
            BindAddress = System.Net.IPAddress.Loopback,
            Port = 0
        });
        var connect = await client.ConnectAsync(new NetConnectOptions { Host = "127.0.0.1", Port = server.LocalEndPoint!.Port });

        var result = await client.SendAsync(connect.ConnectionId, new byte[] { 1 }, NetChannel.Unreliable);

        Assert.That(result.Status, Is.EqualTo(NetSendStatus.ChannelUnsupported));
    }

    [Test]
    public async Task TcpTransport_SendToMissingConnection_ReturnsConnectionUnavailable()
    {
        await using var client = new TcpNetTransport();

        var result = await client.SendAsync(new TransportConnectionId(42), new byte[] { 1 }, NetChannel.Reliable);

        Assert.That(result.Status, Is.EqualTo(NetSendStatus.ConnectionUnavailable));
    }

    [Test]
    public async Task TcpTransport_ApisAfterDispose_ReturnObjectDisposed()
    {
        var transport = new TcpNetTransport();

        await transport.DisposeAsync();

        var start = await transport.StartServerAsync(new NetListenOptions { Port = 0 });
        var connect = await transport.ConnectAsync(new NetConnectOptions { Host = "127.0.0.1", Port = 1 });
        var send = await transport.SendAsync(new TransportConnectionId(1), new byte[] { 1 }, NetChannel.Reliable);

        Assert.That(start.Status, Is.EqualTo(NetTransportStatus.ObjectDisposed));
        Assert.That(connect.Status, Is.EqualTo(NetTransportStatus.ObjectDisposed));
        Assert.That(send.Status, Is.EqualTo(NetSendStatus.ObjectDisposed));
    }

    [Test]
    public async Task TcpTransport_ClientDispose_NotifiesServerDisconnect()
    {
        await using var server = new TcpNetTransport();
        var client = new TcpNetTransport();
        var disconnected = new TaskCompletionSource<TransportPeerDisconnected>();

        server.PeerDisconnected += e => disconnected.TrySetResult(e);

        await server.StartServerAsync(new NetListenOptions
        {
            BindAddress = System.Net.IPAddress.Loopback,
            Port = 0
        });
        await client.ConnectAsync(new NetConnectOptions { Host = "127.0.0.1", Port = server.LocalEndPoint!.Port });

        await client.DisposeAsync();

        var result = await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(result.Reason, Is.EqualTo(DisconnectReason.RemoteClosed));
    }

    [Test]
    public async Task TcpTransport_ServerDisconnect_NotifiesClientDisconnect()
    {
        await using var server = new TcpNetTransport();
        await using var client = new TcpNetTransport();
        var connected = new TaskCompletionSource<TransportConnectionId>();
        var disconnected = new TaskCompletionSource<TransportPeerDisconnected>();

        server.PeerConnected += e => connected.TrySetResult(e.ConnectionId);
        client.PeerDisconnected += e => disconnected.TrySetResult(e);

        await server.StartServerAsync(new NetListenOptions
        {
            BindAddress = IPAddress.Loopback,
            Port = 0
        });
        await client.ConnectAsync(new NetConnectOptions { Host = "127.0.0.1", Port = server.LocalEndPoint!.Port });
        var serverConnection = await connected.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await server.DisconnectAsync(serverConnection, DisconnectReason.ServerClosed);

        var result = await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(result.Reason, Is.EqualTo(DisconnectReason.RemoteClosed));
    }

    private static int GetUnusedLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
