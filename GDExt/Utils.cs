#if GODOT

using Godot;
using System.Collections.Generic;
using SimpleFramework.Patterns;

namespace SimpleFramework.GDExt;


/// <summary>
/// 游戏对象池，将不用的游戏对象缓存起来。
/// </summary>
public class NodeObjectPool: Singleton<NodeObjectPool>
{
    private readonly Dictionary<PackedScene, ObjectPool<Node2D>> _pooledObjects = new();

    /// <summary>
    /// 获得一个指定Node2D的实例。
    /// </summary>
    /// <param name="scene">scene对象</param>
    /// <returns></returns>
    public Node2D Get(PackedScene scene)
    {
        if (!_pooledObjects.ContainsKey(scene))
        {
            RegisterScene(scene);
        }
        return _pooledObjects[scene].Get();
    }
    
    private void RegisterScene(PackedScene scene)
    {
        CreateObjectPool(scene);
    }

    private void CreateObjectPool(PackedScene scene)
    {
        Node2D CreateFunc()
        {
            return scene.Instantiate() as Node2D;
        }

        void ActionOnGet(Node2D obj)
        {
            obj.Visible = false;
        }

        void ActionOnRelease(Node2D obj)
        {
            obj.Visible = true;
        }

        void ActionOnDestroy(Node2D obj)
        {
            obj.QueueFree();
        }

        _pooledObjects[scene] = new ObjectPool<Node2D>(
            CreateFunc, ActionOnGet, ActionOnRelease, ActionOnDestroy);
    }

    public override void Clear()
    {
        foreach (var prefab in _pooledObjects.Keys)
        {
            _pooledObjects[prefab].Clear();
        }
        _pooledObjects.Clear();
    }


    /// <summary>
    /// 将对象返还给对象池。
    /// </summary>
    /// <param name="scene"></param>
    /// <param name="node"></param>
    public void Return(PackedScene scene, Node2D node)
    {
        _pooledObjects[scene].Return(node);
    }
}

#endif