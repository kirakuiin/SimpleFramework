namespace SimpleFramework.Net.Connection;


/// <summary>
/// 状态转移事件
/// </summary>
internal static class ConnEvent
{
    public const string Stop = "STOP";
    public const string Ok = "OK";
    public const string StartClient = "START_CLIENT";
    public const string StartServer = "START_SERVER";
    public const string Retry = "RETRY";
}


/// <summary>
/// 连接常量
/// </summary>
public static class ConnDefine
{
    /// <summary>
    /// 重连次数
    /// </summary>
    public const int ReconnectTimes = 3;
    
    /// <summary>
    /// 连接超时时间(ms)
    /// </summary>
    public const int Timeout = 5000;
}


/// <summary>
/// 服务端配置
/// </summary>
/// <param name="port">端口</param>
/// <param name="maxPlayer">最大玩家数</param>
public readonly struct ServerConfig(int port, int maxPlayer)
{
    /// <summary>
    /// 端口号 
    /// </summary>
    public int Port => port;
    
    /// <summary>
    /// 最大玩家数
    /// </summary>
    public int MaxPlayer => maxPlayer;
}


/// <summary>
/// 连接服务端配置
/// </summary>
/// <param name="addr">地址</param>
/// <param name="port">端口</param>
/// <param name="timeout">超时时间(ms)</param>
/// <param name="reconnectTimes">重连次数</param>
public readonly struct ClientConfig(string addr, int port, int timeout=ConnDefine.Timeout, int reconnectTimes=ConnDefine.ReconnectTimes)
{
    /// <summary>
    /// 链接地址
    /// </summary>
    public string Addr => addr;
    
    /// <summary>
    /// 端口号 
    /// </summary>
    public int Port => port;
    
    /// <summary>
    /// 超时时间
    /// </summary>
    public int Timeout => timeout;
    
    /// <summary>
    /// 重连次数
    /// </summary>
    public int ReconnectTimes => reconnectTimes;
}

/// <summary>
/// 服务端创建事件
/// <remarks>仅服务端</remarks>
/// </summary>
public readonly struct ServerCreateEvent(TransportReason reason)
{
    public TransportReason Reason => reason;
}


/// <summary>
/// 客户端连接事件
/// <remarks>仅客户端</remarks>
/// </summary>
public readonly struct ClientConnectEvent(TransportReason reason)
{
    /// <summary>
    /// 网络层的连接结果
    /// </summary>
    public TransportReason Reason => reason;
}


/// <summary>
/// 客户端重连事件
/// <remarks>仅客户端</remarks>
/// </summary>
/// <param name="remainTime">剩余尝试次数</param>
public readonly struct ClientReconnectEvent(int remainTime)
{
    /// <summary>
    /// 剩余尝试次数
    /// </summary>
    public int RemainTryTime => remainTime;
}


/// <summary>
/// 服务端断开事件
/// <remarks>仅客户端</remarks>
/// </summary>
public readonly struct ServerDisconnectedEvent(TransportReason reason)
{
    public TransportReason Reason => reason;
}


/// <summary>
/// 远端连接事件
/// </summary>
/// <param name="peerId"></param>
public readonly struct PeerConnectedEvent(long peerId)
{
    public long PeerId => peerId;
}


/// <summary>
/// 远端断开事件
/// </summary>
/// <param name="peerId"></param>
public readonly struct PeerDisconnectedEvent(long peerId)
{
    public long PeerId => peerId;
}


/// <summary>
/// 客户端申请加入
/// </summary>
/// <param name="payload"></param>
[Protocol]
public struct RequestApproveProtocol(long clientId, string payload)
{
    public long ClientId = clientId;
    
    public string Payload = payload;
}

/// <summary>
/// 服务端回复请求
/// </summary>
/// <param name="reason"></param>
[Protocol]
public struct ResponseApproveProtocol(TransportReason reason)
{
    public TransportReason Reason = reason;
}


/// <summary>
/// peer加入连接
/// </summary>
/// <param name="clientId"></param>
[Protocol]
public struct PeerConnectProtocol(long clientId)
{
    public long ClientId = clientId;
}
