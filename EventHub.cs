using SimpleFramework.FrameworkImpl;

namespace SimpleFramework;

/// <summary>
/// 跨 Domain 共享的事件中心。它是普通 Utility：由调用方创建、持有和释放，可以注册到多个 Domain。
/// <para>作用范围由注册位置决定：注册在根 Domain 时整棵树共享，子域注册自己的事件中心会遮蔽父级。
/// 分发语义与 Domain 本地事件一致：同步、按注册顺序、分发中新增的订阅下次生效、分发中取消的订阅本轮跳过、首个异常即停止。</para>
/// <para>非线程安全：即使被互不相连的多个 Domain 共享，也必须在同一线程使用。任何持有者都能释放它，释放应由创建者负责。</para>
/// </summary>
public interface IEventHub : IUtility, IDisposable
{
    /// <summary>同步发布事件。</summary>
    /// <exception cref="ObjectDisposedException">事件中心已释放。</exception>
    void Publish<T>(T message);

    /// <summary>订阅事件；订阅由调用方取消，事件中心释放时全部失效。</summary>
    /// <exception cref="ObjectDisposedException">事件中心已释放。</exception>
    IUnRegister Subscribe<T>(Action<T> handler);
}

/// <summary><see cref="IEventHub"/> 的默认实现，复用 Domain 本地事件的分发实现以保证两者语义一致。</summary>
public sealed class EventHub : IEventHub
{
    /// <summary>实际保存订阅和执行分发的事件容器。</summary>
    private readonly DomainEventBus _events = new();

    /// <summary>是否已释放。</summary>
    private bool _isDisposed;

    /// <inheritdoc />
    public void Publish<T>(T message)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        _events.Send(message);
    }

    /// <inheritdoc />
    public IUnRegister Subscribe<T>(Action<T> handler)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        return _events.Register(handler);
    }

    /// <summary>失效全部订阅；正在进行的分发会跳过剩余处理器。重复调用无效果。</summary>
    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        _events.Clear();
    }
}

/// <summary>为发布、订阅语法糖查找事件中心。</summary>
internal static class EventHubResolver
{
    /// <summary>通过 Domain 的 Utility 查找事件中心；找不到时给出注册提示，而不是通用的“找不到组件”。</summary>
    /// <exception cref="KeyNotFoundException">Domain 及其父链中没有注册事件中心。</exception>
    public static IEventHub Resolve(IDomain domain)
    {
        if (domain.TryGetUtility<IEventHub>(out var hub)) return hub;

        throw new KeyNotFoundException(
            "未找到 IEventHub：请在根 Domain 的 Configure 中注册，例如 RegisterUtility<IEventHub>(hub)。");
    }
}
