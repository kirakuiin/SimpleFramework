using System.Reflection;
using System.Text;

namespace SimpleFramework.Net;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class NetMessageAttribute : Attribute
{
    public NetMessageAttribute(string key)
    {
        Key = key;
    }

    public string Key { get; }
}

public sealed class NetMessageRegistry
{
    private readonly Dictionary<Type, NetMessageDescriptor> _byType = new();
    private readonly Dictionary<string, NetMessageDescriptor> _byKey = new(StringComparer.Ordinal);
    private readonly Dictionary<ulong, NetMessageDescriptor> _byId = new();

    public NetMessageDescriptor Register<T>() => Register(typeof(T));

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

    public NetMessageDescriptor Get<T>() => Get(typeof(T));

    public NetMessageDescriptor Get(Type messageType)
    {
        if (_byType.TryGetValue(messageType, out var descriptor))
            return descriptor;

        return Register(messageType);
    }

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

public sealed record NetMessageDescriptor(Type MessageType, string Key, ulong MessageId);
