using System.Net;

namespace SimpleFramework.Utility;

/// <summary>
/// 提供若干和网络地址相关函数的静态类。
/// </summary>
public static class NetworkUtil
{
    /// <summary>
    /// 返回默认的终结点对象。
    /// </summary>
    public static readonly IPEndPoint DefaultIpEndPoint = new IPEndPoint(IPAddress.Any, 0);

    /// <summary>
    /// 获得万用的IP，代表本机所有的IP。
    /// </summary>
    /// <returns></returns>
    public static IPAddress GetUniversalIpAddress()
    {
        IPAddress.TryParse("0.0.0.0", out var result);
        return result;
    }

    /// <summary>
    /// 获得广播的终结点对象。
    /// </summary>
    /// <param name="port">广播使用的端口号</param>
    /// <returns><c>IPEndPoint</c>代表一个网络上的IP,端口对</returns>
    public static IPEndPoint GetBroadcastIpEndPoint(int port)
    {
        return new IPEndPoint(IPAddress.Broadcast, port);
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
        if (IPAddress.TryParse(ip, out var ipAddress))
        {
            return new IPEndPoint(ipAddress, port);
        }
        throw new ArgumentException("Invalid ip address");
    }
}