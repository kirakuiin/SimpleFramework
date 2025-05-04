namespace SimpleFramework.ECS;


public partial class World
{
    /// <summary>
    /// 创建一个具有单个组件类型的新实体。
    /// </summary>
    /// <typeparam name="T1">实体应具有的组件类型。</typeparam>
    /// <returns>新创建的实体。</returns>
    public Entity CreateEntity<T1>() where T1 : IComponent
    {
        return CreateEntity(typeof(T1));
    }

    /// <summary>
    /// 创建一个具有两个组件类型的新实体。
    /// </summary>
    /// <typeparam name="T1">第一个组件类型。</typeparam>
    /// <typeparam name="T2">第二个组件类型。</typeparam>
    /// <returns>新创建的实体。</returns>
    public Entity CreateEntity<T1, T2>() where T1 : IComponent where T2 : IComponent
    {
        return CreateEntity(typeof(T1), typeof(T2));
    }

    /// <summary>
    /// 创建一个具有三个组件类型的新实体。
    /// </summary>
    /// <typeparam name="T1">第一个组件类型。</typeparam>
    /// <typeparam name="T2">第二个组件类型。</typeparam>
    /// <typeparam name="T3">第三个组件类型。</typeparam>
    /// <returns>新创建的实体。</returns>
    public Entity CreateEntity<T1, T2, T3>() where T1 : IComponent where T2 : IComponent where T3 : IComponent
    {
        return CreateEntity(typeof(T1), typeof(T2), typeof(T3));
    }

    /// <summary>
    /// 创建一个具有四个组件类型的新实体。
    /// </summary>
    /// <typeparam name="T1">第一个组件类型。</typeparam>
    /// <typeparam name="T2">第二个组件类型。</typeparam>
    /// <typeparam name="T3">第三个组件类型。</typeparam>
    /// <typeparam name="T4">第四个组件类型。</typeparam>
    /// <returns>新创建的实体。</returns>
    public Entity CreateEntity<T1, T2, T3, T4>() where T1 : IComponent where T2 : IComponent where T3 : IComponent where T4 : IComponent
    {
        return CreateEntity(typeof(T1), typeof(T2), typeof(T3), typeof(T4));
    }
    
    /// <summary>
    /// 创建一个具有单个组件类型的新实体。
    /// </summary>
    /// <typeparam name="T1">实体应具有的组件类型。</typeparam>
    /// <returns>新创建的实体。</returns>
    public Entity CreateEntity<T1>(T1 comp) where T1 : IComponent
    {
        var entity = CreateEntity();
        entity.Add(comp);
        return entity;
    }
    
    /// <summary>
    /// 创建一个具有两个组件类型的新实体。
    /// </summary>
    /// <typeparam name="T1">第一个组件类型。</typeparam>
    /// <typeparam name="T2">第二个组件类型。</typeparam>
    /// <returns>新创建的实体。</returns>
    public Entity CreateEntity<T1, T2>(T1 comp1, T2 comp2) where T1 : IComponent where T2 : IComponent
    {
        var entity = CreateEntity();
        entity.Add(comp1).Add(comp2);
        return entity;
    }
    
    /// <summary>
    /// 创建一个具有三个组件类型的新实体。
    /// </summary>
    /// <typeparam name="T1">第一个组件类型。</typeparam>
    /// <typeparam name="T2">第二个组件类型。</typeparam>
    /// <typeparam name="T3">第三个组件类型。</typeparam>
    /// <returns>新创建的实体。</returns>
    public Entity CreateEntity<T1, T2, T3>(T1 comp1, T2 comp2, T3 comp3) where T1 : IComponent where T2 : IComponent where T3 : IComponent
    {
        var entity = CreateEntity();
        entity.Add(comp1).Add(comp2).Add(comp3);
        return entity;
    }
    
    /// <summary>
    /// 创建一个具有四个组件类型的新实体。
    /// </summary>
    /// <typeparam name="T1">第一个组件类型。</typeparam>
    /// <typeparam name="T2">第二个组件类型。</typeparam>
    /// <typeparam name="T3">第三个组件类型。</typeparam>
    /// <typeparam name="T4">第四个组件类型。</typeparam>
    /// <returns>新创建的实体。</returns>
    public Entity CreateEntity<T1, T2, T3, T4>(T1 comp1, T2 comp2, T3 comp3, T4 comp4) where T1 : IComponent where T2 : IComponent where T3 : IComponent where T4 : IComponent
    {
        var entity = CreateEntity();
        entity.Add(comp1).Add(comp2).Add(comp3).Add(comp4);
        return entity;
    }
    
    /// <summary>
    /// 为此世界创建新查询。
    /// </summary>
    /// <typeparam name="T1">第一个组件类型。</typeparam>
    /// <returns>新的查询实例。</returns>
    public Query CreateQuery<T1>() where T1 : IComponent
    {
        var query = CreateQuery();
        return query.Has<T1>();
    }
    
    /// <summary>
    /// 为此世界创建新查询。
    /// </summary>
    /// <typeparam name="T1">第一个组件类型。</typeparam>
    /// <typeparam name="T2">第二个组件类型。</typeparam>
    /// <returns>新的查询实例。</returns>
    public Query CreateQuery<T1, T2>() where T1 : IComponent where T2 : IComponent
    {
        var query = CreateQuery();
        return query.Has<T1>().Has<T2>();
    }
    
    /// <summary>
    /// 为此世界创建新查询。
    /// </summary>
    /// <typeparam name="T1">第一个组件类型。</typeparam>
    /// <typeparam name="T2">第二个组件类型。</typeparam>
    /// <typeparam name="T3">第三个组件类型。</typeparam>
    /// <returns>新的查询实例。</returns>
    public Query CreateQuery<T1, T2, T3>() where T1 : IComponent where T2 : IComponent where T3 : IComponent
    {
        var query = CreateQuery();
        return query.Has<T1>().Has<T2>().Has<T3>();
    }
    
    /// <summary>
    /// 为此世界创建新查询。
    /// </summary>
    /// <typeparam name="T1">第一个组件类型。</typeparam>
    /// <typeparam name="T2">第二个组件类型。</typeparam>
    /// <typeparam name="T3">第三个组件类型。</typeparam>
    /// <typeparam name="T4">第四个组件类型。</typeparam>
    /// <returns>新的查询实例。</returns>
    public Query CreateQuery<T1, T2, T3, T4>() where T1 : IComponent where T2 : IComponent where T3 : IComponent where T4 : IComponent
    {
        var query = CreateQuery();
        return query.Has<T1>().Has<T2>().Has<T3>().Has<T4>();
    }
}