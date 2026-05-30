using System.Text;
using System.Text.Json;

namespace SimpleFramework.Net;

/// <summary>
/// 局域网发现配置。
/// </summary>
public sealed class DiscoveryOptions
{
    /// <summary>
    /// 单个发现元数据载荷允许的最大字节数。
    /// </summary>
    public int MaxMetadataPayloadSize { get; init; } = 8 * 1024;

    /// <summary>
    /// 浏览器在未收到更新后保留房间的最长时间。
    /// </summary>
    public TimeSpan RoomTimeout { get; init; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// 局域网房间广告的公开信息。
/// </summary>
public sealed class LanAdvertiseInfo
{
    /// <summary>
    /// 房间稳定标识。
    /// </summary>
    public required string RoomId { get; init; }

    /// <summary>
    /// 游戏会话监听端口。
    /// </summary>
    public required int GamePort { get; init; }

    /// <summary>
    /// 公开元数据结构的稳定标识。
    /// </summary>
    public required uint MetadataSchemaId { get; init; }
}

/// <summary>
/// 一条局域网扫描结果。
/// </summary>
/// <typeparam name="TMetadata">公开元数据类型。</typeparam>
public sealed class LanScanResult<TMetadata>
{
    /// <summary>
    /// 房间稳定标识。
    /// </summary>
    public required string RoomId { get; init; }

    /// <summary>
    /// 游戏会话监听端口。
    /// </summary>
    public required int GamePort { get; init; }

    /// <summary>
    /// 是否可被当前应用版本直接加入。
    /// </summary>
    public required bool IsJoinable { get; init; }

    /// <summary>
    /// 广告中的应用标识。
    /// </summary>
    public required Guid ApplicationId { get; init; }

    /// <summary>
    /// 广告中的协议版本。
    /// </summary>
    public required int ProtocolVersion { get; init; }

    /// <summary>
    /// 元数据结构标识。
    /// </summary>
    public required uint MetadataSchemaId { get; init; }

    /// <summary>
    /// 公开房间元数据。
    /// </summary>
    public required TMetadata Metadata { get; init; }

    /// <summary>
    /// 可用时的房间列表往返延迟估计。
    /// </summary>
    public TimeSpan? EstimatedLatency { get; init; }
}

/// <summary>
/// 连续浏览器当前看到的房间快照。
/// </summary>
/// <typeparam name="TMetadata">公开元数据类型。</typeparam>
public sealed class LanBrowserSnapshot<TMetadata>
{
    /// <summary>
    /// 当前仍未过期的房间列表。
    /// </summary>
    public required IReadOnlyList<LanScanResult<TMetadata>> Rooms { get; init; }
}

/// <summary>
/// 连续房间浏览器。
/// </summary>
/// <typeparam name="TMetadata">公开元数据类型。</typeparam>
public sealed class LanBrowser<TMetadata> : IAsyncDisposable
{
    private readonly NetDiscovery _discovery;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _roomTimeout;
    private readonly Dictionary<string, BrowserRoom<TMetadata>> _rooms = new();

    internal LanBrowser(NetDiscovery discovery, TimeProvider timeProvider, TimeSpan roomTimeout)
    {
        _discovery = discovery;
        _timeProvider = timeProvider;
        _roomTimeout = roomTimeout;
        Snapshot = new LanBrowserSnapshot<TMetadata> { Rooms = Array.Empty<LanScanResult<TMetadata>>() };
    }

    /// <summary>
    /// 首次发现房间时触发。
    /// </summary>
    public event Action<LanScanResult<TMetadata>>? RoomFound;

    /// <summary>
    /// 已知房间信息更新时触发。
    /// </summary>
    public event Action<LanScanResult<TMetadata>>? RoomUpdated;

    /// <summary>
    /// 已知房间过期移除时触发。
    /// </summary>
    public event Action<LanScanResult<TMetadata>>? RoomLost;

    /// <summary>
    /// 当前浏览器房间快照。
    /// </summary>
    public LanBrowserSnapshot<TMetadata> Snapshot { get; private set; }

    /// <summary>
    /// 执行一次浏览刷新并触发对应事件。
    /// </summary>
    public async Task RefreshAsync()
    {
        var now = _timeProvider.GetUtcNow();
        var scanned = await _discovery.ScanAsync<TMetadata>(TimeSpan.Zero).ConfigureAwait(false);
        foreach (var room in scanned)
        {
            if (!_rooms.TryGetValue(room.RoomId, out var existing))
            {
                _rooms[room.RoomId] = new BrowserRoom<TMetadata>(room, now);
                RaiseRoomEvent(RoomFound, room);
                continue;
            }

            _rooms[room.RoomId] = new BrowserRoom<TMetadata>(room, now);
            if (!AreSameRoom(existing.Room, room))
                RaiseRoomEvent(RoomUpdated, room);
        }

        foreach (var pair in _rooms.ToArray())
        {
            if (now - pair.Value.LastSeenAt <= _roomTimeout)
                continue;

            _rooms.Remove(pair.Key);
            RaiseRoomEvent(RoomLost, pair.Value.Room);
        }

        Snapshot = new LanBrowserSnapshot<TMetadata>
        {
            Rooms = _rooms.Values.Select(room => room.Room).ToArray()
        };
    }

    /// <summary>
    /// 释放浏览器。
    /// </summary>
    public ValueTask DisposeAsync()
    {
        _rooms.Clear();
        Snapshot = new LanBrowserSnapshot<TMetadata> { Rooms = Array.Empty<LanScanResult<TMetadata>>() };
        return ValueTask.CompletedTask;
    }

    private static bool AreSameRoom(LanScanResult<TMetadata> left, LanScanResult<TMetadata> right)
    {
        return left.RoomId == right.RoomId &&
               left.GamePort == right.GamePort &&
               left.IsJoinable == right.IsJoinable &&
               left.ApplicationId == right.ApplicationId &&
               left.ProtocolVersion == right.ProtocolVersion &&
               left.MetadataSchemaId == right.MetadataSchemaId &&
               EqualityComparer<TMetadata>.Default.Equals(left.Metadata, right.Metadata);
    }

    private void RaiseRoomEvent(Action<LanScanResult<TMetadata>>? callbacks, LanScanResult<TMetadata> room)
    {
        if (callbacks is null)
            return;

        foreach (Action<LanScanResult<TMetadata>> callback in callbacks.GetInvocationList())
            _discovery.DispatchEvent(() => callback(room));
    }

    private sealed record BrowserRoom<T>(LanScanResult<T> Room, DateTimeOffset LastSeenAt);
}

/// <summary>
/// 发现元数据结构注册表。
/// </summary>
public sealed class DiscoveryMetadataRegistry
{
    private readonly Dictionary<uint, DiscoveryMetadataDescriptor> _bySchemaId = new();
    private readonly Dictionary<Type, DiscoveryMetadataDescriptor> _byType = new();

    /// <summary>
    /// 注册公开元数据类型及其稳定结构键。
    /// </summary>
    public DiscoveryMetadataDescriptor Register<TMetadata>(string schemaKey)
    {
        if (string.IsNullOrWhiteSpace(schemaKey))
            throw new InvalidOperationException("Discovery metadata schema key cannot be empty.");

        var type = typeof(TMetadata);
        var schemaId = GetSchemaId(schemaKey);
        if (_bySchemaId.TryGetValue(schemaId, out var existing) && existing.MetadataType != type)
            throw new InvalidOperationException($"Discovery metadata schema '{schemaKey}' conflicts with '{existing.SchemaKey}'.");

        var descriptor = new DiscoveryMetadataDescriptor(type, schemaKey, schemaId);
        _bySchemaId[schemaId] = descriptor;
        _byType[type] = descriptor;
        return descriptor;
    }

    /// <summary>
    /// 根据稳定结构键生成确定性的结构标识。
    /// </summary>
    public static uint GetSchemaId(string schemaKey)
    {
        if (string.IsNullOrWhiteSpace(schemaKey))
            throw new InvalidOperationException("Discovery metadata schema key cannot be empty.");

        unchecked
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;
            var hash = offset;
            foreach (var value in Encoding.UTF8.GetBytes(schemaKey))
            {
                hash ^= value;
                hash *= prime;
            }

            return hash;
        }
    }
}

/// <summary>
/// 已注册的发现元数据结构描述。
/// </summary>
public sealed record DiscoveryMetadataDescriptor(Type MetadataType, string SchemaKey, uint SchemaId);

/// <summary>
/// 用于单元测试的内存发现网络。
/// </summary>
public sealed class MemoryDiscoveryNetwork
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, DiscoveryPacket> _advertisements = new();

