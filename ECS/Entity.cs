namespace SimpleFramework.ECS;

/// <summary>
/// ECS 实体句柄。
/// </summary>
public readonly struct Entity : IEquatable<Entity>
{
    public Entity(int worldId, int id, int version)
    {
        WorldId = worldId;
        Id = id;
        Version = version;
    }

    /// <summary>
    /// 所属世界编号。
    /// </summary>
    public int WorldId { get; }

    /// <summary>
    /// 全局分配的不透明实体标识；所属世界将其映射到世界内的存储槽位。
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// 实体槽位版本。
    /// </summary>
    public int Version { get; }

    public bool Equals(Entity other)
    {
        return WorldId == other.WorldId && Id == other.Id && Version == other.Version;
    }

    public override bool Equals(object? obj)
    {
        return obj is Entity other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(WorldId, Id, Version);
    }

    public override string ToString()
    {
        return $"Entity(WorldId: {WorldId}, Id: {Id}, Version: {Version})";
    }

    public static bool operator ==(Entity left, Entity right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Entity left, Entity right)
    {
        return !left.Equals(right);
    }
}
