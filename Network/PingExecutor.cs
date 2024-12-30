using System.Net;
using System.Net.NetworkInformation;
using SimpleFramework.Utility;

namespace SimpleFramework.Network;

/// <summary>
/// ping命令执行器
/// </summary>
public class PingExecutor
{
    /// <summary>
    /// 异步的等待ping的结果。
    /// </summary>
    /// <param name="targetIp">目标IP</param>
    /// <param name="timeout">超时时间</param>
    /// <returns></returns>
    public static async Task<NetworkStatus> GetLatency(string targetIp, int timeout=DefaultTimeout)
    {
        var executor = new PingExecutor(targetIp, timeout);
        return await executor.GetLatencyAsync();
    }
        
    private readonly Ping _sender = new ();

    private readonly int _timeout;

    /// <value>
    /// 获得目标主机IP地址
    /// </value>
    private readonly string _targetAddress;

    /// <summary>
    /// 默认探测超时时间(s)
    /// </summary>
    private const int DefaultTimeout = 10;

    /// <summary>
    /// 以IP/host地址字符串和超时时间构造对象。
    /// </summary>
    /// <param name="targetIp"></param>
    /// <param name="timeout"></param>
    public PingExecutor(string targetIp, int timeout=DefaultTimeout)
    {
        _targetAddress = targetIp;
        _timeout = timeout;
    }

    /// <summary>
    /// 异步探测网络延迟信息。
    /// </summary>
    /// <returns><c>Task&lt;NetworkStatus&gt;</c></returns>
    public async Task<NetworkStatus> GetLatencyAsync()
    {
        var ipList = await Dns.GetHostAddressesAsync(_targetAddress);
        foreach (var ipAddr in ipList)
        {
            var reply = await _sender.SendPingAsync(ipAddr, TimeUtil.ToMs(_timeout));
            if (reply.Status == IPStatus.Success)
            {
                return new NetworkStatus(reply);
            }
        }

        return NetworkStatus.CreateUnreachableStatus(_targetAddress);
    }

    /// <summary>
    /// 网络状况结果
    /// </summary>
    public readonly struct NetworkStatus
    {
        private const int UnreachableTime = 9999;
        
        /// <summary>
        /// 创建不可达状态。
        /// </summary>
        /// <returns></returns>
        public static NetworkStatus CreateUnreachableStatus(string ip)
        {
            return new NetworkStatus(ip);
        }

        private NetworkStatus(string ip)
        {
            TargetIp = IPAddress.TryParse(ip, out var ipAddress) ? ipAddress : IPAddress.None;
            Status = IPStatus.DestinationUnreachable;
            Latency = UnreachableTime;
        }
        
        public NetworkStatus(PingReply reply)
        {
            TargetIp = reply.Address;
            Status = reply.Status;
            Latency = reply.RoundtripTime;
        }
        /// <summary>
        /// 目标主机IP
        /// </summary>
        public IPAddress TargetIp { get; }
        /// <summary>
        /// 网络状态
        /// </summary>
        public IPStatus Status { get; }
        /// <summary>
        /// 延迟(ms)
        /// </summary>
        public long Latency { get; }

        public override string ToString()
        {
            return $"To {TargetIp}, Status is {Status}, Latency is {Latency}ms";
        }

        /// <summary>
        /// 目标地址是否可达
        /// </summary>
        public bool IsReachable => Status == IPStatus.Success;
    }
}