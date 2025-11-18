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
        Model.SendEvent(new ServerCreateEvent(reason));
        Dispatch(reason == TransportReason.Ok ? ConnEvent.Ok: ConnEvent.Stop);
    }

    public override void Exit()
    {
        Model.Transport.ServerCreated -= OnServerCreated;
    }
}

internal class HostingState(ConnectionModel model) : ConnState(model)
{
    public override void Enter()
    {
        Model.Transport.DataReceived += OnDataReceived;
        Model.Transport.PeerConnected += OnPeerConnected;
        Model.Transport.PeerDisconnected += OnPeerDisconnected;
        Model.ProtocolHandler.RegisterHandler<RequestApproveProtocol>(OnRequestApprove);
    }

    private void OnDataReceived(byte[] data)
    {
        Model.ProtocolHandler.HandleData(data);
    }
    
    private void OnPeerConnected(long clientId)
    {
        Model.ConnectionIds.Add(clientId);
        Model.SendEvent(new PeerConnectedEvent(clientId));
    }

    private void OnPeerDisconnected(long clientId)
    {
        Model.ConnectionIds.Remove(clientId);
        Model.SendEvent(new PeerDisconnectedEvent(clientId));
    }

    private async void OnRequestApprove(RequestApproveProtocol protocol)
    {
        NetLog.Info($"收到 client:{protocol.ClientId} request, payload: {protocol.Payload}");
        var clientId = protocol.ClientId;
        
        // 人数满了
        if (Model.Count + 1 > Model.ServerConfig.MaxPlayer)
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
        NetLog.Info($"服务端{(reason == TransportReason.Ok ? "批准" : "拒绝")}来自 client:{clientId} 连接, reason: {reason}");
        if (Model.ProtocolHandler.PackData(new ResponseApproveProtocol(reason), out var data))
        {
            if (Model.ConnectionIds.Contains(clientId))
            {
                Model.Transport.SendData(clientId, data);
            }
        }
        else
        {
            NetLog.Warning("服务端无法压缩数据包");
        }
    }

    public override void Exit()
    {
        Model.Transport.DataReceived -= OnDataReceived;
        Model.Transport.PeerConnected -= OnPeerConnected;
        Model.Transport.PeerDisconnected -= OnPeerDisconnected;
        Model.ProtocolHandler.UnRegisterHandler<RequestApproveProtocol>();
    }
}

internal class ConnectingState(ConnectionModel model) : ConnState(model)
{
    public override void Enter()
    {
        Model.Transport.ConnectionDone += OnConnectionDone;
    }

    public override void Update(float delta)
    {
        var config = Model.ClientConfig;
        try
        {
            Model.Transport.StartClient(config.Addr, config.Port, config.Timeout);
        }
        catch (Exception e)
        {
            NetLog.Error(e.ToString());
            Dispatch(ConnEvent.Stop);
        }
    }

    private void OnConnectionDone(TransportReason reason)
    {
        Dispatch(reason == TransportReason.Ok ? ConnEvent.Ok: ConnEvent.Stop);
    }
    
    public override void Exit()
    {
        Model.Transport.ConnectionDone -= OnConnectionDone;
    }
}

internal class ConnectedState(ConnectionModel model) : ConnState(model)
{
    private bool _isReceiveApprove = false;
    private bool _isApproved = false;
    
    public override void Enter()
    {
        _isReceiveApprove = false;
        _isApproved = false;
        Model.Transport.ServerDisconnected += OnServerDisconnected;
        Model.Transport.DataReceived += OnDataReceived;
        Model.Transport.PeerConnected += OnPeerConnected;
        Model.Transport.PeerDisconnected += OnPeerDisconnected;
        Model.ProtocolHandler.RegisterHandler<ResponseApproveProtocol>(OnResponseApprove);
    }

    public override async void Update(float delta)
    {
        var data = new RequestApproveProtocol(Model.ClientId, Model.Payload);
        NetLog.Info($"发送request到服务端 from: {data.ClientId}");
        if (Model.ProtocolHandler.PackData(data, out var bytes))
        {
            
            Model.Transport.SendData(Model.ServerId, bytes);
        }
        else
        {
            NetLog.Warning("客户端无法压缩数据包");
            Dispatch(ConnEvent.Stop);
        }
        
        StartTimeoutTimer();
    }

    private async Task StartTimeoutTimer()
    {
        await TaskTool.WaitUntil(() => _isReceiveApprove, Model.ClientConfig.Timeout);
        if (StateMachine?.CurrentState == this && !_isApproved)
        {
            Dispatch(ConnEvent.Stop);
            Model.SendEvent(new ClientConnectEvent(TransportReason.Timeout));
        }
    }

    private void OnServerDisconnected(TransportReason reason)
    {
        Model.SendEvent(new ServerDisconnectedEvent(reason));
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
    }
    
    private void OnDataReceived(byte[] data)
    {
        Model.ProtocolHandler.HandleData(data);
    }
    
    private void OnPeerConnected(long clientId)
    {
        Model.ConnectionIds.Add(clientId);
        Model.SendEvent(new PeerConnectedEvent(clientId));
    }

    private void OnPeerDisconnected(long clientId)
    {
        Model.ConnectionIds.Remove(clientId);
        Model.SendEvent(new PeerDisconnectedEvent(clientId));
    }

    private void OnResponseApprove(ResponseApproveProtocol protocol)
    {
        var reason = protocol.Reason;
        _isApproved = reason == TransportReason.Ok;
        _isReceiveApprove = true;
        NetLog.Info($"连接{(_isApproved ? "成功": "失败")}, reason: {reason}");
        if (!_isApproved)
        {
            Dispatch(ConnEvent.Stop);
        }
        Model.SendEvent(new ClientConnectEvent(reason));
    }

    public override void Exit()
    {
        _isReceiveApprove = false;
        _isApproved = false;
        Model.Transport.ServerDisconnected -= OnServerDisconnected;
        Model.Transport.DataReceived -= OnDataReceived;
        Model.Transport.PeerConnected -= OnPeerConnected;
        Model.Transport.PeerDisconnected -= OnPeerDisconnected;
        Model.ProtocolHandler.UnRegisterHandler<ResponseApproveProtocol>();
    }
}

internal class ReconnectingState(ConnectionModel model) : ConnState(model)
{
    private int _remainRetryCnt;
    
    public override void Enter()
    {
        _remainRetryCnt = Model.ClientConfig.ReconnectTimes;
        Model.Transport.ConnectionDone += OnConnectionDone;
        Model.ConnectionIds.Clear();
    }

    public override void Update(float delta)
    {
        Reconnect();
    }

    private void Reconnect()
    {
        try
        {
            Model.SendEvent(new ClientReconnectEvent(_remainRetryCnt));
            _remainRetryCnt -= 1;
            var config = Model.ClientConfig;
            Model.Transport.StartClient(config.Addr, config.Port, config.Timeout);
        }
        catch (Exception e)
        {
            NetLog.Error(e.ToString());
            Dispatch(ConnEvent.Stop);
        }
    }

    private void OnConnectionDone(TransportReason reason)
    {
        if (reason == TransportReason.Ok)
        {
            Dispatch(ConnEvent.Ok);
        }
        else if (_remainRetryCnt > 0)
        {
            Reconnect();
        }
        else
        {
            Model.SendEvent(new ClientConnectEvent(reason));
            Dispatch(ConnEvent.Stop);
        }
    }
    
    public override void Exit()
    {
        Model.Transport.ConnectionDone -= OnConnectionDone;
    }
}
