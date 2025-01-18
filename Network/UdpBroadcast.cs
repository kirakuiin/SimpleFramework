using System.Net;
using System.Net.Sockets;
using SimpleFramework.Utility;

namespace SimpleFramework.Network;

/// <summary>
/// 任务标记。
/// </summary>
internal class BroadcastTaskInfo : IDisposable
{
    public bool IsRunning { set; get; } = true;
    public CancellationTokenSource CancellationSource { get; } = new();

    public void Dispose()
    {
        IsRunning = false;
        try 
        {
            CancellationSource.Cancel();
            CancellationSource.Dispose();
        }
        catch (ObjectDisposedException) { }
    }
}

/// <summary>
/// 提供广播发送功能。
/// </summary>
/// <typeparam name="T">消息类型，必须是值类型</typeparam>
public class BroadcastSender<T> : Disposable where T : struct
{
    private readonly UdpClient _udpSender = new(NetworkUtil.DefaultIpEndPoint);
    private IPEndPoint _endPoint = NetworkUtil.GetBroadcastIpEndPoint(DefaultBroadcastPort);

    /// <summary>
    /// 默认的广播端口。
    /// </summary>
    public const int DefaultBroadcastPort = 3344;

    /// <summary>
    /// 获取或设置广播默认发送的消息。
    /// </summary>
    public T SentMessage { get; set; }

    /// <summary>
    /// 获取或设置广播的端口号。
    /// </summary>
    public int BroadcastPort
    {
        set => _endPoint = NetworkUtil.GetBroadcastIpEndPoint(value);
        get => _endPoint.Port;
    }

    /// <summary>
    /// 广播默认消息。
    /// </summary>
    /// <returns>发送的字节数</returns>
    /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
    public int Broadcast() => Broadcast(SentMessage);

    /// <summary>
    /// 广播指定的消息。
    /// </summary>
    /// <param name="message">待广播消息</param>
    /// <returns>发送的字节数</returns>
    /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
    public int Broadcast(T message)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(BroadcastSender<T>));
        var data = SerializeTool.SerializeBytes(message);
        return _udpSender.Send(data, data.Length, _endPoint);
    }

    ~BroadcastSender()
    {
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (IsDisposed) return;
        IsDisposed = true;
        _udpSender?.Dispose();
    }
}

/// <summary>
/// 提供接收UDP广播功能。
/// </summary>
/// <typeparam name="T">消息类型，必须是值类型</typeparam>
public class BroadcastReceiver<T> : Disposable where T : struct
{
    private readonly UdpClient _udpReceiver;

    /// <summary>
    /// 初始化一个新的广播接收器实例。
    /// </summary>
    /// <param name="broadcastPort">监听的端口号，默认为 <see cref="BroadcastSender{T}.DefaultBroadcastPort"/></param>
    public BroadcastReceiver(int broadcastPort = BroadcastSender<T>.DefaultBroadcastPort)
    {
        _udpReceiver = new UdpClient(new IPEndPoint(IPAddress.Any, broadcastPort));
    }

    /// <summary>
    /// 异步接收一条广播。
    /// </summary>
    /// <param name="token">取消令牌</param>
    /// <returns>UDP接收结果</returns>
    /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
    public async Task<UdpReceiveResult> ReceiveAsync(CancellationToken token = default)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(BroadcastReceiver<T>));
        return await _udpReceiver.ReceiveAsync(token);
    }

    ~BroadcastReceiver()
    {
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (IsDisposed) return;
        IsDisposed = true;
        _udpReceiver?.Dispose();
    }
}

/// <summary>
/// 提供定时发送广播的功能。
/// </summary>
/// <typeparam name="T">消息类型，必须是值类型</typeparam>
public class ScheduledBroadcaster<T> : Disposable where T : struct
{
    private readonly BroadcastSender<T> _sender = new();
    private BroadcastTaskInfo _currentTask;
    private const float DefaultBroadcastInterval = 2.0f;
    private readonly object _syncLock = new();

    /// <summary>
    /// 获取一个值，指示是否正在发送广播。
    /// </summary>
    public bool IsSending => _currentTask?.IsRunning ?? false;

    /// <summary>
    /// 获取或设置广播发送间隔(秒)。
    /// </summary>
    public float BroadcastInterval { get; set; } = DefaultBroadcastInterval;

    /// <summary>
    /// 初始化一个新的定时广播器实例。
    /// </summary>
    /// <param name="broadcastPort">广播端口号，默认为 <see cref="BroadcastSender{T}.DefaultBroadcastPort"/></param>
    public ScheduledBroadcaster(int broadcastPort = BroadcastSender<T>.DefaultBroadcastPort)
    {
        _sender.BroadcastPort = broadcastPort;
    }

    /// <summary>
    /// 开始定时广播指定消息。
    /// <para>如果已经在广播中，则此调用将被忽略。</para>
    /// <para>如果对象已释放，则此调用将被忽略。</para>
    /// </summary>
    /// <param name="message">要广播的消息</param>
    public void StartBroadcast(T message)
    {
        if (IsDisposed) return;
        
        if (!InitializeBroadcastTask()) return;
        StartBroadcastLoop(message);
    }

