using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net;
using System.Net.Sockets;

namespace SimpleFramework.Net;

/// <summary>
/// 局域网发现配置。
/// </summary>
public sealed class DiscoveryOptions
{
    /// <summary>
    /// UDP/LAN 发现端口。
    /// </summary>
    public int Port { get; init; } = 3344;

    /// <summary>
    /// 持续广告发送间隔。
    /// </summary>
    public TimeSpan AdvertiseInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 单个发现元数据载荷允许的最大字节数。
    /// </summary>
    public int MaxMetadataPayloadSize { get; init; } = 8 * 1024;

    /// <summary>
    /// 浏览器在未收到更新后保留房间的最长时间。
    /// </summary>
    public TimeSpan RoomTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 连续浏览器每次刷新使用的默认扫描窗口。
    /// </summary>
    public TimeSpan DefaultScanTimeout { get; init; } = TimeSpan.FromMilliseconds(200);
}

/// <summary>
/// 发现广告句柄。
/// </summary>
public readonly record struct DiscoveryAdvertisementId(Guid Value)
{
    /// <summary>
    /// 表示没有有效发现广告。
    /// </summary>
    public static readonly DiscoveryAdvertisementId None = new(Guid.Empty);
}

/// <summary>
/// 发现后端抽象，用于替换内存测试网络和 UDP/LAN 网络。
/// </summary>
public interface IDiscoveryBackend : IAsyncDisposable
{
    /// <summary>启动持续广告并返回其句柄。</summary>
    /// <param name="packet">要广播的发现数据包。</param>
    /// <param name="options">发现配置。</param>
    /// <param name="token">取消标记。</param>
    /// <returns>已启动广告的句柄。</returns>
    Task<DiscoveryAdvertisementId> StartAdvertiseAsync(DiscoveryPacket packet, DiscoveryOptions options, CancellationToken token = default);

    /// <summary>更新已有广告的数据包。</summary>
    /// <param name="id">广告句柄。</param>
    /// <param name="packet">新的发现数据包。</param>
    /// <param name="options">发现配置。</param>
    /// <param name="token">取消标记。</param>
    /// <returns>表示异步操作的任务。</returns>
    Task UpdateAdvertiseAsync(DiscoveryAdvertisementId id, DiscoveryPacket packet, DiscoveryOptions options, CancellationToken token = default);

    /// <summary>停止指定广告。</summary>
    /// <param name="id">广告句柄。</param>
    /// <param name="token">取消标记。</param>
    /// <returns>表示异步操作的任务。</returns>
    Task StopAdvertiseAsync(DiscoveryAdvertisementId id, CancellationToken token = default);

    /// <summary>在指定时间窗口内扫描发现数据包。</summary>
    /// <param name="duration">扫描窗口。</param>
    /// <param name="options">发现配置。</param>
    /// <param name="token">取消标记。</param>
    /// <returns>本次扫描得到的数据包快照。</returns>
    Task<IReadOnlyList<DiscoveryPacket>> ScanAsync(TimeSpan duration, DiscoveryOptions options, CancellationToken token = default);
}

