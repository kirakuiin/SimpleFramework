namespace SimpleFramework;

/// <summary>
/// 请求的组件类型在当前 Domain 分类中存在多个可赋值候选项时抛出的异常。
/// </summary>
public sealed class AmbiguousComponentException : InvalidOperationException
{
    /// <summary>
    /// 创建组件解析歧义异常。
    /// </summary>
    /// <param name="requestedType">请求解析的组件类型。</param>
    /// <param name="candidateKeys">产生歧义的候选注册键。</param>
    /// <param name="category">组件分类名称。</param>
    internal AmbiguousComponentException(
        Type requestedType,
        IEnumerable<Type> candidateKeys,
        string category)
        : base(CreateMessage(requestedType, candidateKeys, category, out var keys))
    {
        RequestedType = requestedType;
        CandidateKeys = keys;
    }

    /// <summary>
    /// 获取请求解析的组件类型。
    /// </summary>
    public Type RequestedType { get; }

    /// <summary>
    /// 获取产生歧义的候选注册键。
    /// </summary>
    public IReadOnlyList<Type> CandidateKeys { get; }

    private static string CreateMessage(
        Type requestedType,
        IEnumerable<Type> candidateKeys,
        string category,
        out IReadOnlyList<Type> keys)
    {
        ArgumentNullException.ThrowIfNull(requestedType);
        ArgumentNullException.ThrowIfNull(candidateKeys);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        var snapshot = candidateKeys.ToArray();
        keys = Array.AsReadOnly(snapshot);
        return $"Ambiguous {category} lookup for {requestedType.FullName}: " +
               string.Join(", ", snapshot.Select(key => key.FullName));
    }
}
