#if GODOT
namespace SimpleFramework.GDExt;

public static class UnRegisterAbleExtensions
{
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
}

public static class Channel
{
    /// <summary>
    /// 玩法信道
    /// </summary>
    public const int Gameplay= 1;

    /// <summary>
    /// 系统信道
    /// </summary>
    public const int System= 2;

    /// <summary>
    /// 数据信道
    /// </summary>
    public const int Data = 3;
}
#endif
