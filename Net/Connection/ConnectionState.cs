using SimpleFramework.Patterns;
using SimpleFramework.Utility;

namespace SimpleFramework.Net.Connection;


/// <summary>
/// 状态基类
/// </summary>
/// <param name="model">连接Model</param>
internal abstract class ConnState(ConnectionModel model) : State
{
    /// <summary>
    /// 连接Model
    /// </summary>
    protected readonly ConnectionModel Model = model;
    
    protected virtual void OnDataReceived(byte[] data)
    {
        Model.ProtocolHandler.HandleData(data);
    }
}


/// <summary>
/// 断开连接状态
/// </summary>
/// <param name="model"></param>
internal class OfflineState(ConnectionModel model) : ConnState(model)
{
    public override void Enter()
    {
        if (Model.Transport.IsServer())
        {
            Model.Transport.StopServer();
        }
        else if (Model.Transport.IsConnected())
        {
            Model.Transport.StopClient();
        }
        Model.ConnectionIds.Clear();
    }
}

/// <summary>
/// 创建服务器状态
/// </summary>
/// <param name="model"></param>
internal class StartHostingState(ConnectionModel model) : ConnState(model)
{
    public override void Enter()
    {
        Model.Transport.ServerCreated += OnServerCreated;
    }

    public override void Update(float delta)
    {
        var config = Model.ServerConfig;
        try
        {
            Model.Transport.StartServer(config.Port);
        }
        catch (Exception e)
        {
            NetLog.Error(e.ToString());
            Dispatch(ConnEvent.Stop);
        }
    }

    private void OnServerCreated(TransportReason reason)
    {
        Dispatch(reason == TransportReason.Ok ? ConnEvent.Ok: ConnEvent.Stop);
        Model.SendEvent(new ServerCreateEvent(reason));
    }

    public override void Exit()
    {
        Model.Transport.ServerCreated -= OnServerCreated;
    }
}

internal class HostingState(ConnectionModel model) : ConnState(model)
{
    /// <summary>
    /// 实际的建立连接的peer id set
    /// </summary>
    private readonly HashSet<long> _connectionIds = [];
    
    public override void Enter()
    {
        _connectionIds.Clear();
        Model.Transfer.DataReceived += OnDataReceived;
        Model.Transport.PeerConnected += OnPeerConnected;
        Model.Transport.PeerDisconnected += OnPeerDisconnected;
        Model.ProtocolHandler.RegisterHandler<RequestApproveProtocol>(OnRequestApprove);
    }

    
    private void OnPeerConnected(long clientId)
    {
        _connectionIds.Add(clientId);
    }

    private void OnPeerDisconnected(long clientId)
    {
        _connectionIds.Remove(clientId);
        if (Model.ConnectionIds.Remove(clientId))
        {
            Model.SendEvent(new PeerDisconnectedEvent(clientId));
        }
    }

    private async void OnRequestApprove(RequestApproveProtocol protocol)
    {
        NetLog.Info($"收到 client:{protocol.ClientId} request, payload: {protocol.Payload}");
        var clientId = protocol.ClientId;
        
        // 人数满了
        if (_connectionIds.Count >= Model.ServerConfig.MaxPlayer)
        {
            ResponseApprove(clientId, TransportReason.ReachMaxConnections);
            return;
        }
        
        if (Model.AuthenticationFunc != null)
        {
            try
            {
                var result = await Model.AuthenticationFunc.Invoke(protocol.Payload);
                ResponseApprove(clientId, result ? TransportReason.Ok : TransportReason.AuthenticationFailed);
            }
            catch (Exception e)
            {
                ResponseApprove(clientId, TransportReason.Failed);
                NetLog.Error("认证发生错误.", e);
            }
        }
        else
        {
            ResponseApprove(clientId, TransportReason.Ok);
        }
    }

