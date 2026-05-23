using System.Collections;

namespace SimpleFramework.ECS;

/// <summary>
/// 不可变的组件类型签名。
/// </summary>
public sealed class TypeSignature : IEquatable<TypeSignature>, IReadOnlyCollection<Type>
{
    private static readonly IComparer<Type> TypeComparer = Comparer<Type>.Create(CompareTypes);
    private readonly int _hashCode;
    private readonly Type[] _types;

    /// <summary>
    /// 使用组件类型创建签名。
    /// </summary>
    /// <param name="types">组件类型集合。</param>
    public TypeSignature(params Type[] types)
        : this((IEnumerable<Type>)types)
    {
    }

    /// <summary>
    /// 使用组件类型集合创建签名。
    /// </summary>
    /// <param name="types">组件类型集合。</param>
    public TypeSignature(IEnumerable<Type> types)
    {
        ArgumentNullException.ThrowIfNull(types);

        _types = types.Select(ValidateType).Distinct().OrderBy(type => type, TypeComparer).ToArray();
        _hashCode = CalculateHashCode(_types);
    }

    /// <summary>
    /// 复制已有签名。
    /// </summary>
    /// <param name="signature">要复制的签名。</param>
    public TypeSignature(TypeSignature signature)
    {
        ArgumentNullException.ThrowIfNull(signature);

        _types = signature._types;
        _hashCode = signature._hashCode;
    }

    /// <summary>
    /// 签名内的组件类型数量。
    /// </summary>
    public int Count => _types.Length;

    /// <summary>
    /// 判断签名是否包含指定组件类型。
    /// </summary>
    /// <typeparam name="T">组件类型。</typeparam>
    /// <returns>如果包含该组件类型则为 true。</returns>
    public bool Has<T>() where T : IComponent
    {
        return Has(typeof(T));
    }

    /// <summary>
    /// 判断签名是否包含指定组件类型。
    /// </summary>
    /// <param name="type">组件类型。</param>
    /// <returns>如果包含该组件类型则为 true。</returns>
    public bool Has(Type type)
    {
        return Array.BinarySearch(_types, ValidateType(type), TypeComparer) >= 0;
    }

    /// <summary>
    /// 判断签名是否包含另一个签名的全部组件类型。
    /// </summary>
    /// <param name="signature">要检查的签名。</param>
    /// <returns>如果包含全部组件类型则为 true。</returns>
    public bool HasAll(TypeSignature signature)
    {
        ArgumentNullException.ThrowIfNull(signature);
        return signature._types.All(Has);
    }

    /// <summary>
    /// 判断签名是否包含另一个签名中的任意组件类型。
    /// </summary>
    /// <param name="signature">要检查的签名。</param>
    /// <returns>如果包含任意组件类型则为 true。</returns>
    public bool HasAny(TypeSignature signature)
    {
        ArgumentNullException.ThrowIfNull(signature);
        return signature._types.Any(Has);
    }

    public IEnumerator<Type> GetEnumerator()
    {
        return ((IEnumerable<Type>)_types).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public bool Equals(TypeSignature? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null && _types.SequenceEqual(other._types);
    }

    public override bool Equals(object? obj)
    {
        return obj is TypeSignature signature && Equals(signature);
    }

    public override int GetHashCode()
    {
        return _hashCode;
    }

    public override string ToString()
    {
        return $"TypeSignature [{string.Join(", ", _types.Select(type => type.Name))}]";
    }

    private static Type ValidateType(Type type)
    {
        if (type is null || !typeof(IComponent).IsAssignableFrom(type))
        {
            throw new ArgumentException("签名类型必须实现 IComponent。", nameof(type));
        }

        return type;
    }

    private static int CalculateHashCode(IEnumerable<Type> types)
    {
        var hash = new HashCode();
        foreach (var type in types)
        {
            hash.Add(type);
        }

        return hash.ToHashCode();
    }

    private static int CompareTypes(Type? left, Type? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        return string.Compare(left.FullName, right.FullName, StringComparison.Ordinal);
    }
}
