namespace SimpleFramework.Net;

/// <summary>
/// 将 Net 事件投递到调用方指定上下文的调度器。
/// </summary>
public interface INetEventDispatcher
{
    /// <summary>
    /// 投递一个待执行回调。
    /// </summary>
    /// <param name="action">需要在目标上下文执行的回调。</param>
    void Post(Action action);
}
