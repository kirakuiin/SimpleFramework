using SimpleFramework.Net.Connection;
using SimpleFramework.Utility;

namespace SimpleFramework.Net;

/// <summary>
/// 一个用于同步网络间各种数据，状态的系统
/// <para>底层通过监听网络连接事件，比如<see cref="PeerConnectedEvent"/>等事件来实现功能</para>
/// <remarks>底层依赖<see cref="ConnectionModel"/></remarks>
/// </summary>
public class NetSyncSystem : AbstractSystem
{
    private ITransfer Transfer => this.GetUtility<ITransfer>();
    
    private readonly ProtocolHandler _protocolHandler = new (NetDefine.SyncGuard);
    
    private readonly DisposableGroup _events =  new (); // 存储各种事件的引用
    
    protected override void OnInitialize()
    {
        this.ThrowIfNull<ConnectionModel>();
        RegisterEvents();
    }

    protected override void OnUninitialize()
    {
        _events.Dispose();
    }

    private void RegisterEvents()
    {
        _events.Add(this.RegisterEvent<ServerCreateEvent>(OnServerCreate));
        _events.Add(this.RegisterEvent<ClientConnectEvent>(OnClientConnect));
        _events.Add(this.RegisterEvent<PeerConnectedEvent>(OnPeerConnected));
        _events.Add(this.RegisterEvent<PeerDisconnectedEvent>(OnPeerDisconnected));
        _events.Add(this.RegisterEvent<ServerDisconnectedEvent>(OnServerDisconnected));
    }

    private void OnServerCreate(ServerCreateEvent e)
    {
    }
    
    private void OnClientConnect(ClientConnectEvent e)
    {
    }
    
    private void OnPeerConnected(PeerConnectedEvent e)
    {
    }
    
    private void OnPeerDisconnected(PeerDisconnectedEvent e)
    {
    }
    
    private void OnServerDisconnected(ServerDisconnectedEvent e)
    {
    }
}