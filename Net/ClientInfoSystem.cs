using SimpleFramework.Net.Connection;
using SimpleFramework.Utility;

namespace SimpleFramework.Net;

/// <summary>
/// 客户端信息接口 - 用户自定义的 ClientInfo 必须实现此接口
/// </summary>
public interface IClientInfo
{
    /// <summary>
    /// 用户唯一标识（必须）
    /// </summary>
    string Uid { get; set; }

    /// <summary>
    /// 网络层客户端ID（由框架设置）
    /// </summary>
    long ClientId { get; set; }
}


/// <summary>
/// 客户端信息同步协议
/// </summary>
[Protocol]
public struct ClientInfoSyncProtocol
{
    /// <summary>
    /// 序列化后的客户端信息
    /// </summary>
    public byte[] InfoData;
}


/// <summary>
/// 客户端信息变更事件
/// </summary>
public readonly struct ClientInfoChangedEvent<TInfo>(long clientId, TInfo info, bool isNew)
    where TInfo : struct, IClientInfo
{
    public long ClientId => clientId;
    public TInfo Info => info;
    public bool IsNew => isNew;
}

/// <summary>
/// 客户端信息配置
/// </summary>
/// <typeparam name="TInfo">用户自定义的客户端信息类型</typeparam>
public class ClientInfoConfig<TInfo> where TInfo : struct, IClientInfo
{
    /// <summary>
    /// 创建本地客户端信息的工厂函数
    /// </summary>
    public required Func<TInfo> CreateLocalInfo { get; init; }
}


/// <summary>
/// 客户端信息数据模型 - 负责存储和查询客户端信息
/// </summary>
/// <typeparam name="TInfo">用户自定义的客户端信息类型</typeparam>
public class ClientInfoModel<TInfo> : AbstractModel where TInfo : struct, IClientInfo
{
    private readonly Dictionary<long, TInfo> _clientInfos = new();
    private readonly Dictionary<string, long> _uidToClientId = new();

    /// <summary>
    /// 本地客户端信息
    /// </summary>
    public TInfo LocalInfo { get; private set; }

    protected override void OnInitialize()
    {
    }

    #region 数据操作（供 System 调用）

    /// <summary>
    /// 设置本地信息
    /// </summary>
    internal void SetLocalInfo(TInfo info)
    {
        LocalInfo = info;
        AddClientInfo(info);
    }

    /// <summary>
    /// 添加客户端信息
    /// </summary>
    internal void AddClientInfo(TInfo info)
    {
        // 检查是否存在相同 Uid 但不同 ClientId 的旧记录（断线重连场景）
        if (_uidToClientId.TryGetValue(info.Uid, out var oldClientId) && oldClientId != info.ClientId)
        {
            _clientInfos.Remove(oldClientId);
            NetLog.Info($"清理旧的客户端映射: {info.Uid} -> {oldClientId}");
        }
        
        var isNew = !_uidToClientId.ContainsKey(info.Uid);
        _clientInfos[info.ClientId] = info;
        _uidToClientId[info.Uid] = info.ClientId;

        this.SendEvent(new ClientInfoChangedEvent<TInfo>(info.ClientId, info, isNew));
        NetLog.Info($"客户端信息{(isNew ? "添加" : "更新")}: {info.Uid} -> {info.ClientId}");
    }

    /// <summary>
    /// 移除客户端信息
    /// </summary>
    internal bool RemoveClientInfo(long clientId)
    {
        if (!_clientInfos.TryGetValue(clientId, out var info)) return false;

        _clientInfos.Remove(clientId);
        _uidToClientId.Remove(info.Uid);
        NetLog.Info($"客户端信息移除: {info.Uid} -> {clientId}");
        return true;
    }

    /// <summary>
    /// 清空所有数据
    /// </summary>
    internal void Clear()
    {
        _clientInfos.Clear();
        _uidToClientId.Clear();
        LocalInfo = default;
    }

    #endregion

    #region 公开查询接口（只读）

    /// <summary>
    /// 获取所有客户端信息
    /// </summary>
    public IReadOnlyDictionary<long, TInfo> GetAllClientInfos() => _clientInfos;

    /// <summary>
    /// 根据clientId获取客户端信息
    /// </summary>
    public TInfo? GetClientInfo(long clientId)
        => _clientInfos.TryGetValue(clientId, out var info) ? info : null;

    /// <summary>
    /// 根据uid获取客户端信息
    /// </summary>
    public TInfo? GetClientInfoByUid(string uid)
        => _uidToClientId.TryGetValue(uid, out var clientId) ? GetClientInfo(clientId) : null;

    /// <summary>
    /// 根据uid获取clientId
    /// </summary>
    public long? GetClientIdByUid(string uid)
        => _uidToClientId.TryGetValue(uid, out var clientId) ? clientId : null;

    /// <summary>
    /// 根据clientId获取uid
    /// </summary>
    public string? GetUidByClientId(long clientId)
        => _clientInfos.TryGetValue(clientId, out var info) ? info.Uid : null;

    #endregion

