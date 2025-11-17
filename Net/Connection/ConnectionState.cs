using SimpleFramework.Patterns;

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
    }

    private void OnDataReceived(byte[] data)
    {
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

    public override void Exit()
    {
        Model.Transport.DataReceived -= OnDataReceived;
        Model.Transport.PeerConnected -= OnPeerConnected;
        Model.Transport.PeerDisconnected -= OnPeerDisconnected;
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
        Model.SendEvent(new ClientConnectEvent(reason));
        Dispatch(reason == TransportReason.Ok ? ConnEvent.Ok: ConnEvent.Stop);
    }
    
    public override void Exit()
    {
        Model.Transport.ConnectionDone -= OnConnectionDone;
    }
}

internal class ConnectedState(ConnectionModel model) : ConnState(model)
{
    public override void Enter()
    {
        Model.Transport.ServerDisconnected += OnServerDisconnected;
        Model.Transport.DataReceived += OnDataReceived;
        Model.Transport.PeerConnected += OnPeerConnected;
        Model.Transport.PeerDisconnected += OnPeerDisconnected;
    }

    private void OnServerDisconnected(TransportReason reason)
    {
        model.SendEvent(new ServerDisconnectedEvent(reason));
        switch (reason)
        {
            case TransportReason.ServerRejected:
            case TransportReason.ServerClosed:
                Dispatch(ConnEvent.Stop);
                break;
            default:
                Dispatch(ConnEvent.Retry);
                break;
        }
    }
    
    private void OnDataReceived(byte[] data)
    {
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

    public override void Exit()
    {
        Model.Transport.ServerDisconnected -= OnServerDisconnected;
        Model.Transport.DataReceived -= OnDataReceived;
        Model.Transport.PeerConnected -= OnPeerConnected;
        Model.Transport.PeerDisconnected -= OnPeerDisconnected;
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
            Model.SendEvent(new ClientConnectEvent(reason));
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
