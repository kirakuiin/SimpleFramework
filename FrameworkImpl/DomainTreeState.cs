namespace SimpleFramework.FrameworkImpl;

/// <summary>由一棵已连接 Domain 树共享的同步执行深度和结构转换状态。</summary>
internal sealed class DomainTreeState
{
    public int ExecutionDepth { get; set; }
    public bool IsTransitioning { get; set; }
}

/// <summary>Domain 内部的一次性生命周期状态。</summary>
internal enum DomainState
{
    /// <summary>正在收集注册并初始化组件。</summary>
    Starting,

    /// <summary>已经完成启动，可正常使用。</summary>
    Active,

    /// <summary>正在执行终止清理。</summary>
    Disposing,

    /// <summary>已经永久释放。</summary>
    Disposed
}
