using SimpleFramework.Net.Connection;
using SimpleFramework.Utility;

namespace SimpleFramework.Net;


/// <summary>
/// 一个用于同步网络间各种数据，状态的模块
/// <para>底层通过监听网络连接事件，比如<see cref="PeerConnectedEvent"/>等事件来实现功能</para>
/// <remarks>底层依赖<see cref="Connection.ConnectionModel"/></remarks>
/// <param name="playerUniqueId">玩家唯一id</param>
/// </summary>
public class NetSyncSystem(string playerUniqueId): AbstractSystem
{
    private ITransfer Transfer => this.GetUtility<ITransfer>();
    
    private ConnectionModel ConnModel => this.GetModel<ConnectionModel>();
    
    private readonly ProtocolHandler _protocolHandler = new (NetDefine.SyncGuard);
    
    private readonly Dictionary<ulong, Type> _eventTypes = new();

    private readonly Dictionary<Type, object> _globalData = new(); // 全局数据
    
    private readonly Dictionary<Type, Dictionary<string, object>> _playerPersonalData = new();  // 玩家数据

    private readonly Dictionary<Type, Dictionary<string, object>> _playerGlobalData = new();  // 玩家数据，但全局共享
    
    
    protected override void OnInitialize()
    {
        InitConnEvent();
        InitProtocolHandler();
        Transfer.DataReceived += OnDataReceived;
    }
    
    private void OnDataReceived(byte[] data)
    {
        _protocolHandler.HandleData(data);
    }

    protected override void OnUninitialize()
    {
        Transfer.DataReceived -= OnDataReceived;
    }

    private void InitConnEvent()
    {
        this.RegisterEvent<ServerCreateEvent>(OnServerCreate);
        this.RegisterEvent<ClientConnectEvent>(OnClientConnect);
    }

    private void OnClientConnect(ClientConnectEvent @event)
    {
        if (@event.Reason == TransportReason.Ok)
        {
        }
    }

    private void OnServerCreate(ServerCreateEvent @event)
    {
        if (@event.Reason == TransportReason.Ok)
        {
        }
    }

    private void InitProtocolHandler()
    {
        _protocolHandler.RegisterExecutingProtocol();
        _protocolHandler.RegisterHandler<SyncDataProtocol>(OnSyncData);
    }

    private void OnSyncData(SyncDataProtocol data)
    {
        if (_eventTypes.TryGetValue(data.TypeHash, out var type))
        {
            var realData = SerializeUtil.Deserialize(data.JsonData, type); 
        }
    }

    /// <summary>
    /// 注册需要同步的数据类型
    /// </summary>
    /// <remarks>注意: 数据类型必须在双端被注册之后才能被同步</remarks>
    /// <typeparam name="T"></typeparam>
    public void RegisterType<T>()
    {
        _eventTypes.Add(MiscUtil.TypeHash<T>(), typeof(T));
    }

    /// <summary>
    /// 同步数据
    /// </summary>
    /// <param name="scope">数据作用域</param>
    /// <param name="message">数据体</param>
    /// <typeparam name="T"></typeparam>
    public void SyncData<T>(EDataScope scope, T message)
    {
        var data = new SyncDataProtocol(scope, MiscUtil.TypeHash<T>(), SerializeUtil.Serialize(message));
        if (ConnModel.IsServer())
        {
            OnSyncData(data);
        }
        else if (_protocolHandler.PackData(message, out var bytes))
        {
            Transfer.SendData(ConnModel.ServerId, bytes);
        }
        else
        {
            NetLog.Error($"同步数据类型{nameof(T)}失败");
        }
    }

    public bool GetGlobalData<T>(out T? message)
    {
        var isSuccess = false;
        isSuccess = _globalData.TryGetValue(typeof(T), out var value);
        message = (T)value!;
        return isSuccess;
    }

    public bool GetPlayerGlobalData<T>(out T? message, string uniqueId = "")
    {
        uniqueId = string.IsNullOrEmpty(uniqueId) ? playerUniqueId : uniqueId;
        var isSuccess = false;
        message = default;
        if (!_playerGlobalData.TryGetValue(typeof(T), out var value)) return isSuccess;
        isSuccess = value.TryGetValue(uniqueId, out var result);
        message = (T)result!;
        return isSuccess;
    }
    
}


/// <summary>
/// 被同步数据的作用范围
/// </summary>
public enum EDataScope
{
    Global,  // 所有玩家可见
    PlayerGlobal,  // 和玩家相关，但是所有玩家可见
    Person,  // 仅玩家和服务端可见
}


[Protocol]
public struct SyncDataProtocol(EDataScope scope, ulong typeHash, string jsonData)
{
    public EDataScope Scope = scope;
    
    public ulong TypeHash = typeHash;
    
    public string JsonData = jsonData;
}