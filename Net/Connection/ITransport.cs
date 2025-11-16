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
    /// <returns>创建结果</returns>
    TransportReason StartServer(int port, int maxConnections);
    
    /// <summary>
    /// 停止服务器
    /// </summary>
    Task StopServer();

    /// <summary>
    /// 踢出某个客户端
    /// </summary>
    /// <param name="clientId">客户端id</param>
    /// <returns></returns>
    void Kick(long clientId);

    /// <summary>
    /// 启动客户端
    /// </summary>
    /// <param name="addr">ip或完全的域名</param>
    /// <param name="port">端口号</param>
    /// <param name="timeout">超时时间(ms)</param>
    /// <returns>创建结果</returns>
    Task<TransportReason> StartClient(string addr, int port, int timeout);
    
    /// <summary>
    /// 断开客户端
    /// </summary>
    /// <returns></returns>
    void StopClient();
    
    /// <summary>
    /// 向指定的客户端发送字节数据
    /// </summary>
    /// <param name="clientId">远端客户端id</param>
    /// <param name="data">字节流数据</param>
    void SendData(long clientId, byte[] data);

    /// <summary>
    /// 是否处于连接中
    /// </summary>
    /// <returns></returns>
    bool IsConnected();

    /// <summary>
    /// 是否为服务端
    /// </summary>
    /// <returns></returns>
    bool IsServer();
    
    /// <summary>
    /// 获得当前客户端的ID
    /// <remarks>如果连接未建立，获取的值是无效的</remarks>
    /// </summary>
    long ClientId { get; }
    
    /// <summary>
    /// 获得服务端的ID
    /// <remarks>如果连接未建立，获取的值是无效的</remarks>
    /// </summary>
    long ServerId { get; }
    
    /// <summary>
    /// 当有新的对等体加入时触发, 参数为对等体的id
    /// <remarks>每当有一个新的客户端加入时，它会依次收到所有之前加入的客户端的id</remarks>
    /// </summary>
    event Action<long> PeerConnected;
    
    /// <summary>
    /// 当有对等体断开时触发, 参数为对等体的id
    /// <remarks>如果服务端主动关闭，不保证会服务端会收到各个客户端的此信号</remarks>
    /// </summary>
    event Action<long> PeerDisconnected;

    /// <summary>
    /// 成功连接到服务端时触发
    /// <remarks>仅客户端触发</remarks>
    /// </summary>
    event Action ConnectedToServer;
    
    /// <summary>
    /// 服务端断开时触发
    /// <remarks>仅自身为客户端且非自身主动断开时触发</remarks>
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
    AlreadyCreate, // 已经创建
    CantConnect,  // 无法连接
    ServerClosed,  // 服务端关闭
    ServerRejected,  // 服务端拒绝
}