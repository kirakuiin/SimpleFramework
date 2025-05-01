using System.Collections;
using System.Text;
using SimpleFramework.Utility.Extensions;

namespace SimpleFramework.ECS;

/// <summary>
/// 将一系列类型聚合为一个签名
/// </summary>
public sealed class TypeSignature : IEquatable<TypeSignature>, IReadOnlyList<Type>
{
    private readonly List<Type> _typeList = new();

    /// <summary>
    /// 签名中包含的类型数量
    /// </summary>
    public int Count => _typeList.Count;

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

    public TypeSignature(Archetype archetype)
    {
        /*
        if (archetype.IsValid())
        {
            var signature = archetype.GetTypeSignature();
            type_ids = new int[signature.type_count + 1];
            this.Copy(signature);
        }
        else type_ids = new int[2];
        */
    }

    /// <summary>
    /// 清理签名内的全部类型
    /// </summary>
    public TypeSignature Clear()
    {
        _typeList.Clear();
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
        foreach (var typeId in signature._typeList)
        {
            _typeList.Add(typeId);
        }
        return this;
    }

    /// <summary>
    /// 将原型的签名复制到自身
    /// </summary>
    /// public TypeSignature Copy(Archetype archetype) => this.Copy(archetype.GetTypeSignature());

    /// <summary>
    /// 新增一个类型
    /// </summary>
    public TypeSignature Add(Type type)
    {
         _typeList.Add(type);
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
         _typeList.Remove(type);   
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
    public bool Has(Type type) => _typeList.Contains(type);

    /// <summary>
    /// 如果含有签名里的任意一个类型，返回真
    /// </summary>
    public bool HasAny(TypeSignature other)
    {
        return other._typeList.Any(Has);
    }

    /// <summary>
    /// 如果含有签名里的所有类型，返回真
    /// </summary>
    /// <returns></returns>
    public bool HasAll(TypeSignature other)
    {
        return other._typeList.All(Has);
    }

    public override int GetHashCode()
    {
        var b = new StringBuilder();
        foreach (var type in _typeList)
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
        return _typeList.SequenceEqual(other._typeList);
    }

    public override bool Equals(object? obj)
        => obj is TypeSignature sig && sig.Equals(this);

    public override string ToString()
    {
        var sig = new StringBuilder("TypeSignature [");
        foreach (var type in _typeList)
        {
            sig.Append($"{type.Name}, ");
        }
        sig.Append(']');
        return sig.ToString();
    }

    Type IReadOnlyList<Type>.this[int index] => _typeList[index];

    IEnumerator<Type> IEnumerable<Type>.GetEnumerator()
    {
        foreach (var type in _typeList)
        {
            yield return type;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        foreach (var type in _typeList)
        {
            yield return type;
        }
    }

    int IReadOnlyCollection<Type>.Count => Count;
}