/// <summary>
/// 标记发现元数据类型使用的稳定 schema key。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class DiscoveryMetadataAttribute : Attribute
{
    /// <summary>
    /// 创建发现元数据标记。
    /// </summary>
    /// <param name="schemaKey">跨版本保持稳定且非空的 schema 键。</param>
    /// <exception cref="ArgumentException"><paramref name="schemaKey"/> 为空或仅包含空白。</exception>
    public DiscoveryMetadataAttribute(string schemaKey)
    {
        if (string.IsNullOrWhiteSpace(schemaKey))
            throw new ArgumentException("Discovery metadata schema key cannot be empty.", nameof(schemaKey));

        SchemaKey = schemaKey;
        SchemaId = DiscoveryMetadataRegistry.GetSchemaId(schemaKey);
    }

    /// <summary>
    /// 稳定 schema key。
    /// </summary>
    public string SchemaKey { get; }

    /// <summary>
    /// 由 schema key 派生的 schema id。
    /// </summary>
    public uint SchemaId { get; }
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
    /// 发现包来源端点；内存后端可能为空。
    /// </summary>
    public IPEndPoint? EndPoint { get; init; }

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
    private readonly TimeSpan _refreshInterval;
    private readonly TimeSpan _scanDuration;
    private readonly uint _metadataSchemaId;
    private readonly Dictionary<string, BrowserRoom<TMetadata>> _rooms = new();
    private readonly CancellationTokenSource _refreshCancellation = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly CancellationTokenRegistration _ownerCancellationRegistration;
    private readonly Task _refreshTask;
    private readonly object _disposeGate = new();
    private int _disposed;
    private Task? _disposeTask;

    internal LanBrowser(
        NetDiscovery discovery,
        TimeProvider timeProvider,
        TimeSpan roomTimeout,
        TimeSpan refreshInterval,
        TimeSpan scanDuration,
        uint metadataSchemaId,
        CancellationToken ownerCancellation)
    {
        _discovery = discovery;
        _timeProvider = timeProvider;
        _roomTimeout = roomTimeout;
        _refreshInterval = refreshInterval;
        _scanDuration = scanDuration;
        _metadataSchemaId = metadataSchemaId;
        Snapshot = new LanBrowserSnapshot<TMetadata> { Rooms = Array.Empty<LanScanResult<TMetadata>>() };
        _ownerCancellationRegistration = ownerCancellation.Register(static state =>
            ((CancellationTokenSource)state!).Cancel(), _refreshCancellation);
        _refreshTask = RunRefreshLoopAsync(_refreshCancellation.Token);
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
        await RefreshAsync(_refreshCancellation.Token).ConfigureAwait(false);
    }

    private async Task RefreshAsync(CancellationToken token)
    {
        var notifications = new List<(Action<LanScanResult<TMetadata>>? Callbacks, LanScanResult<TMetadata> Room)>();
        await _refreshGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var now = _timeProvider.GetUtcNow();
            var scanned = await _discovery.ScanAsync<TMetadata>(_scanDuration, _metadataSchemaId, token).ConfigureAwait(false);
            foreach (var room in scanned)
            {
                if (_rooms.TryGetValue(room.RoomId, out var existing))
                {
                    _rooms[room.RoomId] = new BrowserRoom<TMetadata>(room, now);
                    if (!AreSameRoom(existing.Room, room))
                        notifications.Add((RoomUpdated, room));
                    continue;
                }

                _rooms[room.RoomId] = new BrowserRoom<TMetadata>(room, now);
                notifications.Add((RoomFound, room));
            }

            foreach (var pair in _rooms.ToArray())
            {
                if (now - pair.Value.LastSeenAt <= _roomTimeout)
                    continue;

                _rooms.Remove(pair.Key);
                notifications.Add((RoomLost, pair.Value.Room));
            }

            Snapshot = new LanBrowserSnapshot<TMetadata>
            {
                Rooms = _rooms.Values.Select(room => room.Room).ToArray()
            };
        }
        finally
        {
            _refreshGate.Release();
        }

        foreach (var notification in notifications)
            RaiseRoomEvent(notification.Callbacks, notification.Room);
    }

    /// <summary>
    /// 释放浏览器。
    /// </summary>
    public ValueTask DisposeAsync()
    {
        lock (_disposeGate)
        {
            if (_disposeTask is null)
            {
                Volatile.Write(ref _disposed, 1);
                _refreshCancellation.Cancel();
                _disposeTask = Task.Run(DisposeCoreAsync);
            }

            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        try
        {
            await _refreshTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        await _refreshGate.WaitAsync().ConfigureAwait(false);
        _refreshGate.Release();
        await _ownerCancellationRegistration.DisposeAsync().ConfigureAwait(false);
        _refreshCancellation.Dispose();
        _refreshGate.Dispose();
        _rooms.Clear();
        Snapshot = new LanBrowserSnapshot<TMetadata> { Rooms = Array.Empty<LanScanResult<TMetadata>>() };
    }

    private async Task RunRefreshLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_refreshInterval, _timeProvider, token).ConfigureAwait(false);
                await RefreshAsync(token).ConfigureAwait(false);
            }
            catch (ObjectDisposedException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _discovery.Diagnostics.RecordError(new NetError("BrowserRefreshFailed", ex.Message, ex));
            }
        }
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
    private readonly object _gate = new();
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
        lock (_gate)
        {
            if (_bySchemaId.TryGetValue(schemaId, out var existing) && existing.MetadataType != type)
                throw new InvalidOperationException($"Discovery metadata schema '{schemaKey}' conflicts with '{existing.SchemaKey}'.");

            var descriptor = new DiscoveryMetadataDescriptor(type, schemaKey, schemaId);
            _bySchemaId[schemaId] = descriptor;
            _byType[type] = descriptor;
            return descriptor;
        }
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
public sealed class MemoryDiscoveryNetwork : IDiscoveryBackend
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, DiscoveryPacket> _advertisements = new();

    public Task<DiscoveryAdvertisementId> StartAdvertiseAsync(DiscoveryPacket packet, DiscoveryOptions options, CancellationToken token = default)
    {
        if (token.IsCancellationRequested)
            return Task.FromCanceled<DiscoveryAdvertisementId>(token);

        lock (_gate)
        {
            var id = Guid.NewGuid();
            _advertisements[id] = packet;
            return Task.FromResult(new DiscoveryAdvertisementId(id));
        }
    }

    public Task UpdateAdvertiseAsync(DiscoveryAdvertisementId id, DiscoveryPacket packet, DiscoveryOptions options, CancellationToken token = default)
    {
        if (token.IsCancellationRequested)
            return Task.FromCanceled(token);

        lock (_gate)
        {
            if (_advertisements.ContainsKey(id.Value))
                _advertisements[id.Value] = packet;
        }

        return Task.CompletedTask;
    }

    public Task StopAdvertiseAsync(DiscoveryAdvertisementId id, CancellationToken token = default)
    {
        if (token.IsCancellationRequested)
            return Task.FromCanceled(token);

        lock (_gate)
            _advertisements.Remove(id.Value);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DiscoveryPacket>> ScanAsync(TimeSpan duration, DiscoveryOptions options, CancellationToken token = default)
    {
        if (token.IsCancellationRequested)
            return Task.FromCanceled<IReadOnlyList<DiscoveryPacket>>(token);

        lock (_gate)
            return Task.FromResult<IReadOnlyList<DiscoveryPacket>>(_advertisements.Values.ToArray());
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// 基于 UDP broadcast 的局域网发现后端。
/// </summary>
public sealed class UdpDiscoveryNetwork : IDiscoveryBackend
{
    private readonly object _gate = new();
    private readonly object _disposeGate = new();
    private readonly Dictionary<DiscoveryAdvertisementId, AdvertisementRun> _advertisements = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly TimeProvider _timeProvider;
    private int _disposed;
    private int _activeScans;
    private Task? _disposeTask;
    private TaskCompletionSource? _scansDrained;

    public UdpDiscoveryNetwork(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<DiscoveryAdvertisementId> StartAdvertiseAsync(DiscoveryPacket packet, DiscoveryOptions options, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        await _lifecycleGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            var id = new DiscoveryAdvertisementId(Guid.NewGuid());
            token.ThrowIfCancellationRequested();
            var cancellation = new CancellationTokenSource();
            var loopTask = AdvertiseLoopAsync(packet, options, cancellation.Token);
            lock (_gate)
                _advertisements[id] = new AdvertisementRun(cancellation, loopTask);

            return id;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task UpdateAdvertiseAsync(DiscoveryAdvertisementId id, DiscoveryPacket packet, DiscoveryOptions options, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        await _lifecycleGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            await StopAdvertiseCoreAsync(id).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            var cancellation = new CancellationTokenSource();
            var loopTask = AdvertiseLoopAsync(packet, options, cancellation.Token);
            lock (_gate)
                _advertisements[id] = new AdvertisementRun(cancellation, loopTask);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task StopAdvertiseAsync(DiscoveryAdvertisementId id, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        await _lifecycleGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            await StopAdvertiseCoreAsync(id).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task StopAdvertiseCoreAsync(DiscoveryAdvertisementId id)
    {
        AdvertisementRun? run = null;
        lock (_gate)
        {
            if (_advertisements.Remove(id, out var existing))
                run = existing;
        }

        if (run is null)
            return;

        run.Cancellation.Cancel();
        try
        {
            await run.LoopTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (run.Cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            run.Cancellation.Dispose();
        }
    }

    public Task<IReadOnlyList<DiscoveryPacket>> ScanAsync(TimeSpan duration, DiscoveryOptions options, CancellationToken token = default)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            _activeScans++;
        }

        return TrackScanAsync(duration, options, token);
    }

    private async Task<IReadOnlyList<DiscoveryPacket>> TrackScanAsync(
        TimeSpan duration,
        DiscoveryOptions options,
        CancellationToken token)
    {
        try
        {
            return await ScanCoreAsync(duration, options, token).ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                _activeScans--;
                if (_activeScans == 0)
                    _scansDrained?.TrySetResult();
            }
        }
    }

    private async Task<IReadOnlyList<DiscoveryPacket>> ScanCoreAsync(
        TimeSpan duration,
        DiscoveryOptions options,
        CancellationToken token)
    {
        if (duration <= TimeSpan.Zero)
            return Array.Empty<DiscoveryPacket>();

        var results = new List<DiscoveryPacket>();
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        socket.ExclusiveAddressUse = false;
        socket.Bind(new IPEndPoint(IPAddress.Any, options.Port));
        using var client = new UdpClient { Client = socket };
        using var timeout = new CancellationTokenSource(duration, _timeProvider);
        using var linkedTimeout = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token, _lifetimeCts.Token);

        while (!linkedTimeout.IsCancellationRequested)
        {
            UdpReceiveResult received;
            try
            {
                received = await client.ReceiveAsync(linkedTimeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            try
            {
                var packet = JsonSerializer.Deserialize<DiscoveryPacket>(received.Buffer);
                if (packet is null ||
                    packet.Magic != DiscoveryPacket.ExpectedMagic ||
                    packet.PacketVersion != DiscoveryPacket.CurrentPacketVersion)
                {
                    continue;
                }

                results.Add(packet with { RemoteEndPoint = received.RemoteEndPoint });
            }
            catch (JsonException)
            {
            }
        }

        return results;
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposeGate)
        {
            if (_disposeTask is null)
            {
                Volatile.Write(ref _disposed, 1);
                _disposeTask = DisposeCoreAsync();
            }

            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        _lifetimeCts.Cancel();
        await _lifecycleGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            DiscoveryAdvertisementId[] ids;
            lock (_gate)
                ids = _advertisements.Keys.ToArray();

            foreach (var id in ids)
                await StopAdvertiseCoreAsync(id).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }

        Task scansDrained;
        lock (_gate)
        {
            scansDrained = _activeScans == 0
                ? Task.CompletedTask
                : (_scansDrained ??= new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        }
        await scansDrained.ConfigureAwait(false);
        _lifetimeCts.Dispose();
    }

    private async Task AdvertiseLoopAsync(DiscoveryPacket packet, DiscoveryOptions options, CancellationToken token)
    {
        using var client = new UdpClient(AddressFamily.InterNetwork)
        {
            EnableBroadcast = true
        };
        var endPoint = new IPEndPoint(IPAddress.Broadcast, options.Port);
        var payload = JsonSerializer.SerializeToUtf8Bytes(packet);

        while (!token.IsCancellationRequested)
        {
            try
            {
                await client.SendAsync(payload, endPoint, token).ConfigureAwait(false);
                await Task.Delay(options.AdvertiseInterval, _timeProvider, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                await Task.Delay(options.AdvertiseInterval, _timeProvider, token).ConfigureAwait(false);
            }
        }
    }

    private sealed record AdvertisementRun(CancellationTokenSource Cancellation, Task LoopTask);

    private bool IsDisposed => Volatile.Read(ref _disposed) == 1;
}

/// <summary>
/// 负责局域网广告、扫描和房间元数据处理。
/// </summary>
public sealed class NetDiscovery : IAsyncDisposable
{
    private readonly GameNetOptions _options;
    private readonly IDiscoveryBackend _backend;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CancellationTokenSource _disposeCancellation = new();
    private readonly SemaphoreSlim _advertiseGate = new(1, 1);
    private readonly object _schemaGate = new();
    private readonly Dictionary<uint, Type> _metadataTypesBySchemaId = new();
    private readonly Dictionary<Type, uint> _schemaIdsByMetadataType = new();
    private readonly object _disposeGate = new();
    private DiscoveryAdvertisementId _advertisementId;
    private LanAdvertiseInfo? _advertiseInfo;
    private int _disposed;
    private Task? _disposeTask;

    /// <summary>
    /// 创建基于内存发现网络的发现组件。
    /// </summary>
    public NetDiscovery(GameNetOptions options)
        : this(options, new UdpDiscoveryNetwork(options?.TimeProvider))
    {
    }

    /// <summary>
    /// 创建基于指定发现 backend 的发现组件。
    /// </summary>
    public NetDiscovery(GameNetOptions options, IDiscoveryBackend backend, NetDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(backend);
        GameNet.ValidateOptions(options);

        _options = options;
        _backend = backend;
        Diagnostics = diagnostics ?? new NetDiagnostics(options.EventDispatcher);
    }

    /// <summary>
    /// 发现组件的诊断计数器。
    /// </summary>
    public NetDiagnostics Diagnostics { get; }

    /// <summary>
    /// 启动持续房间广告。
    /// </summary>
    public async Task<NetSessionResult> StartAdvertiseAsync<TMetadata>(LanAdvertiseInfo info, TMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (IsDisposed)
            return new NetSessionResult(NetSessionStatus.ObjectDisposed);

        await _advertiseGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsDisposed)
                return new NetSessionResult(NetSessionStatus.ObjectDisposed);
            if (_advertisementId != DiscoveryAdvertisementId.None)
                return new NetSessionResult(NetSessionStatus.InvalidState, "Discovery advertise is already running.");
            if (string.IsNullOrWhiteSpace(info.RoomId))
                return new NetSessionResult(NetSessionStatus.InvalidState, "RoomId cannot be empty.");
            if (info.GamePort <= 0 || info.GamePort > 65535)
                return new NetSessionResult(NetSessionStatus.InvalidState, "GamePort must be between 1 and 65535.");
            if (!ValidateAndRegisterMetadataSchema<TMetadata>(info.MetadataSchemaId, out var schemaError))
                return new NetSessionResult(NetSessionStatus.InvalidState, schemaError);

            if (!TryCreatePacket(info, metadata, out var packet, out var error))
                return new NetSessionResult(NetSessionStatus.TransportFailed, error);

            _advertisementId = await _backend.StartAdvertiseAsync(packet, _options.Discovery).ConfigureAwait(false);
            _advertiseInfo = info;
            return NetSessionResult.Ok();
        }
        finally
        {
            _advertiseGate.Release();
        }
    }

    /// <summary>
    /// 更新当前广告的公开元数据。
    /// </summary>
    public async Task<NetSessionResult> UpdateAdvertiseMetadataAsync<TMetadata>(TMetadata metadata)
    {
        if (IsDisposed)
            return new NetSessionResult(NetSessionStatus.ObjectDisposed);

        await _advertiseGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsDisposed)
                return new NetSessionResult(NetSessionStatus.ObjectDisposed);

            if (_advertisementId == DiscoveryAdvertisementId.None || _advertiseInfo is null)
                return new NetSessionResult(NetSessionStatus.InvalidState, "Discovery advertise has not started.");
            if (!ValidateAndRegisterMetadataSchema<TMetadata>(_advertiseInfo.MetadataSchemaId, out var schemaError))
                return new NetSessionResult(NetSessionStatus.InvalidState, schemaError);

            if (!TryCreatePacket(_advertiseInfo, metadata, out var packet, out var error))
                return new NetSessionResult(NetSessionStatus.TransportFailed, error);

            await _backend.UpdateAdvertiseAsync(_advertisementId, packet, _options.Discovery).ConfigureAwait(false);
            return NetSessionResult.Ok();
        }
        finally
        {
            _advertiseGate.Release();
        }
    }

    /// <summary>
    /// 停止当前房间广告。
    /// </summary>
    public async Task<NetSessionResult> StopAdvertiseAsync()
    {
        if (IsDisposed)
            return new NetSessionResult(NetSessionStatus.ObjectDisposed);

        await _advertiseGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsDisposed)
                return new NetSessionResult(NetSessionStatus.ObjectDisposed);

            if (_advertisementId == DiscoveryAdvertisementId.None)
                return NetSessionResult.Ok();

            await _backend.StopAdvertiseAsync(_advertisementId).ConfigureAwait(false);
            _advertisementId = DiscoveryAdvertisementId.None;
            _advertiseInfo = null;
            return NetSessionResult.Ok();
        }
        finally
        {
            _advertiseGate.Release();
        }
    }

    /// <summary>
    /// 执行一次房间扫描。
    /// </summary>
    /// <typeparam name="TMetadata">房间公开元数据类型。</typeparam>
    /// <param name="duration">后端扫描窗口；非正值返回空结果。</param>
    /// <param name="token">取消标记。</param>
    /// <returns>与当前应用和元数据 schema 匹配的房间快照。</returns>
    /// <exception cref="InvalidOperationException"><typeparamref name="TMetadata"/> 未声明发现元数据 schema。</exception>
    /// <exception cref="ObjectDisposedException">发现组件已释放。</exception>
    /// <exception cref="OperationCanceledException">调用方取消或发现组件开始释放。</exception>
    public Task<IReadOnlyList<LanScanResult<TMetadata>>> ScanAsync<TMetadata>(
        TimeSpan duration,
        CancellationToken token = default) =>
        ScanAsync<TMetadata>(duration, GetRequiredMetadataSchemaId<TMetadata>(), token);

    /// <summary>
    /// 按指定 metadata schema 执行一次房间扫描。
    /// </summary>
    /// <typeparam name="TMetadata">房间公开元数据类型。</typeparam>
    /// <param name="duration">后端扫描窗口；非正值返回空结果。</param>
    /// <param name="metadataSchemaId">预期的元数据 schema 标识。</param>
    /// <param name="token">取消标记。</param>
    /// <returns>与当前应用和指定 schema 匹配的房间快照。</returns>
    /// <exception cref="InvalidOperationException">元数据类型已绑定到不同 schema。</exception>
    /// <exception cref="ObjectDisposedException">发现组件已释放。</exception>
    /// <exception cref="OperationCanceledException">调用方取消或发现组件开始释放。</exception>
    public Task<IReadOnlyList<LanScanResult<TMetadata>>> ScanAsync<TMetadata>(
        TimeSpan duration,
        uint metadataSchemaId,
        CancellationToken token = default) =>
        ScanCoreAsync<TMetadata>(duration, metadataSchemaId, token);

    internal async Task<IReadOnlyList<LanScanResult<TMetadata>>> ScanCoreAsync<TMetadata>(
        TimeSpan duration,
        uint metadataSchemaId,
        CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (!ValidateAndRegisterMetadataSchema<TMetadata>(metadataSchemaId, out var schemaError))
            throw new InvalidOperationException(schemaError);

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(token, _disposeCancellation.Token);
        linkedCancellation.Token.ThrowIfCancellationRequested();
        if (duration <= TimeSpan.Zero)
            return Array.Empty<LanScanResult<TMetadata>>();
        var results = new List<LanScanResult<TMetadata>>();
        foreach (var packet in await _backend.ScanAsync(duration, _options.Discovery, linkedCancellation.Token).ConfigureAwait(false))
        {
            if (packet.Magic != DiscoveryPacket.ExpectedMagic ||
                packet.PacketVersion != DiscoveryPacket.CurrentPacketVersion)
            {
                Diagnostics.AddDroppedPacket();
                Diagnostics.RecordError(new NetError("DiscoveryEnvelopeInvalid", "Discovery packet magic or version is invalid."));
                continue;
            }
            if (packet.ApplicationId != _options.Application.ApplicationId)
                continue;
            if (packet.MetadataSchemaId != metadataSchemaId)
                continue;
            if (string.IsNullOrWhiteSpace(packet.RoomId) || packet.GamePort <= 0 || packet.GamePort > 65535)
            {
                Diagnostics.AddDroppedPacket();
                Diagnostics.RecordError(new NetError("DiscoveryEndpointInvalid", "Discovery room id or game port is invalid."));
                continue;
            }
            if (packet.MetadataPayload is null)
            {
                Diagnostics.AddDroppedPacket();
                Diagnostics.RecordError(new NetError("DiscoveryPayloadMissing", "Discovery metadata payload is missing."));
                continue;
            }
            if (packet.MetadataPayload.Length > _options.Discovery.MaxMetadataPayloadSize)
            {
                Diagnostics.AddDroppedPacket();
                Diagnostics.RecordError(new NetError("DiscoveryPacketTooLarge", "Discovery metadata payload exceeds MaxMetadataPayloadSize."));
                continue;
            }
            if (packet.PayloadLength != packet.MetadataPayload.Length)
            {
                Diagnostics.AddDroppedPacket();
                Diagnostics.RecordError(new NetError("DiscoveryPayloadLengthMismatch", "Discovery metadata payload length does not match the envelope."));
                continue;
            }

            TMetadata? metadata;
            try
            {
                metadata = JsonSerializer.Deserialize<TMetadata>(packet.MetadataPayload, _jsonOptions);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                Diagnostics.AddDroppedPacket();
                Diagnostics.RecordError(new NetError("DiscoveryMetadataDecodeFailed", ex.Message, ex));
                continue;
            }

            if (metadata is null)
            {
                Diagnostics.AddDroppedPacket();
                continue;
            }

            results.Add(new LanScanResult<TMetadata>
            {
                RoomId = packet.RoomId,
                GamePort = packet.GamePort,
                IsJoinable = packet.ProtocolVersion == _options.Application.ProtocolVersion,
                ApplicationId = packet.ApplicationId,
                ProtocolVersion = packet.ProtocolVersion,
                MetadataSchemaId = packet.MetadataSchemaId,
                Metadata = metadata,
                EndPoint = packet.RemoteEndPoint,
                EstimatedLatency = packet.EstimatedLatency
            });
        }

        return results;
    }

    /// <summary>
    /// 创建连续房间浏览器。
    /// </summary>
    public Task<LanBrowser<TMetadata>> StartBrowserAsync<TMetadata>()
    {
        return StartBrowserAsync<TMetadata>(GetRequiredMetadataSchemaId<TMetadata>());
    }

    /// <summary>
    /// 按指定 metadata schema 创建连续房间浏览器。
    /// </summary>
    public Task<LanBrowser<TMetadata>> StartBrowserAsync<TMetadata>(uint metadataSchemaId)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (!ValidateAndRegisterMetadataSchema<TMetadata>(metadataSchemaId, out var schemaError))
            throw new InvalidOperationException(schemaError);

        return Task.FromResult(new LanBrowser<TMetadata>(
            this,
            _options.TimeProvider,
            _options.Discovery.RoomTimeout,
            _options.Discovery.AdvertiseInterval,
            _options.Discovery.DefaultScanTimeout,
            metadataSchemaId,
            _disposeCancellation.Token));
    }

    /// <summary>
    /// 释放发现组件并停止当前广告。
    /// </summary>
    public ValueTask DisposeAsync()
    {
        lock (_disposeGate)
        {
            if (_disposeTask is null)
            {
                Volatile.Write(ref _disposed, 1);
                _disposeCancellation.Cancel();
                _disposeTask = Task.Run(DisposeCoreAsync);
            }

            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        await _advertiseGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_advertisementId != DiscoveryAdvertisementId.None)
            {
                await _backend.StopAdvertiseAsync(_advertisementId).ConfigureAwait(false);
                _advertisementId = DiscoveryAdvertisementId.None;
                _advertiseInfo = null;
            }
        }
        finally
        {
            _advertiseGate.Release();
        }

        await _backend.DisposeAsync().ConfigureAwait(false);
        _disposeCancellation.Dispose();
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
        if (metadata is null)
        {
            packet = null!;
            error = "Discovery metadata cannot be null.";
            Diagnostics.RecordError(new NetError("DiscoveryMetadataMissing", error));
            return false;
        }
        if (!TryValidatePublicMetadata(typeof(TMetadata), out error))
        {
            Diagnostics.RecordError(new NetError("DiscoveryPrivateMetadata", error ?? "Discovery metadata is private."));
            packet = null!;
            return false;
        }

        byte[] payload;
        try
        {
            payload = JsonSerializer.SerializeToUtf8Bytes(metadata, _jsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            packet = null!;
            error = ex.Message;
            Diagnostics.RecordError(new NetError("DiscoveryMetadataEncodeFailed", ex.Message, ex));
            return false;
        }
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
            PayloadLength: payload.Length,
            MetadataPayload: payload);
        error = null;
        return true;
    }

    private static bool TryValidatePublicMetadata(Type metadataType, out string? error)
    {
        return TryValidatePublicMetadata(metadataType, new HashSet<Type>(), out error);
    }

    private static bool TryValidatePublicMetadata(Type metadataType, HashSet<Type> visitedTypes, out string? error)
    {
        metadataType = Nullable.GetUnderlyingType(metadataType) ?? metadataType;
        if (IsTerminalMetadataType(metadataType) || !visitedTypes.Add(metadataType))
        {
            error = null;
            return true;
        }

        foreach (var property in metadataType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.Name == "HasPassword" && property.PropertyType == typeof(bool))
                continue;

            var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;
            if (IsPrivateMetadataName(name))
            {
                error = $"Discovery metadata property '{property.Name}' is private data and cannot be advertised.";
                return false;
            }

            foreach (var nestedType in GetNestedMetadataTypes(property.PropertyType))
            {
                if (!TryValidatePublicMetadata(nestedType, visitedTypes, out error))
                    return false;
            }
        }

        error = null;
        return true;
    }

    private static bool IsPrivateMetadataName(string name)
    {
        return name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("PrivateKey", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Secret", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<Type> GetNestedMetadataTypes(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (IsTerminalMetadataType(type))
            yield break;

        if (type.IsArray)
        {
            var elementType = type.GetElementType();
            if (elementType is not null)
                yield return elementType;
            yield break;
        }

        if (type.IsGenericType &&
            type.GetGenericTypeDefinition() != typeof(KeyValuePair<,>))
        {
            foreach (var argument in type.GetGenericArguments())
                yield return argument;
            yield break;
        }

        yield return type;
    }

    private static bool IsTerminalMetadataType(Type type)
    {
        return type.IsPrimitive ||
               type.IsEnum ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(Guid) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan);
    }

    private static uint GetRequiredMetadataSchemaId<TMetadata>()
    {
        var attribute = Attribute.GetCustomAttribute(typeof(TMetadata), typeof(DiscoveryMetadataAttribute)) as DiscoveryMetadataAttribute;
        if (attribute is null)
        {
            throw new InvalidOperationException(
                $"Discovery metadata type '{typeof(TMetadata).FullName}' must declare {nameof(DiscoveryMetadataAttribute)} or use an overload with an explicit metadata schema id.");
        }

        return attribute.SchemaId;
    }

    private bool ValidateAndRegisterMetadataSchema<TMetadata>(uint metadataSchemaId, out string? error)
    {
        var metadataType = typeof(TMetadata);
        var attribute = Attribute.GetCustomAttribute(metadataType, typeof(DiscoveryMetadataAttribute)) as DiscoveryMetadataAttribute;
        if (attribute is null)
        {
            return RegisterMetadataSchema(metadataType, metadataSchemaId, metadataSchemaId.ToString(), out error);
        }

        if (attribute.SchemaId == metadataSchemaId)
            return RegisterMetadataSchema(metadataType, metadataSchemaId, attribute.SchemaKey, out error);

        error = $"MetadataSchemaId '{metadataSchemaId}' does not match metadata type '{metadataType.FullName}' schema id '{attribute.SchemaId}'.";
        return false;
    }

    private bool RegisterMetadataSchema(Type metadataType, uint metadataSchemaId, string schemaKey, out string? error)
    {
        lock (_schemaGate)
        {
            if (_metadataTypesBySchemaId.TryGetValue(metadataSchemaId, out var existingType) &&
                existingType != metadataType)
            {
                error = $"Discovery metadata schema '{schemaKey}' conflicts with '{existingType.FullName}'.";
                return false;
            }

            if (_schemaIdsByMetadataType.TryGetValue(metadataType, out var existingSchemaId) &&
                existingSchemaId != metadataSchemaId)
            {
                error = $"Discovery metadata type '{metadataType.FullName}' is already registered with schema id '{existingSchemaId}'.";
                return false;
            }

            _metadataTypesBySchemaId[metadataSchemaId] = metadataType;
            _schemaIdsByMetadataType[metadataType] = metadataSchemaId;
        }

        error = null;
        return true;
    }
}

