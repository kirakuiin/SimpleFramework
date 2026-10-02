namespace SimpleFramework.FrameworkImpl;

/// <summary>Domain 内部的一次性生命周期状态。</summary>
internal enum DomainState
{
    /// <summary>正在执行 Configure 并初始化组件。</summary>
    Starting,

    /// <summary>已经完成启动，可正常使用。</summary>
    Active,

    /// <summary>
    /// 正在释放；仍可读取组件，本地事件订阅已失效，不能订阅本地事件或修改树结构。
    /// <para>事件中心订阅不会随此状态统一失效；AbstractSystem 自有订阅在各 System 释放时、OnRelease 之前取消。</para>
    /// </summary>
    Disposing,

    /// <summary>已经永久释放。</summary>
    Disposed
}
