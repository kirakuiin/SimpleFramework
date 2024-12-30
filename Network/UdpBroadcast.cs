using System.Net;
using System.Net.Sockets;
using SimpleFramework.Utility;

namespace SimpleFramework.Network;

/// <summary>
/// 任务标记。
/// </summary>
internal class BroadcastTaskInfo
{
    public bool IsRunning { set; get; } = true;
}


/// <summary>
/// 提供广播发送功能。
/// </summary>
/// <typeparam name="T"></typeparam>
public class BroadcastSender<T> : Disposable where T : struct
{
    private readonly UdpClient _udpSender = new(NetworkUtil.DefaultIpEndPoint);

    private IPEndPoint _endPoint = NetworkUtil.GetBroadcastIpEndPoint(DefaultBroadcastPort);

    /// <summary>
    /// 默认的广播端口。
    /// </summary>
    public const int DefaultBroadcastPort = 3344;

    /// <value>
    /// 广播默认发送的消息。
    /// <para>
    /// 默认值为空
    /// </para>
    /// </value>
    public T SentMessage {get; set;}

    /// <value>
    /// 广播的端口号。
    /// </value>
    public int BroadcastPort
    {
        set => _endPoint = NetworkUtil.GetBroadcastIpEndPoint(value);
        get => _endPoint.Port;
    }

    /// <summary>
    /// 广播<c>SentMessage</c>内的消息。
    /// </summary>
    /// <returns>发送的字节数。</returns>
    public int Broadcast()
    {
        return Broadcast(SentMessage);
    }

    /// <summary>
    /// 广播指定的消息。
    /// </summary>
    /// <param name="message">待广播消息</param>
    /// <returns>发送的字节数</returns>
    public int Broadcast(T message)
    {
        var data = SerializeTool.SerializeBytes(message);
        return _udpSender.Send(data, data.Length, _endPoint);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (IsDisposed) return;
        IsDisposed = true;
        _udpSender?.Dispose();
    }
}

/// <summary>
/// 提供接受Udp广播功能。
/// </summary>
/// <typeparam name="T"></typeparam>
public class BroadcastReceiver<T> : Disposable where T : struct
{
    private readonly UdpClient _udpReceiver;

    /// <summary>
    /// 以广播端口构建一个接受对象
    /// </summary>
    /// <param name="broadcastPort"></param>
    public BroadcastReceiver(int broadcastPort=BroadcastSender<T>.DefaultBroadcastPort)
    {
        _udpReceiver = new UdpClient(new IPEndPoint(IPAddress.Any, broadcastPort));
    }

    /// <summary>
    /// 异步的接受一条广播。
    /// </summary>
    /// <returns><c>Task&lt;UdpReceiveResult&gt;</c></returns>
    /// <exception cref="SocketException"></exception>
    public async Task<UdpReceiveResult> ReceiveAsync()
    {
        return await _udpReceiver.ReceiveAsync();
    }
    
    protected override void Dispose(bool isDisposing)
    {
        if (IsDisposed) return;
        IsDisposed = true;
        _udpReceiver?.Dispose();
    }

}

/// <summary>
/// 提供定时发出广播的功能。
/// </summary>
/// <typeparam name="T"></typeparam>
public class ScheduledBroadcaster<T> : Disposable where T : struct
{
    private readonly BroadcastSender<T> _sender;

    private const float DefaultBroadcastInterval = 2.0f;

    private readonly Queue<BroadcastTaskInfo> _tasks = new();

    public bool IsSending => _tasks.Any(info => info.IsRunning);

    /// <summary>
    /// 以广播端口为参数构造对象
    /// </summary>
    /// <param name="broadcastPort"></param>
    public ScheduledBroadcaster(int broadcastPort=BroadcastSender<T>.DefaultBroadcastPort)
    {
        _sender = new(){BroadcastPort=broadcastPort};
    }

    /// <value>
    /// 广播发送间隔(s)
    /// <para>默认值为<c>DefaultBroadcastInterval</c></para>
    /// </value>
    public float BroadcastInterval {set; get;} = DefaultBroadcastInterval;

    /// <summary>
    /// 修改发送信息。
    /// </summary>
    /// <param name="message">新的信息</param>
    public void ChangeMessage(T message)
    {
        _sender.SentMessage = message;
    }

    /// <summary>
    /// 开启广播，发送指定消息
    /// </summary>
    /// <param name="message">消息字符串</param>
    public async void StartBroadcast(T message)
    {
        var taskInfo = new BroadcastTaskInfo();
        _tasks.Enqueue(taskInfo);
        _sender.SentMessage = message;
        while (taskInfo.IsRunning)
        {
            _sender.Broadcast();
            await Task.Delay(TimeUtil.ToMs((int)BroadcastInterval));
        }
    }

    /// <summary>
    /// 停止发送广播
    /// </summary>
    public void StopBroadcast()
    {
        if (_tasks.Count > 0)
        {
            _tasks.Dequeue().IsRunning = false;
        }
    }
    
    protected override void Dispose(bool isDisposing)
    {
        if (IsDisposed) return;
        IsDisposed = true;
        StopBroadcast();
        _sender?.Dispose();
    }
}

/// <summary>
/// 提供不断接受广播的功能。
/// </summary>
/// <typeparam name="T"></typeparam>
public class BroadcastListener<T> : Disposable where T : struct
{
    private readonly BroadcastReceiver<T> _receiver;

    private readonly Queue<BroadcastTaskInfo> _tasks = new();

    public BroadcastListener(int broadcastPort=BroadcastSender<T>.DefaultBroadcastPort)
    {
        _receiver = new BroadcastReceiver<T>(broadcastPort);
    }

    /// <value>
    /// 是否正在监听
    /// </value>
    public bool IsListening => _tasks.Any(info => info.IsRunning);

    /// <summary>
    /// 开始监听广播
    /// </summary>
    public async void StartListen()
    {
        var task = new BroadcastTaskInfo();
        _tasks.Enqueue(task);
        while (task.IsRunning)
        {
            try
            {
                var package = await _receiver.ReceiveAsync();
                InvokeEvent(package);
            }
            // 忽略udp客户端关闭导致的异常。
            catch (ObjectDisposedException) {}
        }
    }

    private void InvokeEvent(UdpReceiveResult package)
    {
        var message = SerializeTool.Deserialize<T>(package.Buffer);
        OnReceivedBroadcast?.Invoke(package.RemoteEndPoint.Address, message);
    }

    /// <summary>
    /// 停止监听广播
    /// </summary>
    public void StopListen()
    {
        if (_tasks.Count > 0)
        {
            _tasks.Dequeue().IsRunning = false;
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        if (IsDisposed) return;
        IsDisposed = true;
        StopListen();
        _receiver?.Dispose();
    }

    /// <summary>
    /// 当收到广播时触发事件。
    /// <para>
    /// <c>IPAddress</c>: 发送者的IP信息
    /// <c>T</c>: 广播携带的消息
    /// </para>
    /// </summary>
    public event Action<IPAddress, T> OnReceivedBroadcast;
}