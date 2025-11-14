namespace SimpleFramework.Net.Connection;

/// <summary>
/// 代表了一个实现了底层网络连接功能的对象
/// </summary>
public interface ITransport: IUtility
{
    /// <summary>
    /// 启动服务器
    /// </summary>
    /// <param name="port">端口</param>
    /// <param name="maxConnections">最大连接人数</param>
    /// <returns></returns>
    TransportReason StartServer(int port, int maxConnections);
    
    /// <summary>
    /// 停止服务器
    /// </summary>
    void StopServer();

    /// <summary>
    /// 踢出某个客户端
    /// </summary>
    /// <param name="clientId">客户端id</param>
    /// <returns></returns>
    void Kick(long clientId);

    /// <summary>
    /// 启动客户端
    /// </summary>
    /// <param name="addr"></param>
    /// <param name="port"></param>
    /// <returns></returns>
    TransportReason StartClient(string addr,  int port);
    
    /// <summary>
    /// 断开客户端
    /// </summary>
    /// <returns></returns>
    void StopClient();
    
    /// <summary>
    /// 向指定的客户端发送字节数据
    /// </summary>
    /// <param name="clientId"></param>
    /// <param name="data"></param>
    void SendData(long clientId, byte[] data);
    
    /// <summary>
    /// 当有新的对等体加入时触发, 参数为对等体的id
    /// </summary>
    event Action<long> PeerConnected;
    
    /// <summary>
    /// 当有对等体断开时触发, 参数为对等体的id
    /// </summary>
    event Action<long> PeerDisconnected;

    /// <summary>
    /// 成功连接到服务端时触发
    /// <remarks>仅客户端触发</remarks>
    /// </summary>
    event Action ConnectedToServer;
    
    /// <summary>
    /// 服务端断开时触发
    /// <remarks>仅客户端触发</remarks>
    /// </summary>
    event Action<TransportReason> ServerDisconnected;

    /// <summary>
    /// 连接服务端失败时触发
    /// <remarks>仅客户端触发</remarks>
    /// </summary>
    event Action<TransportReason> ConnectionFailed;

    /// <summary>
    /// 收到远端的数据
    /// </summary>
    event Action<byte[]> DataReceived;
}


/// <summary>
/// 传输时常见问题的定义
/// </summary>
public enum TransportReason
{
    Ok = 0,  // 成功
    Failed = 1,  // 通用失败
    Timeout,  // 超时
    CantConnect,  // 无法连接
    IpInvalid,  // ip地址无效
    ServerClosed,  // 服务端关闭
    ServerRejected,  // 服务端拒绝
}