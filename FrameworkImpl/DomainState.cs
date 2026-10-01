namespace SimpleFramework.FrameworkImpl;

/// <summary>Domain 内部的一次性生命周期状态。</summary>
internal enum DomainState
{
    /// <summary>正在执行 Configure 并初始化组件。</summary>
    Starting,

    /// <summary>已经完成启动，可正常使用。</summary>
    Active,

    /// <summary>正在释放；仍可读取组件，发送事件不会调用任何处理器，不能订阅事件或修改树结构。</summary>
    Disposing,

    /// <summary>已经永久释放。</summary>
    Disposed
}
