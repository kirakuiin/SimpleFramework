namespace SimpleFramework.Engines;

public static class UnRegisterAbleExtensions
{
    #if GODOT
    public static IUnRegister UnRegisterWhenNodeExit(this IUnRegister self, Godot.Node node)
    {
        node.TreeExited += self.UnRegister;
        return self;
    }
    #endif
}
