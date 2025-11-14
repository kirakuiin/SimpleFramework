#if GODOT

using Godot;
using SimpleFramework.Net;
using SimpleFramework.Net.Connection;

namespace SimpleFramework.GDExt;

/// <summary>
/// Godot中实现<see cref="ITransport"/>接口的对象
/// </summary>
public class MultiplayerTransport : Node, ITransport
{
    /// <summary>
    /// 全局的Transport实例
    /// </summary>
    public static MultiplayerTransport? Instance { get; private set; }

    public override void _Ready()
    {
        Instance = this;
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += OnConnectionOk;
        Multiplayer.ConnectionFailed += OnConnectionFail;
        Multiplayer.ServerDisconnected += OnServerDisconnected;
    }

    public override void _ExitTree()
    {
        Instance = null;
        Multiplayer.PeerConnected -= OnPeerConnected;
        Multiplayer.PeerDisconnected -= OnPeerDisconnected;
        Multiplayer.ConnectedToServer -= OnConnectionOk;
        Multiplayer.ConnectionFailed -= OnConnectionFail;
        Multiplayer.ServerDisconnected -= OnServerDisconnected;
    }

    private void OnPeerConnected(long clientId)
    {
        PeerConnected?.Invoke(clientId);
    }

    private void OnPeerDisconnected(long clientId)
    {
        PeerDisconnected?.Invoke(clientId);
    }

    private void OnConnectionOk()
    {
        ConnectedToServer?.Invoke();
    }

    private void OnConnectionFail()
    {
        ConnectionFailed?.Invoke(TransportReason.Failed);
    }

    private void OnServerDisconnected()
    {
        if (Multiplayer.HasMultiplayerPeer())
        {
            ServerDisconnected?.Invoke(TransportReason.Failed);
        }
    }

    public TransportReason StartServer(int port, int maxConnections)
    {
        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateServer(port, maxConnections);

        switch (error)
        {
            case Error.Ok:
                Multiplayer.MultiplayerPeer = peer;
                return TransportReason.Ok;
            default:
                NetLog.Error($"[GD]创建服务端失败, 原因:{error}");
                return TransportReason.Failed;
        }
    }

    public void StopServer()
    {
        if (Multiplayer.IsServer())
        {
            Rpc(nameof(_StopServerRpc));
        }

        // TODO: 需要await
        StopClient();
    }

    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable, TransferChannel = Channel.System)]
    private void _StopServerRpc()
    {
        StopClient();
        ServerDisconnected?.Invoke(TransportReason.ServerClosed);
    }

    public void Kick(long clientId)
    {
        if (Multiplayer.IsServer() && clientId != GdConst.ServerId)
        {
            RpcId(clientId, nameof(_KickRpc));
        }
    }
    
    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable, TransferChannel = Channel.System)]
    private void _KickRpc()
    {
        StopClient();
        ServerDisconnected?.Invoke(TransportReason.ServerRejected);
    }

    public TransportReason StartClient(string ip, int port)
    {
        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateClient(ip, port);
        switch (error)
        {
            case Error.Ok:
                Multiplayer.MultiplayerPeer = peer;
                return TransportReason.Ok;
            default:
                NetLog.Error("[GD]连接服务端失败, 原因:{error}");
                return TransportReason.Failed;
        }
        
    }

    public void StopClient()
    {
        Multiplayer.MultiplayerPeer.Close();
        Multiplayer.MultiplayerPeer = null;
    }

    public void SendData(long clientId, byte[] data)
    {
        if (Multiplayer.HasMultiplayerPeer() && clientId != Multiplayer.GetUniqueId())
        {
            RpcId(clientId, nameof(_SendDataRpc), data);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer ,TransferMode = MultiplayerPeer.TransferModeEnum.Reliable, TransferChannel = Channel.Data)]
    private void _SendDataRpc(byte[] data)
    {
        DataReceived?.Invoke(data);
    }

    public event Action<long>? PeerConnected;
    public event Action<long>? PeerDisconnected;
    public event Action? ConnectedToServer;
    public event Action<TransportReason>? ServerDisconnected;
    public event Action<TransportReason>? ConnectionFailed;
    public event Action<byte[]>? DataReceived;
}

#endif