    private void ResponseApprove(long clientId, TransportReason reason)
    {
        var isApprove = reason == TransportReason.Ok;
        NetLog.Info($"服务端{(isApprove ? "批准" : "拒绝")}来自 client:{clientId} 连接, reason: {reason}");
        if (!_connectionIds.Contains(clientId)) return;
        if (Model.SendData(clientId, new ResponseApproveProtocol(reason)) && isApprove)
        {
            SendPeerConnect(clientId); 
        }
    }

    private void SendPeerConnect(long joinId)
    {
        Model.ConnectionIds.Add(joinId);
        
        foreach (var clientId in Model.GetAllPeerIds())
        {
            if (clientId == joinId) continue;
            Model.SendData(clientId, new PeerConnectProtocol(joinId));
        }
        
        foreach (var alreadyId in Model.GetAllIds().Except([joinId]).Union([Model.ServerId]))
        {
            Model.SendData(joinId, new PeerConnectProtocol(alreadyId));
        }
        
        Model.SendEvent(new PeerConnectedEvent(joinId));
    }

    public override void Exit()
    {
        _connectionIds.Clear();
        Model.Transfer.DataReceived -= OnDataReceived;
        Model.Transport.PeerConnected -= OnPeerConnected;
        Model.Transport.PeerDisconnected -= OnPeerDisconnected;
        Model.ProtocolHandler.UnRegisterHandler<RequestApproveProtocol>();
    }
}

internal class ConnectingState(ConnectionModel model) : ConnState(model)
{
    protected bool IsReceiveApprove;  // 是否收到服务端的认证回复
    private bool _isApproved;  // 服务端是否同意连接
    private long _enterTick;  // 进入状态时的tick
    
    public override void Enter()
    {
        IsReceiveApprove = false;
        _isApproved = false;
        _enterTick = DateTime.UtcNow.Ticks;
        
        Model.Transport.ConnectionDone += OnConnectionDone;
        Model.Transport.ServerDisconnected += OnServerDisconnected;
        Model.ProtocolHandler.RegisterHandler<ResponseApproveProtocol>(OnResponseApprove);
        Model.Transfer.DataReceived += OnDataReceived;
    }

    public override void Update(float delta)
    {
        ConnectToServer();
    }

    protected virtual void ConnectToServer()
    {
        try
        {
            var config = Model.ClientConfig;
            Model.Transport.StartClient(config.Addr, config.Port, config.Timeout);
            StartTimeoutTimer();
        }
        catch (Exception e)
        {
            NetLog.Error(e.ToString());
            Dispatch(ConnEvent.Stop);
        }
    }

    protected virtual void OnConnectionDone(TransportReason reason)
    {
        if (reason == TransportReason.Ok)
        {
            StartRequest();
        }
        else
        {
            Dispatch(ConnEvent.Stop);
        }
    }

    protected void StartRequest()
    {
        IsReceiveApprove = false;
        _isApproved = false;
        var data = new RequestApproveProtocol(Model.ClientId, Model.Payload);
        NetLog.Info($"发送request到服务端 from: {data.ClientId}");
        if (!Model.SendData(Model.ServerId, data))
        {
            Dispatch(ConnEvent.Stop);
        }
    }
    
    protected bool IsTimerExpired(long tick) => tick != _enterTick;

    protected long CurTick => _enterTick;
    
    protected virtual async Task StartTimeoutTimer()
    {
        var timeTick = CurTick;
        await TaskUtil.WaitUntil(() => IsReceiveApprove, Model.ClientConfig.Timeout);
        if (!IsTimerExpired(timeTick) && !_isApproved)
        {
            Dispatch(ConnEvent.Stop);
            Model.SendEvent(new ClientConnectEvent(TransportReason.Timeout));
        }
    }
    
    private void OnServerDisconnected(TransportReason reason)
    {
        Dispatch(ConnEvent.Stop);
        Model.SendEvent(new ServerDisconnectedEvent(reason));
    }
    