    private bool InitializeBroadcastTask()
    {
        lock (_syncLock)
        {
            if (IsSending) return false;
            _currentTask = new BroadcastTaskInfo();
            return true;
        }
    }

    private void StartBroadcastLoop(T message)
    {
        Task.Run(async () => await RunBroadcastLoop(message));
    }

    private async Task RunBroadcastLoop(T message)
    {
        using var taskInfo = _currentTask;
        _sender.SentMessage = message;

        try
        {
            while (taskInfo.IsRunning)
            {
                _sender.Broadcast();
                await Task.Delay(TimeUtil.ToMs((int)BroadcastInterval),
                    taskInfo.CancellationSource.Token);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            CleanupBroadcastTask(taskInfo);
        }
    }

    private void CleanupBroadcastTask(BroadcastTaskInfo taskInfo)
    {
        lock (_syncLock)
        {
            if (_currentTask == taskInfo)
            {
                _currentTask = null;
            }
        }
    }

    /// <summary>
    /// 停止当前的广播任务。
    /// </summary>
    public void StopBroadcast()
    {
        lock (_syncLock)
        {
            _currentTask?.Dispose();
            _currentTask = null;
        }
    }

    ~ScheduledBroadcaster()
    {
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (IsDisposed) return;
        IsDisposed = true;
        
        StopBroadcast();
        
        if (isDisposing)
        {
            _sender?.Dispose();
        }
    }
}

/// <summary>
/// 提供持续监听广播的功能。
/// </summary>
/// <typeparam name="T">消息类型，必须是值类型</typeparam>
public class BroadcastListener<T> : Disposable where T : struct
{
    private readonly BroadcastReceiver<T> _receiver;
    private BroadcastTaskInfo _currentTask;
    private readonly object _syncLock = new();

    /// <summary>
    /// 获取一个值，指示是否正在监听广播。
    /// </summary>
    public bool IsListening => _currentTask?.IsRunning ?? false;

    /// <summary>
    /// 初始化一个新的广播监听器实例。
    /// </summary>
    /// <param name="broadcastPort">监听的端口号，默认为 <see cref="BroadcastSender{T}.DefaultBroadcastPort"/></param>
    public BroadcastListener(int broadcastPort = BroadcastSender<T>.DefaultBroadcastPort)
    {
        _receiver = new BroadcastReceiver<T>(broadcastPort);
    }

    /// <summary>
    /// 开始监听广播。
    /// <para>如果已经在监听中，则此调用将被忽略。</para>
    /// <para>如果对象已释放，则此调用将被忽略。</para>
    /// </summary>
    public void StartListen()
    {
        if (IsDisposed) return;
        
        var taskInfo = InitializeListenTask();
        if (taskInfo == null) return;

        StartListenLoop(taskInfo);
    }

    private BroadcastTaskInfo InitializeListenTask()
    {
        lock (_syncLock)
        {
            if (IsListening) return null;
            _currentTask = new BroadcastTaskInfo();
            return _currentTask;
        }
    }

    private void StartListenLoop(BroadcastTaskInfo taskInfo)
    {
        Task.Run(async () =>
        {
            try
            {
                await RunListenLoop(taskInfo);
            }
            finally
            {
                CleanupListenTask(taskInfo);
                taskInfo.Dispose();
            }
        });
    }

    private async Task RunListenLoop(BroadcastTaskInfo taskInfo)
    {
        while (taskInfo.IsRunning)
        {
            try
            {
                await ProcessNextMessage(taskInfo);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
        }
    }

    private async Task ProcessNextMessage(BroadcastTaskInfo taskInfo)
    {
        var package = await _receiver.ReceiveAsync(taskInfo.CancellationSource.Token);
        var message = SerializeTool.Deserialize<T>(package.Buffer);
        OnReceivedBroadcast?.Invoke(package.RemoteEndPoint.Address, message);
    }

    private void CleanupListenTask(BroadcastTaskInfo taskInfo)
    {
        lock (_syncLock)
        {
            if (_currentTask == taskInfo)
            {
                _currentTask = null;
            }
        }
    }

    /// <summary>
    /// 停止监听广播。
    /// </summary>
    public void StopListen()
    {
        lock (_syncLock)
        {
            _currentTask?.Dispose();
            _currentTask = null;
        }
    }

    ~BroadcastListener()
    {
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (IsDisposed) return;
        IsDisposed = true;
        
        StopListen();
        
        if (isDisposing)
        {
            _receiver?.Dispose();
        }
    }

    /// <summary>
    /// 当收到广播消息时触发的事件。
    /// </summary>
    /// <remarks>
    /// 第一个参数是发送者的IP地址，第二个参数是接收到的消息。
    /// </remarks>
    public event Action<IPAddress, T> OnReceivedBroadcast;
}