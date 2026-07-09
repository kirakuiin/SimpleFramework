#if GODOT

using Godot;
using SimpleFramework.Patterns;

namespace SimpleFramework.GDExt;

/// <summary>
/// Godot 节点对象池，用于缓存暂时不用的 <see cref="Node2D"/>。
/// </summary>
public class NodeObjectPool : Singleton<NodeObjectPool>
{
    private readonly Dictionary<PackedScene, ObjectPool<Node2D>> _pooledObjects = new();

    /// <summary>
    /// 获取指定场景的 <see cref="Node2D"/> 实例。
    /// </summary>
    /// <param name="scene">要实例化或复用的 Godot 场景。</param>
    /// <returns>可直接加入场景树使用的节点实例。</returns>
    public Node2D Get(PackedScene scene)
    {
        if (!_pooledObjects.TryGetValue(scene, out var pool))
        {
            RegisterScene(scene);
            pool = _pooledObjects[scene];
        }

        return pool.Get();
    }

    public override void Clear()
    {
        foreach (var pool in _pooledObjects.Values)
        {
            pool.Clear();
        }

        _pooledObjects.Clear();
    }

    /// <summary>
    /// 将节点归还给对应场景的对象池。
    /// </summary>
    /// <param name="scene">节点来源的 Godot 场景。</param>
    /// <param name="node">要归还的节点实例。</param>
    public void Return(PackedScene scene, Node2D node)
    {
        _pooledObjects[scene].Return(node);
    }

    private void RegisterScene(PackedScene scene)
    {
        _pooledObjects[scene] = new ObjectPool<Node2D>(
            () => scene.Instantiate() as Node2D
                ?? throw new InvalidOperationException("PackedScene root node must inherit Node2D."),
            obj => obj.Visible = true,
            obj => obj.Visible = false,
            obj => obj.QueueFree());
    }
}

#endif
