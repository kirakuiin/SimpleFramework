namespace SimpleFramework.ECS;

/// <summary>
/// 访问一个组件引用的遍历委托。
/// </summary>
/// <typeparam name="T1">组件类型。</typeparam>
/// <param name="entity">当前实体。</param>
/// <param name="c1">组件引用。</param>
public delegate void EachRef<T1>(Entity entity, ref T1 c1) where T1 : IComponent;

/// <summary>
/// 访问两个组件引用的遍历委托。
/// </summary>
/// <typeparam name="T1">第一个组件类型。</typeparam>
/// <typeparam name="T2">第二个组件类型。</typeparam>
/// <param name="entity">当前实体。</param>
/// <param name="c1">第一个组件引用。</param>
/// <param name="c2">第二个组件引用。</param>
public delegate void EachRef<T1, T2>(Entity entity, ref T1 c1, ref T2 c2)
    where T1 : IComponent
    where T2 : IComponent;

/// <summary>
/// ECS 语法糖扩展。
/// </summary>
public static class ECSExtension
{
    /// <summary>
    /// 遍历拥有指定组件的实体。
    /// </summary>
    /// <typeparam name="T1">组件类型。</typeparam>
    /// <param name="world">要遍历的世界。</param>
    /// <param name="action">每个实体执行的操作。</param>
    public static void Each<T1>(this World world, EachRef<T1> action) where T1 : IComponent
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(action);

        foreach (var entity in world.Query<T1>())
        {
            action(entity, ref world.Get<T1>(entity));
        }
    }

    /// <summary>
    /// 遍历同时拥有两个指定组件的实体。
    /// </summary>
    /// <typeparam name="T1">第一个组件类型。</typeparam>
    /// <typeparam name="T2">第二个组件类型。</typeparam>
    /// <param name="world">要遍历的世界。</param>
    /// <param name="action">每个实体执行的操作。</param>
    public static void Each<T1, T2>(this World world, EachRef<T1, T2> action)
        where T1 : IComponent
        where T2 : IComponent
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(action);

        foreach (var entity in world.Query<T1, T2>())
        {
            action(entity, ref world.Get<T1>(entity), ref world.Get<T2>(entity));
        }
    }
}
