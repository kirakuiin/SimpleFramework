using System.Reflection;
using System.Text;

namespace SimpleFramework.Net;

/// <summary>
/// 标记一个类型化网络消息的稳定协议键。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class NetMessageAttribute : Attribute
{
    /// <summary>
    /// 创建消息标记。
    /// </summary>
    /// <param name="key">稳定消息键，重命名 C# 类型时不应改变。</param>
    public NetMessageAttribute(string key)
    {
        Key = key;
    }

    /// <summary>
    /// 稳定消息键。
    /// </summary>
    public string Key { get; }
}

/// <summary>
/// 维护类型化消息与稳定协议标识之间的映射。
/// </summary>
public sealed class NetMessageRegistry
{
    private readonly Dictionary<Type, NetMessageDescriptor> _byType = new();
    private readonly Dictionary<string, NetMessageDescriptor> _byKey = new(StringComparer.Ordinal);
    private readonly Dictionary<ulong, NetMessageDescriptor> _byId = new();

    /// <summary>
    /// 注册一个消息类型。
    /// </summary>
    public NetMessageDescriptor Register<T>() => Register(typeof(T));

    /// <summary>
    /// 注册一个消息类型。
    /// </summary>
    /// <param name="messageType">消息类型。</param>
    public NetMessageDescriptor Register(Type messageType)
    {
        if (_byType.TryGetValue(messageType, out var existing))
            return existing;

        var attribute = messageType.GetCustomAttribute<NetMessageAttribute>()
            ?? throw new InvalidOperationException($"Message type '{messageType.FullName}' must declare NetMessageAttribute.");
        if (string.IsNullOrWhiteSpace(attribute.Key))
            throw new InvalidOperationException($"Message type '{messageType.FullName}' has an empty message key.");
        if (_byKey.TryGetValue(attribute.Key, out var duplicate))
            throw new InvalidOperationException($"Duplicate message key '{attribute.Key}' for '{messageType.FullName}' and '{duplicate.MessageType.FullName}'.");

        var descriptor = new NetMessageDescriptor(messageType, attribute.Key, StableHash(attribute.Key));
        if (_byId.TryGetValue(descriptor.MessageId, out var collision))
            throw new InvalidOperationException($"Message id collision for '{attribute.Key}' and '{collision.Key}'.");

        _byType[messageType] = descriptor;
        _byKey[attribute.Key] = descriptor;
        _byId[descriptor.MessageId] = descriptor;
        return descriptor;
    }

    /// <summary>
    /// 获取消息类型描述，未注册时会自动注册。
    /// </summary>
    public NetMessageDescriptor Get<T>() => Get(typeof(T));

    /// <summary>
    /// 获取消息类型描述，未注册时会自动注册。
    /// </summary>
    /// <param name="messageType">消息类型。</param>
    public NetMessageDescriptor Get(Type messageType)
    {
        if (_byType.TryGetValue(messageType, out var descriptor))
            return descriptor;

        return Register(messageType);
    }

    /// <summary>
    /// 按消息 ID 查找消息描述。
    /// </summary>
    public bool TryGet(ulong messageId, out NetMessageDescriptor descriptor) => _byId.TryGetValue(messageId, out descriptor!);

    private static ulong StableHash(string value)
    {
        const ulong offset = 14695981039346656037;
        const ulong prime = 1099511628211;
        var hash = offset;
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            hash ^= b;
            hash *= prime;
        }

        return hash;
    }
}

/// <summary>
/// 已注册消息的协议描述。
/// </summary>
public sealed record NetMessageDescriptor(Type MessageType, string Key, ulong MessageId);
