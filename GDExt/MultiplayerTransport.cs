#if GODOT

#nullable enable
using System;
using Godot;
using SimpleFramework.Net;
using SimpleFramework.Net.Connection;
using System.Threading.Tasks;
using Environment = System.Environment;

namespace SimpleFramework.GDExt;

/// <summary>
/// Godot中实现<see cref="ITransport"/>接口的对象
/// </summary>
public partial class MultiplayerTransport : Node, ITransport, ITransfer
{
    /// <summary>
    /// 客户端连接的状态
    /// </summary>
    private enum EConnState
    {
        Idle,
        Connecting,
        Error,
        Connected,
    }
    
    /// <summary>
    /// 全局的Transport实例
    /// </summary>
    public static MultiplayerTransport Instance { get; private set; } = null!;

    /// <summary>
    /// await间隔
    /// </summary>
    private const int AwaitInterval = 100;
    
    private EConnState _connState = EConnState.Idle;

    public override void _Ready()
    {
        Instance = this;
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += OnConnectionOk;
        Multiplayer.ConnectionFailed += OnConnectionFail;
        Multiplayer.ServerDisconnected += OnServerDisconnected;
        Multiplayer.MultiplayerPeer = null;
        NetLog.Info("Transport初始化完毕");
    }

    public override void _ExitTree()
    {
        Instance = null!;
        Multiplayer.PeerConnected -= OnPeerConnected;
        Multiplayer.PeerDisconnected -= OnPeerDisconnected;
        Multiplayer.ConnectedToServer -= OnConnectionOk;
        Multiplayer.ConnectionFailed -= OnConnectionFail;
        Multiplayer.ServerDisconnected -= OnServerDisconnected;
        Multiplayer.MultiplayerPeer?.Close();
        NetLog.Info("Transport释放完毕");
    }

    private void OnPeerConnected(long clientId)
    {
        NetLog.Info($"peer id:{clientId} 加入连接");
        PeerConnected?.Invoke(clientId);
    }

    private void OnPeerDisconnected(long clientId)
    {
        NetLog.Info($"peer id:{clientId} 断开连接");
        PeerDisconnected?.Invoke(clientId);
    }

    private void OnConnectionOk()
    {
        NetLog.Info("连接服务端成功");
        _connState = EConnState.Connected;
        ConnectionDone?.Invoke(TransportReason.Ok);
    }

    private void OnConnectionFail()
    {
        NetLog.Error($"连接服务端失败, 原因:{TransportReason.Failed}");
        _connState = EConnState.Error;
        ConnectionDone?.Invoke(TransportReason.Failed);
    }

    private void OnServerDisconnected()
    {
        NetLog.Info("与服务端断开连接");
        if (Multiplayer.HasMultiplayerPeer())
        {
            StopClient();
            ServerDisconnected?.Invoke(TransportReason.Failed);
        }
    }

    public Task<TransportReason> StartServer(int port)
    {
        if (Multiplayer.HasMultiplayerPeer())
        {
            NetLog.Error("peer已经创建, 请先关闭");
            ServerCreated?.Invoke(TransportReason.AlreadyCreate);
            return Task.FromResult(TransportReason.AlreadyCreate);
        }
        
        NetLog.Info($"创建服务端 port:{port}");
        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateServer(port);

        switch (error)
        {
            case Godot.Error.Ok:
                Multiplayer.MultiplayerPeer = peer;
                ServerCreated?.Invoke(TransportReason.Ok);
                return Task.FromResult(TransportReason.Ok);
            default:
                NetLog.Error($"创建服务端失败, 原因:{error}");
                ServerCreated?.Invoke(TransportReason.Failed);
                return Task.FromResult(TransportReason.Failed);
        }
    }

