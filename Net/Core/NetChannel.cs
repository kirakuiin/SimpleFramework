namespace SimpleFramework.Net;

/// <summary>
/// 表示网络包使用的逻辑通道。
/// </summary>
public enum NetChannel
{
    /// <summary>
    /// 框架内部系统消息通道。
    /// </summary>
    System,

    /// <summary>
    /// 可靠传输通道。
    /// </summary>
    Reliable,

    /// <summary>
    /// 不可靠传输通道；不支持时应返回明确错误。
    /// </summary>
    Unreliable
}