    protected override void OnUninitialize() => Clear();
}


/// <summary>
/// 客户端信息管理系统 - 负责同步和协调客户端信息
/// <para>此 System 会自动注册 <see cref="ClientInfoModel{TInfo}"/></para>
/// </summary>
/// <typeparam name="TInfo">用户自定义的客户端信息类型</typeparam>
public class ClientInfoSystem<TInfo>(ClientInfoConfig<TInfo> config) : AbstractSystem
    where TInfo : struct, IClientInfo
{
    private readonly DisposableGroup _events = new();
    private readonly ProtocolHandler _protocolHandler = new(NetDefine.SyncGuard);

    private ConnectionModel ConnModel => this.GetModel<ConnectionModel>();
    private ClientInfoModel<TInfo> InfoModel => this.GetModel<ClientInfoModel<TInfo>>();
    private ITransfer Transfer => this.GetUtility<ITransfer>();

    protected override void OnInitialize()
    {
        // 声明依赖
        this.Require<ConnectionModel>();
        this.Require<ITransfer>();
        
        // 自动注册关联的 Model
        Domain.RegisterModel(new ClientInfoModel<TInfo>());

        _protocolHandler.RegisterExecutingProtocol();
        _protocolHandler.RegisterHandler<ClientInfoSyncProtocol>(OnReceiveClientInfo);

        InitEvents();
    }

    private void InitEvents()
    {
        Transfer.DataReceived += OnDataReceived;

        _events.Add(this.RegisterEvent<ServerCreateEvent>(OnServerCreate));
        _events.Add(this.RegisterEvent<ServerStopEvent>(OnServerStop));
        _events.Add(this.RegisterEvent<ClientConnectEvent>(OnClientConnect));
        _events.Add(this.RegisterEvent<PeerConnectedEvent>(OnPeerConnected));
        _events.Add(this.RegisterEvent<PeerDisconnectedEvent>(OnPeerDisconnected));
        _events.Add(this.RegisterEvent<ServerDisconnectedEvent>(OnServerDisconnected));
    }

    private void OnDataReceived(byte[] data) => _protocolHandler.HandleData(data);

    #region 事件处理

    private void OnServerCreate(ServerCreateEvent e)
    {
        if (e.Reason != TransportReason.Ok) return;
        InfoModel.Clear();
        var info = CreateLocalInfo(ConnModel.ServerId);
        InfoModel.SetLocalInfo(info);
    }

    private void OnServerStop(ServerStopEvent e) => InfoModel.Clear();

    private void OnClientConnect(ClientConnectEvent e)
    {
        if (e.Reason != TransportReason.Ok) return;
        InfoModel.Clear();
        var info = CreateLocalInfo(ConnModel.ClientId);
        InfoModel.SetLocalInfo(info);
        BroadcastLocalInfo();
    }

    private void OnPeerConnected(PeerConnectedEvent e)
    {
        SendInfoTo(e.PeerId, InfoModel.LocalInfo);
    }

    private void OnPeerDisconnected(PeerDisconnectedEvent e)
    {
        InfoModel.RemoveClientInfo(e.PeerId);
    }

    private void OnServerDisconnected(ServerDisconnectedEvent e) => InfoModel.Clear();

    #endregion

    #region 公开接口

    /// <summary>
    /// 更新本地客户端信息并同步给所有已连接的客户端
    /// </summary>
    /// <param name="newInfo">新的客户端信息（ClientId 会被自动保留）</param>
    public void UpdateLocalInfo(TInfo newInfo)
    {
        if (!ConnModel.IsConnected()) return;
        
        // 保留原有的 ClientId
        newInfo.ClientId = InfoModel.LocalInfo.ClientId;
        InfoModel.SetLocalInfo(newInfo);
        BroadcastLocalInfo();
    }

    #endregion

    #region 协议处理

    private TInfo CreateLocalInfo(long clientId)
    {
        var info = config.CreateLocalInfo();
        info.ClientId = clientId;
        return info;
    }

    private void OnReceiveClientInfo(ClientInfoSyncProtocol protocol)
    {
        try
        {
            var info = SerializeUtil.Deserialize<TInfo>(protocol.InfoData);
            InfoModel.AddClientInfo(info);
        }
        catch (Exception ex)
        {
            NetLog.Error("反序列化客户端信息失败", ex);
        }
    }

    private void SendInfoTo(long targetClientId, TInfo info)
    {
        var protocol = new ClientInfoSyncProtocol
        {
            InfoData = SerializeUtil.SerializeBytes(info)
        };

        if (_protocolHandler.PackData(protocol, out var bytes))
        {
            Transfer.SendData(targetClientId, bytes);
        }
    }

    private void BroadcastLocalInfo()
    {
        foreach (var peerId in ConnModel.GetAllPeerIds())
        {
            SendInfoTo(peerId, InfoModel.LocalInfo);
        }
    }

    #endregion

    protected override void OnUninitialize()
    {
        Transfer.DataReceived -= OnDataReceived;
        _events.Dispose();
        _protocolHandler.Clear();
    }
}