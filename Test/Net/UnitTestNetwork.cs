using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleFramework.Net;

namespace Test.Net;

[TestFixture]
public class TestPing
{
    private readonly List<string> _ipList = new(){"127.0.0.1", "www.baidu.com"};

    [Test]
    public async Task TestLatency()
    {
        foreach (var addr in _ipList)
        {
            var latency = await PingExecutor.GetLatency(addr);
            Assert.IsTrue(latency.IsReachable);
        }
    }
    
    [Test]
    public async Task TestNotReach()
    {
        var latency = await PingExecutor.GetLatency("1.2.3.4", 1);
        Assert.IsFalse(latency.IsReachable);
    }
}


[TestFixture]
public class TestUdpBroadcast
{
    private BroadcastListener<Message> _listener;
    private readonly List<Message> _receivedMessages = new();
    private readonly List<IPAddress> _senderAddresses = new();
    
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _listener = new BroadcastListener<Message>();
        _listener.OnReceivedBroadcast += OnMessageReceived;
        _listener.StartListen();
    }
    
    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _listener.OnReceivedBroadcast -= OnMessageReceived;
        _listener.Dispose();
    }

    [SetUp]
    public void Setup()
    {
        _receivedMessages.Clear();
        _senderAddresses.Clear();
    }

    private void OnMessageReceived(IPAddress sender, Message message)
    {
        _senderAddresses.Add(sender);
        _receivedMessages.Add(message);
    }

    [Test]
    public async Task TestBasicBroadcast()
    {
        using var sender = new BroadcastSender<Message>();
        var message = new Message { Text = "Test", Num = 1 };
        
        sender.Broadcast(message);
        await Task.Delay(100); // 等待消息接收
        
        Assert.That(_receivedMessages, Has.Count.EqualTo(1));
        Assert.That(_receivedMessages[0].Text, Is.EqualTo(message.Text));
        Assert.That(_receivedMessages[0].Num, Is.EqualTo(message.Num));
    }

    [Test]
    public async Task TestScheduledBroadcast()
    {
        const int expectedCount = 3;
        using var broadcaster = new ScheduledBroadcaster<Message>();
        var message = new Message { Text = "Scheduled", Num = 2 };
        
        broadcaster.BroadcastInterval = 0.1f; // 100ms间隔
        broadcaster.StartBroadcast(message);
        
        // 等待接收足够的消息
        var startTime = DateTime.Now;
        while (_receivedMessages.Count < expectedCount && 
               DateTime.Now - startTime < TimeSpan.FromSeconds(1))
        {
            await Task.Delay(50);
        }
        
        broadcaster.StopBroadcast();
        
        Assert.Greater(_receivedMessages.Count, expectedCount);
        Assert.That(_receivedMessages[0].Text == message.Text);
        Assert.That(_receivedMessages[1].Num == message.Num);
    }

    [Test]
    public void TestDisposedBehavior()
    {
        var broadcaster = new ScheduledBroadcaster<Message>();
        var message = new Message { Text = "Dispose Test", Num = 3 };
        
        broadcaster.StartBroadcast(message);
        broadcaster.Dispose();
        
        // 尝试在Dispose后操作
        Assert.DoesNotThrow(() => broadcaster.StartBroadcast(message));
        Assert.DoesNotThrow(() => broadcaster.StopBroadcast());
        Assert.That(broadcaster.IsSending, Is.False);
    }

    [Test]
    public async Task TestListenerRestart()
    {
        _listener.StopListen();
        await Task.Delay(100);
        
        using var sender = new BroadcastSender<Message>();
        var message = new Message { Text = "Restart Test", Num = 4 };
        
        // 停止状态下不应该收到消息
        sender.Broadcast(message);
        await Task.Delay(100);
        var countBeforeRestart = _receivedMessages.Count;
        
        // 重新启动应该能收到消息
        _listener.StartListen();
        await Task.Delay(100);
        sender.Broadcast(message);
        await Task.Delay(100);
        
        Assert.Zero(countBeforeRestart);
        Assert.Greater(_receivedMessages.Count, countBeforeRestart);
    }

    [Test]
    public async Task TestCustomPort()
    {
        const int customPort = 5566;
        using var customListener = new BroadcastListener<Message>(customPort);
        using var customSender = new BroadcastSender<Message> { BroadcastPort = customPort };
        
        var receivedOnCustomPort = false;
        customListener.OnReceivedBroadcast += (_, _) => receivedOnCustomPort = true;
        
        customListener.StartListen();
        await Task.Delay(100);
        
        var message = new Message { Text = "Custom Port", Num = 5 };
        customSender.Broadcast(message);
        
        await Task.Delay(100);
        Assert.That(receivedOnCustomPort, Is.True);
    }
}

[Serializable]
public struct Message
{
    public string Text;
    public int Num;
}