    public async Task StopServer()
    {
        NetLog.Info("关闭服务端");
        if (IsServer())
        {
            Rpc(nameof(_StopServerRpc));
        }
        
        await Task.Delay(100);

        StopClient();
    }

    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable, CallLocal = false, TransferChannel = Channel.System)]
    private void _StopServerRpc()
    {
        NetLog.Info("服务端主动关闭");
        StopClient();
        ServerDisconnected?.Invoke(TransportReason.ServerClosed);
    }
    
    public void Kick(long clientId, TransportReason reason=TransportReason.ServerRejected)
    {
        NetLog.Info($"踢出 peer id:{clientId} reason: {reason}");
        if (!IsServer() || clientId == GdConst.ServerId) return;
        var result = RpcId(clientId, nameof(_KickRpc), (int)reason);
        if (result > 0)
        {
            NetLog.Warning($"RPC失败: {result}");
        }
    }
    
    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable, TransferChannel = Channel.System)]
    private void _KickRpc(TransportReason reason)
    {
        NetLog.Info($"被踢出, 原因: {reason}");
        StopClient();
        ServerDisconnected?.Invoke(reason);
    }

    public async Task<TransportReason> StartClient(string addr, int port, int timeout=GdConst.Timeout)
    {
        if (Multiplayer.HasMultiplayerPeer())
        {
            NetLog.Error("peer已经创建, 请先关闭");
            ConnectionDone?.Invoke(TransportReason.AlreadyCreate);
            return TransportReason.AlreadyCreate;
        }
        
        NetLog.Info($"启动客户端 连接至{addr}:{port}, timeout:{timeout}");
        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateClient(addr, port);
        if (error > 0)
        {
            NetLog.Error($"连接服务端失败, 原因:{error}");
            _connState = EConnState.Idle;
            ConnectionDone?.Invoke(TransportReason.CantConnect);
            return TransportReason.CantConnect;
        }
        _connState = EConnState.Connecting;
        Multiplayer.MultiplayerPeer = peer;

        return await WaitConnecting(timeout);
    }

    private async Task<TransportReason> WaitConnecting(int timeout)
    {
        var count = 0;
        while (count < timeout)
        {
            await Task.Delay(AwaitInterval);
            count += AwaitInterval;
            switch (_connState)
            {
                case EConnState.Idle:
                    return TransportReason.Failed;
                case EConnState.Error:
                    StopClient();
                    return TransportReason.Failed;
                case EConnState.Connected:
                    return TransportReason.Ok;
            }
        }
        
        StopClient();
        NetLog.Error($"连接服务端失败, 原因:{TransportReason.Timeout}");
        ConnectionDone?.Invoke(TransportReason.Timeout);
        return TransportReason.Timeout;
    }

    public void StopClient()
    {
        NetLog.Info("关闭客户端连接");
        _connState = EConnState.Idle;
        if (!Multiplayer.HasMultiplayerPeer()) return;
        Multiplayer.MultiplayerPeer.Close();
        Multiplayer.MultiplayerPeer = null;
    }

    public void SendData(long clientId, byte[] data)
    {
        if (!IsConnected() || clientId == Multiplayer.GetUniqueId()) return;
        var result = RpcId(clientId, nameof(_SendDataRpc), data);
        if (result > 0)
        {
            NetLog.Warning($"RPC失败: {result}");
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer ,TransferMode = MultiplayerPeer.TransferModeEnum.Reliable, TransferChannel = Channel.Data)]
    private void _SendDataRpc(byte[] data)
    {
        DataReceived?.Invoke(data);
    }

    public bool IsConnected()
    {
        return Multiplayer.HasMultiplayerPeer() && Multiplayer.MultiplayerPeer.GetConnectionStatus() ==
            MultiplayerPeer.ConnectionStatus.Connected;
    }

    public bool IsServer()
    {
        return Multiplayer.HasMultiplayerPeer() && Multiplayer.IsServer();
    }

    public long ClientId => Multiplayer.GetUniqueId();

    public long ServerId => GdConst.ServerId;

    public event Action<TransportReason>? ServerCreated;
    public event Action<long>? PeerConnected;
    public event Action<long>? PeerDisconnected;
    public event Action<TransportReason>? ConnectionDone;
    public event Action<TransportReason>? ServerDisconnected;
    public event Action<byte[]>? DataReceived;
}

#endif