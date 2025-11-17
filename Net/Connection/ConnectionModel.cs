using SimpleFramework.Patterns;

namespace SimpleFramework.Net.Connection;

/// <summary>
/// 一个网络连接管理模型，负责管理网络连接中的各个状态
/// <remarks>此model依赖<see cref="ITransport"/>, 因此<see cref="ITransport"/>必须先于此对象初始化</remarks>
/// </summary>
public class ConnectionModel: AbstractModel
{
    /// <summary>
    /// 网络状态机
    /// </summary>
    private readonly StateMachine _stateMachine = new();
    
    /// <summary>
    /// 服务端启动配置
    /// </summary>
    internal ServerConfig ServerConfig;
    
    /// <summary>
    /// 客户端连接配置
    /// </summary>
    internal ClientConfig ClientConfig;
    
    /// <summary>
    /// 客户端连接时发送的额外信息
    /// </summary>
    internal object? Payload = null;

    /// <summary>
    /// 认证函数
    /// </summary>
    internal Func<object, Task<bool>>? AuthenticationFunc;

    /// <summary>
    /// 当前所有连接的id
    /// </summary>
    internal readonly HashSet<long> ConnectionIds = [];
    
    /// <summary>
    /// 获取网络底层transport
    /// </summary>
    internal ITransport Transport => this.GetUtility<ITransport>();
    
    protected override void OnInitialize()
    {
        if (Transport is null)
        {
            throw new NullReferenceException("ITransport 组件尚未初始化!");
        }
        InitStateMachine();
    }

    private void InitStateMachine()
    {
        var offline = new OfflineState(this);
        var startHosting = new StartHostingState(this);
        var hosting = new HostingState(this);
        var connecting = new ConnectingState(this);
        var connected = new ConnectedState(this);
        var reconnect = new ReconnectingState(this);
        
        _stateMachine.AddState(offline);
        _stateMachine.AddState(startHosting);
        _stateMachine.AddState(hosting);
        _stateMachine.AddState(connecting);
        _stateMachine.AddState(connected);
        _stateMachine.AddState(reconnect);
        
        _stateMachine.AddTransition(StateEvents.AnyState, offline, ConnEvent.Stop);
        
        // 服务端转换
        _stateMachine.AddTransition(offline, startHosting, ConnEvent.StartServer);
        _stateMachine.AddTransition(startHosting, hosting, ConnEvent.Ok);
        
        // 客户端转换
        _stateMachine.AddTransition(offline, connecting, ConnEvent.StartClient);
        _stateMachine.AddTransition(connecting, connected, ConnEvent.Ok);
        _stateMachine.AddTransition(connected, reconnect, ConnEvent.Retry);
        _stateMachine.AddTransition(reconnect, connected, ConnEvent.Ok);
        
        _stateMachine.InitialState = offline;
        _stateMachine.SetActive(true);
    }
    
    /// <summary>
    /// 获得所有已连接的id(不包含自己)
    /// </summary>
    public IReadOnlySet<long> GetAllConnectionIds() => ConnectionIds;

    /// <summary>
    /// 获得当前网络内的所有id
    /// </summary>
    /// <returns></returns>
    public IEnumerable<long> GetAllIds()
    {
        return ConnectionIds.Union([ClientId]);
    }

    /// <summary>
    /// 获得当前peer的id
    /// </summary>
    public long ClientId => Transport.ClientId;
    
    /// <summary>
    /// 获得当前的服务端id
    /// </summary>
    public long ServerId => Transport.ServerId;
    
    /// <summary>
    /// 是否正在连接中
    /// </summary>
    /// <returns></returns>
    public bool IsConnected() => Transport.IsConnected();
    
    /// <summary>
    /// 是否是服务端
    /// </summary>
    /// <returns></returns>
    public bool IsServer() => Transport.IsServer();
    
    /// <summary>
    /// 当前已连接的peer数量
    /// </summary>
    public int Count => GetAllConnectionIds().Count;

    /// <summary>
    /// 启动服务器
    /// </summary>
    public void StartServer(ServerConfig config)
    {
        ServerConfig = config;
        _stateMachine.Dispatch(ConnEvent.StartServer);
    }

    /// <summary>
    /// 停止服务器/客户端/重连
    /// </summary>
    public void Stop()
    {
        _stateMachine.Dispatch(ConnEvent.Stop);
    }

    /// <summary>
    /// 启动一个到服务端的连接
    /// </summary>
    /// <param name="config">配置信息</param>
    /// <param name="payload">额外发送给服务端的负载信息</param>
    /// <typeparam name="T">负载信息类型</typeparam>
    public void StartClient<T>(ClientConfig config, T? payload=null) where T : struct
    {
        ClientConfig = config;
        Payload = payload;
        _stateMachine.Dispatch(ConnEvent.StartClient);
    }
    
    /// <summary>
    /// 注册一个认证函数
    /// <para>当需要实现一些服务端认证来验证加如的客户端时需要注册此函数。</para>
    /// <para>客户端的数据包通过<see cref="StartClient{T}"/>里的Payload来传递</para>
    /// <para>当认证未通过时。客户端会收到<see cref="TransportReason.AuthenticationFailed"/></para>
    /// </summary>
    /// <param name="func">异步的认证函数, 返回true代表通过, 否则不通过</param>
    /// <typeparam name="T">客户端数据包的类型</typeparam>
    public void RegisterAuthenticationFuncAsync<T>(Func<T, Task<bool>>? func) where T : struct 
    {
        if (func is null)
        {
            AuthenticationFunc = null;
        }
        else
        {
            AuthenticationFunc = value => func((T)value);
        };
    }
}

