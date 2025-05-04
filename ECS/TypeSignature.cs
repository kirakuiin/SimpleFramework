using System.Collections;
using System.Text;
using SimpleFramework.Utility.Extensions;

namespace SimpleFramework.ECS;

/// <summary>
/// 将一系列类型聚合为一个签名，类型不能重复，且类型的顺序不会被保留
/// </summary>
public sealed class TypeSignature : IEquatable<TypeSignature>, IReadOnlyList<Type>
{
    private readonly SortedSet<Type> _typeSet = new(TypeSignatureComparer.Comparer);

    /// <summary>
    /// 签名中包含的类型数量
    /// </summary>
    public int Count => _typeSet.Count;

    public TypeSignature(IEnumerable<Type> types)
    {
        types.Apply(Add);
    }

    public TypeSignature(TypeSignature signature)
    {
        Copy(signature);
    }

    public TypeSignature(params Type[] types)
    {
        types.Apply(Add);
    }

    /// <summary>
    /// 清理签名内的全部类型
    /// </summary>
    public TypeSignature Clear()
    {
        _typeSet.Clear();
        return this;
    }

    /// <summary>
    /// 新增类型
    /// </summary>
    /// <param name="types"></param>
    /// <returns></returns>
    public TypeSignature Add(params Type[] types)
    {
        types.Apply(Add);
        return this;
    }

    /// <summary>
    /// 移除类型
    /// </summary>
    /// <param name="types"></param>
    /// <returns></returns>
    public TypeSignature Remove(params Type[] types)
    {
        types.Apply(Remove);
        return this;
    }

    /// <summary>
    /// 复制另一份签名到自身
    /// </summary>
    public TypeSignature Copy(TypeSignature signature)
    {
        Clear();
        _typeSet.UnionWith(signature._typeSet);
        return this;
    }

    /// <summary>
    /// 新增一个类型
    /// </summary>
    public TypeSignature Add(Type type)
    {
        _typeSet.Add(type);
        return this;
    }
    
    /// <summary>
    /// 新增一个类型
    /// </summary>
    public TypeSignature Add<T>() => Add(typeof(T));

    /// <summary>
    /// 从类型中移除签名
    /// </summary>
    public TypeSignature Remove(Type type)
    {
         _typeSet.Remove(type);   
         return this;
    }

    /// <summary>
    /// 从类型中移除签名
    /// </summary>
    public TypeSignature Remove<T>() => Remove(typeof(T));

    /// <summary>
    /// 如果签名中含有类型，返回真
    /// </summary>
    public bool Has<T>() => Has(typeof(T));

    /// <summary>
    /// 如果签名中含有类型，返回真
    /// </summary>
    public bool Has(Type type) => _typeSet.Contains(type);

    /// <summary>
    /// 如果含有签名里的任意一个类型，返回真
    /// </summary>
    public bool HasAny(TypeSignature other)
    {
        return other._typeSet.Intersect(_typeSet).Any();
    }

    /// <summary>
    /// 如果含有签名里的所有类型，返回真
    /// </summary>
    /// <returns></returns>
    public bool HasAll(TypeSignature other)
    {
        return _typeSet.IsSupersetOf(other._typeSet);
    }

    public override int GetHashCode()
    {
        var b = new StringBuilder();
        foreach (var type in _typeSet)
        {
            b.Append(type.Name);
        }
        return b.ToString().GetHashCode();
    }

    public bool Equals(TypeSignature? other)
    {
        if (other == null || Count != other.Count)
        {
            return false;
        }

        return _typeSet.SetEquals(other._typeSet);
    }

    public override bool Equals(object? obj)
        => obj is TypeSignature sig && sig.Equals(this);

    public override string ToString()
    {
        var sig = new StringBuilder("TypeSignature [");
        foreach (var type in _typeSet)
        {
            sig.Append($"{type.Name}, ");
        }
        sig.Append(']');
        return sig.ToString();
    }

    Type IReadOnlyList<Type>.this[int index] => _typeSet.ToList()[index];

    IEnumerator<Type> IEnumerable<Type>.GetEnumerator()
    {
        return ((IEnumerable<Type>)_typeSet).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _typeSet.GetEnumerator();
    }

    int IReadOnlyCollection<Type>.Count => Count;
}



/// <summary>
/// 签名排序函数
/// </summary>
internal class TypeSignatureComparer : IComparer<Type>
{
    /// <summary>
    /// 静态比较器
    /// </summary>
    public static readonly TypeSignatureComparer Comparer = new();
    
    public int Compare(Type? x, Type? y)
    {
        return x switch
        {
            null when y == null => 0,
            null => -1,
            _ => y == null ? 1 : string.Compare(x.FullName, y.FullName, StringComparison.Ordinal)
        };
    }
}