    internal Guid Publish(DiscoveryPacket packet)
    {
        lock (_gate)
        {
            var id = Guid.NewGuid();
            _advertisements[id] = packet;
            return id;
        }
    }

    internal void Update(Guid id, DiscoveryPacket packet)
    {
        lock (_gate)
        {
            if (_advertisements.ContainsKey(id))
                _advertisements[id] = packet;
        }
    }

    internal void Remove(Guid id)
    {
        lock (_gate)
            _advertisements.Remove(id);
    }

    internal IReadOnlyList<DiscoveryPacket> Snapshot()
    {
        lock (_gate)
            return _advertisements.Values.ToArray();
    }
}

/// <summary>
/// 负责局域网广告、扫描和房间元数据处理。
/// </summary>
public sealed class NetDiscovery : IAsyncDisposable
{
    private readonly GameNetOptions _options;
    private readonly MemoryDiscoveryNetwork _network;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private Guid? _advertisementId;
    private LanAdvertiseInfo? _advertiseInfo;
    private int _disposed;

    /// <summary>
    /// 创建基于内存发现网络的发现组件。
    /// </summary>
    public NetDiscovery(GameNetOptions options, MemoryDiscoveryNetwork network)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(network);
        GameNet.ValidateOptions(options);

        _options = options;
        _network = network;
        Diagnostics = new NetDiagnostics(options.EventDispatcher);
    }

    /// <summary>
    /// 发现组件的诊断计数器。
    /// </summary>
    public NetDiagnostics Diagnostics { get; }

    /// <summary>
    /// 启动持续房间广告。
    /// </summary>
    public Task<NetSessionResult> StartAdvertiseAsync<TMetadata>(LanAdvertiseInfo info, TMetadata metadata)
    {
        if (IsDisposed)
            return Task.FromResult(new NetSessionResult(NetSessionStatus.ObjectDisposed));

        ArgumentNullException.ThrowIfNull(info);
        if (_advertisementId is not null)
            return Task.FromResult(new NetSessionResult(NetSessionStatus.InvalidState, "Discovery advertise is already running."));

        if (!TryCreatePacket(info, metadata, out var packet, out var error))
            return Task.FromResult(new NetSessionResult(NetSessionStatus.TransportFailed, error));

        _advertisementId = _network.Publish(packet);
        _advertiseInfo = info;
        return Task.FromResult(NetSessionResult.Ok());
    }

    /// <summary>
    /// 更新当前广告的公开元数据。
    /// </summary>
    public Task<NetSessionResult> UpdateAdvertiseMetadataAsync<TMetadata>(TMetadata metadata)
    {
        if (IsDisposed)
            return Task.FromResult(new NetSessionResult(NetSessionStatus.ObjectDisposed));

        if (_advertisementId is null || _advertiseInfo is null)
            return Task.FromResult(new NetSessionResult(NetSessionStatus.InvalidState, "Discovery advertise has not started."));

        if (!TryCreatePacket(_advertiseInfo, metadata, out var packet, out var error))
            return Task.FromResult(new NetSessionResult(NetSessionStatus.TransportFailed, error));

        _network.Update(_advertisementId.Value, packet);
        return Task.FromResult(NetSessionResult.Ok());
    }

    /// <summary>
    /// 停止当前房间广告。
    /// </summary>
    public Task<NetSessionResult> StopAdvertiseAsync()
    {
        if (IsDisposed)
            return Task.FromResult(new NetSessionResult(NetSessionStatus.ObjectDisposed));

        if (_advertisementId is null)
            return Task.FromResult(NetSessionResult.Ok());

        _network.Remove(_advertisementId.Value);
        _advertisementId = null;
        _advertiseInfo = null;
        return Task.FromResult(NetSessionResult.Ok());
    }

    /// <summary>
    /// 执行一次房间扫描。
    /// </summary>
    public Task<IReadOnlyList<LanScanResult<TMetadata>>> ScanAsync<TMetadata>(TimeSpan duration)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var results = new List<LanScanResult<TMetadata>>();
        foreach (var packet in _network.Snapshot())
        {
            if (packet.ApplicationId != _options.Application.ApplicationId)
                continue;
            if (packet.MetadataPayload.Length > _options.Discovery.MaxMetadataPayloadSize)
            {
                Diagnostics.AddDroppedPacket();
                continue;
            }

            var metadata = JsonSerializer.Deserialize<TMetadata>(packet.MetadataPayload, _jsonOptions);
            if (metadata is null)
                continue;

            results.Add(new LanScanResult<TMetadata>
            {
                RoomId = packet.RoomId,
                GamePort = packet.GamePort,
                IsJoinable = packet.ProtocolVersion == _options.Application.ProtocolVersion,
                ApplicationId = packet.ApplicationId,
                ProtocolVersion = packet.ProtocolVersion,
                MetadataSchemaId = packet.MetadataSchemaId,
                Metadata = metadata,
                EstimatedLatency = TimeSpan.Zero
            });
        }

        return Task.FromResult<IReadOnlyList<LanScanResult<TMetadata>>>(results);
    }

    /// <summary>
    /// 创建连续房间浏览器。
    /// </summary>
    public Task<LanBrowser<TMetadata>> StartBrowserAsync<TMetadata>()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        return Task.FromResult(new LanBrowser<TMetadata>(this, _options.TimeProvider, _options.Discovery.RoomTimeout));
    }

    /// <summary>
    /// 释放发现组件并停止当前广告。
    /// </summary>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return ValueTask.CompletedTask;

        if (_advertisementId is not null)
        {
            _network.Remove(_advertisementId.Value);
            _advertisementId = null;
            _advertiseInfo = null;
        }

        return ValueTask.CompletedTask;
    }

    private bool IsDisposed => Volatile.Read(ref _disposed) == 1;

    internal void DispatchEvent(Action action)
    {
        if (_options.EventDispatcher is null)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Diagnostics.RecordError(new NetError("EventCallbackError", ex.Message, ex));
            }

            return;
        }

        try
        {
            _options.EventDispatcher.Post(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Diagnostics.RecordError(new NetError("EventCallbackError", ex.Message, ex));
                }
            });
        }
        catch (Exception ex)
        {
            Diagnostics.RecordError(new NetError("EventDispatchError", ex.Message, ex));
        }
    }

    private bool TryCreatePacket<TMetadata>(LanAdvertiseInfo info, TMetadata metadata, out DiscoveryPacket packet, out string? error)
    {
        if (!TryValidatePublicMetadata(typeof(TMetadata), out error))
        {
            Diagnostics.RecordError(new NetError("DiscoveryPrivateMetadata", error ?? "Discovery metadata is private."));
            packet = null!;
            return false;
        }

        var payload = JsonSerializer.SerializeToUtf8Bytes(metadata, _jsonOptions);
        if (payload.Length > _options.Discovery.MaxMetadataPayloadSize)
        {
            Diagnostics.AddDroppedPacket();
            packet = null!;
            error = "Discovery metadata payload exceeds MaxMetadataPayloadSize.";
            return false;
        }

        packet = new DiscoveryPacket(
            Magic: DiscoveryPacket.ExpectedMagic,
            PacketVersion: DiscoveryPacket.CurrentPacketVersion,
            ApplicationId: _options.Application.ApplicationId,
            ProtocolVersion: _options.Application.ProtocolVersion,
            RoomId: info.RoomId,
            GamePort: info.GamePort,
            MetadataSchemaId: info.MetadataSchemaId,
            MetadataPayload: payload);
        error = null;
        return true;
    }

    private static bool TryValidatePublicMetadata(Type metadataType, out string? error)
    {
        foreach (var property in metadataType.GetProperties())
        {
            if (property.Name == "HasPassword" && property.PropertyType == typeof(bool))
                continue;

            var name = property.Name;
            if (name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("PrivateKey", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Secret", StringComparison.OrdinalIgnoreCase))
            {
                error = $"Discovery metadata property '{property.Name}' is private data and cannot be advertised.";
                return false;
            }
        }

        error = null;
        return true;
    }
}

internal sealed record DiscoveryPacket(
    uint Magic,
    ushort PacketVersion,
    Guid ApplicationId,
    int ProtocolVersion,
    string RoomId,
    int GamePort,
    uint MetadataSchemaId,
    byte[] MetadataPayload)
{
    public const uint ExpectedMagic = 0x53464E44;
    public const ushort CurrentPacketVersion = 1;
}
