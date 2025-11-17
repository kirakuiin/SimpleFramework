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
    /// 连接超时时间
    /// </summary>
    public const int Timeout = 500;
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
/// </summary>
public struct ServerCreateEvent(TransportReason reason)
{
    public TransportReason Reason = reason;
}


/// <summary>
/// 客户端连接事件
/// </summary>
public struct ClientConnectEvent(TransportReason reason)
{
    public TransportReason Reason = reason;
}