    private void OnResponseApprove(ResponseApproveProtocol protocol)
    {
        var reason = protocol.Reason;
        _isApproved = reason == TransportReason.Ok;
        IsReceiveApprove = true;
        NetLog.Info($"连接{(_isApproved ? "成功": "失败")}, reason: {reason}");
        Dispatch(_isApproved ? ConnEvent.Ok : ConnEvent.Stop);
        Model.SendEvent(new ClientConnectEvent(reason));
    }
    
    public override void Exit()
    {
        IsReceiveApprove = false;
        _isApproved = false;
        _enterTick = 0;
        
        Model.Transport.ConnectionDone -= OnConnectionDone;
        Model.Transport.ServerDisconnected -= OnServerDisconnected;
        Model.ProtocolHandler.UnRegisterHandler<ResponseApproveProtocol>();
        Model.Transfer.DataReceived -= OnDataReceived;
    }
}

internal class ConnectedState(ConnectionModel model) : ConnState(model)
{
    
    public override void Enter()
    {
        Model.Transfer.DataReceived += OnDataReceived;
        Model.Transport.ServerDisconnected += OnServerDisconnected;
        Model.Transport.PeerDisconnected += OnPeerDisconnected;
        Model.ProtocolHandler.RegisterHandler<PeerConnectProtocol>(OnPeerConnected);
    }

    private void OnServerDisconnected(TransportReason reason)
    {
        NetLog.Info($"服务端断开 ：{reason}");
        switch (reason)
        {
            case TransportReason.ServerRejected:
            case TransportReason.ServerClosed:
            case TransportReason.AuthenticationFailed:
            case TransportReason.ReachMaxConnections:
                Dispatch(ConnEvent.Stop);
                break;
            default:
                Dispatch(ConnEvent.Retry);
                break;
        }
        Model.SendEvent(new ServerDisconnectedEvent(reason));
    }
    
    private void OnPeerConnected(PeerConnectProtocol protocol)
    {
        Model.ConnectionIds.Add(protocol.ClientId);
        Model.SendEvent(new PeerConnectedEvent(protocol.ClientId));
    }

    private void OnPeerDisconnected(long clientId)
    {
        if (Model.ConnectionIds.Remove(clientId))
        {
            Model.SendEvent(new PeerDisconnectedEvent(clientId));
        }
    }

    public override void Exit()
    {
        Model.Transfer.DataReceived -= OnDataReceived;
        Model.Transport.ServerDisconnected -= OnServerDisconnected;
        Model.Transport.PeerDisconnected -= OnPeerDisconnected;
        Model.ProtocolHandler.UnRegisterHandler<PeerConnectProtocol>();
    }
}

internal class ReconnectingState(ConnectionModel model) : ConnectingState(model)
{
    private int _remainRetryCnt;
    
    public override void Enter()
    {
        base.Enter();
        _remainRetryCnt = Model.ClientConfig.ReconnectTimes;
        Model.ConnectionIds.Clear();
    }

    private void Retry()
    {
        if (_remainRetryCnt > 0)
        {
            ConnectToServer();
        }
        else
        {
            Dispatch(ConnEvent.Stop);
            Model.SendEvent(new ClientConnectEvent(TransportReason.ReconnectFailed));
        }
    }

    protected override void ConnectToServer()
    {
        NetLog.Info($"重连中... 剩余{_remainRetryCnt}次");
        Model.SendEvent(new ClientReconnectEvent(_remainRetryCnt));
        _remainRetryCnt -= 1;
        Model.Transport.StopClient();
        base.ConnectToServer();
    }

    protected override void OnConnectionDone(TransportReason reason)
    {
        if (reason == TransportReason.Ok)
        {
            StartRequest();
        }
        else
        {
            Retry();
        }
    }
    
    protected override async Task StartTimeoutTimer()
    {
        var timeTick = CurTick;
        await TaskUtil.WaitUntil(() => IsReceiveApprove, Model.ClientConfig.Timeout);
        // 仅在服务端未回复状态的状态下重连
        if (!IsTimerExpired(timeTick) && !IsReceiveApprove)
        {
            Retry();
        }
    }
}
