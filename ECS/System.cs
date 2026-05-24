namespace SimpleFramework.ECS;

/// <summary>
/// ECS 系统基类。
/// </summary>
public abstract class EcsSystem
{
    /// <summary>
    /// 创建 ECS 系统。
    /// </summary>
    /// <param name="world">系统所属世界。</param>
    protected EcsSystem(World world)
    {
        World = world;
    }

    /// <summary>
    /// 系统所属世界。
    /// </summary>
    protected World World { get; }

    /// <summary>
    /// 执行系统更新。
    /// </summary>
    public abstract void Update();

    /// <summary>
    /// 使用帧间隔时间执行系统更新。
    /// </summary>
    /// <param name="deltaTime">距离上次更新经过的时间。</param>
    public virtual void Update(float deltaTime)
    {
        Update();
    }
}
