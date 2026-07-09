using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

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
/// 标记一个普通消息处理方法。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class NetHandlerAttribute : Attribute
{
    /// <summary>
    /// 创建普通消息处理方法标记。
    /// </summary>
    /// <param name="messageType">该方法处理的消息类型。</param>
    public NetHandlerAttribute(Type messageType)
    {
        MessageType = messageType;
    }

    /// <summary>
    /// 该方法处理的消息类型。
    /// </summary>
    public Type MessageType { get; }
}

/// <summary>
/// 标记一个请求响应处理方法。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class NetRequestHandlerAttribute : Attribute
{
    /// <summary>
    /// 创建请求响应处理方法标记。
    /// </summary>
    /// <param name="requestType">请求类型。</param>
    /// <param name="responseType">响应类型。</param>
    public NetRequestHandlerAttribute(Type requestType, Type responseType)
    {
        RequestType = requestType;
        ResponseType = responseType;
    }

    /// <summary>
    /// 请求类型。
    /// </summary>
    public Type RequestType { get; }

    /// <summary>
    /// 响应类型。
    /// </summary>
    public Type ResponseType { get; }
}

/// <summary>
/// 标记一个流程提案处理方法。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class NetFlowHandlerAttribute : Attribute
{
    /// <summary>
    /// 创建流程提案处理方法标记。
    /// </summary>
    /// <param name="proposalType">提案类型。</param>
    /// <param name="responseType">响应类型。</param>
    public NetFlowHandlerAttribute(Type proposalType, Type responseType)
    {
        ProposalType = proposalType;
        ResponseType = responseType;
    }

    /// <summary>
    /// 提案类型。
    /// </summary>
    public Type ProposalType { get; }

    /// <summary>
    /// 响应类型。
    /// </summary>
    public Type ResponseType { get; }
}

