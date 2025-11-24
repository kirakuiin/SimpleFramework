using System.Text;

namespace SimpleFramework.Utility;

/// <summary>
/// 存放一些不太好分类的杂项功能
/// </summary>
public static class MiscUtil
{
    private static Dictionary<Type, ulong> _typeHashCaches = new ();
    
    /// <summary>
    /// 获得类型的唯一名称
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static string GetUniqueTypeName<T>()
    {
        return GetUniqueTypeName(typeof(T));
    }

    /// <summary>
    /// 获得类型的唯一名称
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns></returns>
    public static string GetUniqueTypeName(Type type)
    {
        return $"{type.Assembly.GetName().Name}.{type.FullName}";
    }

    /// <summary>
    /// 生成一个GUID
    /// </summary>
    /// <returns></returns>
    public static Guid GenerateGuid()
    {
        return Guid.NewGuid();
    }

    /// <summary>
    /// 获得类型的hash值
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static ulong TypeHash<T>()
    {
        return TypeHash(typeof(T));
    }

    /// <summary>
    /// 获得指定类型的hash值
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns></returns>
    public static ulong TypeHash(Type type)
    {
        if (_typeHashCaches.TryGetValue(type, out var hash))
        {
            return hash;
        }
        var typeName = GetUniqueTypeName(type);
        return ComputeHash(typeName);
    }

    /// <summary>
    /// 生成一个64位hash值
    /// </summary>
    /// <returns></returns>
    public static ulong ComputeHash<T>(T data)
    {
        var bytes = SerializeUtil.Serialize(data);
        const ulong offset = 14695981039346656037;
        const ulong prime = 1099511628211;

        var hash = offset;
        foreach (byte b in bytes)
        {
            hash ^= b;
            hash *= prime;
        }
        return hash;
    }
}