/// <summary>
/// 发现后端交换的版本化房间广告数据包。
/// </summary>
/// <param name="Magic">协议魔数。</param>
/// <param name="PacketVersion">数据包格式版本。</param>
/// <param name="ApplicationId">应用标识。</param>
/// <param name="ProtocolVersion">应用协议版本。</param>
/// <param name="RoomId">房间标识。</param>
/// <param name="GamePort">游戏服务端口。</param>
/// <param name="MetadataSchemaId">元数据 schema 标识。</param>
/// <param name="PayloadLength">声明的元数据字节数。</param>
/// <param name="MetadataPayload">UTF-8 JSON 元数据。</param>
public sealed record DiscoveryPacket(
    uint Magic,
    ushort PacketVersion,
    Guid ApplicationId,
    int ProtocolVersion,
    string RoomId,
    int GamePort,
    uint MetadataSchemaId,
    int PayloadLength,
    byte[] MetadataPayload)
{
    /// <summary>发现协议魔数。</summary>
    public const uint ExpectedMagic = 0x53464E44;

    /// <summary>当前数据包格式版本。</summary>
    public const ushort CurrentPacketVersion = 1;

    /// <summary>后端观察到的远端地址；内存后端可为空。</summary>
    [JsonIgnore]
    public IPEndPoint? RemoteEndPoint { get; init; }

    /// <summary>后端估算的往返延迟；无法估算时为空。</summary>
    [JsonIgnore]
    public TimeSpan? EstimatedLatency { get; init; }
}
