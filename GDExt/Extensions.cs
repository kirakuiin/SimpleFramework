#if GODOT

namespace SimpleFramework.GDExt;

/// <summary>
/// 提供将取消注册句柄绑定到 Godot 节点退出事件的扩展方法。
/// </summary>
public static class UnRegisterAbleExtensions
{
    /// <summary>
    /// 在节点退出场景树时取消注册，并返回原句柄以便链式调用。
    /// </summary>
    /// <param name="self">要绑定的取消注册句柄。</param>
    /// <param name="node">负责触发取消注册的 Godot 节点。</param>
    /// <returns>传入的取消注册句柄。</returns>
    public static IUnRegister UnRegisterWhenNodeExit(this IUnRegister self, Godot.Node node)
    {
        node.TreeExited += self.UnRegister;
        return self;
    }
}


/// <summary>
/// Godot常量定义
/// </summary>
public static class GdConst
{
    /// <summary>
    /// 服务端的ID
    /// </summary>
    public const long ServerId = 1;

    /// <summary>
    /// 默认的超时时间(ms)
    /// </summary>
    public const int Timeout = 5000;
}

/// <summary>
/// Godot 多人通信使用的逻辑信道编号。
/// </summary>
public static class Channel
{
    /// <summary>
    /// 玩法信道
    /// </summary>
    public const int Gameplay = 1;

    /// <summary>
    /// 系统信道
    /// </summary>
    public const int System = 2;

    /// <summary>
    /// 数据信道
    /// </summary>
    public const int Data = 3;
}

#endif
