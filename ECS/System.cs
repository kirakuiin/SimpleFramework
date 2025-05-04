namespace SimpleFramework.ECS;

/// <summary>
/// 一个简化版本的系统基类
/// </summary>
public abstract class EcsSystem
{
    protected readonly World World;
    protected Query Query;

    protected EcsSystem(World world)
    {
        World = world;
        Query = world.CreateQuery();
    }

    /// <summary>
    /// 更新函数
    /// </summary>
    public virtual void Update()
    {
        Query.Foreach(ProcessEntity);
    }

    /// <summary>
    /// 处理单个实体的调用
    /// </summary>
    protected abstract void ProcessEntity(Entity entity);
}