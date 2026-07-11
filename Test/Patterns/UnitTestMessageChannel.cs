using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimpleFramework.Patterns;

namespace Test.Patterns;

[TestFixture]
public class TestMessageChannel
{
    private MessageChannel<string> _channel = default!;
    private BufferedMessageChannel<string> _bufferedChannel = default!;

    [SetUp]
    public void Setup()
    {
        _channel = new MessageChannel<string>();
        _bufferedChannel = new BufferedMessageChannel<string>();
    }

    [Test]
    public void TestPublishAndSubscribe()
    {
        var receivedMessages = new List<string>();
        _channel.Subscribe(message => receivedMessages.Add(message));
        
        _channel.Publish("Test Message");
        
        Assert.That(receivedMessages.Count, Is.EqualTo(1));
        Assert.That(receivedMessages[0], Is.EqualTo("Test Message"));
    }

    [Test]
    public void TestMultipleSubscribers()
    {
        var subscriber1Count = 0;
        var subscriber2Count = 0;
        
        _channel.Subscribe(_ => subscriber1Count++);
        _channel.Subscribe(_ => subscriber2Count++);
        _channel.Publish("Test Message");
        
        Assert.That(subscriber1Count, Is.EqualTo(1));
        Assert.That(subscriber2Count, Is.EqualTo(1));
    }

    [Test]
    public void TestUnsubscribe()
    {
        var receivedMessages = new List<string>();
        void Handler(string message) => receivedMessages.Add(message);

        var subscription = _channel.Subscribe(Handler);
        _channel.Publish("First Message");
        subscription.Dispose();
        _channel.Publish("Second Message");
        
        Assert.That(receivedMessages.Count, Is.EqualTo(1));
        Assert.That(receivedMessages[0], Is.EqualTo("First Message"));
    }

    [Test]
    public void TestDispose()
    {
        var receivedMessages = new List<string>();
        _channel.Subscribe(message => receivedMessages.Add(message));
        
        _channel.Dispose();
        
        Assert.That(receivedMessages.Count, Is.EqualTo(0));
        Assert.That(_channel.IsDisposed, Is.True);
        Assert.Throws<ObjectDisposedException>(() => _channel.Publish("Test Message"));
        Assert.Throws<ObjectDisposedException>(() => _channel.Subscribe(message => receivedMessages.Add(message)));
    }

    [Test]
    public void TestBufferedChannelNewSubscriberReceivesLastMessage()
    {
        var receivedMessages = new List<string>();
        
        _bufferedChannel.Publish("First Message");
        _bufferedChannel.Subscribe(message => receivedMessages.Add(message));
        
        Assert.That(receivedMessages.Count, Is.EqualTo(1));
        Assert.That(receivedMessages[0], Is.EqualTo("First Message"));
        Assert.That(_bufferedChannel.HasBufferedMessage, Is.True);
        Assert.That(_bufferedChannel.BufferedMessage, Is.EqualTo("First Message"));
    }

    [Test]
    public void TestBufferedChannelNoBufferedMessage()
    {
        var receivedMessages = new List<string>();
        
        _bufferedChannel.Subscribe(message => receivedMessages.Add(message));
        
        Assert.That(receivedMessages.Count, Is.EqualTo(0));
        Assert.That(_bufferedChannel.HasBufferedMessage, Is.False);
    }

    [Test]
    public void TestBufferedChannelUpdateBufferedMessage()
    {
        _bufferedChannel.Publish("First Message");
        _bufferedChannel.Publish("Second Message");
        
        Assert.That(_bufferedChannel.HasBufferedMessage, Is.True);
        Assert.That(_bufferedChannel.BufferedMessage, Is.EqualTo("Second Message"));
    }

    [Test]
    public void TestBufferedChannelDispose()
    {
        var receivedMessages = new List<string>();
        _bufferedChannel.Subscribe(message => receivedMessages.Add(message));
        
        _bufferedChannel.Dispose();
        
        Assert.That(receivedMessages.Count, Is.EqualTo(0));
        Assert.That(_bufferedChannel.IsDisposed, Is.True);
        Assert.Throws<ObjectDisposedException>(() => _bufferedChannel.Publish("Test Message"));
        Assert.Throws<ObjectDisposedException>(() => _bufferedChannel.Subscribe(message => receivedMessages.Add(message)));
    }

    [Test]
    public void TestBufferedChannelMultipleSubscribersWithBufferedMessage()
    {
        var subscriber1Messages = new List<string>();
        var subscriber2Messages = new List<string>();
        
        _bufferedChannel.Publish("First Message");
        _bufferedChannel.Subscribe(message => subscriber1Messages.Add(message));
        _bufferedChannel.Subscribe(message => subscriber2Messages.Add(message));
        
        Assert.That(subscriber1Messages.Count, Is.EqualTo(1));
        Assert.That(subscriber2Messages.Count, Is.EqualTo(1));
        Assert.That(subscriber1Messages[0], Is.EqualTo("First Message"));
        Assert.That(subscriber2Messages[0], Is.EqualTo("First Message"));
    }

    [Test]
    public void TestHandlerCanUnsubscribeBeforeReentrantPublish()
    {
        var removedMessages = new List<string>();
        var remainingMessages = new List<string>();
        IDisposable? subscription = null;
        subscription = _channel.Subscribe(message =>
        {
            removedMessages.Add(message);
            subscription!.Dispose();
            _channel.Publish("inner");
        });
        _channel.Subscribe(remainingMessages.Add);

        Assert.DoesNotThrow(() => _channel.Publish("outer"));

        Assert.That(removedMessages, Is.EqualTo(new[] { "outer" }));
        Assert.That(remainingMessages, Is.EqualTo(new[] { "inner", "outer" }));
    }

    [Test]
    public void TestSubscriptionMutationIsAppliedWhenHandlerThrows()
    {
        var survivorCount = 0;
        IDisposable? subscription = null;
        subscription = _channel.Subscribe(_ =>
        {
            subscription!.Dispose();
            throw new InvalidOperationException("handler failed");
        });
        _channel.Subscribe(_ => survivorCount++);

        Assert.Throws<InvalidOperationException>(() => _channel.Publish("first"));
        _channel.Publish("second");

        Assert.That(survivorCount, Is.EqualTo(1));
    }

    [Test]
    public void TestDisposedBufferedChannelRejectsPublishWithoutChangingBuffer()
    {
        _bufferedChannel.Publish("first");
        _bufferedChannel.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _bufferedChannel.Publish("second"));
        Assert.That(_bufferedChannel.BufferedMessage, Is.EqualTo("first"));
    }

    [Test]
    public void TestHandlerCanDisposeChannelDuringPublish()
    {
        var laterHandlerCalled = false;
        _channel.Subscribe(_ => _channel.Dispose());
        _channel.Subscribe(_ => laterHandlerCalled = true);

        Assert.DoesNotThrow(() => _channel.Publish("message"));

        Assert.That(_channel.IsDisposed, Is.True);
        Assert.That(laterHandlerCalled, Is.False);
    }
} 
