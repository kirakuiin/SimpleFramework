using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleFramework.Net;
using Test.Net.TestDoubles;

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
    public async Task MemoryTransport_ThrowingConnectedHandler_DoesNotBreakConnect()
    {
        var network = new MemoryNetNetwork();
        await using var server = network.CreateTransport("server");
        await using var client = network.CreateTransport("client");
        var laterHandlerRan = false;
        server.PeerConnected += _ => throw new InvalidOperationException("handler failed");
        server.PeerConnected += _ => laterHandlerRan = true;

        await server.StartServerAsync(new NetListenOptions { Port = 7777 });
        var connect = await client.ConnectAsync(new NetConnectOptions { Host = "server", Port = 7777 });

        Assert.That(connect.Status, Is.EqualTo(NetTransportStatus.Ok));
        Assert.That(laterHandlerRan, Is.True);
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
        var invalidHighPort = await transport.StartServerAsync(new NetListenOptions { Port = 65536 });
        var invalidMaxConnections = await transport.StartServerAsync(new NetListenOptions { Port = 7777, MaxConnections = 0 });
        var invalidConnect = await transport.ConnectAsync(new NetConnectOptions { Host = "", Port = 7777 });
        var invalidHighPortConnect = await transport.ConnectAsync(new NetConnectOptions { Host = "server", Port = 65536 });
        var cancelledStart = await transport.StartServerAsync(new NetListenOptions { Port = 7777 }, cts.Token);
        var cancelledConnect = await transport.ConnectAsync(new NetConnectOptions { Host = "server", Port = 7777 }, cts.Token);
        var cancelledSend = await transport.SendAsync(new TransportConnectionId(99), new byte[] { 1 }, NetChannel.Reliable, cts.Token);

        Assert.That(invalidStart.Status, Is.EqualTo(NetTransportStatus.InvalidEndpoint));
        Assert.That(invalidHighPort.Status, Is.EqualTo(NetTransportStatus.InvalidEndpoint));
        Assert.That(invalidMaxConnections.Status, Is.EqualTo(NetTransportStatus.InvalidEndpoint));
        Assert.That(invalidConnect.Status, Is.EqualTo(NetTransportStatus.InvalidEndpoint));
        Assert.That(invalidHighPortConnect.Status, Is.EqualTo(NetTransportStatus.InvalidEndpoint));
        Assert.That(cancelledStart.Status, Is.EqualTo(NetTransportStatus.Cancelled));
        Assert.That(cancelledConnect.Status, Is.EqualTo(NetTransportStatus.Cancelled));
        Assert.That(cancelledSend.Status, Is.EqualTo(NetSendStatus.TransportFailed));
    }

    [Test]
    public async Task MemoryTransport_MaxConnections_RefusesAdditionalClient()
    {
        var network = new MemoryNetNetwork();
        await using var server = network.CreateTransport("server");
        await using var first = network.CreateTransport("first");
        await using var second = network.CreateTransport("second");

        await server.StartServerAsync(new NetListenOptions { Port = 7777, MaxConnections = 1 });
        var firstConnect = await first.ConnectAsync(new NetConnectOptions { Host = "server", Port = 7777 });
        var secondConnect = await second.ConnectAsync(new NetConnectOptions { Host = "server", Port = 7777 });

        Assert.That(firstConnect.Status, Is.EqualTo(NetTransportStatus.Ok));
        Assert.That(secondConnect.Status, Is.EqualTo(NetTransportStatus.ConnectionRefused));
    }

    [Test]
    public async Task MemoryTransport_Disconnect_NotifiesRemotePeer()
    {
        var network = new MemoryNetNetwork();
        await using var server = network.CreateTransport("server");
        await using var client = network.CreateTransport("client");
        var disconnected = new TaskCompletionSource<TransportPeerDisconnected>();
        var localDisconnected = new TaskCompletionSource<TransportPeerDisconnected>();

        server.PeerDisconnected += e => disconnected.TrySetResult(e);
        client.PeerDisconnected += e => localDisconnected.TrySetResult(e);

        await server.StartServerAsync(new NetListenOptions { Port = 7777 });
        var connect = await client.ConnectAsync(new NetConnectOptions { Host = "server", Port = 7777 });

        await client.DisconnectAsync(connect.ConnectionId, DisconnectReason.LocalClosed);

        var disconnect = await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(disconnect.ConnectionId, Is.Not.EqualTo(TransportConnectionId.None));
        Assert.That(disconnect.Reason, Is.EqualTo(DisconnectReason.RemoteClosed));
        var localDisconnect = await localDisconnected.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(localDisconnect.ConnectionId, Is.EqualTo(connect.ConnectionId));
        Assert.That(localDisconnect.Reason, Is.EqualTo(DisconnectReason.LocalClosed));
    }

    [Test]
    public async Task MemoryTransport_ImmediateDisconnect_PublishesConnectedBeforeDisconnected()
    {
        for (var attempt = 0; attempt < 500; attempt++)
        {
            var network = new MemoryNetNetwork();
            await using var server = network.CreateTransport("server");
            await using var client = network.CreateTransport("client");
            var events = new List<string>();
            var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            server.PeerConnected += _ => { lock (events) events.Add("connected"); };
            server.PeerDisconnected += _ =>
            {
                lock (events) events.Add("disconnected");
                disconnected.TrySetResult();
            };

            await server.StartServerAsync(new NetListenOptions { Port = 7777 });
            var connect = await client.ConnectAsync(new NetConnectOptions { Host = "server", Port = 7777 });
            await client.DisconnectAsync(connect.ConnectionId);
            await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(1));

            lock (events)
                Assert.That(events, Is.EqualTo(new[] { "connected", "disconnected" }), $"Attempt {attempt}");
        }
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
    public async Task MemoryTransport_ConcurrentConnectAndStop_NeverLeavesUsableStaleConnection()
    {
        var network = new MemoryNetNetwork();
        await using var server = network.CreateTransport("server");
        await using var client = network.CreateTransport("client");
        var staleConnectionObserved = false;

        for (var iteration = 0; iteration < 2000 && !staleConnectionObserved; iteration++)
        {
            Assert.That((await server.StartServerAsync(new NetListenOptions { Port = 7777 })).Status,
                Is.EqualTo(NetTransportStatus.Ok));
            using var start = new Barrier(2);
            var connectTask = Task.Run(async () =>
            {
                start.SignalAndWait();
                return await client.ConnectAsync(new NetConnectOptions { Host = "server", Port = 7777 });
            });
            var stopTask = Task.Run(async () =>
            {
                start.SignalAndWait();
                return await server.StopServerAsync();
            });

            var connect = await connectTask;
            Assert.That((await stopTask).Status, Is.EqualTo(NetTransportStatus.Ok));
            if (connect.Status != NetTransportStatus.Ok)
                continue;

            var send = await client.SendAsync(connect.ConnectionId, new byte[] { 1 }, NetChannel.Reliable);
            staleConnectionObserved = send.Succeeded;
            await client.DisconnectAsync(connect.ConnectionId);
        }

        Assert.That(staleConnectionObserved, Is.False);
    }

    [Test]
    public async Task MemoryTransport_ConnectAndStop_PublishConnectedBeforeDisconnected()
    {
        var network = new MemoryNetNetwork();
        await using var server = network.CreateTransport("server");
        await using var client = network.CreateTransport("client");
        var connectedDispatchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseConnectedDispatch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var events = new List<string>();
        server.PeerConnected += _ =>
        {
            connectedDispatchStarted.TrySetResult();
            releaseConnectedDispatch.Task.GetAwaiter().GetResult();
        };
        server.PeerConnected += _ =>
        {
            lock (events) events.Add("connected");
        };
        server.PeerDisconnected += _ =>
        {
            lock (events) events.Add("disconnected");
        };
        await server.StartServerAsync(new NetListenOptions { Port = 7777 });

        var connectTask = Task.Run(() => client.ConnectAsync(new NetConnectOptions { Host = "server", Port = 7777 }));
        await connectedDispatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var stopTask = server.StopServerAsync();
        await Task.Delay(50);
        releaseConnectedDispatch.TrySetResult();
        await connectTask;
        await stopTask;

        lock (events)
            Assert.That(events, Is.EqualTo(new[] { "connected", "disconnected" }));
    }

    [Test]
    public async Task MemoryTransport_ConnectRacingClientDispose_DoesNotReturnStaleSuccess()
    {
        var network = new MemoryNetNetwork();
        await using var server = network.CreateTransport("server");
        var client = network.CreateTransport("client");
        var connectedDispatchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseConnectedDispatch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        server.PeerConnected += _ =>
        {
            connectedDispatchStarted.TrySetResult();
            releaseConnectedDispatch.Task.GetAwaiter().GetResult();
        };
        await server.StartServerAsync(new NetListenOptions { Port = 7777 });

        var connectTask = Task.Run(() => client.ConnectAsync(new NetConnectOptions { Host = "server", Port = 7777 }));
        await connectedDispatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var disposeTask = client.DisposeAsync().AsTask();
        releaseConnectedDispatch.TrySetResult();

        var connect = await connectTask;
        await disposeTask;
        Assert.That(connect.Status, Is.EqualTo(NetTransportStatus.ObjectDisposed));
    }

    [Test]
    public async Task MemoryTransport_PeerConnectedHandlerCanSynchronouslyRejectConnection()
    {
        var network = new MemoryNetNetwork();
        await using var server = network.CreateTransport("server");
        await using var client = network.CreateTransport("client");
        server.PeerConnected += connected =>
            server.DisconnectAsync(connected.ConnectionId).GetAwaiter().GetResult();
        await server.StartServerAsync(new NetListenOptions { Port = 7777 });

        var connect = await client.ConnectAsync(new NetConnectOptions { Host = "server", Port = 7777 })
            .WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(connect.Status, Is.EqualTo(NetTransportStatus.ConnectionRefused));
    }

    [Test]
    public async Task MemoryTransport_ConcurrentDisposeCallersShareCompletion()
    {
        var network = new MemoryNetNetwork();
        await using var server = network.CreateTransport("server");
        var client = network.CreateTransport("client");
        var connectedDispatchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseConnectedDispatch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        server.PeerConnected += _ =>
        {
            connectedDispatchStarted.TrySetResult();
            releaseConnectedDispatch.Task.GetAwaiter().GetResult();
        };
        await server.StartServerAsync(new NetListenOptions { Port = 7777 });
        var connectTask = Task.Run(() => client.ConnectAsync(new NetConnectOptions { Host = "server", Port = 7777 }));
        await connectedDispatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var first = client.DisposeAsync().AsTask();
        var second = client.DisposeAsync().AsTask();
        try
        {
            Assert.That(first.IsCompleted, Is.False);
            Assert.That(second.IsCompleted, Is.False);
        }
        finally
        {
            releaseConnectedDispatch.TrySetResult();
        }
        await Task.WhenAll(first, second, connectTask);
    }

    [Test]
    public async Task MemoryTransport_ConcurrentStartAndDispose_ReleasesEndpointForReplacement()
    {
        for (var attempt = 0; attempt < 2000; attempt++)
        {
            var network = new MemoryNetNetwork();
            var transport = network.CreateTransport("server");
            using var barrier = new Barrier(2);
            var startTask = Task.Run(() =>
            {
                barrier.SignalAndWait();
                return transport.StartServerAsync(new NetListenOptions { Port = 7777 });
            });
            var disposeTask = Task.Run(async () =>
            {
                barrier.SignalAndWait();
                await transport.DisposeAsync();
            });

            await Task.WhenAll(startTask, disposeTask);

            await using var replacement = network.CreateTransport("server");
            var replacementStart = await replacement.StartServerAsync(new NetListenOptions { Port = 7777 });
            Assert.That(replacementStart.Status, Is.EqualTo(NetTransportStatus.Ok), $"Attempt {attempt}");
        }
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
    public async Task TcpTransport_CancelledWhileWaitingForWriteLock_KeepsConnectionUsable()
    {
        await using var server = new TcpNetTransport();
        var writer = new GatedTcpFrameWriter();
        await using var client = new TcpNetTransport(64 * 1024, null, writer);
        await server.StartServerAsync(new NetListenOptions { BindAddress = IPAddress.Loopback, Port = 0 });
        var connect = await client.ConnectAsync(new NetConnectOptions
        {
            Host = "127.0.0.1",
            Port = server.LocalEndPoint!.Port
        });
        using var cancellation = new CancellationTokenSource();

        var first = client.SendAsync(connect.ConnectionId, new byte[] { 1 }, NetChannel.Reliable).AsTask();
        await writer.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var waiting = client.SendAsync(
            connect.ConnectionId,
            new byte[] { 2 },
            NetChannel.Reliable,
            cancellation.Token).AsTask();
        cancellation.Cancel();

        Assert.That((await waiting.WaitAsync(TimeSpan.FromSeconds(1))).Status,
            Is.EqualTo(NetSendStatus.TransportFailed));

        writer.Release.TrySetResult();
        Assert.That((await first.WaitAsync(TimeSpan.FromSeconds(1))).Status, Is.EqualTo(NetSendStatus.Ok));
        Assert.That((await client.SendAsync(connect.ConnectionId, new byte[] { 3 }, NetChannel.Reliable)).Status,
            Is.EqualTo(NetSendStatus.Ok));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task TcpTransport_FrameWriteFailureAfterBytesBegin_DisconnectsBeforeNextSend(bool failInPayload)
    {
        await using var server = new TcpNetTransport();
        var writer = new PartiallyFailingTcpFrameWriter(failInPayload);
        await using var client = new TcpNetTransport(64 * 1024, null, writer);
        var disconnected = new TaskCompletionSource<TransportPeerDisconnected>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.PeerDisconnected += value => disconnected.TrySetResult(value);
        await server.StartServerAsync(new NetListenOptions { BindAddress = IPAddress.Loopback, Port = 0 });
        var connect = await client.ConnectAsync(new NetConnectOptions
        {
            Host = "127.0.0.1",
            Port = server.LocalEndPoint!.Port
        });

        var failed = await client.SendAsync(connect.ConnectionId, new byte[] { 1, 2, 3 }, NetChannel.Reliable);
        var next = await client.SendAsync(connect.ConnectionId, new byte[] { 4 }, NetChannel.Reliable);

        Assert.That(failed.Status, Is.EqualTo(NetSendStatus.TransportFailed));
        Assert.That(next.Status, Is.EqualTo(NetSendStatus.ConnectionUnavailable));
        Assert.That((await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(1))).Reason,
            Is.EqualTo(DisconnectReason.TransportFailed));
    }

    [Test]
    public async Task TcpTransport_StopDuringAcceptRace_CompletesAndCanRestart()
    {
        await using var server = new TcpNetTransport();

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var start = await server.StartServerAsync(new NetListenOptions
            {
                BindAddress = IPAddress.Loopback,
                Port = 0
            });
            Assert.That(start.Status, Is.EqualTo(NetTransportStatus.Ok));

            using var client = new TcpClient();
            var connect = client.ConnectAsync(IPAddress.Loopback, server.LocalEndPoint!.Port);
            var stop = server.StopServerAsync();
            try
            {
                await connect.WaitAsync(TimeSpan.FromSeconds(1));
            }
            catch (Exception ex) when (ex is SocketException or TimeoutException)
            {
            }

            var stopped = await stop.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.That(stopped.Status, Is.EqualTo(NetTransportStatus.Ok));
        }
    }

    [Test]
    public async Task TcpTransport_ConcurrentStartAndDispose_NeverLeavesListenerRunning()
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var port = GetUnusedLoopbackPort();
            var transport = new TcpNetTransport();
            using var barrier = new Barrier(2);
            var startTask = Task.Run(async () =>
            {
                barrier.SignalAndWait();
                return await transport.StartServerAsync(new NetListenOptions
                {
                    BindAddress = IPAddress.Loopback,
                    Port = port
                });
            });
            var disposeTask = Task.Run(async () =>
            {
                barrier.SignalAndWait();
                await transport.DisposeAsync();
            });

            await Task.WhenAll(startTask, disposeTask).WaitAsync(TimeSpan.FromSeconds(2));

            var probe = new TcpListener(IPAddress.Loopback, port);
            try
            {
                probe.Start();
            }
            finally
            {
                probe.Stop();
            }
        }
    }

    [Test]
    public async Task TcpTransport_ConnectRacingStop_LeavesNoAcceptedConnections()
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using var server = new TcpNetTransport();
            using var client = new TcpClient();
            await server.StartServerAsync(new NetListenOptions
            {
                BindAddress = IPAddress.Loopback,
                Port = 0
            });
            var port = server.LocalEndPoint!.Port;
            using var connectCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
            using var barrier = new Barrier(2);
            var connectTask = Task.Run(async () =>
            {
                barrier.SignalAndWait();
                try
                {
                    await client.ConnectAsync(IPAddress.Loopback, port, connectCancellation.Token);
                }
                catch (Exception ex) when (ex is SocketException or OperationCanceledException)
                {
                }
            });
            var stopTask = Task.Run(async () =>
            {
                barrier.SignalAndWait();
                await server.StopServerAsync();
            });

            await Task.WhenAll(connectTask, stopTask).WaitAsync(TimeSpan.FromSeconds(5));

            var connections = typeof(TcpNetTransport)
                .GetField("_connections", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(server)!;
            var count = (int)connections.GetType().GetProperty("Count")!.GetValue(connections)!;
            Assert.That(count, Is.Zero, $"Attempt {attempt}");
        }
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void TcpTransport_MaxFrameSize_RejectsNonPositiveValues(int value)
    {
        var transport = new TcpNetTransport();

        Assert.Throws<ArgumentOutOfRangeException>(() => transport.MaxFrameSize = value);
    }

    [Test]
    public async Task TcpTransport_ConnectRacingDispose_LeavesNoConnectionAfterCleanup()
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var transport = new TcpNetTransport();
            try
            {
                var acceptTask = listener.AcceptTcpClientAsync();
                var connectTask = transport.ConnectAsync(new NetConnectOptions
                {
                    Host = "127.0.0.1",
                    Port = ((IPEndPoint)listener.LocalEndpoint).Port
                });
                using var accepted = await acceptTask.WaitAsync(TimeSpan.FromSeconds(1));
                var disposeTask = transport.DisposeAsync().AsTask();

                await Task.WhenAll(connectTask, disposeTask).WaitAsync(TimeSpan.FromSeconds(1));
                var connections = typeof(TcpNetTransport)
                    .GetField("_connections", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .GetValue(transport)!;
                var count = (int)connections.GetType().GetProperty("Count")!.GetValue(connections)!;
                Assert.That(count, Is.Zero, $"Attempt {attempt}");
            }
            finally
            {
                listener.Stop();
                await transport.DisposeAsync();
            }
        }
    }

    [Test]
    public async Task TcpTransport_InvalidEndpointAndRepeatedStart_ReturnClearStatus()
    {
        await using var server = new TcpNetTransport();

        var invalid = await server.StartServerAsync(new NetListenOptions { Port = -1 });
        var invalidConnections = await server.StartServerAsync(new NetListenOptions { Port = 0, MaxConnections = 0 });
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
        Assert.That(invalidConnections.Status, Is.EqualTo(NetTransportStatus.InvalidEndpoint));
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
    public async Task TcpTransport_NonPositiveConnectTimeout_ReturnsInvalidEndpoint()
    {
        await using var client = new TcpNetTransport();

        var zero = await client.ConnectAsync(new NetConnectOptions { Host = "127.0.0.1", Port = 1, Timeout = TimeSpan.Zero });
        var negative = await client.ConnectAsync(new NetConnectOptions { Host = "127.0.0.1", Port = 1, Timeout = TimeSpan.FromMilliseconds(-2) });

        Assert.That(zero.Status, Is.EqualTo(NetTransportStatus.InvalidEndpoint));
        Assert.That(negative.Status, Is.EqualTo(NetTransportStatus.InvalidEndpoint));
    }

    [Test]
    public async Task TcpTransport_ThrowingPacketHandler_DoesNotStopLaterHandlersOrConnection()
    {
        await using var server = new TcpNetTransport();
        await using var client = new TcpNetTransport();
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        server.PacketReceived += _ => throw new InvalidOperationException("handler failed");
        server.PacketReceived += _ => received.TrySetResult();
        await server.StartServerAsync(new NetListenOptions { BindAddress = IPAddress.Loopback, Port = 0 });
        var connect = await client.ConnectAsync(new NetConnectOptions { Host = "127.0.0.1", Port = server.LocalEndPoint!.Port });

        var firstSend = await client.SendAsync(connect.ConnectionId, new byte[] { 1 }, NetChannel.Reliable);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var secondSend = await client.SendAsync(connect.ConnectionId, new byte[] { 2 }, NetChannel.Reliable);

        Assert.That(firstSend.Status, Is.EqualTo(NetSendStatus.Ok));
        Assert.That(secondSend.Status, Is.EqualTo(NetSendStatus.Ok));
    }

    [Test]
    public async Task TcpTransport_ConnectTimeout_UsesConfiguredTimeProvider()
    {
        var clock = new ManualTimeProvider();
        await using var client = new TcpNetTransport(timeProvider: clock);

        var connectTask = client.ConnectAsync(new NetConnectOptions
        {
            Host = "10.255.255.1",
            Port = 65000,
            Timeout = TimeSpan.FromSeconds(5)
        });

        clock.Advance(TimeSpan.FromSeconds(5));
        var result = await connectTask.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(result.Status, Is.EqualTo(NetTransportStatus.Timeout));
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
    public async Task TcpTransport_SendLargerThanMaxFrame_ReturnsPacketTooLarge()
    {
        await using var server = new TcpNetTransport(maxFrameSize: 16);
        await using var client = new TcpNetTransport(maxFrameSize: 16);

        await server.StartServerAsync(new NetListenOptions
        {
            BindAddress = IPAddress.Loopback,
            Port = 0
        });
        var connect = await client.ConnectAsync(new NetConnectOptions { Host = "127.0.0.1", Port = server.LocalEndPoint!.Port });

        var result = await client.SendAsync(connect.ConnectionId, new byte[32], NetChannel.Reliable);

        Assert.That(result.Status, Is.EqualTo(NetSendStatus.PacketTooLarge));
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
    public async Task TcpTransport_Dispose_ReleasesOwnedCancellationSource()
    {
        var transport = new TcpNetTransport();
        var field = typeof(TcpNetTransport).GetField(
            "_disposeCts",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var cancellation = (CancellationTokenSource)field!.GetValue(transport)!;

        await transport.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => _ = cancellation.Token);
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
    public async Task TcpTransport_DisposeWaitsForInFlightPacketCallback()
    {
        await using var server = new TcpNetTransport();
        await using var client = new TcpNetTransport();
        var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCallback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        server.PacketReceived += _ =>
        {
            callbackStarted.TrySetResult();
            releaseCallback.Task.GetAwaiter().GetResult();
        };

        await server.StartServerAsync(new NetListenOptions
        {
            BindAddress = IPAddress.Loopback,
            Port = 0
        });
        var connect = await client.ConnectAsync(new NetConnectOptions
        {
            Host = "127.0.0.1",
            Port = server.LocalEndPoint!.Port
        });

        var send = client.SendAsync(connect.ConnectionId, new byte[] { 1 }, NetChannel.Reliable).AsTask();
        await callbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var dispose = server.DisposeAsync().AsTask();
        await Task.Delay(50);
        try
        {
            Assert.That(dispose.IsCompleted, Is.False);
        }
        finally
        {
            releaseCallback.TrySetResult();
        }

        await dispose.WaitAsync(TimeSpan.FromSeconds(1));
        await send;
    }

    [Test]
    public async Task TcpTransport_LocalDisconnect_RaisesOneEventWithRequestedReason()
    {
        await using var server = new TcpNetTransport();
        await using var client = new TcpNetTransport();
        var connected = new TaskCompletionSource<TransportConnectionId>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstDisconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disconnects = new List<TransportPeerDisconnected>();
        var errors = new List<TransportError>();
        var eventGate = new object();

        server.PeerConnected += e => connected.TrySetResult(e.ConnectionId);
        server.PeerDisconnected += e =>
        {
            lock (eventGate)
                disconnects.Add(e);
            firstDisconnected.TrySetResult();
        };
        server.Error += e =>
        {
            lock (eventGate)
                errors.Add(e);
        };

        await server.StartServerAsync(new NetListenOptions
        {
            BindAddress = IPAddress.Loopback,
            Port = 0
        });
        await client.ConnectAsync(new NetConnectOptions { Host = "127.0.0.1", Port = server.LocalEndPoint!.Port });
        var connectionId = await connected.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await server.DisconnectAsync(connectionId, DisconnectReason.Kicked);
        await firstDisconnected.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(100);

        lock (eventGate)
        {
            Assert.That(disconnects, Has.Count.EqualTo(1));
            Assert.That(disconnects[0].Reason, Is.EqualTo(DisconnectReason.Kicked));
            Assert.That(errors, Is.Empty);
        }
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

    private sealed class GatedTcpFrameWriter : ITcpFrameWriter
    {
        private readonly ITcpFrameWriter _inner = DefaultTcpFrameWriter.Instance;
        private int _calls;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask WriteAsync(
            NetworkStream stream,
            Memory<byte> lengthBuffer,
            ReadOnlyMemory<byte> data,
            Action writeStarted,
            CancellationToken token)
        {
            if (Interlocked.Increment(ref _calls) == 1)
            {
                Started.TrySetResult();
                await Release.Task.WaitAsync(token);
            }

            await _inner.WriteAsync(stream, lengthBuffer, data, writeStarted, token);
        }
    }

    private sealed class PartiallyFailingTcpFrameWriter(bool failInPayload) : ITcpFrameWriter
    {
        public async ValueTask WriteAsync(
            NetworkStream stream,
            Memory<byte> lengthBuffer,
            ReadOnlyMemory<byte> data,
            Action writeStarted,
            CancellationToken token)
        {
            BinaryPrimitives.WriteInt32LittleEndian(lengthBuffer.Span, data.Length);
            writeStarted();
            if (failInPayload)
            {
                await stream.WriteAsync(lengthBuffer, token);
                await stream.WriteAsync(data[..1], token);
            }
            else
            {
                await stream.WriteAsync(lengthBuffer[..2], token);
            }

            throw new IOException("injected partial frame failure");
        }
    }
}
