using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleFramework.Network;

namespace Test.Network;

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
    private readonly BroadcastListener<Message> _listener = new();
    
    private int _broadcastCount;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _listener.StartListen();
    }
    
    [Test]
    public async Task TestBroadcastOnce()
    {
        _broadcastCount = 0;
        BroadcastSender<Message> broadcastSender = new();
        
        _listener.OnReceivedBroadcast += ListenerOnOnReceivedBroadcast;

        void ListenerOnOnReceivedBroadcast(IPAddress addr, Message msg)
        {
            _broadcastCount++;
            Assert.AreEqual(3, msg.Num);
            Assert.AreEqual("Hello World", msg.Text);
        }

        broadcastSender.Broadcast(new Message {Num = 3, Text = "Hello World"});
        await Task.Delay(10);
        _listener.OnReceivedBroadcast -= ListenerOnOnReceivedBroadcast;
        Assert.AreEqual(1, _broadcastCount);
    }
    
    [Test]
    public async Task TestBroadcastSchedule()
    {
        _broadcastCount = 0;
        ScheduledBroadcaster<Message> broadcastSender = new();
        
        _listener.OnReceivedBroadcast += ListenerOnOnReceivedBroadcast;

        void ListenerOnOnReceivedBroadcast(IPAddress addr, Message msg)
        {
            if (_broadcastCount < 3)
            {
                _broadcastCount++;
                if (_broadcastCount == 3)
                {
                    broadcastSender.StopBroadcast();
                }
            }
            Assert.AreEqual(1, msg.Num);
            Assert.AreEqual("Hi", msg.Text);
        }

        broadcastSender.BroadcastInterval = 0.1f;
        broadcastSender.StartBroadcast(new Message {Num = 1, Text = "Hi"});
        await Task.Delay(400);
        _listener.OnReceivedBroadcast -= ListenerOnOnReceivedBroadcast;
        Assert.AreEqual(3, _broadcastCount);
    }
}

[Serializable]
public struct Message
{
    public string Text;
    public int Num;
}