/// <summary>
/// 维护类型化消息与稳定协议标识之间的映射。
/// </summary>
public sealed class NetMessageRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<Type, NetMessageDescriptor> _byType = new();
    private readonly Dictionary<string, NetMessageDescriptor> _byKey = new(StringComparer.Ordinal);
    private readonly Dictionary<ulong, NetMessageDescriptor> _byId = new();
    private readonly List<NetHandlerDescriptor> _handlerDescriptors = new();
    private readonly List<NetRequestHandlerDescriptor> _requestHandlerDescriptors = new();
    private readonly List<NetFlowHandlerDescriptor> _flowHandlerDescriptors = new();
    private int _frozen;
    private int _permanentlyFrozen;

    /// <summary>
    /// 扫描得到的普通消息处理方法。
    /// </summary>
    public IReadOnlyList<NetHandlerDescriptor> HandlerDescriptors
    {
        get { lock (_gate) return _handlerDescriptors.ToArray(); }
    }

    /// <summary>
    /// 扫描得到的请求响应处理方法。
    /// </summary>
    public IReadOnlyList<NetRequestHandlerDescriptor> RequestHandlerDescriptors
    {
        get { lock (_gate) return _requestHandlerDescriptors.ToArray(); }
    }

    /// <summary>
    /// 扫描得到的流程处理方法。
    /// </summary>
    public IReadOnlyList<NetFlowHandlerDescriptor> FlowHandlerDescriptors
    {
        get { lock (_gate) return _flowHandlerDescriptors.ToArray(); }
    }

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
        ArgumentNullException.ThrowIfNull(messageType);

        lock (_gate)
        {
            if (_byType.TryGetValue(messageType, out var existing))
                return existing;
            EnsureCanMutate();

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
        ArgumentNullException.ThrowIfNull(messageType);

        lock (_gate)
        {
            if (_byType.TryGetValue(messageType, out var descriptor))
                return descriptor;

            return Register(messageType);
        }
    }

    /// <summary>
    /// 按消息 ID 查找消息描述。
    /// </summary>
    public bool TryGet(ulong messageId, out NetMessageDescriptor descriptor)
    {
        lock (_gate)
            return _byId.TryGetValue(messageId, out descriptor!);
    }

    internal bool Contains(Type messageType)
    {
        lock (_gate)
            return _byType.ContainsKey(messageType);
    }

    internal void RegisterBatch(params Type[] messageTypes)
    {
        lock (_gate)
        {
            if (messageTypes.All(_byType.ContainsKey))
                return;
            EnsureCanMutate();
            var typeSnapshot = _byType.ToArray();
            var keySnapshot = _byKey.ToArray();
            var idSnapshot = _byId.ToArray();
            try
            {
                foreach (var messageType in messageTypes)
                    Register(messageType);
            }
            catch
            {
                Restore(_byType, typeSnapshot);
                Restore(_byKey, keySnapshot);
                Restore(_byId, idSnapshot);
                throw;
            }
        }
    }

    /// <summary>
    /// 扫描程序集中的消息类型和处理器标记。
    /// </summary>
    /// <param name="assembly">要扫描的程序集。</param>
    /// <param name="typeFilter">可选类型过滤器，用于测试或局部扫描。</param>
    public void RegisterAssembly(Assembly assembly, Func<Type, bool>? typeFilter = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var types = assembly.GetTypes()
            .Where(type => typeFilter?.Invoke(type) ?? true)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        lock (_gate)
        {
            EnsureCanMutate();
            var typeSnapshot = _byType.ToArray();
            var keySnapshot = _byKey.ToArray();
            var idSnapshot = _byId.ToArray();
            var handlerCount = _handlerDescriptors.Count;
            var requestCount = _requestHandlerDescriptors.Count;
            var flowCount = _flowHandlerDescriptors.Count;

            try
            {
                foreach (var type in types.Where(type => type.GetCustomAttribute<NetMessageAttribute>() is not null))
                    Register(type);

                foreach (var method in types.SelectMany(GetOrderedMethods))
                {
                    var handler = method.GetCustomAttribute<NetHandlerAttribute>();
                    if (handler is not null)
                    {
                        Register(handler.MessageType);
                        if (!_handlerDescriptors.Any(descriptor =>
                                descriptor.MessageType == handler.MessageType && descriptor.Method == method))
                        {
                            _handlerDescriptors.Add(new NetHandlerDescriptor(handler.MessageType, method));
                        }
                    }

                    var requestHandler = method.GetCustomAttribute<NetRequestHandlerAttribute>();
                    if (requestHandler is not null)
                    {
                        Register(requestHandler.RequestType);
                        Register(requestHandler.ResponseType);
                        if (!_requestHandlerDescriptors.Any(descriptor =>
                                descriptor.RequestType == requestHandler.RequestType &&
                                descriptor.ResponseType == requestHandler.ResponseType &&
                                descriptor.Method == method))
                        {
                            _requestHandlerDescriptors.Add(new NetRequestHandlerDescriptor(requestHandler.RequestType, requestHandler.ResponseType, method));
                        }
                    }

                    var flowHandler = method.GetCustomAttribute<NetFlowHandlerAttribute>();
                    if (flowHandler is not null)
                    {
                        Register(flowHandler.ProposalType);
                        Register(flowHandler.ResponseType);
                        if (!_flowHandlerDescriptors.Any(descriptor =>
                                descriptor.ProposalType == flowHandler.ProposalType &&
                                descriptor.ResponseType == flowHandler.ResponseType &&
                                descriptor.Method == method))
                        {
                            _flowHandlerDescriptors.Add(new NetFlowHandlerDescriptor(flowHandler.ProposalType, flowHandler.ResponseType, method));
                        }
                    }
                }
            }
            catch
            {
                Restore(_byType, typeSnapshot);
                Restore(_byKey, keySnapshot);
                Restore(_byId, idSnapshot);
                _handlerDescriptors.RemoveRange(handlerCount, _handlerDescriptors.Count - handlerCount);
                _requestHandlerDescriptors.RemoveRange(requestCount, _requestHandlerDescriptors.Count - requestCount);
                _flowHandlerDescriptors.RemoveRange(flowCount, _flowHandlerDescriptors.Count - flowCount);
                throw;
            }
        }
    }

    /// <summary>
    /// 计算当前消息表的稳定指纹。
    /// </summary>
    public string GetFingerprint()
    {
        lock (_gate)
        {
            var manifest = string.Join(
                "\n",
                _byKey.Values
                    .OrderBy(descriptor => descriptor.Key, StringComparer.Ordinal)
                    .Select(descriptor => $"{descriptor.Key}:{descriptor.MessageId}:{GetPayloadShape(descriptor.MessageType)}"));
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifest)));
        }
    }

    internal void Freeze()
    {
        lock (_gate)
        {
            _permanentlyFrozen = 1;
            _frozen = 1;
        }
    }

    internal RegistryReservation Reserve()
    {
        lock (_gate)
        {
            if (Volatile.Read(ref _permanentlyFrozen) == 1)
                return new RegistryReservation(this, completed: true);

            EnsureCanMutate();
            _frozen = 1;
            return new RegistryReservation(this);
        }
    }

    private void EnsureCanMutate()
    {
        if (Volatile.Read(ref _frozen) == 1)
            throw new InvalidOperationException("Message protocol manifest is frozen after the session starts.");
    }

    private void ReleaseReservation()
    {
        lock (_gate)
        {
            if (Volatile.Read(ref _permanentlyFrozen) == 0)
                _frozen = 0;
        }
    }

    private static void Restore<TKey, TValue>(Dictionary<TKey, TValue> dictionary, KeyValuePair<TKey, TValue>[] snapshot)
        where TKey : notnull
    {
        dictionary.Clear();
        foreach (var pair in snapshot)
            dictionary.Add(pair.Key, pair.Value);
    }

    /// <summary>
    /// 根据策略比较远端消息表指纹。
    /// </summary>
    public NetFingerprintCheckResult CheckFingerprint(string remoteFingerprint, NetFingerprintPolicy policy)
    {
        var local = GetFingerprint();
        if (string.Equals(local, remoteFingerprint, StringComparison.Ordinal))
            return new NetFingerprintCheckResult(NetFingerprintStatus.Match, null);

        return policy switch
        {
            NetFingerprintPolicy.Strict => new NetFingerprintCheckResult(NetFingerprintStatus.Rejected, "Message fingerprint mismatch."),
            NetFingerprintPolicy.Warn => new NetFingerprintCheckResult(NetFingerprintStatus.Warning, "Message fingerprint mismatch."),
            _ => new NetFingerprintCheckResult(NetFingerprintStatus.Ignored, "Message fingerprint mismatch.")
        };
    }

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

    private static string GetPayloadShape(Type messageType)
    {
        var properties = messageType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetMethod is not null && property.GetIndexParameters().Length == 0)
            .OrderBy(property => GetJsonMemberName(property), StringComparer.Ordinal)
            .ThenBy(property => property.PropertyType.FullName, StringComparer.Ordinal)
            .Select(property => $"{GetJsonMemberName(property)}:{GetStableTypeName(property.PropertyType)}");
        return string.Join(",", properties);
    }

    private static string GetJsonMemberName(PropertyInfo property)
    {
        return property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;
    }

    private static string GetStableTypeName(Type type)
    {
        if (!type.IsGenericType)
            return type.FullName ?? type.Name;

        var genericDefinition = type.GetGenericTypeDefinition().FullName ?? type.Name;
        var arguments = string.Join(",", type.GetGenericArguments().Select(GetStableTypeName));
        return $"{genericDefinition}<{arguments}>";
    }

    private static IEnumerable<MethodInfo> GetOrderedMethods(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        return type.GetMethods(flags)
            .OrderBy(method => method.Name, StringComparer.Ordinal)
            .ThenBy(method => method.MetadataToken);
    }

    internal sealed class RegistryReservation : IDisposable
    {
        private readonly NetMessageRegistry _owner;
        private int _completed;

        public RegistryReservation(NetMessageRegistry owner, bool completed = false)
        {
            _owner = owner;
            _completed = completed ? 1 : 0;
        }

        public void Commit()
        {
            if (Interlocked.Exchange(ref _completed, 1) == 1)
                return;

            _owner.Freeze();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _completed, 1) == 1)
                return;

            _owner.ReleaseReservation();
        }
    }
}

/// <summary>
/// 已注册消息的协议描述。
/// </summary>
public sealed record NetMessageDescriptor(Type MessageType, string Key, ulong MessageId);

/// <summary>
/// 普通消息处理方法描述。
/// </summary>
public sealed record NetHandlerDescriptor(Type MessageType, MethodInfo Method);

/// <summary>
/// 请求响应处理方法描述。
/// </summary>
public sealed record NetRequestHandlerDescriptor(Type RequestType, Type ResponseType, MethodInfo Method);

/// <summary>
/// 流程提案处理方法描述。
/// </summary>
public sealed record NetFlowHandlerDescriptor(Type ProposalType, Type ResponseType, MethodInfo Method);

/// <summary>
/// 指纹不一致时使用的处理策略。
/// </summary>
public enum NetFingerprintPolicy
{
    Strict,
    Warn,
    Ignore
}

/// <summary>
/// 指纹比较结果状态。
/// </summary>
public enum NetFingerprintStatus
{
    Match,
    Rejected,
    Warning,
    Ignored
}

/// <summary>
/// 消息表指纹比较结果。
/// </summary>
public readonly record struct NetFingerprintCheckResult(NetFingerprintStatus Status, string? Message);
