namespace SimpleFramework;

/// <summary>Domain 的轻量组合便利方法。</summary>
public static class DomainExtensions
{
    /// <summary>构造并同步执行一个无参命令。</summary>
    public static void SendCommand<T>(this IDomain domain) where T : ICommand, new()
    {
        ArgumentNullException.ThrowIfNull(domain);
        domain.SendCommand(new T());
    }

    /// <summary>构造并发送一个无参事件对象。</summary>
    public static void SendEvent<T>(this IDomain domain) where T : new()
    {
        ArgumentNullException.ThrowIfNull(domain);
        domain.SendEvent(new T());
    }
}
