using System.Net;
using SimpleFramework.Utility;
using static System.Net.IPAddress;

namespace SimpleFramework.Net;

/// <summary>
/// 提供若干和网络地址相关函数的静态类。
/// </summary>
public static class NetworkUtil
{
    /// <summary>
    /// 返回默认的终结点对象。
    /// </summary>
    public static readonly IPEndPoint DefaultIpEndPoint = new IPEndPoint(Any, 0);

    /// <summary>
    /// 获得万用的IP，代表本机所有的IP。
    /// </summary>
    /// <returns></returns>
    public static IPAddress GetUniversalIpAddress()
    {
        TryParse("0.0.0.0", out var result);
        return result!;
    }

    /// <summary>
    /// 获得广播的终结点对象。
    /// </summary>
    /// <param name="port">广播使用的端口号</param>
    /// <returns><c>IPEndPoint</c>代表一个网络上的IP,端口对</returns>
    public static IPEndPoint GetBroadcastIpEndPoint(int port)
    {
        return new IPEndPoint(Broadcast, port);
    }

    /// <summary>
    /// 根据输入的ip和端口获得<see cref="IPEndPoint"/>对象。
    /// </summary>
    /// <param name="ip">ip地址</param>
    /// <param name="port">端口号</param>
    /// <returns>IPEndPoint对象</returns>
    /// <exception cref="ArgumentException"></exception>
    public static IPEndPoint GetIpEndPoint(string ip, ushort port)
    {
        if (TryParse(ip, out var ipAddress))
        {
            return new IPEndPoint(ipAddress, port);
        }
        throw new ArgumentException("Invalid ip address");
    }

    /// <summary>
    /// 是否为有效的端口
    /// </summary>
    /// <param name="port"></param>
    /// <returns></returns>
    public static bool IsValidPort(int port)
    {
        return port is >= 1 and <= 65535;
    }
}


/// <summary>
/// 网络常量定义 
/// </summary>
public static class NetDefine
{
    public const ushort ConnGuard = 0xc001;

    public const ushort SyncGuard = 0xc002;
}

/// <summary>
/// 网络模块的logger
/// </summary>
public static class NetLog
{
    public static Logger Logger { get; } = Logging.GetLogger("Net");
    
    public static void Debug(string message)
    {
        Logger.Debug(message);
    }
    
    public static void Info(string message)
    {
        Logger.Info(message);
    }
    
    public static void Warning(string message)
    {
        Logger.Warning(message);
    }
    
    public static void Error(string message, Exception? exception = null)
    {
        Logger.Error(message, exception);
    }
}


/// <summary>
/// 主协议编号
/// </summary>
public static class MainProtocol
{
    /// <summary>
    /// 连接协议
    /// </summary>
    public const ushort Connection = 1;
}


/// <summary>
/// 被此属性标记的结构体视作一个网络协议结构体
/// </summary>
/// <param name="mainId">主协议id</param>
/// <param name="subId">子协议id</param>
[AttributeUsage(AttributeTargets.Struct)]
public class ProtocolAttribute(ushort mainId, ushort subId) : Attribute
{
    /// <summary>
    /// 主协议号
    /// </summary>
    public ushort MainId {get;} = mainId;
    
    /// <summary>
    /// 子协议号
    /// </summary>
    public ushort SubId {get;} = subId;
}
